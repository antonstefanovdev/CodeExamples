using App.Base.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Data;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Models;
using QLab.Robot.Bankrupt.Efrsb.MessageService.Services;
using System.Net;
using System.Text;

namespace QLab.Robot.Bankrupt.Efrsb.MessageService
{
    internal class HostedService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private Timer? _timer;

        public HostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var seed = serviceProvider.GetRequiredService<Seed>();
            try
            {
                await seed.InitializeAsync();
            }
            catch (Exception ex)
            {
                //TODO:
            }

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.Zero);
        }

        private async void DoWork(object? state)
        {
            using var scope = _serviceProvider.CreateScope();
            var serviceProvider = scope.ServiceProvider;

            var messageServise = serviceProvider.GetRequiredService<MessageService>();
            var taskManager = serviceProvider.GetRequiredService<TaskManager>();

            await messageServise.RequestMessagesAsync();
            await taskManager.CreateTasksAsync();

            var timeOut = GetTimeOut();
            _timer?.Change(timeOut, TimeSpan.Zero);
        }

        private static TimeSpan GetTimeOut()
        {            
            var tSpan = TimeSpan.FromMinutes(60);
            return tSpan;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }
    }
}
