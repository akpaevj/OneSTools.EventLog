using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneSTools.EventLog.Exporter.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.SqlServer
{
    public class EventLogStorage : IEventLogStorage
    {
        private readonly IServiceProvider _serviceProvider;
        public EventLogStorage(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<EventLogPosition> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await context.EventLogPositions.FirstOrDefaultAsync(cancellationToken);
        }

        public async Task WriteEventLogDataAsync(EventLogPosition eventLogPosition, List<EventLogItem> entities, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE [EventLogPositions]", cancellationToken);
                await context.EventLogPositions.AddAsync(eventLogPosition, cancellationToken);
                await context.BulkInsertAsync(entities);

                context.ChangeTracker.AutoDetectChangesEnabled = true;
                await context.SaveChangesAsync();

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);

                throw ex;
            }
        }

        public void Dispose()
        {
            
        }
    }
}
