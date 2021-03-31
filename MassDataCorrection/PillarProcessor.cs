using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MassDataCorrection
{
    public class PillarProcessor
    {
        private string _path;

        public PillarProcessor(string instancePath)
        {
            _path = instancePath;
        }

        public void Process(
            Action<InstanceInfo, Action<float>> processor,
            string[] instances = null)
        {
            var files = Directory.GetFiles(_path, "*.sls");

            if (instances != null && instances.Length > 0)
                files = files
                    .Where(x => instances.Contains(x.Split("_")[1].Split(".")[0]))
                    .ToArray();

            for (int i = 1; i <= files.Length; i++)
                ProcessPillar(files[i - 1], i, files.Length, processor);
        }

        private void ProcessPillar(
            string file,
            int current,
            int total,
            Action<InstanceInfo, Action<float>> processor)
        {
            InstanceInfo instInfo = null;
            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Processing {current}/{total}: {Path.GetFileName(file)}");
                Console.ForegroundColor = ConsoleColor.Gray;

                instInfo = new InstanceInfo(file);

                var s = new Stopwatch();
                s.Start();
                processor(instInfo, ReportProgress);
                s.Stop();

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Elapsed time: {s.Elapsed.TotalMilliseconds.ToString("#,##0")}ms\n");
                Console.ForegroundColor = ConsoleColor.Gray;

                Console.WriteLine("\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine(new string('-', 50));
                Console.WriteLine(ex.ToString());
                Console.WriteLine(new string('-', 50));
                Console.ReadKey();
            }
        }

        private void ReportProgress(float percent)
        {
            var cur = 0;
            var progressMax = 50; // chars

            Console.SetCursorPosition(0, Console.CursorTop);
            cur = (int)Math.Ceiling(50f * Math.Clamp(percent, 0f, 1f));
            Console.Write($"[{new String('#', cur)}{new String(' ', progressMax - cur)}] {percent.ToString("0%")}   ");
        }

        private string GetPillarSetting(string settingName, string contents)
        {
            var exp = new Regex($"(?<={settingName}:)\\s*.*?(?=(\\s|\\n))", RegexOptions.IgnoreCase);

            var match = exp.Match(contents);
            if (match != null && match.Index > -1)
                return match.Value.Trim();

            return null;
        }
    }

    public enum SosyncVersion
    {
        All,
        Sosync1,
        Sosync2
    }
}
