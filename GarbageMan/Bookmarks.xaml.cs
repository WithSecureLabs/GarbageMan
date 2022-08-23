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

namespace GarbageMan
{
    /// <summary>
    /// Interaction logic for Bookmarks.xaml
    /// </summary>
    public partial class Bookmarks : Window
    {
        public Bookmarks(List<UIBookmark> bookmarks)
        {
            InitializeComponent();
            BookmarksDataGrid.DataContext = bookmarks;
        }

        private void BookmarkDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            return;
        }

        private void BookmarkDataGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dg = sender as DataGrid;
            UIBookmark obj = dg.SelectedItem as UIBookmark;
            if (obj != null)
            {
                ((MainWindow)this.Owner).NavigateToObject(obj.Data);
            }
        }

        private void BookmarksDataGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            var grid = (DataGrid)sender;
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void BookmarkContextMenuItem_Address_Click(object sender, RoutedEventArgs e)
        {
            UIBookmark item = BookmarksDataGrid.SelectedItem as UIBookmark;
            if (item != null)
            {
                Clipboard.SetText($"{item.Data.Address}");
            }
        }

        private void BookmarkContextMenuItem_Type_Click(object sender, RoutedEventArgs e)
        {
            UIBookmark item = BookmarksDataGrid.SelectedItem as UIBookmark;
            if (item != null)
            {
                Clipboard.SetText(item.Data.Type);
            }
        }

        private void BookmarkContextMenuItem_Value_Click(object sender, RoutedEventArgs e)
        {
            UIBookmark item = BookmarksDataGrid.SelectedItem as UIBookmark;
            if (item != null)
            {
                Clipboard.SetText(item.Data.Value);
            }
        }
        private void BookmarkContextMenuItem_RemoveBookmark_Click(object sender, RoutedEventArgs e)
        {
            UIBookmark item = BookmarksDataGrid.SelectedItem as UIBookmark;
            if (item != null)
            {
                ((MainWindow)this.Owner).RemoveBookmark(item.Data);
            }
        }
    }
}
