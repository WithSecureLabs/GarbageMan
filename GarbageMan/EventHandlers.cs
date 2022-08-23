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
using System.Threading.Tasks;
using System.Reflection;

namespace GarbageMan
{

    public class WaitCursor : IDisposable
    {
        private Cursor _previousCursor;

        public WaitCursor()
        {
            _previousCursor = Mouse.OverrideCursor;

            Mouse.OverrideCursor = Cursors.Wait;
        }

        #region IDisposable Members

        public void Dispose()
        {
            Mouse.OverrideCursor = _previousCursor;
        }

        #endregion
    }

    public partial class MainWindow : Window
    {

        // Navigation
        private void ObjectDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var dg = sender as DataGrid;
            UIObjectData data = dg.SelectedItem as UIObjectData;
            if (data == null)
                return;
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.PageUp || e.Key == Key.PageDown)
            {
                _snapshots[_snapshot].Settings.History.Push(data.Item);
            }
        }
        private void ObjectDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dg = sender as DataGrid;
            UIObjectData data = dg.SelectedItem as UIObjectData;
            if (data == null)
                return;
            _snapshots[_snapshot].Settings.History.Push(data.Item);
        }

        private void ViewObject(UIObjectData obj)
        {
            if (obj.IsString || obj.IsBlob)
            {
                if (obj.HexReader == null)
                    obj.HexReader = new UIHexReader(obj);
                if (obj.HexReader != null && obj.HexReader.Printable != null)
                    DataViewPrintable.Text = obj.HexReader.Printable.Substring(0, Math.Min(1000, obj.HexReader.Printable.Length));
                else
                    DataViewPrintable.Text = "";

                if (obj.HexReader != null && obj.HexReader.IsImage)
                {
                    ObjectViewImage.Visibility = Visibility.Visible;
                    ObjectViewDataGrid.Visibility = Visibility.Hidden;
                    ObjectViewImageData.Source = obj.HexReader.Img;
                }
                else
                {
                    ObjectViewImage.Visibility = Visibility.Hidden;
                    ObjectViewDataGrid.Visibility = Visibility.Visible;
                    ObjectViewDataGrid.DataContext = obj.HexReader;
                    ObjectViewDataGrid.ToolTip = obj.HexReader.ToolTip;
                    if (obj.HexReader.Size != 0)
                        ObjectViewDataGrid.ScrollIntoView(obj.HexReader[0]);
                }
            }
            else
            {
                ObjectViewImage.Visibility = Visibility.Hidden;
                ObjectViewDataGrid.Visibility = Visibility.Visible;
                ObjectViewDataGrid.DataContext = null;
                DataViewPrintable.Text = "";
            }
        }

        private void ObjectDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dg = sender as DataGrid;
            UIObjectData obj = dg.SelectedItem as UIObjectData;

            int size = _snapshots[_snapshot].ObjectReader.Size;
            int count = _snapshots[_snapshot].ObjectReader.Count-1;
            StatusTextBlock.Text = $"Cache usage: {count}/{(size - (size % 1000)) / 1000}";

            if (obj != null)
            {
                ulong addr = obj.Address;
                _snapshots[_snapshot].Settings.Index = obj.Item;

                using (new WaitCursor())
                {
                    // Load data to hex
                    if ((bool)AutoViewCheckBox.IsChecked)
                        ViewObject(obj);

                    // Load refs
                    if (obj.Refsto != null)
                        RefsToGrid.DataContext = obj.Refsto;
                    else if ((bool)AutoAnalyzeCheckBox.IsChecked)
                        RefsToGrid.DataContext = LoadRefData(obj, true, dbCtx);
                    else
                        RefsToGrid.DataContext = null;
                    if (obj.Refsby != null)
                        RefsByGrid.DataContext = obj.Refsby;
                    else if ((bool)AutoAnalyzeCheckBox.IsChecked)
                        RefsByGrid.DataContext = LoadRefData(obj, false, dbCtx);
                    else
                        RefsByGrid.DataContext = null;
                }
            }
        }

        private void RefToDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void RefToDataGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_snapshots == null)
                return;
            var dg = sender as DataGrid;
            UIRefData refs = dg.SelectedItem as UIRefData;
            if (refs != null)
            {
                _snapshots[_snapshot].Settings.History.Push(_snapshots[_snapshot].Settings.Index);
                NavigateToIndex((int)refs.Data.Object.ObjectId);
            }
        }


        private void RefByDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void RefByDataGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_snapshots == null)
                return;
            var dg = sender as DataGrid;
            UIRefData refs = dg.SelectedItem as UIRefData;
            if (refs != null)
            {
                _snapshots[_snapshot].Settings.History.Push(_snapshots[_snapshot].Settings.Index);
                NavigateToIndex((int)refs.Data.Object.ObjectId);
            }
        }

        private void First_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            _snapshots[_snapshot].Settings.History.Push(_snapshots[_snapshot].Settings.Index);
            NavigateToIndex(0);
        }

        private void Last_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            _snapshots[_snapshot].Settings.History.Push(_snapshots[_snapshot].Settings.Index);
            NavigateToIndex(_snapshots[_snapshot].ObjectReader.Size - 1);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            NavigateBack();
        }

        public void NavigateBack_CommandBinding(Object sender, ExecutedRoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            NavigateBack();
        }

        private void Bookmark_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            if (_bookmark == null)
            {
                _bookmark = new(_bookmarks);
                _bookmark.Owner = this;
                _bookmark.Closed += (a, b) => _bookmark = null;
                _bookmark.ShowInTaskbar = false;
                _bookmark.Show();
            }
            else
            {
                _bookmark.Show();
            }
        }
        private void SnapshotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_snapshots == null)
                return;
            ComboBox cb = sender as ComboBox;
            if (cb.Items.Count == 0 || cb.SelectedIndex == -1)
                return;
            using (new WaitCursor())
            {
                SwitchSnapshot(cb.SelectedIndex);
                NavigateToIndex(_snapshots[cb.SelectedIndex].Settings.Index);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            if (SearchInputType.Text == "" && SearchInputValue.Text == "")
                MessageBox.Show("Nothing to search!", "GarbageMan", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                GetSearchResults();
        }

        private void ObjectSearchDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("ObjectSearchDataGrid_SelectionChanged");
        }

        private void ObjectSearchDataGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_snapshots == null)
                return;
            var dg = sender as DataGrid;
            UIObjectData obj = dg.SelectedItem as UIObjectData;

            if (obj != null)
                NavigateToObject(obj);
        }

        private void SearchInputValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (SearchInputValue.Text != "")
                    GetSearchResults();
            }
        }

        private void SearchInputType_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (SearchInputType.Text != "")
                    GetSearchResults();
            }
        }

        private void Navigation_KeyDown(object sender, KeyEventArgs e)
        {
            /* Unstable behavior
            if (_snapshots == null)
                return;
            if (e.Key == Key.Return)
            {
                if (NavigationTextBox.Text != "")
                {
                    _snapshots[_snapshot].Settings.History.Push(_snapshots[_snapshot].Settings.Index);
                    Thread.Sleep(100);
                    NavigateToAddress(ulong.Parse(NavigationTextBox.Text));
                }
            }
            */
        }
        private void JumpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            if (NavigationTextBox.Text != "")
            {
                _snapshots[_snapshot].Settings.History.Push(_snapshots[_snapshot].Settings.Index);
                NavigateToAddress(ulong.Parse(NavigationTextBox.Text));
            }
        }

        private void SaveBinaryButton_Click(object sender, RoutedEventArgs e)
        {
            UIHexReader data = ObjectViewDataGrid.ItemsSource as UIHexReader;
            if (data != null)
            {
                Stream fileStream;
                SaveFileDialog saveFileDialog1 = new SaveFileDialog();

                saveFileDialog1.Filter = "Binary files (*.bin, *.dat, *.exe, *.dll)|*.bin;*.dat;*.exe;*.dll|All files (*.*)|*.*";
                saveFileDialog1.FilterIndex = 2;
                saveFileDialog1.RestoreDirectory = true;

                if (saveFileDialog1.ShowDialog() == true)
                {
                    if ((fileStream = saveFileDialog1.OpenFile()) != null)
                    {
                        fileStream.Write(data.Binary, 0, data.Binary.Length);
                        fileStream.Close();
                    }
                }

            }
        }
        private void ThreadComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            if (cb.Items.Count == 0 || cb.SelectedIndex == -1)
                return;

            // New thread selector
            _snapshots[_snapshot].Settings.Tid = _snapshots[_snapshot].Tids[cb.SelectedIndex];

            using (new WaitCursor())
            {
                // Update Thread context
                if (_snapshots[_snapshot].Threads[cb.SelectedIndex].Context == null)
                        _snapshots[_snapshot].Threads[cb.SelectedIndex].Context = new(_snapshots[_snapshot]);
                ThreadContextDataGrid.DataContext = null;
                ThreadContextDataGrid.Items.Refresh();
                ThreadContextDataGrid.DataContext = _snapshots[_snapshot].Threads[cb.SelectedIndex].Context.Registers;
                
                // Update frames
                FrameDataGrid.DataContext = null;
                FrameDataGrid.Items.Refresh();
                FrameDataGrid.DataContext = _snapshots[_snapshot].Frames[cb.SelectedIndex];

                // Update stack
                StackDataGrid.DataContext = null;
                StackDataGrid.Items.Refresh();
                _snapshots[_snapshot].Stacks[_snapshots[_snapshot].Settings.Tid] =
                    AddStack(_snapshots[_snapshot], _snapshot, _snapshots[_snapshot].Settings.Tid);
                StackDataGrid.DataContext = _snapshots[_snapshot].Stacks[_snapshots[_snapshot].Settings.Tid];
            }

            int size = _snapshots[_snapshot].ObjectReader.Size;
            int count = _snapshots[_snapshot].ObjectReader.Count-1;
            StatusTextBlock.Text = $"Cache usage: {count}/{(size - (size % 1000)) / 1000}";
        }

        private void ObjectContextMenuItem_Address_Click(object sender, RoutedEventArgs e)
        {
            UIObjectData item = ObjectDataGrid.SelectedItem as UIObjectData;
            if (item != null)
            {
                Clipboard.SetText($"{item.Address}");
            }
        }

        private void ObjectContextMenuItem_Type_Click(object sender, RoutedEventArgs e)
        {
            UIObjectData item = ObjectDataGrid.SelectedItem as UIObjectData;
            if (item != null)
            {
                Clipboard.SetText(item.Type);
            }
        }

        private void ObjectContextMenuItem_Value_Click(object sender, RoutedEventArgs e)
        {
            UIObjectData item = ObjectDataGrid.SelectedItem as UIObjectData;
            if (item != null)
            {
                Clipboard.SetText(item.Value);
            }
        }

        private void ObjectSearchContextMenuItem_Address_Click(object sender, RoutedEventArgs e)
        {
            UIObjectData item = ObjectSearchDataGrid.SelectedItem as UIObjectData;
            if (item != null)
            {
                Clipboard.SetText($"{item.Address}");
            }
        }

        private void ObjectSearchContextMenuItem_Type_Click(object sender, RoutedEventArgs e)
        {
            UIObjectData item = ObjectSearchDataGrid.SelectedItem as UIObjectData;
            if (item != null)
            {
                Clipboard.SetText(item.Type);
            }
        }

        private void ObjectSearchContextMenuItem_Value_Click(object sender, RoutedEventArgs e)
        {
            UIObjectData item = ObjectSearchDataGrid.SelectedItem as UIObjectData;
            if (item != null)
            {
                Clipboard.SetText(item.Value);
            }
        }

        private void RefToContextMenuItem_Address_Click(object sender, RoutedEventArgs e)
        {
            UIRefData item = RefToDataGrid.SelectedItem as UIRefData;
            if (item != null)
            {
                Clipboard.SetText($"{item.Data.Address}");
            }
        }

        private void RefToContextMenuItem_Type_Click(object sender, RoutedEventArgs e)
        {
            UIRefData item = RefToDataGrid.SelectedItem as UIRefData;
            if (item != null)
            {
                Clipboard.SetText(item.Data.Type);
            }
        }

        private void RefToContextMenuItem_Value_Click(object sender, RoutedEventArgs e)
        {
            UIRefData item = RefToDataGrid.SelectedItem as UIRefData;
            if (item != null)
            {
                Clipboard.SetText(item.Data.Value);
            }
        }

        private void RefByContextMenuItem_Address_Click(object sender, RoutedEventArgs e)
        {
            UIRefData item = RefByDataGrid.SelectedItem as UIRefData;
            if (item != null)
            {
                Clipboard.SetText($"{item.Data.Address}");
            }
        }

        private void RefByContextMenuItem_Type_Click(object sender, RoutedEventArgs e)
        {
            UIRefData item = RefByDataGrid.SelectedItem as UIRefData;
            if (item != null)
            {
                Clipboard.SetText(item.Data.Type);
            }
        }

        private void RefByContextMenuItem_Value_Click(object sender, RoutedEventArgs e)
        {
            UIRefData item = RefByDataGrid.SelectedItem as UIRefData;
            if (item != null)
            {
                Clipboard.SetText(item.Data.Value);
            }
        }

        private void ModuleContextMenuItem_Assembly_Click(object sender, RoutedEventArgs e)
        {
            GMModule item = ModulesDataGrid.SelectedItem as GMModule;
            if (item != null)
            {
                Clipboard.SetText($"{item.AsmAddress}");
            }
        }

        private void ModuleContextMenuItem_Image_Click(object sender, RoutedEventArgs e)
        {
            GMModule item = ModulesDataGrid.SelectedItem as GMModule;
            if (item != null)
            {
                Clipboard.SetText($"{item.ImgAddress}");
            }
        }
        private void ModuleContextMenuItem_Module_Click(object sender, RoutedEventArgs e)
        {
            GMModule item = ModulesDataGrid.SelectedItem as GMModule;
            if (item != null)
            {
                Clipboard.SetText($"{item.Name}");
            }
        }

        private void ObjectAddBookmarkCommandBinding_Executed(object sender, RoutedEventArgs e)
        {
            UIObjectData item = ObjectDataGrid.SelectedItem as UIObjectData;
            if (item != null)
            {
                AddBookmark(item);
            }
        }

        private void ObjectRemoveBookmarkCommandBinding_Executed(object sender, RoutedEventArgs e)
        {
            UIObjectData item = ObjectDataGrid.SelectedItem as UIObjectData;
            if (item != null)
            {
                RemoveBookmark(item);
            }
        }
        private void ObjectViewCommandBinding_Executed(object sender, RoutedEventArgs e)
        {
            UIObjectData obj = ObjectDataGrid.SelectedItem as UIObjectData;
            ViewObject(obj);
        }
        private void ObjectAnalyzeCommandBinding_Executed(object sender, RoutedEventArgs e)
        {
            UIObjectData obj = ObjectDataGrid.SelectedItem as UIObjectData;
            if (obj != null)
            {
                using (new WaitCursor())
                {
                    RefsToGrid.DataContext = LoadRefData(obj, true, dbCtx);
                    RefsByGrid.DataContext = LoadRefData(obj, false, dbCtx);
                }
            }
        }
        private void ObjectTraceCommandBinding_Executed(object sender, RoutedEventArgs e)
        {
            UIObjectData obj = ObjectDataGrid.SelectedItem as UIObjectData;
            TraceObject(obj);
        }

        private void TraceSmartRefsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            UIObjectData obj = ObjectDataGrid.SelectedItem as UIObjectData;
            TraceObject(obj);
        }

        private void RunRawSqlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            var sql = new RawSql();
            sql.Owner = this;
            sql.Closed += (a, b) => { this.Activate(); };
            sql.ShowInTaskbar = false;
            sql.Show();
        }

        private void SnapshotsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshots == null)
                return;
            if (_snapshotsWindow == null)
            {
                _snapshotsWindow = new Snapshots();
                _snapshotsWindow.Owner = this;
                _snapshotsWindow.Closed += (a, b) => _snapshotsWindow = null;
                _snapshotsWindow.ShowInTaskbar = false;
                _snapshotsWindow.SnapshotsDataGrid.DataContext = _snapshots;
                _snapshotsWindow.Show();
            }
            else
            {
                _snapshotsWindow.Show();
            }
        }

        private void ThreadContextContextMenuItem_Frame_Click(object sender, RoutedEventArgs e)
        {
            UIRegister item = ThreadContextDataGrid.SelectedItem as UIRegister;
            if (item != null)
            {
                Clipboard.SetText($"{item.Value}");
            }
        }

        private void FrameContextMenuItem_Frame_Click(object sender, RoutedEventArgs e)
        {
            UIFrame item = FrameDataGrid.SelectedItem as UIFrame;
            if (item != null)
            {
                Clipboard.SetText($"{item.Frame}");
            }
        }

        private void StackContextMenuItem_Address_Click(object sender, RoutedEventArgs e)
        {
            UIStack item = StackDataGrid.SelectedItem as UIStack;
            if (item != null)
            {
                Clipboard.SetText($"{item.Data.Address}");
            }
        }

        private void StackContextMenuItem_Type_Click(object sender, RoutedEventArgs e)
        {
            UIStack item = StackDataGrid.SelectedItem as UIStack;
            if (item != null)
            {
                Clipboard.SetText(item.Data.Type);
            }
        }

        private void StackContextMenuItem_Value_Click(object sender, RoutedEventArgs e)
        {
            UIStack item = StackDataGrid.SelectedItem as UIStack;
            if (item != null)
            {
                Clipboard.SetText(item.Data.Value);
            }
        }

        private void StackDataGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            /* Doesn't work as expected
            if (_snapshots == null)
                return;
            var dg = sender as DataGrid;
            UIStack stack = dg.SelectedItem as UIStack;
            if (stack != null)
            {
                _snapshots[_snapshot].Settings.History.Push(_snapshots[_snapshot].Settings.Index);
                NavigateToIndex((int)stack.Data.Object.ObjectId);
                Dispatcher.BeginInvoke((Action)(() => MainTabControl.SelectedIndex = 1));
            }
            */
        }

        private void ObjectViewContextMenuItem_Hex_Click(object sender, RoutedEventArgs e)
        {
            string hexString = "";
            foreach (var item in ObjectViewDataGrid.SelectedItems)
            {
                hexString += (item as UIHexData).Hex;
            }
            Clipboard.SetText(hexString.Replace(" ", String.Empty));
        }

        private void AutoAnalyzeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
        }

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                using (new WaitCursor())
                {
                    if (_dbOpen)
                        CloseDataBase();
                    OpenDataBase(openFileDialog.FileName);
                }
            }
        }
        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                CloseDataBase();
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                CloseDataBase();
            }
            System.Windows.Application.Current.Shutdown();
        }
        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"GarbageMan v{Assembly.GetExecutingAssembly().GetName().Version.ToString()}", "About GarbageMan", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AttachMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Attach attach = new Attach(BasePath);
            attach.Owner = this;
            attach.Closed += (a, b) =>
            {
                this.Activate();
                if (attach.DataBasePath != null)
                {
                    using (new WaitCursor())
                    {
                        OpenDataBase(attach.DataBasePath, realPath: attach.RealPath);
                    }
                }
            };
            attach.Show();
        }

        private void RunExecutableMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RunExecutable run = new RunExecutable(BasePath);
            run.Owner = this;
            run.Closed += (a, b) =>
            {
                this.Activate();
                if (run.DataBasePath != null)
                {
                    using (new WaitCursor())
                    {
                        OpenDataBase(run.DataBasePath, realPath: run.RealPath);
                    }
                }
            };
            run.Show();
        }
        private void CrashDumpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CrashDump crashdump = new CrashDump(BasePath);
            crashdump.Owner = this;
            crashdump.Closed += (a, b) =>
            {
                this.Activate();
                if (crashdump.DataBasePath != null)
                {
                    using (new WaitCursor())
                    {
                        OpenDataBase(crashdump.DataBasePath, realPath: crashdump.RealPath);
                    }
                }
            };
            crashdump.Show();
        }

    }
}
