using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneSTools.EventLog.Exporter.Core;

namespace OneSTools.EventLog.Exporter.ElasticSearch
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
                    services.AddSingleton<IEventLogStorage<EventLogItem>, EventLogStorage<EventLogItem>>();
                    services.AddSingleton<IEventLogExporter<EventLogItem>, EventLogExporter<EventLogItem>>();
                    services.AddHostedService<EventLogExporterService<EventLogItem>>();
                });
    }
}
