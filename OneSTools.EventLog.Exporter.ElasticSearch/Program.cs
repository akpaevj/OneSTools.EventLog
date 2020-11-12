using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                .ConfigureAppConfiguration(c => {
                    c.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                    c.AddJsonFile("appsettings.json");
                })
                .UseWindowsService()
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IEventLogStorage<EventLogItem>, EventLogStorage<EventLogItem>>();
                    services.AddHostedService<EventLogExporterService<EventLogItem>>();
                });
    }
}
