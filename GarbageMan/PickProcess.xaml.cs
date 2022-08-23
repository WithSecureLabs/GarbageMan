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
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using GMLib;
using System.Text.Json;
using PInvoke;

namespace GarbageMan
{

    public partial class PickProcess : Window
    {
        string BasePath { get; set; }
        public PickProcess(string basePath)
        {
            BasePath = basePath;
            InitializeComponent();

            ProcessPickStatusText.Visibility = Visibility.Visible;
            ProcessPickProgressBar.Visibility = Visibility.Visible;

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += backgroundWorker_GetProcesses;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                (sender, e) => {
                    ProcessPickStatusText.Visibility = Visibility.Hidden;
                    ProcessPickProgressBar.Visibility = Visibility.Hidden;
                    ProcessPickerDataGrid.DataContext = e.Result;
                });
            worker.RunWorkerAsync(argument: new WorkerArguments { BasePath = this.BasePath });

        }

        static void backgroundWorker_GetProcesses(object sender, DoWorkEventArgs e)
        {
            WorkerArguments args = e.Argument as WorkerArguments;

            List<UIPickedProcess> picked = new();
            Dictionary<int, UIPickedProcess> fromGM = new();

            int me = Process.GetCurrentProcess().Id;
            foreach (var p in GetProcessList(args, true))
                fromGM.Add(p.Pid, p);
            foreach (var p in GetProcessList(args, false))
                fromGM.Add(p.Pid, p);
            foreach (var p in Process.GetProcesses())
            {
                if (p.Id == me)
                    continue;
                if (fromGM.ContainsKey(p.Id))
                    picked.Add(fromGM[p.Id]);
                else
                {
                    using (var handle = Kernel32.OpenProcess(Kernel32.ProcessAccess.PROCESS_QUERY_LIMITED_INFORMATION | Kernel32.ProcessAccess.PROCESS_VM_READ, false, p.Id))
                    {
                        if (!handle.IsInvalid)
                        {
                            picked.Add(new UIPickedProcess { Pid = p.Id, Name = p.ProcessName, Runtime = "Native/CoreCLR", Arch = Kernel32.IsWow64Process(handle) ? "x86" : "x64"});
                        }
                    }
                }
            }
            e.Result = picked;
        }

        static List<UIPickedProcess> GetProcessList(WorkerArguments args, bool Is32bit)
        {
            List<UIPickedProcess> picked = new();

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

            List<GMCmdOutputPList> cmdLines = new();

            using (var pipeRead = new AnonymousPipeServerStream(PipeDirection.In,
                HandleInheritability.Inheritable))
            {
                process.StartInfo.Arguments = $"--pipe {pipeRead.GetClientHandleAsString()}";
                process.StartInfo.Arguments += $" --ps";
                process.Start();

                pipeRead.DisposeLocalCopyOfClientHandle();
                using (var sr = new StreamReader(pipeRead))
                {
                    string temp;
                    while ((temp = sr.ReadLine()) != null)
                    {
                        GMCmdOutputPList output = JsonSerializer.Deserialize<GMCmdOutputPList>(temp);
                        cmdLines.Add(output);
                    }
                }
            }
            process.WaitForExit();
            process.Close();

            foreach (GMCmdOutputPList line in cmdLines)
            {
                if (line.Type == "pslist" && line.PList != null)
                {
                    foreach (var p in line.PList)
                        picked.Add(new UIPickedProcess { Pid = p.Pid, Name=p.Name, Runtime = p.Runtimes[0], Arch = Is32bit ? "x86" : "x64" });
                }
            }
            return picked;
        }

        private void ProcessPickButton_Click(object sender, RoutedEventArgs e)
        {
            UIPickedProcess p = ProcessPickerDataGrid.SelectedItem as UIPickedProcess;
            if (p != null)
            {
                ((Attach)this.Owner).AttachPidTextBox.Text = $"{p.Pid}";
                this.Close();
            }
        }
        private void ProcessPickCancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ProcessPickerDataGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dg = sender as DataGrid;
            UIPickedProcess obj = dg.SelectedItem as UIPickedProcess;
            if (obj != null)
            {
                ((Attach)this.Owner).AttachPidTextBox.Text = $"{obj.Pid}";
                this.Close();
            }
        }
    }
}
