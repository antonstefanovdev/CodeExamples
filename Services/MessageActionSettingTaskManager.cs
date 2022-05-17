using App.Base.Extensions;
using App.Base.Interfaces;
using Microsoft.AspNetCore.Identity;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Models;

namespace QLab.Robot.Bankrupt.Efrsb.MessageService.Services
{
    public class MessageActionSettingTaskManager : IManager<MessageActionSettingTask>
    {
        private readonly IRepository<MessageActionSettingTask> _repository;

        public MessageActionSettingTaskManager(IRepository<MessageActionSettingTask> repository)
        {
            _repository = repository;
        }

        public IQueryable<MessageActionSettingTask> DataSet => _repository.DbSet;

        public async Task<IdentityResult> CreateAsync(MessageActionSettingTask item)
        {
            try
            {
                await _repository.AddAsync(item);
                return IdentityResult.Success;
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(ex.GetErrors());
            }
        }

        public Task<IdentityResult> CreateAsync(List<MessageActionSettingTask> items, Action<MessageActionSettingTask>? action = null)
        {
            throw new NotImplementedException();
        }

        public async Task<IdentityResult> DeleteAsync(MessageActionSettingTask item)
        {
            try
            {
                await _repository.DeleteAsync(item);
                return IdentityResult.Success;
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(ex.GetErrors());
            };
        }

        public async Task<MessageActionSettingTask?> FindByIdAsync(object id)
        {
            return await _repository.FindByIdAsync(id);
        }

        public async Task<IdentityResult> UpdateAsync(MessageActionSettingTask item)
        {
            try
            {
                await _repository.UpdateAsync(item);
                return IdentityResult.Success;
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(ex.GetErrors());
            }
        }
    }
}
