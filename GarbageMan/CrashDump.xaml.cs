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
using System.Threading;

namespace GarbageMan
{
    /// <summary>
    /// Interaction logic for CrashDump.xaml
    /// </summary>
    public partial class CrashDump : Window
    {
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

        void CrashDump_Closing(object sender, CancelEventArgs e)
        {
            CancelDump();
        }
        public CrashDump(string basePath)
        {
            BasePath = basePath;
            DataBasePath = null;
            InitializeComponent();
        }
        private void CrashDumpCancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelDump();
            DataBasePath = null;
            this.Close();
        }

        private void CrashDumpStartButton_Click(object sender, RoutedEventArgs e)
        {
            // Get all the settings
            string path = CrashDumpPathTextBox.Text;

            string initialFlags = "";
            if ((bool)CrashDumpInitialBasicCheckBox.IsChecked) initialFlags += "basic refs ";
            if ((bool)CrashDumpInitialHeapCheckBox.IsChecked) initialFlags += "heap ";
            if ((bool)CrashDumpInitialStackCheckBox.IsChecked) initialFlags += "stack threads ";

            RealPath = System.IO.Path.GetTempFileName();

            if (path == "")
                System.Windows.MessageBox.Show("Please pick up proper dump", "CrashDump", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (initialFlags == "")
                System.Windows.MessageBox.Show("No features selected!", "CrashDump", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (CrashDumpDatabaseNameTextBox.Text == "")
                System.Windows.MessageBox.Show("Please pick proper filename", "CrashDump", MessageBoxButton.OK, MessageBoxImage.Information);
            else
            {
                string cmdLine = $"--crashdump {path} --dbpath {RealPath} --items {initialFlags} ";

                CrashDumpStatusText.Visibility = Visibility.Visible;
                CrashDumpProgressBar.Visibility = Visibility.Visible;

                _worker = new BackgroundWorker();
                _worker.DoWork += backgroundWorker_Dump;

                _worker.WorkerReportsProgress = true;
                _worker.ProgressChanged += backgroundWorker_ProgressChanged;

                _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    (sender, e) =>
                    {
                        CrashDumpStatusText.Visibility = Visibility.Hidden;
                        CrashDumpProgressBar.Visibility = Visibility.Hidden;
                        if ((bool)e.Result)
                        {
                            DataBasePath = CrashDumpDatabaseNameTextBox.Text;
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show($"Cannot create dump", "Dump", MessageBoxButton.OK, MessageBoxImage.Error);
                            this.Close();
                        }
                    });
                _args = new WorkerArguments { CommandLine = cmdLine, BasePath = this.BasePath, Done = _dumpClosing };
                _worker.RunWorkerAsync(argument: _args);
            }
        }

        void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CrashDumpStatusText.Text = e.UserState.ToString();
        }

        static void backgroundWorker_Dump(object sender, DoWorkEventArgs e)
        {
            bool success = false;
            WorkerArguments args = e.Argument as WorkerArguments;

            // When in doubt, use brute force (we don't know the architecture here):
            success = DumpCrashDump(sender, args, true);
            if (!success)
                success = DumpCrashDump(sender, args, false);

            e.Result = success;
        }
        static bool DumpCrashDump(object sender, WorkerArguments args, bool Is32bit)
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
                        (sender as BackgroundWorker).ReportProgress(i++, output.Msg);
                        if (output.Msg.Contains("ERROR"))
                        {
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
        private void CrashDumpDatabasePickerButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
            saveFileDialog.FileName = CrashDumpDatabaseNameTextBox.Text;
            if (saveFileDialog.ShowDialog() == true)
                CrashDumpDatabaseNameTextBox.Text = saveFileDialog.FileName;
        }

        private void CrashDumpPathPickerButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.FileName = CrashDumpPathTextBox.Text;
            if (openFileDialog.ShowDialog() == true)
                CrashDumpPathTextBox.Text = openFileDialog.FileName;
        }
    }
}
