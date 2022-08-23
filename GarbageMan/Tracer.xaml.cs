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
using System.IO;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO.Pipes;
using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using System.Timers;

namespace GarbageMan
{
    public class TracerArguments
    {
        public UIObjectData Object { get; set; }
        public string Database { get; set; }
        public int Snapshot { get; set; }
        public bool IsCanceled { get; set; }
        public bool IsStopped { get; set; }
        public int TraceDepth { get; set; }
        public ManualResetEvent Done { get; set; }
    }
    public partial class Tracer : Window
    {
        public List<UITraceObject> Trace { get; set; }

        private BackgroundWorker _worker = null;
        private static ReferenceTracer _tracer;
        private TracerArguments _args;

        private UIObjectData _object;
        private string _dbPath;
        private int _snapshot;
        private bool _ready = false;

        private ManualResetEvent _tracerClosing = new(false);

        private void StopTracer()
        {
            _args.IsStopped = true;
            _tracerClosing.WaitOne(TimeSpan.FromSeconds(5));
            _tracer.Close();
        }

        void Tracer_Closing(object sender, CancelEventArgs e)
        {
            if (_worker != null && _worker.IsBusy)
            {
                StopTracer();
            }
        }
        public Tracer(UIObjectData obj, string dbPath, int snapshot = 0)
        {
            InitializeComponent();

            _object = obj;
            _dbPath = dbPath;
            _snapshot = snapshot;

            if (obj.Trace != null && obj.Trace.Count != 0)
            {
                // Trace data is already available
                TracerProgressBar.Visibility = Visibility.Hidden;
                Trace = obj.Trace;
                TracerDataGrid.DataContext = Trace;
                _ready = true;
            }
        }

        private void backgroundWorker_Timer(object source, ElapsedEventArgs e)
        {
            _args.IsStopped = true;
            _ready = true;
        }

        static void backgroundWorker_Trace(object sender, DoWorkEventArgs e)
        {
            TracerArguments args = e.Argument as TracerArguments;
            _tracer = new();
            try
            {
                _tracer.Trace(args);
            }
            catch
            {
                _tracer.Close();
                throw;
            }
        }

        private void TracerDataGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dg = sender as DataGrid;
            UITraceObject obj = dg.SelectedItem as UITraceObject;
            if (obj != null)
            {
                ((MainWindow)this.Owner).NavigateToIndex((int)obj.Object.Object.ObjectId);
            }
        }

        private void TracerContextMenuItem_View_Click(object sender, RoutedEventArgs e)
        {
            UITraceObject item = TracerDataGrid.SelectedItem as UITraceObject;
            if (item != null)
            {
                PathViewer viewer = new(item.Path);
                viewer.Show();
            }
        }

        private void CommandBindingStart_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Start();
        }

        private void Start()
        {
            if (_worker != null && _worker.IsBusy)
                return;
            if (_ready)
                TracerDataGrid.DataContext = null;

            _ready = false;

            _worker = new BackgroundWorker();
            _worker.DoWork += backgroundWorker_Trace;
            _worker.WorkerReportsProgress = true;
            _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                (sender, e) =>
                {
                    TracerProgressBar.Visibility = Visibility.Hidden;
                    Trace = _object.Trace;
                    if (Trace == null || Trace.Count == 0)
                    {
                        TracerDataGrid.DataContext = null;
                        _ready = true;
                        _worker = null;
                        _tracer.Close();
                        //this.Close();
                    }
                    else
                    {
                        TracerDataGrid.DataContext = _object.Trace;
                        _ready = true;
                        _worker = null;
                        _tracer.Close();
                    }
                });

            _args = new TracerArguments
            {
                Object = _object,
                Database = _dbPath,
                Snapshot = _snapshot,
                IsCanceled = false,
                IsStopped = false,
                TraceDepth = Int32.Parse(DeptTeaxtBox.Text == "" ? "7" : DeptTeaxtBox.Text),
                Done = _tracerClosing
            };
            _worker.RunWorkerAsync(argument: _args);

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += new ElapsedEventHandler(backgroundWorker_Timer);
            timer.Interval = Int32.Parse(TimeTextBox.Text == "" ? "10" : TimeTextBox.Text);
            timer.Interval *= 1000;
            timer.AutoReset = false;
            timer.Enabled = true;

            TracerProgressBar.Visibility = Visibility.Visible;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ready == true)
                return;
            if (_worker != null && _worker.IsBusy)
                _args.IsStopped = true;
        }

        private void CommandBindingClose_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (_worker != null && _worker.IsBusy)
            {
                StopTracer();
            }
            this.Close();
        }
    }
}
