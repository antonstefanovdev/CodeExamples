using App.Base.Extensions;
using App.Base.Interfaces;
using App.Base.Services;
using Microsoft.AspNetCore.Identity;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QLab.Robot.Bankrupt.Efrsb.MessageService.Services
{
    public class MessageActionSettingManager : IManager<MessageActionSetting>
    {
        private readonly IRepository<MessageActionSetting> _repository;

        public MessageActionSettingManager(IRepository<MessageActionSetting> repository)
        {
            _repository = repository;
        }

        public IQueryable<MessageActionSetting> DataSet => _repository.DbSet;

        public async Task<IdentityResult> CreateAsync(MessageActionSetting item)
        {
            try
            {
                item.Key = item.Key.Capitalize()!;
                item.NormalizedKey = item.Key.Normalize(Case.Upp)!;

                await _repository.AddAsync(item);

                return IdentityResult.Success;
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(ex.GetErrors());
            }
        }

        public Task<IdentityResult> CreateAsync(List<MessageActionSetting> items, Action<MessageActionSetting>? action = null)
        {
            throw new NotImplementedException();
        }

        public async Task<IdentityResult> DeleteAsync(MessageActionSetting item)
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

        public async Task<MessageActionSetting?> FindByIdAsync(object id)
        {
            return await _repository.FindByIdAsync(id);
        }

        public async Task<IdentityResult> UpdateAsync(MessageActionSetting item)
        {
            try
            {
                item.NormalizedKey = item.Key.Normalize(Case.Upp)!;
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
