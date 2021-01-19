using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneSTools.EventLog.Exporter.ClickHouse;
using OneSTools.EventLog.Exporter.Core;
using OneSTools.EventLog.Exporter.ElasticSearch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Manager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSystemd()
                .ConfigureLogging((hostingContext, logging) =>
                {
                    var logPath = Path.Combine(hostingContext.HostingEnvironment.ContentRootPath, "log.txt");
                    logging.AddFile(logPath);
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var storageType = hostContext.Configuration.GetValue("Exporter:StorageType", StorageType.None);

                    switch (storageType)
                    {
                        case StorageType.ClickHouse:
                            services.AddSingleton<IEventLogStorage, ClickHouseStorage>();
                            break;
                        case StorageType.ElasticSearch:
                            services.AddTransient<IEventLogStorage, ElasticSearchStorage>();
                            break;
                        case StorageType.None:
                            throw new Exception($"You must set StorageType parameter before starting the exporter");
                        default:
                            throw new Exception($"{storageType} is not available value of StorageType enum");
                    }

                    services.AddSingleton<EventLogExporter>();
                    services.AddHostedService<ExportersManager>();
                });
    }
}
