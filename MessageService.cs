using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Data;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Dto.MessageService;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Interfaces;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Models;
using System.Linq;
using System.ServiceModel;
using System.Xml;

namespace QLab.Robot.Bankrupt.Efrsb.MessageService
{
    internal class MessageService
    {
        private readonly IOptions<EfrsbRequestOptions> _requestOptions;
        private readonly MessageServiceDbContext _context;

        public MessageService(
            IOptions<EfrsbRequestOptions> requestOptions,
            MessageServiceDbContext context)
        {
            _requestOptions = requestOptions;
            _context = context;
        }

        EfrsbRequestOptions RequestOptions => _requestOptions.Value;

        internal async Task RequestMessagesAsync()
        {
            if (!ValidateClientOptions(RequestOptions))
                return;

            ChannelFactory<IMessageService>? channelFactory = null;

            try
            {                         
                var endPoint = new EndpointAddress(RequestOptions.BaseUrl);
                var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
                {
                    MaxBufferSize = 524288,
                    MaxReceivedMessageSize = 524288
                };
                binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Digest;

                channelFactory = new(binding, endPoint);
                channelFactory.Credentials.HttpDigest.ClientCredential.UserName = RequestOptions.Login;
                channelFactory.Credentials.HttpDigest.ClientCredential.Password = RequestOptions.Password;
                var messageService = channelFactory.CreateChannel();

                await GetMessageIdsAsync(messageService);
                await GetMessageContentsAsync(messageService);               
            }
            catch (Exception ex)
            {
            }
            finally
            {
                channelFactory?.Close();
            }
        }

        private async Task GetMessageIdsAsync(IMessageService messageService)
        {
            DateTime startDate = await GetStartDateAsync();
            GetMessageIdsRequest request = new(startDate, DateTime.Now);

            var response = await messageService.GetMessageIdsAsync(request);
            var result = response.GetMessageIdsResult;

            if (result != null && result.Any())
            {
                var efrsbIdsResponse = new EfrsbResponse
                {
                    DateTimeOfWrited = DateTime.Now,
                    Messages = result
                    .Select(x => new EfrsbMessage
                    {
                        EfrsbMessageId = x,
                    })
                    .ToList(),
                };

                
                try
                {
                    //ToDo: вынести
                    await _context.EfrsbResponses!.AddAsync(efrsbIdsResponse);
                    await _context.SaveChangesAsync();
                }
                catch(Exception ex)
                {
                    //TOdo: LogError
                }                
            }
        }

        private async Task GetMessageContentsAsync(IMessageService messageService)
        {
            var messages = await _context.EfrsbMessages!
                   .Where(x => x.MessageContent == null)
                   .ToListAsync();

            foreach (var efrsbMessage in messages)
            {
                try
                {
                    var messageContent = await messageService.GetMessageContentAsync(efrsbMessage.EfrsbMessageId);

                    if (string.IsNullOrEmpty(messageContent))
                        throw new FaultException("Пустое сообщение.");

                    efrsbMessage.MessageContent = messageContent;
                    await SaveEfrsbMessageAsync(efrsbMessage);
                }
                catch(Exception ex)
                {
                    efrsbMessage.IsFailed = true;
                    efrsbMessage.MessageContent = $"{ex.Message}";
                    await SaveEfrsbMessageAsync(efrsbMessage);
                }              
            }
        }

        //ToDo: вынести
        private async Task SaveEfrsbMessageAsync(EfrsbMessage efrsbMessage)
        {
            try
            {
                efrsbMessage.DateTimeOfWrited = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            catch(Exception ex)
            {
                //ToDo: LogError
            }
        }

        private  async Task<DateTime> GetStartDateAsync()
        {
            //ToDo: через manager
            if (!_context.EfrsbResponses!.Any())
                return DateTime.Now.AddHours(-1);

            var startDate = await _context.EfrsbResponses!
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => x.DateTimeOfWrited)
                .FirstOrDefaultAsync();

            return startDate;                       
        }
              
        private bool ValidateClientOptions(EfrsbRequestOptions options)
        {
            var result = !string.IsNullOrEmpty(options.Login)
                && !string.IsNullOrEmpty(options.Password)
                && !string.IsNullOrEmpty(options.BaseUrl);

            if (!result)
            {
                //ToDo: LogError, SendMessage "Не указаны параметры EFRSB_RequestOptions в файле apsettings.json"(кому?) 
            }

            return result;
        }
    }    
}
