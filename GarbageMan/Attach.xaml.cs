using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading;
using GMLib;
using System.IO;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO.Pipes;
using System.Text.Json;
using System.Diagnostics;
using PInvoke;
using System.Runtime.InteropServices;

namespace GarbageMan
{

    public partial class Attach : Window
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(Kernel32.ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        public string BasePath { get; set; }
        public string DataBasePath { get; set; }
        public string RealPath { get; set; }

        private BackgroundWorker _worker = null;
        private WorkerArguments _args;
        private ManualResetEvent _dumpClosing = new(false);

        void CancelDump()
        {
            if (_worker != null && _worker.IsBusy)
            {
                _args.IsStopped = true;
                _args.Dumper.Kill();
                _dumpClosing.WaitOne();
                _worker = null;
            }
        }

        void Attach_Closing(object sender, CancelEventArgs e)
        {
            CancelDump();
        }

        public Attach(string basePath)
        {
            InitializeComponent();
            DataBasePath = null;
            BasePath = basePath;
        }

        private void AttachPidPickerButton_Click(object sender, RoutedEventArgs e)
        {
            Window picker = new PickProcess(BasePath);
            picker.Owner = this;
            picker.Closed += (a, b) =>
            {
            };
            picker.Show();
        }

        private void AttachCancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelDump();
            DataBasePath = null;
            this.Close();
        }

        private void AttachStartButton_Click(object sender, RoutedEventArgs e)
        {
            // Get all the settings
            int pid = int.Parse((AttachPidTextBox.Text == "") ? "0" : AttachPidTextBox.Text);
            int count = int.Parse((AttachSnapshotCountTextBox.Text == "") ? "1" : AttachSnapshotCountTextBox.Text);
            int interval = int.Parse((AttachSnapshotIntervalTextBox.Text == "") ? "0" : AttachSnapshotIntervalTextBox.Text);

            string initialFlags = "";
            if ((bool)AttachInitialBasicCheckBox.IsChecked) initialFlags += "basic refs ";
            if ((bool)AttachInitialHeapCheckBox.IsChecked) initialFlags += "heap ";
            if ((bool)AttachInitialStackCheckBox.IsChecked) initialFlags += "stack threads ";
            string nextFlags = "";
            if ((bool)AttachNextBasicCheckBox.IsChecked) nextFlags += "basic refs ";
            if ((bool)AttachNextHeapCheckBox.IsChecked) nextFlags += "heap ";
            if ((bool)AttachNextStackCheckBox.IsChecked) nextFlags += "stack threads ";

            RealPath = System.IO.Path.GetTempFileName();
//            string dbPath = AttachDatabaseNameTextBox.Text;

            if (pid == 0)
                System.Windows.MessageBox.Show("Please pick up proper process", "Attach", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (initialFlags == "" || (count > 1 && nextFlags == ""))
                System.Windows.MessageBox.Show("No features selected!", "Attach", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (count > 1 && interval == 0)
                System.Windows.MessageBox.Show("Please set the interval", "Attach", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (AttachDatabaseNameTextBox.Text == "")
                System.Windows.MessageBox.Show("Please pick proper filename", "Attach", MessageBoxButton.OK, MessageBoxImage.Information);
            else
            {
                string cmdLine = $"--pid {pid} --dbpath {RealPath} --items {initialFlags} ";
                if (count > 1)
                    cmdLine += $"--count {count} --interval {interval} --nextitems {nextFlags}";

                AttachStatusText.Visibility = Visibility.Visible;
                AttachProgressBar.Visibility = Visibility.Visible;

                _worker = new BackgroundWorker();
                _worker.DoWork += backgroundWorker_Dump;

                _worker.WorkerReportsProgress = true;
                _worker.ProgressChanged += backgroundWorker_ProgressChanged;

                _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    (sender, e) =>
                    {
                        AttachStatusText.Visibility = Visibility.Hidden;
                        AttachProgressBar.Visibility = Visibility.Hidden;
                        if ((bool)e.Result)
                        {
                            _worker = null;
                            DataBasePath = AttachDatabaseNameTextBox.Text;
                            this.Close();
                        }
                    });
                _args = new WorkerArguments { CommandLine = cmdLine, BasePath = this.BasePath, Pid = pid, Done = _dumpClosing };
                _worker.RunWorkerAsync(argument: _args);
            }
        }

        void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            AttachStatusText.Text = e.UserState.ToString();
        }

        static void backgroundWorker_Dump(object sender, DoWorkEventArgs e)
        {
            bool success = false;
            WorkerArguments args = e.Argument as WorkerArguments;
            using (var handle = Kernel32.OpenProcess(Kernel32.ProcessAccess.PROCESS_QUERY_LIMITED_INFORMATION | Kernel32.ProcessAccess.PROCESS_VM_READ, false, args.Pid))
            {
                if (!handle.IsInvalid)
                {
                    success = DumpPid(sender, args, Kernel32.IsWow64Process(handle));
                }
                else
                    MessageBox.Show($"Cannot open process", "Attach", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Result = success;
        }

        static void ResumeTarget(int pid)
        {
            var process = Process.GetProcessById(pid);
            if (process.ProcessName == string.Empty)
                return;
            foreach (ProcessThread pT in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(Kernel32.ThreadAccess.THREAD_SUSPEND_RESUME, false, (uint)pT.Id);
                if (pOpenThread == IntPtr.Zero)
                {
                    continue;
                }
                var suspendCount = 0;
                do
                {
                    suspendCount = ResumeThread(pOpenThread);
                } while (suspendCount > 0);

                CloseHandle(pOpenThread);
            }
        }

        static bool DumpPid(object sender, WorkerArguments args, bool Is32bit)
        {
            bool success = false;

            string arch = Is32bit ? "x86" : "x64";
            string exePath = args.BasePath + $"bin\\{arch}\\GM.exe";
            var process = new Process
            {
                StartInfo =
                {
                    FileName = exePath,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            args.Dumper = process;

            using (var pipeRead = new AnonymousPipeServerStream(PipeDirection.In,
                HandleInheritability.Inheritable))
            {
                process.StartInfo.Arguments = $"--debug --pipe {pipeRead.GetClientHandleAsString()} ";
                process.StartInfo.Arguments += args.CommandLine;
                process.Start();

                pipeRead.DisposeLocalCopyOfClientHandle();
                using (var sr = new StreamReader(pipeRead))
                {
                    string temp;
                    int i = 0;
                    while ((temp = sr.ReadLine()) != null)
                    {
                        if (args.IsStopped)
                        {
                            break;
                        }
                        GMCmdOutput output = JsonSerializer.Deserialize<GMCmdOutput>(temp);
                        if (!output.Msg.Contains("ERROR"))
                            (sender as BackgroundWorker).ReportProgress(i++, output.Msg);
                        else
                        {
                            MessageBox.Show($"{output.Msg}", "Attach", MessageBoxButton.OK, MessageBoxImage.Error);
                            process.Close();
                            return false;
                        }
                        if (output.Msg.Contains("SUCCESS"))
                        {
                            process.Close();
                            return true;
                        }
                    }
                }
            }
            if (args.IsStopped)
            {
                // Dumper process was just brutally killed, so we need to resume target here manually
                process.WaitForExit();
                ResumeTarget(args.Pid);
            }
            else
                process.WaitForExit();
            process.Close();
            args.Done?.Set();
            return success;
        }
        private void AttachDatabasePickerButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
            saveFileDialog.FileName = AttachDatabaseNameTextBox.Text;
            if (saveFileDialog.ShowDialog() == true)
                AttachDatabaseNameTextBox.Text = saveFileDialog.FileName;
        }
    }
}
