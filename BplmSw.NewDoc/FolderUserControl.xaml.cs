using BplmSw.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static BplmSw.HttpRequest;

namespace BplmSw.NewDoc
{
    public class FolderItem : INotifyPropertyChanged
    {
        public string Uid { get; set; }
        public string ObjectType { get; set; }

        private string name;
        public string Name
        {
            get { return name; }
            set { name = value;  OnPropertyChanged(); }
        }
        public string Ownner { get; set; }
        public string CheckInOutStatus { get; set; }

        private ObservableCollection<FolderItem> subItems;

        public ObservableCollection<FolderItem> SubItems
        {
            get => subItems ?? (subItems = new ObservableCollection<FolderItem>());
            set { subItems = value; OnPropertyChanged(); }
        }

        private bool isEditing;

        public bool IsEditing
        {
            get { return isEditing; }
            set { isEditing = value; OnPropertyChanged(); }
        }

        private FolderItem parent;

        public FolderItem Parent
        {
            get { return parent; }
            set { parent = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    /// <summary>
    /// FolderUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class FolderUserControl : UserControl
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<FolderItem> rootFolders;

        public ObservableCollection<FolderItem> RootFolders
        {
            get { if (null == rootFolders) { rootFolders = new ObservableCollection<FolderItem>(); } return rootFolders; }
            set { rootFolders = value; OnPropertyChanged(nameof(rootFolders)); }
        }

        // 文件夹搜索列表
        private ObservableCollection<FolderItem> searchFolders;

        public ObservableCollection<FolderItem> SearchFolders
        {
            get { if (null == searchFolders) searchFolders = new ObservableCollection<FolderItem>(); return searchFolders; }
            set { searchFolders = value; OnPropertyChanged(nameof(SearchFolders)); }
        }

        private string editingText;


        private void dfsParseFolderItem(FolderResponse.Folder folder, FolderItem parentFolder, ref FolderItem item)
        {
            item.Uid = folder.strUid;
            item.ObjectType = folder.strObjectType;
            item.Name = folder.strObjectString;
            item.Ownner = folder.strOwningUserInfo;
            item.CheckInOutStatus = folder.strCheckInOutStatus;
            item.Parent = parentFolder;
            item.IsEditing = false;
            foreach (FolderResponse.Folder subFolder in folder.children)
            {
                FolderItem subItem = new FolderItem();
                dfsParseFolderItem(subFolder, item, ref subItem);
                item.SubItems.Add(subItem);
            }
        }

        public FolderUserControl()
        {
            InitializeComponent();
            // 订阅Loaded事件
            this.Loaded += MainWindow_Loaded;

            this.DataContext = this;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 在Loaded事件处理程序中执行异步初始化
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            // 异步加载数据
            FolderResponse res = (FolderResponse)await HttpRequest.getCurrentUserHomeDirectory();
            if (res.isSuccess)
            {
                // 初始化树列表
                rootFolders.Clear();
                FolderItem item = new FolderItem();
                dfsParseFolderItem(res.folder, null, ref item);
                RootFolders.Add(item);

                // 初始化当前文件夹为NewStuff
                foreach (var i in RootFolders[0].SubItems)
                {
                    if ("NewStuff" == i.Name && "Stuff" == i.ObjectType)
                    {
                        curFolder.Text = i.Name;
                        KeyValuePair<string, string> pair = new KeyValuePair<string, string>(i.Uid, i.ObjectType);
                        DataPassed?.Invoke(this, pair);
                    }
                }
            }
            else
            {
                curFolder.Text = res.mesg;
            }
        }

        public event EventHandler<KeyValuePair<string, string>> DataPassed;

        private async void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 获取选中项
                var selectedItem = treeViewFolder.SelectedItem;
                if (selectedItem is FolderItem folderItem)
                {
                    // 设为当前文件夹，触发事件
                    curFolder.Text = folderItem.Name;
                    KeyValuePair<string, string> pair = new KeyValuePair<string, string>(folderItem.Uid, folderItem.ObjectType);
                    DataPassed?.Invoke(this, pair);

                    // 展开子列表
                    if (folderItem.SubItems.Count > 0)
                        return;

                    var baseRes = await HttpRequest.getChildFolderListByParentFolder(folderItem.Uid, folderItem.ObjectType);
                    FolderResponse res = (FolderResponse)baseRes;
                    if (res.isSuccess)
                    {
                        foreach (FolderResponse.Folder subFolder in res.folder.children)
                        {
                            FolderItem subItem = new FolderItem();
                            subItem.Uid = subFolder.strUid;
                            subItem.ObjectType = subFolder.strObjectType;
                            subItem.Name = subFolder.strObjectString;
                            subItem.Ownner = subFolder.strOwningUserInfo;
                            subItem.CheckInOutStatus = subFolder.strCheckInOutStatus;
                            subItem.Parent = folderItem;
                            subItem.IsEditing = false;
                            folderItem.SubItems.Add(subItem);
                        }

                        // 获取双击的源元素
                        var source = e.OriginalSource as DependencyObject;

                        // 向上查找TreeViewItem
                        var treeViewItem = FindVisualParent<TreeViewItem>(source);

                        if (treeViewItem != null)
                        {
                            // 切换展开状态
                            treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
                            e.Handled = true; // 标记事件已处理
                        }
                    }
                    else
                    {
                        throw new Exception(res.mesg);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 辅助方法：查找视觉树上的父元素
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox && textBox.DataContext is FolderItem node)
                {
                    if (e.Key == Key.Enter || e.Key == Key.Escape)
                    {
                        //var baseRes = await HttpRequest.editFolder(node.Uid, node.ObjectType, textBox.Text, "");
                        //if (!baseRes.isSuccess)
                        //    throw new Exception("重命名失败: " + baseRes.mesg);

                        node.IsEditing = false;
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void AddChildMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var node = treeViewFolder.SelectedItem as FolderItem;
                if (null == node)
                    return;

                var baseRes = await HttpRequest.createFolder("Folder", "新建文件夹", "", node.ObjectType, node.Uid);
                if (!baseRes.isSuccess)
                    throw new Exception("文件夹创建失败: " + baseRes.mesg);
                StringResponse strRes = (StringResponse)baseRes;

                baseRes = await HttpRequest.checkOut(strRes.str, "Folder");
                if (!baseRes.isSuccess)
                    throw new Exception("文件夹签出失败: " + baseRes.mesg);

                var newNode = new FolderItem { Name = "新建文件夹", IsEditing = true, Parent = node, Uid = strRes.str, ObjectType = "Folder" };
                node.SubItems.Add(newNode);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = treeViewFolder.SelectedItem as FolderItem;
            if (null == node)
                return;

            var baseRes = await HttpRequest.checkOut(node.Uid, "Folder");
            if (!baseRes.isSuccess)
                throw new Exception("文件夹签出失败: " + baseRes.mesg);

            node.IsEditing = true;
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var node = treeViewFolder.SelectedItem as FolderItem;
                if (null == node)
                    return;

                var baseRes = await HttpRequest.deleteItem(new Dictionary<string, string> { { node.Uid, node.ObjectType } });
                if (!baseRes.isSuccess)
                    throw new Exception("文件夹删除失败: " + baseRes.mesg);

                // 如果选中节点有父节点，则从父节点的Children集合中移除
                if (node.Parent != null)
                    node.Parent.SubItems.Remove(node);
                else
                    // 否则，从根集合中移除
                    RootFolders?.Remove(node);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = treeViewFolder.SelectedItem;
                if (selectedItem is FolderItem folderItem)
                {
                    folderItem.SubItems.Clear();

                    var baseRes = await HttpRequest.getChildFolderListByParentFolder(folderItem.Uid, folderItem.ObjectType);
                    FolderResponse res = (FolderResponse)baseRes;
                    if (!res.isSuccess)
                        throw new Exception("刷新失败: " + res.mesg);

                    foreach (FolderResponse.Folder subFolder in res.folder.children)
                    {
                        FolderItem subItem = new FolderItem();
                        subItem.Uid = subFolder.strUid;
                        subItem.ObjectType = subFolder.strObjectType;
                        subItem.Name = subFolder.strObjectString;
                        subItem.Ownner = subFolder.strOwningUserInfo;
                        subItem.CheckInOutStatus = subFolder.strCheckInOutStatus;
                        subItem.Parent = folderItem;
                        folderItem.SubItems.Add(subItem);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FolderSearchParams folderParams = new FolderSearchParams();
                folderParams.objectName = textName.Text;
                folderParams.owningUser = textOwnner.Text;
                var baseRes = await HttpRequest.folderSearch(folderParams);
                if (!baseRes.isSuccess)
                    throw new Exception("文件夹搜索失败: " + baseRes.mesg);

                FolderSearchResponse res = (FolderSearchResponse)baseRes;
                treeViewFolder.Visibility = Visibility.Collapsed;
                dataGridSearch.Visibility = Visibility.Visible;
                SearchFolders.Clear();
                foreach (var i in res.folders)
                {
                    if (i.owningUser != null && !i.owningUser.Contains(SessionContext.UserName))
                        continue;

                    FolderItem folderItem = new FolderItem();
                    folderItem.Uid = i.uid;
                    folderItem.Name = i.objectName;
                    folderItem.Ownner = i.owningUser;
                    folderItem.ObjectType = i.objectType;
                    SearchFolders.Add(folderItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            treeViewFolder.Visibility = Visibility.Visible;
            dataGridSearch.Visibility = Visibility.Collapsed;
        }

        private void dataGridSearch_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedItem = dataGridSearch.SelectedItem;
            if (selectedItem is FolderItem folderItem)
            {
                curFolder.Text = folderItem.Name;
                KeyValuePair<string, string> pair = new KeyValuePair<string, string>(folderItem.Uid, folderItem.ObjectType);
                DataPassed?.Invoke(this, pair);
            }
        }

        private async void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox && textBox.DataContext is FolderItem item)
                {
                    if (RootFolders[0] == item)
                        return;

                    if(editingText != item.Name) // 判断文本是否经过修改
                    {
                        var baseRes = await HttpRequest.editFolder(item.Uid, item.ObjectType, textBox.Text, "");
                        if (!baseRes.isSuccess)
                            throw new Exception("重命名失败: " + baseRes.mesg);
                    }
                    item.IsEditing = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                editingText = textBox.Text;
                textBox.Focus();
                textBox.SelectAll();
            }
        }

    }
}