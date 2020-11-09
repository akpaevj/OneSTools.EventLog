using Microsoft.EntityFrameworkCore;
using OneSTools.EventLog.Exporter.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EFCore.BulkExtensions;

namespace OneSTools.EventLog.Exporter.SqlServer
{
    public class AppDbContext<T> : DbContext where T : class, IEventLogItem, new()
    {
        public DbSet<T> EventLogItems { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext<T>> options) : base(options)
            => Database.Migrate();
    }
}