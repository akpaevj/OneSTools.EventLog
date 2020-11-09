using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneSTools.EventLog.Exporter.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace OneSTools.EventLog.Exporter.SqlServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;

                    services.AddEntityFrameworkSqlServer()
                        .AddDbContext<AppDbContext<EventLogItem>>((sp, opt) =>
                            opt.UseSqlServer(configuration["ConnectionStrings:Default"])
                                .UseInternalServiceProvider(sp));

                    services.AddSingleton<IEventLogStorage<EventLogItem>, EventLogStorage>();
                    services.AddSingleton<IEventLogExporter<EventLogItem>, EventLogExporter<EventLogItem>>();
                    services.AddHostedService<EventLogExporterService<EventLogItem>>();
                });
    }
}
