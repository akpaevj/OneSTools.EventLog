using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.EventLog;
using OneSTools.EventLog;
using OneSTools.EventLog.Exporter.Core;
using OneSTools.EventLog.Exporter.ElasticSearch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EventLogExporterElasticSearch
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
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IEventLogStorage<EventLogItem>, EventLogStorage<EventLogItem>>();
                    services.AddSingleton<IEventLogExporter<EventLogItem>, EventLogExporter<EventLogItem>>();
                    services.AddHostedService<EventLogExporterService<EventLogItem>>();
                });
    }
}
