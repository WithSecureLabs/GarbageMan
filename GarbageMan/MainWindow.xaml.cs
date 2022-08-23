using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GMLib;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Text.Json;
using System.Threading;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;

namespace GarbageMan
{

    public class ScriptGlobals
    {
        public DatabaseContext ctx;
    }

    public class FrameTextBlock : TextBlock
    {
        public static DependencyProperty InlineProperty;
        static FrameTextBlock()
        {
            //OverrideMetadata call tells the system that this element wants to provide a style that is different than in base class
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FrameTextBlock), new FrameworkPropertyMetadata(
                                typeof(FrameTextBlock)));
            InlineProperty = DependencyProperty.Register("RichText", typeof(List<Inline>), typeof(FrameTextBlock),
                            new PropertyMetadata(null, new PropertyChangedCallback(OnInlineChanged)));
        }
        public List<Inline> RichText
        {
            get { return (List<Inline>)GetValue(InlineProperty); }
            set { SetValue(InlineProperty, value); }
        }

        public static void OnInlineChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue)
                return;
            FrameTextBlock r = sender as FrameTextBlock;
            List<Inline> i = e.NewValue as List<Inline>;
            if (r == null || i == null)
                return;
            r.Inlines.Clear();
            foreach (Inline inline in i)
            {
                r.Inlines.Add(inline);
            }
        }
    }

    public class SyntaxHighlight : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Color boringGray = Color.FromRgb(0x66, 0x66, 0x66);

            List<Inline> inlines = new();
            var input = value as string;
            input = input.Replace("\n", "").Replace("\r", "");
            var pos1 = input.IndexOf("(");
            var pos2 = input.IndexOf(")");

            if (pos1 != -1 && pos2 != -1)
            {
                string[] args = input.Substring(pos1 + 1, (pos2 - pos1 - 1)).Split(',');

                // Type/method
                inlines.Add(new Run(input.Substring(0, pos1)) { Foreground = Brushes.SaddleBrown, FontWeight = FontWeights.DemiBold });

                // Opening bracket
                inlines.Add(new Run("(") { Foreground = Brushes.Black, FontWeight = FontWeights.DemiBold });

                // Arguments
                for (int i = 0; i < args.Length; i++)
                {
                    // Argument
                    if (args[i] == "")
                        continue;
                    if (args[i][0] == ' ') args[i] = args[i].Substring(1);
                    inlines.Add(new Run(" " + args[i] + " ") { Foreground = new SolidColorBrush(boringGray) });
                    // Comma
                    if (i != args.Length-1)
                        inlines.Add(new Run(",") { Foreground = Brushes.Black, FontWeight = FontWeights.DemiBold });
                }
                // Closing bracket
                inlines.Add(new Run(")") { Foreground = Brushes.Black, FontWeight = FontWeights.DemiBold });
            }
            else
            {
                inlines.Add(new Run(input) { Foreground = new SolidColorBrush(boringGray), FontWeight = FontWeights.DemiBold });
            }
            return inlines;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }

    public class StripNewlines : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var input = value as string;
            if (input != null)
            {
                string pattern = "[^ -~]+";
                Regex reg_exp = new Regex(pattern);
                return reg_exp.Replace(input, "");
                //return input.Replace("\n", "").Replace("\r", "");
            }
            else
                return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AddExtraSpace : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var input = value as string;
            return input + " ";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : Window
    {
        public string DbContextPath { get; set; }
        //public string BasePath { get; set; } = @"..\..\..\..\..\";
        public string BasePath { get; set; } = "";

        private DatabaseContext dbCtx;
        private List<UISnapshot> _snapshots;
        private List<string> _snapshotCb;
        private int _snapshot;
        private List<UIBookmark> _bookmarks;
        private Bookmarks _bookmark;
        private Snapshots _snapshotsWindow;
        private bool _dbOpen = false;

        private void VerifySearchItems(UISearchItems items)
        {
            for (int i = 0; i < items.Items.Count; i++)
            {
                UISearchItem item = items.Items[i];

                if (item.Header == null)
                    throw new Exception($"Item {i + 1}: No header defined");

                if (item.Category == null) item.Category = "Default";

                if (item.SearchAll == null)
                    item.SearchAll = "Yes";
                else if (item.SearchAll != "Yes" && item.SearchAll != "No")
                    throw new Exception($"Item {i + 1}:{item.Header}: Illegal SearchAll, use Yes or No");

                if (item.SearchType == "Basic")
                {
                    if (item.Snapshot != "Any" && item.Snapshot != "Current")
                        throw new Exception($"Item {i + 1}:{item.Header}: Illegal Snapshot value, use Any or Current");
                    if (item.Order != "Size" && item.Order != "Address")
                        throw new Exception($"Item {i + 1}:{item.Header}: Illegal Order, use Size or Address");
                    if (item.Sort != "Asc" && item.Sort != "Desc")
                        throw new Exception($"Item {i + 1}:{item.Header}: Illegal Sort, use Asc or Desc");
                    if (item.Limit != "5" && item.Limit != "10" && item.Limit != "100" && item.Limit != "1000" && item.Limit != "Unlimited")
                        throw new Exception($"Item {i + 1}:{item.Header}: Illegal Limit, use 5,10,100,100 or Unlimited");
                    if (item.Type == "" && item.Value == "")
                    {
                        throw new Exception($"Item {i + 1}:{item.Header}: Please define either Type or Value");
                    }
                }
                else if (item.SearchType == "Custom")
                {
                    if (item.SQL == null) throw new Exception($"Line {i+1}:{item.Header}: Missing SQL statement");
                }
                else throw new Exception($"Item {i+1}:{item.Header}: Unknown search type");
            }
        }

        private void LoadSearchJson()
        {
            try
            {
                var bytes = File.ReadAllBytes(BasePath + "Search.json");
                UISearchItems items = (UISearchItems)JsonSerializer.Deserialize(bytes, typeof(UISearchItems));

                try
                {
                    VerifySearchItems(items);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error parsing json: {ex.Message}", "GarbageMan", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SearchMenu.Items.Clear();
                Dictionary<string, MenuItem> menuItems = new();
                foreach (UISearchItem s in items.Items)
                {
                    MenuItem item = new();
                    item.Header = (s.SearchAll == "Yes") ? s.Header : s.Header + " (Search All: no)";
                    item.Tag = s;
                    item.Click += SearchMenuItem_Click;
                    if (s.SearchType == "Custom")
                        item.Icon = new Image
                        {
                            Source = new BitmapImage(new Uri("pack://application:,,,/assets/SQL.png"))
                        };
                    if (s.Category == "Default")
                        SearchMenu.Items.Add(item);
                    else
                    {
                        if (!menuItems.ContainsKey(s.Category))
                        {
                            menuItems.Add(s.Category, new MenuItem { Header = s.Category });
                            SearchMenu.Items.Add(menuItems[s.Category]);
                        }
                        menuItems[s.Category].Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing json: {ex.Message}", "GarbageMan", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TraceObject(UIObjectData obj)
        {
            if (obj != null)
            {
                if (obj.PendingTrace)
                {
                    MessageBox.Show($"Trace already open", "GarbageMan", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Tracer trace = new(obj, DbContextPath, _snapshot);
                    trace.Owner = this;
                    trace.Closed += (a, b) =>
                    {
                        //obj.Trace = trace.Trace;
                        obj.PendingTrace = false;
                        this.Activate();
                        if (obj.Trace != null)
                        {
                            obj.IsTraced = true;
                            _snapshots[_snapshot].ObjectReader.Notify(obj);
                            if (ObjectDataGrid.SelectedItem == obj)
                                NavigateToObject(obj);
                            else
                                NavigateToObject((UIObjectData)ObjectDataGrid.SelectedItem);
                        }
                    };
                    trace.ShowInTaskbar = false;
                    obj.PendingTrace = true;
                    trace.Show();
                }
            }
        }

        private List<UIRefData> LoadRefData(UIObjectData obj, bool to, DatabaseContext db)
        {
            if (obj == null)
                return null;
            if (to)
            {
                if (obj.Refsto != null)
                    return obj.Refsto;

                List<UIRefData> refs = new();
                var g = db.Refs.Where(o => o.Id == _snapshot + 1 && o.Address == obj.Object.ObjectId);
                int item = 0;
                foreach (GMRef r in g)
                {
                    refs.Add(new UIRefData
                    {
                        Address = r.Ref,
                        // This might lead to reckless data pull off, but it is faster
                        //Data = ( UIObjectData)_snapshots[_snapshot].ObjectReader[(int)r.Ref]
                        Data = new UIObjectData(db.Objects.Where(o => o.Id == _snapshot + 1 && o.ObjectId == r.Ref).FirstOrDefault(), item++)
                    });
                }
                obj.Refsto = refs;
                return refs;
            }
            else
            {
                if (obj.Refsby != null)
                    return obj.Refsby;

                List<UIRefData> refs = new();
                var g = db.Refs.Where(o => o.Id == _snapshot+1 &&  o.Ref == obj.Object.ObjectId);
                int item = 0;
                foreach (GMRef r in g)
                {
                    refs.Add(new UIRefData
                    {
                        Address = r.Address,
                        //Data = (UIObjectData)_snapshots[_snapshot].ObjectReader[(int)r.Address]
                        Data = new UIObjectData(db.Objects.Where(o => o.Id == _snapshot+1 && o.ObjectId == r.Address).FirstOrDefault(), item++)
                    });
                }
                obj.Refsby = refs;
                return refs;
            }
        }

        public int GetIndexByAddress(ulong addr, int snapshot)
        {
            return (int)dbCtx.Objects.Where(o => o.Id == snapshot && o.Address == addr).FirstOrDefault()?.ObjectId;
        }

        // Navigate to row index and try to get keyboard focus (not very easy in WPF!)
        public void NavigateToIndex(int index)
        {
            if (_snapshots == null)
                return;

            UIObjectData x = _snapshots[_snapshot].ObjectReader[index] as UIObjectData;
            if (x == null)
                return;
            ObjectDataGrid.SelectedItem = x;
            ObjectDataGrid.ScrollIntoView(x);
            Dispatcher.Invoke(new Action(delegate ()
            {
                var selectedRow = (DataGridRow)ObjectDataGrid.ItemContainerGenerator.ContainerFromIndex(ObjectDataGrid.SelectedIndex);
                if (selectedRow != null)
                {
                    Keyboard.Focus(selectedRow);
                    //FocusManager.SetIsFocusScope(selectedRow, true);
                    //FocusManager.SetFocusedElement(selectedRow, selectedRow);
                    selectedRow.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
            ), System.Windows.Threading.DispatcherPriority.Background);
        }

        public void NavigateToObject(UIObjectData obj)
        {
            if (_snapshots == null)
                return;
            _snapshots[_snapshot].Settings.History.Push(_snapshots[_snapshot].Settings.Index);

            // We are jumping to another snapshot:
            if (obj.Snapshot != _snapshot + 1)
            {
                _snapshot = obj.Snapshot - 1;
                _snapshots[_snapshot].Settings.Index = GetIndexByAddress(obj.Address, _snapshot + 1);
                SnapshotComboBox.SelectedIndex = _snapshot;
            }
            else
            {
                NavigateToIndex((int)obj.Object.ObjectId);
            }
        }

        public void NavigateToAddress(ulong? addr)
        {
            if (addr != null)
            {
                // Existence of object in data grid cannot be quaranteed, so we need to get the virtual index
                try
                {
                    int index = GetIndexByAddress((ulong)addr, _snapshot + 1);
                    NavigateToIndex(index);
                }
                catch { }
            }
        }
        public void NavigateBack()
        {
            if (_snapshots[_snapshot].Settings.History.Count() > 0)
            {
                NavigateToIndex(_snapshots[_snapshot].Settings.History.Pop());
            }
        }

        private void AddFrames(UISnapshot snapshot, int index)
        {
            foreach (int tid in snapshot.Tids)
            {
                List<UIFrame> frames = new();
                var f = dbCtx.Frames.Where(f => f.Id == index && (int)f.Tid == tid);
                foreach (var frame in f)
                {
                    frames.Add(new UIFrame { StackPtr = frame.StackPtr, IP = frame.Ip, Frame = frame.Frame });
                }
                snapshot.Frames.Add(frames);
            }
        }
        private List<UIStack> AddStack(UISnapshot snapshot, int index, int tid)
        {
            if (snapshot.Stacks.ContainsKey(tid))
                return snapshot.Stacks[tid];

            List<UIStack> stack = new();
            var s = dbCtx.Stacks.Where(s => s.Id == index+1 && (int)s.Tid == tid);
            foreach (var slot in s)
            {
                try
                {
                    stack.Add(new UIStack { StackPtr = slot.StackPtr, Data = (UIObjectData)snapshot.ObjectReader[(int)slot.Object] });
                }
                catch
                {
                    throw;
                }
            }
            snapshot.Stacks.Add(tid, stack);
            return snapshot.Stacks[tid];
        }

        public void SwitchToSnapshot(int snapshot)
        {
            SnapshotComboBox.SelectedIndex = snapshot;
        }
        private void SwitchSnapshot(int snapshot)
        {
            // Set current snapshot
            _snapshot = snapshot;
            // XXX: ugly
            foreach (var s in _snapshots)
                s.Settings.IsCurrent = false;
            _snapshots[_snapshot].Settings.IsCurrent = true;

            // Switch object data reader
            ObjectDataGrid.DataContext = null;
            ObjectDataGrid.Items.Refresh();
            ObjectDataGrid.DataContext = _snapshots[_snapshot].ObjectReader;

            // Update thread selector
            ThreadComboBox.DataContext = null;
            ThreadComboBox.DataContext = _snapshots[_snapshot].Tids;
            ThreadComboBox.SelectedItem = _snapshots[_snapshot].Settings.Tid;

            // Update General info
            GeneralStatusText.Text = _snapshots[_snapshot].Status;
            RuntimesDataGrid.DataContext = _snapshots[_snapshot].Runtimes;
            AppDomainsDataGrid.DataContext = _snapshots[_snapshot].AppDomains;
            ModulesDataGrid.DataContext = _snapshots[_snapshot].Modules;

        }

        public void InsertOrUpdate<T>(T entity, DbContext db) where T : class
        {
            if (db.Entry(entity).State == EntityState.Detached)
                db.Set<T>().Add(entity);
            db.SaveChanges();
        }

        public void CloseDataBase()
        {
            if (dbCtx == null)
                return;

            // Close all views
            FrameDataGrid.DataContext = null;
            StackDataGrid.DataContext = null;
            RefsToGrid.DataContext = null;
            RefsByGrid.DataContext = null;
            ObjectViewDataGrid.DataContext = null;
            ObjectViewDataGrid.Visibility = Visibility.Visible;
            DataViewPrintable.Text = "";
            ObjectSearchDataGrid.DataContext = null;
            ObjectDataGrid.DataContext = null;
            ObjectViewImageData.Source = null;
            ObjectViewImage.Visibility = Visibility.Hidden;
            ModulesDataGrid.DataContext = null;
            AppDomainsDataGrid.DataContext = null;
            RuntimesDataGrid.DataContext = null;
            ThreadContextDataGrid.DataContext = null;

            // Reset Other controls
            SnapshotComboBox.SelectedItem = -1;
            SnapshotComboBox.ItemsSource = null;
            SnapshotComboBox.Text = "";
            ThreadComboBox.SelectedItem = -1;
            ThreadComboBox.DataContext = null;
            ThreadComboBox.Text = "";
            GeneralStatusText.Text = "";
            StatusTextBlock.Text = "Cache usage";

            // Save settings
            foreach (var s in dbCtx.Settings)
            {
                dbCtx.Settings.Remove(s);
            }
            foreach (UISnapshot snap in _snapshots)
            {
                string jsonString = JsonSerializer.Serialize(snap.Settings);
                dbCtx.Settings.Add(new GMSetting { Id = snap.Id, Setting = jsonString });
            }
            dbCtx.SaveChanges();

            // Save bookmarks
            if (_bookmark != null)
                _bookmark.Close();

            if (_bookmarks.Count != 0)
            {
                foreach (var b in dbCtx.Bookmarks)
                    dbCtx.Bookmarks.Remove(b);
                foreach (var b in _bookmarks)
                    dbCtx.Bookmarks.Add(new GMBookmark { Id = b.Data.Object.Id, ObjectId = b.Data.Object.ObjectId, Notes = b.Notes });
                dbCtx.SaveChanges();
            }

            // Other resources
            AutoAnalyzeCheckBox.IsChecked = true;
            AutoViewCheckBox.IsChecked = true;
            dbCtx.Dispose();

            _snapshots = null;
            _snapshotCb = null;
            _bookmarks = null;
            dbCtx = null;

            // We're done closing!
            this.Title = $"GarbageMan";
            _dbOpen = false;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        public void OpenDataBase(string path, string realPath = null)
        {
            if (path == null)
                return;
            if (_dbOpen)
                CloseDataBase();

            try
            {
                if (realPath != null)
                {
                    File.Copy(realPath, path, true);
                    File.Copy(realPath + "-shm", path + "-shm", true);
                    File.Copy(realPath + "-wal", path + "-wal", true);
                }
                dbCtx = new(path);

                _snapshots = new();
                _snapshotCb = new();
                _bookmarks = new();

                // Parse snapshots
                int i = 1;
                foreach (var s in dbCtx.Snapshots)
                {
                    // Read settings from database
                    UISettings settings;
                    var _settings = dbCtx.Settings.Where(t => t.Id == s.Id).FirstOrDefault();
                    if (_settings != null)
                    {
                        settings = (UISettings)JsonSerializer.Deserialize(_settings.Setting, typeof(UISettings));
                        // XXX: stack needs to be reversed for some reason
                        settings.History.Reverse();
                        Stack<int> rs = new Stack<int>(settings.History);
                        settings.History = rs;
                    }
                    else
                    {
                        settings = new UISettings { Pid = (int)s.Pid, Tid = 0, Index = 0, History = new(), IsCurrent = (i == 1) ? true : false };
                    }

                    var snapshot = new UISnapshot
                    {
                        Id = (int)s.Id,
                        PID = (int)s.Pid,
                        Time = (int)s.Time,
                        ObjectReader = new(dbCtx, (int)s.Id),
                        Settings = settings,
                        Threads = new(),
                        Tids = new(),
                        Runtimes = new(),
                        AppDomains = new(),
                        Modules = new(),
                        Stacks = new(),
                        Frames = new(),
                        Handles = new(),
                        PointerSize = s.PointerSize
                    };
                    // Disable automatic refs with big database
                    if (snapshot.ObjectReader.Size > 100000)
                        AutoAnalyzeCheckBox.IsChecked = false;

                    // Runtimes
                    foreach (var r in dbCtx.Runtimes.Where(r => r.Id == i))
                    {
                        snapshot.Runtimes.Add(r);
                    }
                    // AppDomains
                    foreach (var a in dbCtx.AppDomains.Where(a => a.Id == i))
                    {
                        snapshot.AppDomains.Add(a);
                    }
                    // Modules
                    foreach (var m in dbCtx.Modules.Where(m => m.Id == i))
                    {
                        snapshot.Modules.Add(m);
                    }
                    // Threads
                    foreach (var t in dbCtx.Threads.Where(t => t.Id == i))
                    {
                        snapshot.Threads.Add(new UIThread { Tid = (int)t.Id, Data = t });
                        snapshot.Tids.Add((int)t.Tid);
                    }
                    snapshot.ThreadCount = snapshot.Threads.Count;

                    // Frames
                    AddFrames(snapshot, i);

                    if (snapshot.Threads.Count != 0)
                    {
                        if (snapshot.Settings.Tid == 0)
                            snapshot.Settings.Tid = snapshot.Tids[0];
                    }
                    _snapshotCb.Add($"#{s.Id} - {s.Time} ms (PID: {s.Pid})");

                    GMProcess p = dbCtx.Processes.Where(p => p.Pid == snapshot.Settings.Pid).FirstOrDefault();
                    if (p != null)
                    {
                        snapshot.Status = $"Process Id: {snapshot.Settings.Pid} ({p.Arch})";
                        if (p.Path != "")
                            snapshot.Status += $", Path: \"{p.Path}\"";
                        if (p.Args != "")
                            snapshot.Status += $", Args: \"{p.Path}\"";
                        snapshot.Status += $"\nStart date: {p.Date}";
                    }

                    CurrentSnapshotCheckbox.IsChecked = false;

                    // Search sockets
                    var sockets = SearchRawSql($"SELECT * FROM Objects WHERE Id = {snapshot.Id} AND Type = \"System.Net.Sockets.Socket\"", false);
                    snapshot.SocketCount = sockets.Count;

                    // Search IO
                    var io = SearchRawSql($"SELECT * FROM Objects WHERE Id = {snapshot.Id} AND Type LIKE \"System.IO%\"", false);
                    snapshot.IOCount = io.Count;

                    CurrentSnapshotCheckbox.IsChecked = true;

                    _snapshots.Add(snapshot);
                    i++;
                }

                // Bookmarks
                foreach (var bookmark in dbCtx.Bookmarks)
                {
                    UIObjectData data = (UIObjectData)_snapshots[(int)bookmark.Id - 1].ObjectReader[(int)bookmark.ObjectId];
                    _bookmarks.Add(new UIBookmark { Notes = bookmark.Notes, Data = data });
                    data.IsBookmarked = true;
                }

                // Select current snapshot
                SnapshotComboBox.ItemsSource = _snapshotCb;
                foreach (var s in _snapshots)
                {
                    if (s.Settings.IsCurrent)
                    {
                        SnapshotComboBox.SelectedIndex = s.Id - 1;
                        break;
                    }
                }

                DbContextPath = path;
                this.Title = $"GarbageMan - {DbContextPath}";

                _dbOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in opening database: {ex.Message}", "GarbageMan", MessageBoxButton.OK, MessageBoxImage.Error);
                if (dbCtx != null)
                    dbCtx.Dispose();
                _snapshots = null;
                _snapshotCb = null;
                _bookmarks = null;
                dbCtx = null;
                this.Title = $"GarbageMan";
                _dbOpen = false;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Using the name of type as key
            ((App)Application.Current).WindowPlace.Register(this);

            LoadSearchJson();
        }



        private void SearchAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (dbCtx == null)
                return;

            // XXX: implement proper search settings backup/restore
            string backupType = SearchInputType.Text;
            string backupValue = SearchInputValue.Text;
            int backupCount = SearchInputLimit.SelectedIndex;

            List<UIObjectData> data = new();
            using (new WaitCursor())
            {
                foreach (MenuItem item in SearchMenu.Items)
                {
                    if (item.Items.Count > 0)
                    {
                        // Submenu
                        foreach (MenuItem sub in item.Items)
                        {
                            if (((UISearchItem)sub.Tag).SearchAll == "No") continue;
                            var objects = SearchObjects(sub.Tag as UISearchItem, displayItems: false);
                            if (objects != null) data.AddRange(objects);
                        }
                    }
                    else
                    {
                        if (((UISearchItem)item.Tag).SearchAll == "No") continue;
                        var objects = SearchObjects(item.Tag as UISearchItem, displayItems: false);
                        if (objects != null) data.AddRange(objects);
                    }
                }
            }
            SearchInputType.Text = backupType;
            SearchInputValue.Text = backupValue;
            SearchInputLimit.SelectedIndex = backupCount;

            data = data.OrderBy(d => d.Address).ToList();
            ObjectSearchDataGrid.DataContext = data;
            if (data != null && data.Count > 0) ObjectSearchDataGrid.ScrollIntoView(data[0]);
        }

        private void ReloadSearchMenuItem_Click(object sender, RoutedEventArgs e)
        {
            LoadSearchJson();
        }

        private void SearchMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (dbCtx == null)
                return;

            string backupType = SearchInputType.Text;
            string backupValue = SearchInputValue.Text;
            int backupCount = SearchInputLimit.SelectedIndex;

            MenuItem menuItem = sender as MenuItem;
            UISearchItem s = menuItem.Tag as UISearchItem;
            SearchObjects(s);

            SearchInputType.Text = backupType;
            SearchInputValue.Text = backupValue;
            SearchInputLimit.SelectedIndex = backupCount;
        }

        private List<UIObjectData> SearchObjects(UISearchItem searchItem, bool displayItems = true)
        {
            List<UIObjectData> data = new();

            if (searchItem.SearchType == "Basic")
            {
                if (searchItem.Snapshot != null) SearchInputSnapshot.SelectedIndex = (searchItem.Snapshot == "Current" || CurrentSnapshotCheckbox.IsChecked) ? 0 : 1;
                if (searchItem.Type != null) SearchInputType.Text = searchItem.Type;
                if (searchItem.Value != null) SearchInputValue.Text = searchItem.Value;
                if (searchItem.Order != null) SearchInputOrderby.Text = searchItem.Order;
                if (searchItem.Sort != null) SearchInputOrderAsc.Text = searchItem.Sort;
                if (searchItem.Limit != null)
                {
                    switch (searchItem.Limit)
                    {
                        case "5":
                        case "10":
                        case "100":
                        case "1000":
                        case "Unlimited":
                            SearchInputLimit.Text = searchItem.Limit;
                            break;
                        default:
                            SearchInputLimit.Text = "100";
                            break;
                    }
                }
                return GetSearchResults(displayItems);

            }
            else if (searchItem.SearchType == "Custom")
            {
                string sqlQuery = searchItem.SQL;
                if (sqlQuery != null && dbCtx != null)
                {
                    return SearchRawSql(sqlQuery, displayItems);
                }
            }
            return null;
        }

        public List<UIObjectData> SearchRawSql(string sqlQuery, bool displayItems = true)
        {
            List<UIObjectData> data = new();
            int item = 0;
            try
            {
                var g = dbCtx.Objects.FromSqlRaw(sqlQuery);
                if (CurrentSnapshotCheckbox.IsChecked)
                    g = g.Where(s => s.Id == _snapshot + 1);
                foreach (var o in g)
                {
                    data.Add(new UIObjectData(o, item++));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in SQL: {ex.Message}", "GarbageMan", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            if (displayItems)
            {
                ObjectSearchDataGrid.DataContext = data;
                if (data != null && data.Count > 0) ObjectSearchDataGrid.ScrollIntoView(data[0]);
            }
            return data;
        }

        private List<UIObjectData> GetSearchResults(bool displayItems = true)
        {
            if (dbCtx == null)
                return null;

            int snapshot = SearchInputSnapshot.SelectedIndex;
            string type = SearchInputType.Text;
            string value = SearchInputValue.Text;
            string orderby = SearchInputOrderby.Text;
            string limit = SearchInputLimit.Text;
            string asc = SearchInputOrderAsc.Text.ToUpper();

            string sqlOrder = $"ORDER BY {orderby} {asc}";
            string sqlLimit = limit != "Unlimited" ? $"LIMIT {limit}" : "";
            string sqlSearch = "";

            if (snapshot == 0)
                sqlSearch += $"Id = {_snapshot+1} AND ";
            if (type != "")
            {
                sqlSearch += (type[0] == '=') ? $"Type = \"{type.Substring(1)}\" " :  $"Type LIKE \"%{type}%\" ";
            }
            if (value != "")
            {
                if (type != "") sqlSearch += " AND ";
                sqlSearch += (value[0] == '=') ? $"Value = \"{value.Substring(1)}\" " : $"Value LIKE \"%{value}%\"";
            }

            string sqlQuery = $"SELECT * FROM Objects WHERE {sqlSearch} {sqlOrder} {sqlLimit};";

            List<UIObjectData> data = new();
            int item = 0;
            var g = dbCtx.Objects.FromSqlRaw(sqlQuery);
            foreach (var o in g)
            {
                data.Add(new UIObjectData(o, item++));
            }
            if (displayItems)
            {
                ObjectSearchDataGrid.DataContext = data;
                if (data != null && data.Count > 0) ObjectSearchDataGrid.ScrollIntoView(data[0]);
            }
            return data;

        }

        private void AddBookmark(UIObjectData obj)
        {
            if (!obj.IsBookmarked)
            {
                obj.IsBookmarked = true;
                _snapshots[_snapshot].ObjectReader.Notify(obj);
                NavigateToObject(obj);
                _bookmarks.Add(new UIBookmark { Data=obj });
                if (_bookmark != null)
                    _bookmark.BookmarksDataGrid.Items.Refresh();
            }
        }

        public void RemoveBookmark(UIObjectData obj)
        {
            if (obj.IsBookmarked)
            {
                obj.IsBookmarked = false;
                _snapshots[_snapshot].ObjectReader.Notify(obj);
                NavigateToObject(obj);
                foreach (var b in _bookmarks)
                {
                    if (b.Data == obj)
                    {
                        _bookmarks.Remove(b);
                        if (_bookmark != null)
                            _bookmark.BookmarksDataGrid.Items.Refresh();
                        break;
                    }
                }
            }
        }
    }
}
