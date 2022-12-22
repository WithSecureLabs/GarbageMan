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
using GMLib;
using System.IO;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO.Pipes;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;


namespace GarbageMan
{
    public enum BinaryType : uint
    {
        SCS_32BIT_BINARY = 0,   // A 32-bit Windows-based application
        SCS_64BIT_BINARY = 6,   // A 64-bit Windows-based application.
        SCS_DOS_BINARY = 1,     // An MS-DOS � based application
        SCS_OS216_BINARY = 5,   // A 16-bit OS/2-based application
        SCS_PIF_BINARY = 3,     // A PIF file that executes an MS-DOS � based application
        SCS_POSIX_BINARY = 4,   // A POSIX � based application
        SCS_WOW_BINARY = 2      // A 16-bit Windows-based application
    }

    public class WorkerArguments
    {
        public string Executable { get; set; }
        public string CommandLine { get; set; }
        public int Pid { get; set; }
        public string BasePath { get; set; }
        public bool IsStopped { get; set; }
        public Process Dumper { get; set; }
        public ManualResetEvent Done { get; set; }
    }

    public partial class RunExecutable : Window
    {
        [DllImport("kernel32.dll")]
        static extern bool GetBinaryType(string lpApplicationName, out BinaryType lpBinaryType);
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

        void RunExecutable_Closing(object sender, CancelEventArgs e)
        {
            CancelDump();
        }

        public RunExecutable(string basePath)
        {
            BasePath = basePath;
            DataBasePath = null;
            InitializeComponent();
        }

        private void RunExecutableCancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelDump();
            DataBasePath = null;
            this.Close();
        }

        private void RunExecutableStartButton_Click(object sender, RoutedEventArgs e)
        {
            // Get all the settings
            string path = RunExecutablePathTextBox.Text;
            string args = RunExectableArgsTextBox.Text;
            int delay = int.Parse((RunExecutableDelayTextBox.Text == "") ? "" : RunExecutableDelayTextBox.Text);
            int count = int.Parse((RunExecutableSnapshotCountTextBox.Text == "") ? "1" : RunExecutableSnapshotCountTextBox.Text);
            int interval = int.Parse((RunExecutableSnapshotIntervalTextBox.Text == "") ? "0" : RunExecutableSnapshotIntervalTextBox.Text);

            string initialFlags = "";
            if ((bool)RunExecutableInitialBasicCheckBox.IsChecked) initialFlags += "basic refs ";
            if ((bool)RunExecutableInitialHeapCheckBox.IsChecked) initialFlags += "heap ";
            if ((bool)RunExecutableInitialStackCheckBox.IsChecked) initialFlags += "stack threads ";
            string nextFlags = "";
            if ((bool)RunExecutableNextBasicCheckBox.IsChecked) nextFlags += "basic refs ";
            if ((bool)RunExecutableNextHeapCheckBox.IsChecked) nextFlags += "heap ";
            if ((bool)RunExecutableNextStackCheckBox.IsChecked) nextFlags += "stack threads ";

            RealPath = System.IO.Path.GetTempFileName();

            if (path == "")
                System.Windows.MessageBox.Show("Please pick up proper executable", "RunExecutable", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (initialFlags == "" || (count > 1 && nextFlags == ""))
                System.Windows.MessageBox.Show("No features selected!", "RunExecutable", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (count > 1 && interval == 0)
                System.Windows.MessageBox.Show("Please set the interval", "RunExecutable", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (RunExecutableDatabaseNameTextBox.Text == "")
                System.Windows.MessageBox.Show("Please pick proper filename", "RunExecutable", MessageBoxButton.OK, MessageBoxImage.Information);
            else
            {
                string cmdLine = $"--path \"{path}\" --delay {delay} --dbpath {RealPath} --items {initialFlags} ";
                if (args != "")
                    cmdLine += $"--arguments=\"{args}\" ";
                if (count > 1)
                    cmdLine += $"--count {count} --interval {interval} --nextitems {nextFlags}";

                RunExecutableStatusText.Visibility = Visibility.Visible;
                RunExecutableProgressBar.Visibility = Visibility.Visible;

                _worker = new BackgroundWorker();
                _worker.DoWork += backgroundWorker_Dump;

                _worker.WorkerReportsProgress = true;
                _worker.ProgressChanged += backgroundWorker_ProgressChanged;

                _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    (sender, e) =>
                    {
                        RunExecutableStatusText.Visibility = Visibility.Hidden;
                        RunExecutableProgressBar.Visibility = Visibility.Hidden;
                        if ((bool)e.Result)
                        {
                            DataBasePath = RunExecutableDatabaseNameTextBox.Text;
                            this.Close();
                        }
                    });
                _args = new WorkerArguments { Executable = path, CommandLine = cmdLine, BasePath = this.BasePath, Done = _dumpClosing };
                _worker.RunWorkerAsync(argument: _args);
            }
        }

        void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            RunExecutableStatusText.Text = e.UserState.ToString();
        }

        static void backgroundWorker_Dump(object sender, DoWorkEventArgs e)
        {
            bool success = false;
            WorkerArguments args = e.Argument as WorkerArguments;

            BinaryType binaryType;
            if (GetBinaryType(args.Executable, out binaryType))
            {
                if (binaryType == BinaryType.SCS_32BIT_BINARY)
                    success = DumpExecutable(sender, args, true);
                else if (binaryType == BinaryType.SCS_64BIT_BINARY )
                    success = DumpExecutable(sender, args, false);
            }
            e.Result = success;
        }

        static bool DumpExecutable(object sender, WorkerArguments args, bool Is32bit)
        {
            bool success = false;

            string arch = Is32bit ? "x86" : "x64";
            string exePath = args.BasePath +  $"bin\\{arch}\\GM.exe";
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
                            // XXX: child process is just left for hanging because we don't know the pid
                            break;
                        }

                        GMCmdOutput output = JsonSerializer.Deserialize<GMCmdOutput>(temp);
                        if (!output.Msg.Contains("ERROR"))
                            (sender as BackgroundWorker).ReportProgress(i++, output.Msg);
                        else
                        {
                            MessageBox.Show($"{output.Msg}", "RunExecutable", MessageBoxButton.OK, MessageBoxImage.Error);
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
            process.WaitForExit();
            process.Close();
            args.Done?.Set();
            return success;
        }
        private void RunExecutableDatabasePickerButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
            saveFileDialog.FileName = RunExecutableDatabaseNameTextBox.Text;
            if (saveFileDialog.ShowDialog() == true)
                RunExecutableDatabaseNameTextBox.Text = saveFileDialog.FileName;
        }

        private void RunExecutablePathPickerButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.FileName = RunExecutablePathTextBox.Text;
            if (openFileDialog.ShowDialog() == true)
                RunExecutablePathTextBox.Text = openFileDialog.FileName;
        }
    }
}
