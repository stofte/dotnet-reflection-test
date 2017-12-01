namespace Analyzer
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;
    using Buildalyzer;
    using System.Diagnostics;

    class Program
    {

        static string TargetJson()
        {
            return Path.Combine(Environment.CurrentDirectory, "Reflect", "ref-list.json");
        }

        static string TargetFolder()
        {
            return Path.Combine(Environment.CurrentDirectory, "Reflect", "refs");
        }

        static void Main(string[] args)
        {
            var createDist = args.Length > 0 && args[0].Contains("dist");
            var allRefs = GetReferences();
            if (!File.Exists(TargetJson()))
            {
                MakeRun(0, allRefs);
            }

            if (createDist)
            {
                Console.WriteLine("Creating file store");
                var json = File.ReadAllText(TargetJson());
                var list = JsonConvert.DeserializeObject<IEnumerable<string>>(json);
                if (!Directory.Exists(TargetFolder()))
                {
                    Directory.CreateDirectory(TargetFolder());
                }
                else
                {
                    var files = Directory.GetFiles(TargetFolder());
                    foreach(var f in files)
                    {
                        File.Delete(f);
                    }
                }
                foreach(var f in allRefs)
                {
                    var newName = Path.Combine(TargetFolder(), Path.GetFileName(f));
                    File.Copy(f, newName);
                }
            }
        }

        static bool MakeRun(int runId, IEnumerable<string> refs)
        {
            var json = JsonConvert.SerializeObject(refs.Select(x => Path.GetFileName(x)), Formatting.Indented);
            File.WriteAllText(TargetJson(), json);
            return true;
            // return StartDotnet(runId);
        }

        static IEnumerable<string> GetReferences()
        {
            // https://github.com/daveaglick/Buildalyzer
            var projPath = Path.Combine(ProjectPath(), "Reflect", "Reflect.csproj");
            var manager = new AnalyzerManager();
            var analyzer = manager.GetProject(projPath);
            analyzer.Load();
            var refs = analyzer.GetReferences();
            Console.WriteLine("Reflect.csproj looks to have about {0} references", refs.Count());
            var orderedBySize = new Dictionary<string, int>();
            foreach(var r in refs)
            {
                orderedBySize.Add(r, File.ReadAllBytes(r).Count());
            }
            return orderedBySize.OrderByDescending(x => x.Value).Select(x => x.Key);
        }

        static string ProjectPath()
        {
            var basePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            // f5
            if (basePath.EndsWith(Path.Combine("Analyzer", "bin", "Debug", "netcoreapp2.0")))
            {
                basePath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", ".."));
            }
            else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPVEYOR")))
            {
                basePath = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            }
            else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TRAVIS")))
            {
                basePath = Environment.GetEnvironmentVariable("TRAVIS_BUILD_DIR");
            }
            return basePath;
        }

        public static bool StartDotnet(int runId)
        {   
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = @"C:\Program Files\dotnet\dotnet.exe",
                    Arguments = "run -p Reflect " + runId,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            // Console.WriteLine("Analyzer saw {0}", process.ExitCode);
            return process.ExitCode == 83;
        }
    }
}
