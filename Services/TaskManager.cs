using App.Base.Extensions;
using App.Base.Helpers;
using Microsoft.EntityFrameworkCore;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Data;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Dto;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Models;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Models.WSLawyerModels;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Static;

namespace QLab.Robot.Bankrupt.Efrsb.MessageService.Services
{
    internal class TaskManager
    {
        private readonly MessageServiceDbContext _context;
        private readonly WSLawyerDbContext _wslContext;

        public TaskManager(MessageServiceDbContext context, WSLawyerDbContext wslContext)
        {
            _context = context;
            _wslContext = wslContext;
        }

        internal async Task CreateTasksAsync()
        {
            var settings = await _context.MessageActionSettings
                .AsNoTracking()
                .Include(x => x.ActionType)
                .Include(x => x.Tasks)
                .ThenInclude(x => x.ExecutorType)
                .Include(x => x.Tasks)
                .ThenInclude(x => x.TaskType)
                .ToListAsync();

            var bankruptcyCases = await _wslContext.BankruptcyCases
                .AsNoTracking()
                .ToListAsync();

#if (DEBUG) //todo: use server [staging]
            //for demo
            //await CreateDemoPotentialBankruptsAsync(settings);
#endif
            var messages = await _context.EfrsbMessages!
                //.AsNoTracking()
                .Where(x => !x.IsClosed && x.MessageContent != null && !x.IsFailed)
                .ToListAsync();

            foreach (var efrsbMessage in messages)
            {
                if (!string.IsNullOrEmpty(efrsbMessage.MessageContent))
                {
                    MessageParser.Parse(efrsbMessage.MessageContent, out var message);

                    if (message != null)
                    {
                        CreateTaskResult? result = await CreateTasksByMessageAsync(message, settings, bankruptcyCases);

                        if (result == null) //Subject not found
                        {
                            _context.Remove(efrsbMessage);
                            await _context.SaveChangesAsync();
                        }
                        else if (result.IsSucceeded)
                        {
                            efrsbMessage.IsClosed = true;
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            efrsbMessage.IsFailed = true;

                            var brokenMessage = new BrokenMessage
                            { 
                                EfrsbMessage = efrsbMessage, 
                                Errors = string.Join("; ", result.InnerResults!.Select(x=>x.Message)),
                            };

                            await _context.BrokenMessages.AddAsync(brokenMessage);                          
                            await _context.SaveChangesAsync();

                            //todo:   send mail to project curator  
                        }
                    }
                }
            }
        }

        private async Task CreateDemoPotentialBankruptsAsync(List<WslCaseBankruptcy> bankruptcyCases,
            List<MessageActionSetting> settings)
        {
            //for demo
            if (!_context.PotentialBankrupts.Any())
            {
                List<PotentialBankrupt> bankrupts = new();

                var ulsubjs = await _wslContext.Subjects
                    .AsNoTracking()
                    .OrderByDescending(x => x.Id)
                    .Where(x => x.StatusJuridical == JuridicalStatuses.Company)
                    .Take(4)
                    .ToListAsync();

                var flsubjs = await _wslContext.Subjects
                    .AsNoTracking()
                    .OrderByDescending(x => x.Id)
                    .Where(x => x.StatusJuridical == JuridicalStatuses.Person)
                    .Take(4)
                    .ToListAsync();
                for(int i = 0; i < flsubjs.Count; i++)
                {
                    if(i % 2 == 0)
                    {
                        if (string.IsNullOrEmpty(flsubjs[i].Snils))
                            flsubjs[i].Snils = "0000000000".Replace("0", $"{i}");
                    }
                }

                var ipsubjs = await _wslContext.Subjects
                    .AsNoTracking()
                    .OrderByDescending(x => x.Id)
                    .Where(x => x.StatusJuridical == JuridicalStatuses.IP)
                    .Take(4)
                    .ToListAsync();

                var subjs = new List<WslSubject>();
                subjs.AddRange(ipsubjs);
                subjs.AddRange(flsubjs);
                subjs.AddRange(ulsubjs);
                List<string> messtypes = new()
                {
                    MessageTypes.AppointAdministration,
                    MessageTypes.ActDealInvalid,
                    MessageTypes.ActDealInvalid2,
                    MessageTypes.ActPersonSubsidiary,
                    MessageTypes.ChangeAuction,
                    MessageTypes.ActDealInvalid,
                    MessageTypes.ActDealInvalid2,
                    MessageTypes.ActPersonSubsidiary,
                    MessageTypes.ChangeAuction,
                    MessageTypes.EstimatesAndUnsoldAssets,
                    MessageTypes.CompletionOfExtrajudicialBankruptcy,
                    MessageTypes.CreditorChoiceRightSubsidiary,
                };

                for (int i = 0; i < subjs.Count; i++)
                {
                    var mess = new Message
                    {
                        MessageType = messtypes[i],
                        EfrsbId = $"{8250141 + 1000 * i - 100 - i}",
                        PublishDate = DateTime.Now.AddDays(-1),
                    };

                    var potentialBankrupt = CreatePotentialBankrupt(subjs[i], mess, settings[i]);
                    bankrupts.Add(potentialBankrupt);
                }

                await _context.PotentialBankrupts.AddRangeAsync(bankrupts);
                await _context.SaveChangesAsync(); // добавлен один потенциальный банкрот (демо данные)
            }
        }
        private async Task CreatePotentialBankruptAsync(WslSubject subject, Message message, MessageActionSetting setting)
        {
            //if (_context.PotentialBankrupts.Where(x => x.SubjectId == subject.Id).Any())
            if (_context.PotentialBankrupts.Any(x => x.SubjectId == subject.Id))
                return;
            var cases = subject.Cases!.FirstOrDefault();
            var projectid = cases!.Case!.ProjectId;
            PotentialBankrupt potentialBankrupt = CreatePotentialBankrupt(subject, message, setting, cases, projectid);
            _context.PotentialBankrupts.Add(potentialBankrupt);
            await _context.SaveChangesAsync();
        }

        private PotentialBankrupt CreatePotentialBankrupt(WslSubject subject, Message message, MessageActionSetting setting, WslSubjectCase? wslCase = null, long? projectid = null)
        {
            string? project = _wslContext.Projects.FirstOrDefault(x => x.Id == projectid)!.Name;
            return subject.StatusJuridical switch
            {
                JuridicalStatuses.Person => new FlPotentialBankrupt(subject, message, setting, wslCase, project),
                JuridicalStatuses.IP => new IpPotentialBankrupt(subject, message, setting, wslCase, project),
                JuridicalStatuses.Company => new UlPotentialBankrupt(subject, message, setting, wslCase, project),
                _ => new SimplePotentialBankrupt(subject, message, setting, wslCase, project),
            };
        }

        private bool GetDisableCaseTrackingFlag(WslSubjectCase? subjectCase, List<WslCaseBankruptcy> bankruptcyCases)
        {
            if (subjectCase == null)
                return true; //если дело не существует не создавать в нем задачи
            var caseTracking = bankruptcyCases
                            .FirstOrDefault(x => x.CaseId == subjectCase.CaseId);
            if (caseTracking == null)
                return false; //если для дела не задана настройка "Отключить мониторинг ЕФРСБ", то создавать в нем задачи
            var disableCaseTracking = caseTracking!.DisableCaseTracking ?? false;
            return disableCaseTracking; //вернуть значение флага "Отключить мониторинг ЕФРСБ"
        }

        private async Task<CreateTaskResult?> CreateTasksByMessageAsync(Message message, List<MessageActionSetting> settings,
            List<WslCaseBankruptcy> bankruptcyCases)
        {
            var jSubjects = await GetJSubjectWithDetailsAsync(message.BankruptInfo);

            if (jSubjects == null || !jSubjects.Any())
                return null;

            CreateTaskResult taskResult = new();

            var setting = settings.FirstOrDefault(x => x.NormalizedKey == message.MessageType?.Normalize(Case.Upp));
            if (setting == null)
            {
                var errorResult = new CreateTaskResult(isFailed: true)
                {
                    Message = $"В справочниках отсутствует настройка для типа сообщения \"{message.MessageType}\" с EFRSBID={message.EfrsbId}"
                };
                taskResult.InnerResults?.Add(errorResult);
                return taskResult;
            }

            foreach (var jSubject in jSubjects)
            {
                var bankruptCases = jSubject.Cases!
                    .Where(x => x.Status == WslCaseTypes.Bankruptcy)
                    .ToList();                

                if (bankruptCases.Any())
                {                    
                    foreach (var jcase in bankruptCases)
                    {
                        if (!GetDisableCaseTrackingFlag(jcase, bankruptcyCases))
                        {
                            await CreateTasksAsync(jcase, message, setting, taskResult);
                        }                            
                    }
                    //return jResult; смысл?
                }
                else
                {
                    await CreatePotentialBankruptAsync(jSubject, message, setting); //создание записи о потенциальном банкроте

                    //todo:
                    //bool res = await CreatePotentialBankruptAsync(jSubject, message, settings);
                    //if(!res)
                    //    jResult.InnerResults?.Add(errorResult);
                }
            }
            return taskResult;
        }

        private async Task CreateTasksAsync(WslSubjectCase jcase, Message message, MessageActionSetting? setting, 
            CreateTaskResult taskResult)
        {
            if (setting != null)
            {
                if (setting.ActionType?.Name == ActionTypes.Comment)
                {
                    var result = await CreateJCommentAsync(jcase, message);
                    if (!result)
                    {
                        var errorResult = new CreateTaskResult(isFailed: true)
                        {
                            Message = $"Не удалось создать комментарий в деле с ID={jcase.Id} для сообщения типа \"{message.MessageType}\" с EFRSBID={message.EfrsbId}"
                        };
                        taskResult.InnerResults?.Add(errorResult);
                    }
                }
                else if (setting.ActionType?.Name == ActionTypes.Task)
                {
                    if (setting.Tasks != null)
                        foreach (var jtask in setting.Tasks)
                        {
                            bool result = await CreateJTaskAsync(jcase.CaseId, jtask);
                            if (!result)
                            {
                                var errorResult = new CreateTaskResult(isFailed: true)
                                {
                                    Message = $"Не удалось создать задачу \"{jtask.TaskType!.Name}\" в деле с ID={jcase.Id} для сообщения типа \"{message.MessageType}\" с EFRSBID={message.EfrsbId}"
                                };
                                taskResult.InnerResults?.Add(errorResult);
                            }
                        }
                    else
                    {
                        var errorResult = new CreateTaskResult(isFailed: true)
                        {
                            Message = $"Настройка для типа сообщений {message.MessageType} содержит пустой список задач (EFRSBID={message.EfrsbId}"
                        };
                        taskResult.InnerResults?.Add(errorResult);
                    }
                }
            }
        }
        
        private async Task<bool> CreateJCommentAsync(WslSubjectCase jcase, Message message)
        {
            if (jcase?.CaseId == null || jcase?.CaseId <= 0)
                return false;

            var addComment = new WslInformationOnCase();
            var dt = DateTime.Now;
            addComment.DateCreate = dt;
            addComment.DateUpdate = dt;
            addComment.UserIdcreator = 1;
            addComment.UserIdupdate = 1;
            addComment.Comment = message.Text ?? "Робот Банкрот: \"Получено пустое сообщение от ЕФРСБ\"";
            addComment.CaseId = jcase?.CaseId;
            //addComment.Case = _jurisContext.JCases.FirstOrDefault(x => x.Id == jcase.CaseId);
            addComment.OrderWeight = await _wslContext.InformationOnCases.MaxAsync(x => x.OrderWeight) + 1;

            try
            {
                await _wslContext.InformationOnCases.AddAsync(addComment);
                await _wslContext.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                //todo: log error
                return false;
            }
        }

        private async Task<List<WslSubject>?> GetJSubjectWithDetailsAsync(BankruptInfo? bankruptInfo)
        {
            if (bankruptInfo != null)
            {
                if (bankruptInfo is BankruptCompany bankruptCompany)
                {
                    var jSubjects = await _wslContext.Subjects
                        .Include(x => x.Cases)
                        .Where(x => (x.StatusJuridical == JuridicalStatuses.Company
                            || x.StatusJuridical == JuridicalStatuses.IP)
                            && (x.Inn == bankruptCompany.Inn || x.Ogrn == bankruptCompany.Ogrn))
                        .ToListAsync();

                    if (jSubjects.Any())
                        return jSubjects;

                    //var regexName = Regex.Replace(bankruptCompany.Name, "[^а-яА-Яa-zA-Z0-9() ]", "");
                    var regexCompanyName = bankruptCompany.Name!.Replace("\"", "");

                    jSubjects = await _wslContext.Subjects
                        .Include(x => x.Cases)
                        .Where(x => (x.StatusJuridical == JuridicalStatuses.Company
                            || x.StatusJuridical == JuridicalStatuses.IP)
                            && x.FullName.Replace("\"", "").Replace("«", "").Replace("»", "")
                            .Contains(regexCompanyName))
                        .ToListAsync();

                    if (jSubjects.Any())
                        return jSubjects;

                    jSubjects = await _wslContext.Subjects
                        .Include(x => x.Cases)
                        .Where(x => (x.StatusJuridical == JuridicalStatuses.Company
                            || x.StatusJuridical == JuridicalStatuses.IP)
                            && x.Name.Replace("\"", "").Replace("«", "").Replace("»", "")
                            .Contains(regexCompanyName))
                        .ToListAsync();

                    if (jSubjects.Any())
                        return jSubjects;
                }
                else if (bankruptInfo is BankruptPerson bankruptPerson)
                {
                    var jSubjects = await _wslContext.Subjects
                        .Include(x => x.Cases)
                        .Where(x => x.StatusJuridical == JuridicalStatuses.Person && x.Inn == bankruptPerson.Inn)
                        .ToListAsync();

                    if (jSubjects.Any())
                        return jSubjects;

                    var fio = bankruptPerson.Fio!;
                    var personFullName = UserNameBuilder.BuildFullName(fio.LastName!, fio.FirstName!, fio.MiddleName!);

                    if (!string.IsNullOrEmpty(personFullName))
                    {
                        jSubjects = await _wslContext.Subjects
                            .Include(x => x.Cases)
                            .Where(x => x.StatusJuridical == JuridicalStatuses.Person && x.FullName.Contains(personFullName))
                            .ToListAsync();

                        if (jSubjects.Any())
                            return jSubjects;

                        jSubjects = await _wslContext.Subjects
                            .Include(x => x.Cases)
                            .Where(x => x.StatusJuridical == JuridicalStatuses.Person && x.Name.Contains(personFullName))
                            .ToListAsync();

                        if (jSubjects!.Any())
                            return jSubjects;
                    }
                }
            }

            return null;
        }

        private async Task<DateTime> GetNearestWorkingDay(DateTime date)
        {
            var calendar = await _wslContext.WorkingCalendars.FirstOrDefaultAsync(x => x.DateDay.Date == date.Date);

            if (calendar != null)
            {
                if (!calendar.IsWorkingDay.HasValue || !calendar.IsWorkingDay.Value)
                {
                    var myday = await _wslContext.WorkingCalendars
                        .Where(x => x.DateDay.Date > date.Date && x.IsWorkingDay!.Value)
                        .OrderBy(x => x.DateDay)
                        .FirstOrDefaultAsync();

                    if (myday != null)
                        return myday.DateDay;
                }
            }

            return date;
        }

        private async Task<WslCourtSession?> GetCurrentCourtSessionAsync(int caseId)
        {
            var session = await _wslContext.CourtSessions
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(x => x.CaseId == caseId && x.StatusStage == 30);

            if(session == null)
            {
                session = await _wslContext.CourtSessions
                    .FirstOrDefaultAsync(x => x.CaseId == caseId && x.IsCurrent == true);
            }
                
            return session;
        }

        private async Task<long?> GetLawyerByCaseIdAsync(int caseId)
        {          
            var wslCase = await _wslContext.Cases.FirstOrDefaultAsync(x => x.Id == caseId); 

            List<long?> excludedIds = new List<long?>() { -1, 0, 1, 11, 12, 10059, 10413, 10498 }; //todo: add setting

            if (wslCase?.UserIdleader != null)
            {
                if(wslCase?.UserIdleader == 10059 || wslCase?.UserIdassistant ==10059) //архивное
                {
                    if (wslCase.ProjectId == 33 || wslCase.ProjectId == 47) //Пробизнесбанк
                    {
                        return 10413;
                    }
                    if (wslCase.ProjectId == 104) //Веб РФ
                    {
                        return null;
                    }
                }
                if (!excludedIds.Contains(wslCase.UserIdleader)) //исключаем служебных пользователей
                {                   
                    return wslCase.UserIdleader;
                }
                else return null;
            }
            else
            {
                return null;
            }
        }

        private async Task<long?> GetApprenticeByCaseIdAsync(int caseId)
        {
            var wslCase = await _wslContext.Cases.FirstOrDefaultAsync(x => x.Id == caseId);

            List<long?> excludedIds = new List<long?>() { -1, 0, 1, 11, 12, 10059, 10413, 10498 }; //todo: add setting

            if (wslCase?.UserIdassistant != null
                && !_wslContext.DepartmentUsers.Where(x => x.DepartmentId == 3)
                .ToList().Any(x => x.UserId == wslCase.UserIdassistant))
            {
                var taskExecutor = wslCase.UserIdassistant;
                if (wslCase?.UserIdleader == 10059 || wslCase?.UserIdassistant == 10059) //архивное
                {
                    if (wslCase.ProjectId == 33 || wslCase.ProjectId == 47) //Пробизнесбанк
                    {
                        return 10413;
                    }
                    if (wslCase.ProjectId == 104) //Веб РФ
                    {
                        return null;
                    }
                }
                if (excludedIds.Contains(taskExecutor))
                {
                    taskExecutor = null;
                }

                return taskExecutor;
            }
            return null;
        }

        private async Task<bool> CreateJTaskAsync(int caseId, MessageActionSettingTask jurisTask)
        {
            if (jurisTask.TaskType?.WSLawyerCode == null || jurisTask.TaskType?.WSLawyerCode <= 0)
                return false;

           // var wslCase = await _wslContext.Cases.FirstOrDefaultAsync(x => x.Id == caseId);
            var session = await GetCurrentCourtSessionAsync(caseId);
            if(session == null)
                return false;


            var addTask = new WslTask();
            var dt = DateTime.Today;
            addTask.CaseId = caseId;
            addTask.UserIdcreator = 13;
            addTask.UserIdupdate = 13;
            addTask.DateCreate = DateTime.Now;
            addTask.DateUpdate = DateTime.Now;
            addTask.UserIdexecutor = await GetExecutorIdAsync(caseId, jurisTask.ExecutorType?.Name);
            addTask.StatusType = (int)jurisTask.TaskType?.WSLawyerCode!;
            addTask.TaskTarget = "";
            addTask.BeginDate = await GetNearestWorkingDay(dt.AddDays(jurisTask.TimeLimit ?? 0));
            addTask.DateEndPlan = await GetNearestWorkingDay(dt.AddDays(jurisTask.TimeLimit ?? 0));
            addTask.DateEndFact = await GetNearestWorkingDay(dt.AddDays(jurisTask.TimeLimit ?? 0));
            addTask.Status = 10;
            addTask.Level = 0;
            addTask.SpentTime = 0;
            addTask.CourtSessionId = session.Id; 
            addTask.IsEvent = false;
            addTask.OnConfirmation = false;
            addTask.StatusSectionAct = 90; //задачи от робота банкрота всегда будут попадать в раздел 90
            addTask.IsNew = true;
            addTask.IsStatusChange = true;
            try
            {
                await _wslContext.Tasks.AddAsync(addTask);
                await _wslContext.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                //todo: log error
                return false;
            }
        }

        private async Task<long?> GetExecutorIdAsync(int caseId, string? name)
        {
            return name switch
            {
                (ExecutorTypes.Lawyer) => await GetLawyerByCaseIdAsync(caseId),
                (ExecutorTypes.Apprentice) => await GetApprenticeByCaseIdAsync(caseId),
                _ => null,
            };
        }
    }
}
