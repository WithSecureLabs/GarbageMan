using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime;
using System.IO;

namespace GMLib
{

    public class DebugEventArgs : EventArgs
    {
        public string Msg { get; set; }
        public DebugEventArgs(string msg)
        {
            Msg = msg;
        }
    }
    // Collector's job is to organize all the data sources (processes, dumps, snapshots etc)
    // as a single, coherent source of events
    // XXX: add support for DataTarget.CreateSnapshotAndAttach and use threading for processing targets
    public class Collector
    {

        private bool Stopped = false;
        public uint Flags { get; set; }
        public uint InitialFlags { get; set; }
        public int Interval { get; set; }
        public int Count { get; set; }
        public string Path { get; set; }
        public string Args { get; set; }
        public string WorkingDirectory { get; set; }
        public int Delay { get; set; }
        public string CrashDump { get; set; }
        public int Pid { get; set; }
        public List<int> Pids { set; get; } = new();
        public DataBase Db { get; set; } = null;
        string DataBasePath { get; set; }

        public event EventHandler<EventArgs> DoneEventHandler;

        // Just a proxy from target handlers
        public event EventHandler<EventArgs> DataEventHandler;

        public event EventHandler<DebugEventArgs> DebugEventHandler;

        void DbgMsg(string msg)
        {
            DebugEventHandler?.Invoke(this, new DebugEventArgs(msg));
        }

        public Collector(
            int pid,
            string dataBasePath = null,
            uint flags = Constants.COLLECT_EVERYTHING,
            uint initialFlags = Constants.COLLECT_EVERYTHING,
            int dumpInterval = 0,
            int dumpCount = 1)
        {
            Interval = dumpInterval;
            Count = dumpCount;
            Pid = pid;
            Flags = flags;
            InitialFlags = initialFlags;
            DataBasePath = dataBasePath;
        }

        public Collector(
            string crashDump,
            uint flags = Constants.COLLECT_EVERYTHING,
            uint initialFlags = Constants.COLLECT_EVERYTHING,
            string dataBasePath = null,
            int dumpInterval = 0,
            int dumpCount = 1)
        {
            Interval = dumpInterval;
            Count = dumpCount;
            CrashDump = crashDump;
            Flags = flags;
            InitialFlags = initialFlags;
            DataBasePath = dataBasePath;
        }

        // Added the option to set the working directory for the process
        public Collector(
            string path,
            string args,
            string workingDirectory,
            int delay = 500,
            uint flags = Constants.COLLECT_EVERYTHING,
            uint initialFlags = Constants.COLLECT_EVERYTHING,
            string dataBasePath = null,
            int dumpInterval = 0,
            int dumpCount = 1)
        {
            Path = path;
            Args = args;
            WorkingDirectory = workingDirectory;
            Delay = delay;
            Interval = dumpInterval;
            Count = dumpCount;
            Flags = flags;
            InitialFlags = initialFlags;
            DataBasePath = dataBasePath;
        }

        // XXX: how to handle child processes?
        // Maybe some external tool for checking any children when looping over snapshots?
        // This loop needs to be completely rewritten for child process support, now it's messy
        public int Run()
        {
            DbgMsg($"Collector started for {Count} snapshots, flags: {InitialFlags:X8}");

            if (DataBasePath != null)
                Db = new(DataBasePath);

            uint flags = InitialFlags;
            long time = 0;
            int id;
            for (id = 1; id < Count+1; id++)
            {
                GMProcess process = new();
                Target target = null;
                if (Pid != 0)
                {
                    DbgMsg($"Attaching to process {Pid}");
                    // Add process only once
                    if (id == 1)
                    {
                        process.Pid = Pid;
                        process.Arch = IntPtr.Size == 8 ? "AMD64" : "X86";
                        process.Date = DateTime.Now.ToString();
                    }
                    try
                    {
                        target = new Target(DataTarget.AttachToProcess(Pid, suspend: true), time, id, Pid, flags);
                    }
                    catch (Exception e)
                    {
                        if (id == 1) throw;
                        else
                        {
                            DbgMsg(e.Message);
                            break;
                        }
                    }
                }
                else if (CrashDump != null)
                {
                    DbgMsg($"Using crash dump file {CrashDump}");
                    process.Pid = 0;
                    process.Path = CrashDump;
                    process.Arch = IntPtr.Size == 8 ? "AMD64" : "X86";
                    process.Date = DateTime.Now.ToString();
                    target = new Target(DataTarget.LoadDump(CrashDump), time, id, flags: flags);
                }
                else if (Path != null)
                {
                    DbgMsg($"Starting process {Path} with arguments: {Args}");
                    Process proc = new Process();
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.FileName = Path;
                    if (Args != null)
                        proc.StartInfo.Arguments = Args;
                    if (WorkingDirectory != null)
                        proc.StartInfo.WorkingDirectory = WorkingDirectory;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.Start();
                    Pid = proc.Id;

                    process.Pid = Pid;
                    process.Path = Path;
                    process.Args = Args;
                    process.Arch = IntPtr.Size == 8 ? "AMD64" : "X86";
                    process.Date = DateTime.Now.ToString();
                    Thread.Sleep(Delay);
                    time += Delay;
                    target = new Target(DataTarget.AttachToProcess(Pid, suspend: true), time, id, Pid, flags);
                }
                if (target == null)
                {
                    if (id == 1)
                        throw new Exception("Cannot find suitable data target");
                    else break;
                }

                GMSnapshot snapshot = new GMSnapshot
                {
                    Id = id,
                    Pid = Pid,
                    Time = time,
                    PointerSize = target.Dt.DataReader.PointerSize
                };

                target.DataEventHandler += (obj, evt) =>
                {
                    // Just push the object to caller if needed
                    DataEventHandler?.Invoke(obj, evt);
                    Stopped = evt.Stop;
                };

                target.DebugEventHandler += (obj, evt) =>
                {
                    DbgMsg(evt.Msg);
                };

                DbgMsg($"Processing target {id} with pid={target.Pid}...");
                try
                {
                    target.Collect();
                }
                catch (Exception e)
                {
                    DbgMsg(e.Message);
                    // Scan was canceled
                    if (Stopped)
                    {
                        DbgMsg("User stopped data collection.");
                    }
                    target.Close();
                    // First target failed, throw exception
                    if (id == 1)
                        throw;
                    else break;
                }

                if (DataBasePath != null)
                    DbgMsg("Adding target to database...");

                // Add the process only once
                if (id == 1)
                    Db?.AddProcess(process);
                Db?.AddSnapshot(snapshot);
                Db?.AddTarget(target);
                target.Close();

                if (CrashDump != null)
                    break;
                if (id < Count)
                {
                    DbgMsg($"Wait interval: {Interval}ms");
                    Thread.Sleep(Interval);
                    time += Interval;
                }
                flags = Flags;
            }

            DbgMsg($"Collected data from {id} targets");
            DoneEventHandler?.Invoke(this, null);
            return 0;
        }
    }
}
