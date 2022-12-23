using System;
using System.Collections.Generic;
using CommandLine;
using GMLib;
using System.Text.Json;
using System.IO;
using System.IO.Pipes;
using PInvoke;
using System.Threading;

namespace GM
{
    class Program
    {
        static private Options options;
        static private AnonymousPipeClientStream pipeStream;
        static private StreamWriter pipeWriter;

        static private bool IsStopped = false;
        static private ManualResetEvent StopEvent = new(false);

        class Options
        {
            [Option(Default = false, HelpText = "Display debug messages")]
            public bool Debug { get; set; }

            [Option(Required = true, SetName="ps", HelpText = "Enumerate managed processes.")]
            public bool Ps { get; set; }

            [Option(Required = true, SetName = "pid", HelpText = "Attach to running program.")]
            public int Pid { get; set; }

            [Option(Required = true, SetName = "path", HelpText = "Create process and attach.")]
            public string Path { get; set; }

            [Option(SetName = "path", HelpText = "Program arguments.")]
            public string Arguments { get; set; }

            [Option(SetName = "path", HelpText = "Set the working directory (Default: %SYSTEMROOT%\\system32)")]
            public string WorkingDirectory { get; set; }

            [Option(SetName = "path", HelpText = "Wait before attaching (ms).")]
            public int Delay { get; set; }

            [Option(Required = true, SetName = "dump", HelpText = "Open crash dump file.")]
            public string CrashDump { get; set; }

            [Option(Default="database.db", HelpText = "Result database.")]
            public string DBPath { get; set; }

            [Option(Default = 1, HelpText = "Snapshot count.")]
            public int Count { get; set; }

            [Option(Default = 100, HelpText = "Snapshot interval (ms).")]
            public int Interval { get; set; }

            [Option(Default = new string[] { "all" }, HelpText = "Items collected. Possible values: 'basic', 'heap', 'refs', 'stack', 'threads', 'handles', 'all'.")]
            public IEnumerable<string> Items { get; set; }

            [Option(Default = new string[] {"all"}, HelpText = "Items collected after first snapshot.")]
            public IEnumerable<string> NextItems { get; set; }

            [Option(HelpText = "Write output as json to pipe.")]
            public string Pipe { get; set; }

        }
        static void Main(string[] args)
        {
            _ = CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunOptions);
        }
        static uint GetItemsFlag(IEnumerable<string> items)
        {
            uint flags = 0;
            foreach (string item in items)
            {
                switch (item)
                {
                    case "heap":
                        flags |= GMLib.Constants.COLLECT_HEAP;
                        break;
                    case "stack":
                        flags |= GMLib.Constants.COLLECT_STACK;
                        break;
                    case "threads":
                        flags |= GMLib.Constants.COLLECT_THREADS;
                        break;
                    case "basic":
                        flags |= GMLib.Constants.COLLECT_BASIC_INFO;
                        break;
                    case "handles":
                        flags |= GMLib.Constants.COLLECT_HANDLES;
                        break;
                    case "refs":
                        flags |= GMLib.Constants.COLLECT_REFS;
                        break;
                    case "all":
                        flags |= GMLib.Constants.COLLECT_EVERYTHING;
                        break;
                    default:
                        Console.WriteLine($"Illegal item: {item}");
                        Environment.Exit(1);
                        break;
                }
            }
            return flags;
        }

        static void PrintOutput(string msg, object arg = null)
        {
            if (options.Pipe == null)
            {
                Console.WriteLine(msg);
                if (arg != null)
                {
                    List<GMProcessInfo> psinfo = arg as List<GMProcessInfo>;
                    foreach (var p in psinfo)
                    {
                        Console.WriteLine($"{p.Pid} {p.Name} ({p.Runtimes[0]})");
                    }
                }
                return;
            }
            object cmdOutput;
            if (arg == null)
            {
                cmdOutput = new GMCmdOutput { Type = GMLib.Constants.CMD_OUTPUT_TYPE_MSG, Msg = msg };
            }
            else
            {
                cmdOutput = new GMCmdOutputPList { Type = GMLib.Constants.CMD_OUTPUT_TYPE_PSLIST, Msg = msg, PList = arg as List<GMProcessInfo> };
            }
            string jsonString = JsonSerializer.Serialize(cmdOutput);
            try
            {
                pipeWriter.WriteLine(jsonString);
                pipeWriter.Flush();
            }
            catch
            {
                throw;
            }
            return;
        }

        static void Exit(int exitCode)
        {
            if (options.Pipe != null)
            {
                pipeWriter.Close();
                pipeStream.Close();
            }
            Environment.Exit(exitCode);
        }

        private static bool ExitProcessHandler(Kernel32.ControlType sig)
        {
            PrintOutput("Shut down requested.");
            switch (sig)
            {
                case Kernel32.ControlType.CTRL_C_EVENT:
                case Kernel32.ControlType.CTRL_LOGOFF_EVENT:
                case Kernel32.ControlType.CTRL_SHUTDOWN_EVENT:
                case Kernel32.ControlType.CTRL_CLOSE_EVENT:
                case Kernel32.ControlType.CTRL_BREAK_EVENT:
                default:
                    IsStopped = true;
                    StopEvent.WaitOne();
                    return false;
            }
        }

        static void RunOptions(Options opts)
        {
            options = opts;
            if (opts.Pipe != null)
            {
                pipeStream = new AnonymousPipeClientStream(PipeDirection.Out, opts.Pipe);
                pipeWriter = new StreamWriter(pipeStream);
            }

            Kernel32.SetConsoleCtrlHandler(ExitProcessHandler, true);

            if (opts.Ps)
            {
                ProcessEnumerator ps = new();
                if (ps.PList.Count != 0)
                    PrintOutput("Process listing", ps.PList);
                Exit(0);
            }

            uint initFlags = GetItemsFlag(opts.Items);
            uint flags = GetItemsFlag(opts.NextItems);

            // Now with the configuration, create Collector and let it run
            Collector collector = null;
            if (opts.Pid != 0)
            {
                collector = new(
                    pid: opts.Pid,
                    dataBasePath: opts.DBPath,
                    dumpInterval: opts.Interval,
                    dumpCount: opts.Count,
                    initialFlags: initFlags,
                    flags: flags);
            }
            else if (opts.Path != null)
            {
                collector = new(
                    path: opts.Path,
                    dataBasePath: opts.DBPath,
                    args: opts.Arguments,
                    workingDirectory: opts.WorkingDirectory,
                    delay: opts.Delay,
                    dumpInterval: opts.Interval,
                    dumpCount: opts.Count,
                    initialFlags: initFlags,
                    flags: flags);
            }
            else if (opts.CrashDump != null)
            {
                if (opts.Count > 1)
                    PrintOutput("Ignoring count as it makes no sense with this option.");
                collector = new(
                    crashDump: opts.CrashDump,
                    dataBasePath: opts.DBPath,
                    initialFlags: initFlags,
                    flags: flags);
            }
            else
            {
                PrintOutput("ERROR: illegal command line");
                Exit(1);
            }

            collector.DoneEventHandler += (obj, evt) =>
            {
                PrintOutput("Collector is done.");
                if (IsStopped == true)
                {
                    StopEvent.Set();
                }
            };

            collector.DataEventHandler += (obj, evt) =>
            {
                if (IsStopped == true)
                {
                    ((TargetEventArgs)evt).Stop = true;
                }
            };

            if (opts.Debug)
            {
                collector.DebugEventHandler += (obj, evt) =>
                {
                    PrintOutput(evt.Msg);
                };
            }

            PrintOutput("Running data collector...");
            try
            {
                collector.Run();
            }
            catch (Exception e)
            {
                PrintOutput($"ERROR: {e.Message}");
                Exit(1);
            }
            if (!IsStopped)
            {
                PrintOutput($"SUCCESS: database {opts.DBPath} created, good luck!");
                Exit(0);
            }
            else Exit(1);
        }
    }
}
