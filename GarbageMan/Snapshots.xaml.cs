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
    /// Interaction logic for Snapshots.xaml
    /// </summary>
    public partial class Snapshots : Window
    {
        public Snapshots()
        {
            InitializeComponent();
        }

        private void SnapshotsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void SnapshotsDataGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dg = sender as DataGrid;
            UISnapshot obj = dg.SelectedItem as UISnapshot;
            if (obj != null)
            {
                ((MainWindow)this.Owner).SwitchToSnapshot(obj.Id-1);
                this.Close();
            }
        }
    }

}
