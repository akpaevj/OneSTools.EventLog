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
                    var configuration = hostContext.Configuration;
                    var host = configuration.GetValue("ElasticSearch:Host", "");
                    var port = configuration.GetValue("ElasticSearch:Port", 9200);
                    var index = configuration.GetValue("ElasticSearch:Index", "");
                    var separation = configuration.GetValue("ElasticSearch:Separation", "");

                    services.AddSingleton<IEventLogStorage<EventLogItem>>(sp =>
                        new EventLogStorage<EventLogItem>(host, port, index, separation));
                    services.AddSingleton<IEventLogExporter<EventLogItem>, EventLogExporter<EventLogItem>>();

                    services.AddHostedService<EventLogExporterService<EventLogItem>>();
                });
    }
}
