using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtlpServer.Db;

namespace OtlpServer
{
    public class MigrationStartupService : IHostedLifecycleService
    {
        private readonly IServiceProvider provider;

        public MigrationStartupService(IServiceProvider provider)
        {
            this.provider = provider;
        }
        public async Task StartingAsync(CancellationToken cancellationToken)
        {
            using var scope = provider.CreateScope();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var dbContext = scope.ServiceProvider.GetRequiredService<TraceContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        #region unused methods
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        #endregion
    }
}
