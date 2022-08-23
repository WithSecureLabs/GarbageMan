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
    /// Interaction logic for RawSql.xaml
    /// </summary>
    public partial class RawSql : Window
    {
        public RawSql()
        {
            InitializeComponent();
        }

        // XXX: verify at least "LIMIT"
        private bool VerifySql()
        {
            if (!SqlTextBox.Text.ToUpper().Contains(" LIMIT "))
            {
                MessageBox.Show($"Please put some kind of LIMIT to the query", "GarbageMan", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (VerifySql())
            {
                string json = "      {\n";
                json += "       \"Header\": \"MySearch\",\n";
                json += "       \"Category\": \"MyCategory\",\n";
                json += "       \"SearchType\": \"Custom\",\n";
                json += "       \"SearchAll\": \"No\",\n";
                json += $"       \"SQL\": \"{SqlTextBox.Text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", String.Empty).Replace("\r", String.Empty)}\"\n";
                json += "      }\n";
                Clipboard.SetText(json);
            }
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                if (VerifySql())
                    ((MainWindow)this.Owner).SearchRawSql(SqlTextBox.Text);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
