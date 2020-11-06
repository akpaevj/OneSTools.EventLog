using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OneSTools.EventLog;

namespace OneSTools.EventLogReaderTest
{
    class Program
    {
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        static void Main(string[] args)
        {
            var count = 0;

            using var reader = new EventLogReader("C:\\Users\\akpaev.e.ENTERPRISE\\Desktop\\1Cv8Log");

            var watcher = new Stopwatch();

            watcher.Start();

            while (true)
            {
                var item = reader.ReadNextEventLogItem(_cancellationTokenSource.Token);

                if (item == null)
                    break;
                else
                    count++;
            }

            watcher.Stop();

            var sec = watcher.ElapsedMilliseconds / 1000;

            WriteOnLine($"Считано {count} событий ({count / sec} событий в секунду)", 0);

            Console.ReadKey();
        }

        private static void WriteOnLine(string str, int line)
        {
            Console.SetCursorPosition(0, line);
            Console.WriteLine(str);
        }
    }
}
