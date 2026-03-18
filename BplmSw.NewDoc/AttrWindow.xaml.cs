using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace BplmSw.NewDoc
{
    /// <summary>
    /// AttrWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AttrWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<AttrItem> attrItems;

        public ObservableCollection<AttrItem> AttrItems
        {
            get { return attrItems; }
            set { attrItems = value; OnPropertyChanged(nameof(AttrItems)); }
        }


        public AttrWindow()
        {
            InitializeComponent();
        }

        public AttrWindow(ObservableCollection<AttrItem> attrItems)
        {
            InitializeComponent();

            AttrItems = attrItems;

            this.DataContext = this;
        }

        public event EventHandler<ObservableCollection<AttrItem>> DataPassed;
        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            DataPassed?.Invoke(this, AttrItems);
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void dataGridAttr_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var dataItem = e.Row.Item as AttrItem;
            if (dataItem == null || dataItem.IsReadOnly)
            {
                e.Cancel = true;
            }
        }
    }
}