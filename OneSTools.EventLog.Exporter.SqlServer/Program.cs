using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneSTools.EventLog.Exporter.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection;

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
                .ConfigureAppConfiguration(c => {
                    c.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                    c.AddJsonFile("appsettings.json");
                })
                .UseWindowsService()
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddEntityFrameworkSqlServer()
                        .AddDbContext<AppDbContext<EventLogItem>>((sp, opt) =>
                            opt.UseSqlServer(hostContext.Configuration["ConnectionStrings:Default"])
                                .UseInternalServiceProvider(sp));

                    services.AddSingleton<IEventLogStorage<EventLogItem>, EventLogStorage>();
                    services.AddHostedService<EventLogExporterService<EventLogItem>>();
                });
    }
}
