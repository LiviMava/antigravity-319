using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using BplmSw.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using static BplmSw.HttpRequest;
using System.Windows.Forms;
using System.Drawing.Printing;
using Cursors = System.Windows.Input.Cursors;
using Mouse = System.Windows.Input.Mouse;
using static BplmSw.Constants;
using MessageBox = System.Windows.MessageBox;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using BplmSw.Utils;
using System.Windows.Shapes;
using Path = System.IO.Path;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;

namespace BplmSw.OpenDoc
{
    public enum OpenDocWindowMode
    {
        /// <summary>
        /// 打开模式：下载 → 打开 → 同步属性 → 关闭窗口
        /// </summary>
        Open,

        /// <summary>
        /// 仅返回零部件信息
        /// </summary>
        Selection
    }
    public class DisplayItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Id { get; set; }//编码
        public string Rev { get; set; }//版本
        public string Desc { get; set; }//中文名称
        public string TypeName { get; set; }
        public string State { get; set; }
        public string Owner { get; set; }
        public string IconPath { get; set; }

        public string Uid { get; set; } // Hidden
        public string ObjectType { get; set; } // Hidden
        public bool IsFolder { get; set; }

        public ObservableCollection<DisplayItem> Children { get; set; } = new ObservableCollection<DisplayItem>();

        private bool isExpanded;
        public bool IsExpanded
        {
            get { return isExpanded; }
            set { isExpanded = value; OnPropertyChanged(); }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get { return isSelected; }
            set { isSelected = value; OnPropertyChanged(); }
        }

        public bool IsLoaded { get; set; } = false;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public partial class OpenDocWindow : Window
    {
        public event EventHandler<ModelDoc2> DataPassed;
        public event EventHandler<KeyValuePair<string, string>> DataPassed_Selection;

        private ISldWorks Sw;
        private ObservableCollection<DisplayItem> MainList = new ObservableCollection<DisplayItem>();
        private ObservableCollection<ItemRevisionResponse.ItemRevision> HistoryList = new ObservableCollection<ItemRevisionResponse.ItemRevision>();
        private ObservableCollection<AttrItem> AttributeList = new ObservableCollection<AttrItem>();
        private bool isSearchMode = false;
        private OpenDocWindowMode _mode = OpenDocWindowMode.Open;

        public OpenDocWindow(ISldWorks sw, OpenDocWindowMode mode)
        {
            InitializeComponent();
            this.Sw = sw;
            this._mode = mode;
            // 根据模式设置窗口标题
            switch (mode)
            {
                case OpenDocWindowMode.Open:
                    this.Title = "打开文件";
                    break;
                case OpenDocWindowMode.Selection:
                    this.Title = "选择零部件";
                    break;
            }

            var interopHelper = new WindowInteropHelper(this);
            if (sw != null) interopHelper.Owner = (IntPtr)sw.IFrameObject().GetHWnd();

            gridMain.ItemsSource = MainList;
            gridHistory.ItemsSource = HistoryList;
            gridAttributes.ItemsSource = AttributeList;

            this.Loaded += OpenDocWindow_Loaded;
        }

        private async void OpenDocWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化类型下拉列表
            List<string> searchTypes = new List<string> { };
            if (ConfigParser.SwItemTypeMap != null && ConfigParser.SwItemTypeMap.Ens2CN != null)
            {
                foreach (var val in ConfigParser.SwItemTypeMap.Ens2CN.Values)
                {
                    if (!searchTypes.Contains(val))
                    {
                        searchTypes.Add(val);
                    }
                }
            }
            cbSearchType.ItemsSource = searchTypes;
            //初始化主目录
            await LoadHomeDirectory();
        }
        //加载主目录
        private async Task LoadHomeDirectory()
        {
            SetBusy(true);
            try
            {
                MainList.Clear();
                var res = (FolderResponse)await HttpRequest.getCurrentUserHomeDirectory();
                if (res.isSuccess && res.folder.children != null)
                {
                    foreach (var child in res.folder.children)
                    {
                        MainList.Add(FolderToDisplayItem(child));
                    }
                }
                isSearchMode = false;
                UpdatePaginationUI(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载主目录失败: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }
        //展示文件夹
        private DisplayItem FolderToDisplayItem(FolderResponse.Folder folder)
        {
            bool isFolder = FOLDER_ITEMS.Contains(folder.strObjectType);

            var item = new DisplayItem
            {
                Name = folder.strObjectString,
                Id = "",
                Rev = "",
                Desc = folder.strObjectString,
                TypeName = folder.strObjectType,
                State = folder.strWorkflowStatus,
                Owner = folder.strOwningUserInfo,
                Uid = folder.strUid,
                ObjectType = folder.strObjectType,
                IsFolder = isFolder,
                IconPath = isFolder ? "/BplmSw.OpenDoc;component/Resources/folder.png" : "/BplmSw.OpenDoc;component/Resources/attribute.png"
            };
            // 假设主目录下的文件夹都有子项，或者是为了显示展开箭头
            if (isFolder)
            {
                item.Children.Add(new DisplayItem { Name = "Loading..." });
            }
            return item;
        }
        //加载子目录
        private async Task LoadFolder(DisplayItem parentItem)
        {
            // 不锁全屏，只在节点下显示加载
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var res = (FatherObjectResponse)await HttpRequest.getChildListByParentObject(parentItem.Uid, parentItem.ObjectType);
                parentItem.Children.Clear();
                if (res.isSuccess && res.fatherObject.children != null)
                {
                    foreach (var child in res.fatherObject.children)
                    {
                        parentItem.Children.Add(ObjectToDisplayItem(child));
                    }
                }
                parentItem.IsLoaded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载文件夹失败: " + ex.Message);
                parentItem.IsExpanded = false;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void TreeView_Expanded(object sender, RoutedEventArgs e)
        {
            var tvi = e.OriginalSource as TreeViewItem;
            if (tvi == null) return;
            var item = tvi.DataContext as DisplayItem;
            if (item == null) return;

            if (item.IsFolder && !item.IsLoaded)
            {
                await LoadFolder(item);
            }
        }
        //展示零部件对象
        private DisplayItem ObjectToDisplayItem(FatherObjectResponse.FatherObject folder)
        {
            bool isFolder = FOLDER_ITEMS.Contains(folder.strObjectType);
            // 获取名称以拆成三个属性展示
            string rawName = folder.strObjectString ?? string.Empty;
            string partId = rawName;
            string partRev = folder.strItemRevisionId;
            string partDesc = "";
            // 构造用于分割的锚点，例如 "-A0-"
            string separator = $"-{partRev}-";
            int revIndex = rawName.LastIndexOf(separator);
            // 如果成功找到"-A0-"
            if (revIndex >= 0)
            {
                partId = rawName.Substring(0, revIndex);
                partDesc = rawName.Substring(revIndex + separator.Length);
            }

            var displayItem = new DisplayItem
            {
                Name = rawName,
                Id = partId,
                Rev = partRev,
                Desc = partDesc,
                TypeName = folder.strObjectType,
                State = folder.strCheckInOutStatus ? "已签出" : "",
                Owner = folder.strOwningUserInfo,
                Uid = folder.strUid,
                ObjectType = folder.strObjectType,
                IsFolder = isFolder,
                IconPath = isFolder ? "/BplmSw.OpenDoc;component/Resources/folder.png" : "/BplmSw.OpenDoc;component/Resources/attribute.png"
            };

            // 如果是文件夹或BOM结构，并且API指示有子项
            if (folder.hasChild == "1" || folder.hasChild == "true" || isFolder)
            {
                displayItem.Children.Add(new DisplayItem { Name = "Loading..." });
            }

            return displayItem;
        }

        //按钮禁用、等待光标
        private bool isBusy = false;
        private void SetBusy(bool busy)
        {
            isBusy = busy;
            if (busy)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                btnSearch.IsEnabled = false;
                btnBack.IsEnabled = false;
                btnOK.IsEnabled = false;
                gridMain.IsEnabled = false;
                cbSearchType.IsEnabled = false;
                txtSearchId.IsEnabled = false;
                txtSearchName.IsEnabled = false;
            }
            else
            {
                Mouse.OverrideCursor = null;
                btnSearch.IsEnabled = true;
                btnBack.IsEnabled = true;
                btnOK.IsEnabled = true;
                gridMain.IsEnabled = true;
                cbSearchType.IsEnabled = true;
                txtSearchId.IsEnabled = true;
                txtSearchName.IsEnabled = true;
            }
            UpdatePaginationUI(isSearchMode);
        }

        private void UpdatePaginationUI(bool isSearch)
        {
            if (isSearch)
            {
                btnPrev.Visibility = Visibility.Visible;
                btnNext.Visibility = Visibility.Visible;
                txtPageInfo.Visibility = Visibility.Visible;

                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (totalPages < 1) totalPages = 1;
                txtPageInfo.Text = $"{currentPage}/{totalPages}";
                btnPrev.IsEnabled = !isBusy && currentPage > 1;
                btnNext.IsEnabled = !isBusy && currentPage < totalPages;
                lblTotal.Content = $"共 {totalCount} 条";
            }
            else
            {
                btnPrev.Visibility = Visibility.Collapsed;
                btnNext.Visibility = Visibility.Collapsed;
                txtPageInfo.Visibility = Visibility.Collapsed;
                lblTotal.Content = $"共 {MainList.Count} 条";
            }
        }
        //翻页
        private int currentPage = 1;
        private int pageSize = 10;
        private int totalCount = 0;
        //上一页
        private async void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                await DoSearch();
            }
        }
        //下一页
        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (totalPages < 1) totalPages = 1;
            if (currentPage < totalPages)
            {
                currentPage++;
                await DoSearch();
            }
        }
        //搜索执行逻辑
        private async Task DoSearch()
        {
            SetBusy(true);
            string id = txtSearchId.Text.Trim();
            string name = txtSearchName.Text.Trim();
            string type = cbSearchType.SelectedItem as string;
            // 映射回英文，反向查找
            if (string.IsNullOrEmpty(type))
                type = "";
            else if (ConfigParser.SwItemTypeMap != null && ConfigParser.SwItemTypeMap.CN2Ens.ContainsKey(type))
                type = ConfigParser.SwItemTypeMap.CN2Ens[type] + "Revision";

            try
            {
                MainList.Clear();
                var res = (LastItemRevisionResponse)await HttpRequest.advancedSearch(id, name, type, currentPage, pageSize);

                if (res.isSuccess && res.data.data != null)
                {
                    if (res.data.total == "0")
                    {
                        MainList.Add(new DisplayItem
                        {
                            Name = "搜索结果为空",
                            Id = "",
                            Rev = "",
                            Desc = "",
                            TypeName = "",
                            State = "",
                            Owner = "",
                            Uid = "",
                            ObjectType = "",
                            IsFolder = false,
                            IconPath = "/BplmSw.OpenDoc;component/Resources/attribute.png"
                        });
                    }
                    foreach (var item in res.data.data)
                    {
                        MainList.Add(new DisplayItem
                        {
                            Name = item.id + "-" + item.versionNum + "-" + item.name,
                            Id = item.id,
                            Rev = item.versionNum,
                            Desc = item.name,
                            TypeName = item.itemType,
                            State = item.checkoutStatusName,
                            Owner = item.owningUserName,
                            Uid = item.uid,
                            ObjectType = item.revisionType,
                            IsFolder = false,
                            IconPath = "/BplmSw.OpenDoc;component/Resources/attribute.png"
                        });
                    }
                    if (!string.IsNullOrEmpty(res.data.total))
                        int.TryParse(res.data.total, out totalCount);
                }
                else
                {
                    totalCount = 0;
                }
                isSearchMode = true;
                UpdatePaginationUI(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("搜索失败: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }
        //搜索按钮
        private async void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            currentPage = 1;
            await DoSearch();
        }
        //返回按钮
        private async void btnBack_Click(object sender, RoutedEventArgs e)
        {
            HistoryList.Clear();
            AttributeList.Clear();
            imgPreview.Source = new BitmapImage(new Uri("/BplmSw.OpenDoc;component/Resources/preview.bmp", UriKind.Relative));
            if (isSearchMode)
            {
                txtSearchId.Text = "";
                txtSearchName.Text = "";
                await LoadHomeDirectory();
            }
            else
            {
                await LoadHomeDirectory();
            }
        }
        //单击 / 选中
        private async void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = e.NewValue as DisplayItem;
            if (item == null) return;
            // Ignore dummy item
            if (item.Name == "Loading..." && string.IsNullOrEmpty(item.Uid)) return;

            HistoryList.Clear();
            AttributeList.Clear();
            imgPreview.Source = new BitmapImage(new Uri("/BplmSw.OpenDoc;component/Resources/preview.bmp", UriKind.Relative));

            // 加载历史
            try
            {
                var histRes = (ItemRevisionResponse)await HttpRequest.getItemRevision(item.ObjectType, item.Uid);
                if (histRes.isSuccess && histRes.data != null)
                {
                    foreach (var h in histRes.data) HistoryList.Add(h);
                }
            }
            catch { }

            // 加载属性
            try
            {
                var attrRes = (AttributesResponse)await HttpRequest.queryItemAttributes(item.Uid, item.ObjectType);
                if (attrRes.isSuccess && attrRes.attributes != null)
                {
                    List<SwAttr> attrList;
                    string tempObjectType = null;
                    //处理版本对象
                    if (item.ObjectType.EndsWith(REVISION_SUFFIX)) tempObjectType = item.ObjectType.Substring(0, item.ObjectType.Length - REVISION_SUFFIX.Length);
                    //从内存中的字典根据类型取出属性list
                    if (tempObjectType != null && ConfigParser.SwAttrs != null)
                    {
                        ConfigParser.SwAttrs.TryGetValue(tempObjectType, out attrList);
                        if (null != attrList)
                        {
                            foreach (SwAttr i in attrList)
                            {
                                //给required的属性赋值
                                if (i.Required)
                                {
                                    var attrItem = new AttrItem
                                    {
                                        AttrId = i.AttrId,//key
                                        Value = attrRes.attributes.ContainsKey(i.AttrId) ? attrRes.attributes[i.AttrId] : "",//value
                                        Description = i.Description,
                                        IsReadOnly = i.ReadOnly,
                                        IsRequired = i.Required,
                                        Mode = i.Mode
                                    };
                                    //添加到显示栏
                                    AttributeList.Add(attrItem);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 加载预览图
            try
            {
                PreviewExpander.Header = "空附件";
                var dsRes = (ItemDataSetResponse)await HttpRequest.getPartAttachmentByPartIdBatch(new Dictionary<string, string> { { item.Uid, item.ObjectType } });
                if (dsRes.isSuccess && dsRes.itemDataSets.Count > 0)
                {
                    var itemDs = dsRes.itemDataSets[0];
                    if (itemDs.dataSets != null)
                    {
                        foreach (var ds in itemDs.dataSets)
                        {
                            // 检查扩展名
                            string ext = Path.GetExtension(ds.strObjectName).ToLower();
                            if (ext == ".sldprt" || ext == ".slddrw" || ext == ".sldasm")
                            {
                                //展示文件类型
                                PreviewImageTitleMap.TryGetValue(ds.strDatasetType, out string title);
                                PreviewExpander.Header = title;
                            }
                            if (ext == ".jpg" || ext == ".png" || ext == ".bmp" || ext == ".jpeg")
                            {
                                // 下载图片
                                string imagePath = Path.Combine(Constants.SW_CACHE_PATH, "Previews", ds.strObjectName);
                                Directory.CreateDirectory(Path.GetDirectoryName(imagePath));

                                if (!File.Exists(imagePath))
                                {
                                    await HttpRequest.partAttachmentDownload(ds.strFileId, ds.strObjectName);
                                    string downloadedPath = Path.Combine(Constants.SW_CACHE_PATH, ds.strObjectName);
                                    if (File.Exists(downloadedPath) && downloadedPath != imagePath)
                                    {
                                        File.Move(downloadedPath, imagePath);
                                    }
                                    else
                                    {
                                        imagePath = downloadedPath;
                                    }
                                }

                                if (File.Exists(imagePath))
                                {
                                    BitmapImage bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.UriSource = new Uri(imagePath);
                                    bitmap.EndInit();
                                    imgPreview.Source = bitmap;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        //双击
        private async void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {// 双击文件夹展开/折叠，双击文件打开
            // 获取实际点击的项
            if (e.OriginalSource is DependencyObject d)
            {
                var tvi = FindVisualParent<TreeViewItem>(d);
                if (tvi != null)
                {
                    var item = tvi.DataContext as DisplayItem;
                    if (item != null && !item.IsFolder && item.Name != "Loading...")
                    {
                        await OpenFile(item);
                    }
                    // 如果是文件夹，标准的TreeView行为会展开/折叠
                }
            }
        }

        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }
        //确定
        private async void btnOK_Click(object sender, RoutedEventArgs e)
        {
            var item = gridMain.SelectedItem as DisplayItem;
            if (item != null && !item.IsFolder)
            {
                await OpenFile(item);
            }
        }
        //取消
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        //打开文件执行逻辑
        private async Task OpenFile(DisplayItem item)
        {
            string id = item.Uid, type = item.ObjectType;
            try
            {
                if (_mode == OpenDocWindowMode.Selection)
                {
                    DataPassed_Selection?.Invoke(this, new KeyValuePair<string, string>(id, type));
                    this.Close();
                    return;
                }

                // 1. 获取附件Response
                ItemDataSetResponse dsRes = (ItemDataSetResponse)await HttpRequest.getPartAttachmentByPartIdBatch(new Dictionary<string, string> { { id, type } });
                if (!dsRes.isSuccess)
                    throw new Exception("查询零部件数据集失败: " + dsRes.mesg);

                bool isDatasetExist = false;
                ItemDataSetResponse.DataSet topDataset = default(ItemDataSetResponse.DataSet);
                string fileExtType = "";
                // 2. 判断空，记录类型
                foreach (var ds in dsRes.itemDataSets[0].dataSets)
                {
                    if (DATASET_ASM_MASTER == ds.strDatasetType || DATASET_PRT_MASTER == ds.strDatasetType || DATASET_DRW_MASTER == ds.strDatasetType)
                    {
                        fileExtType = ds.strDatasetType;
                        topDataset = ds;
                        isDatasetExist = true;
                        break;
                    }
                }

                if (!isDatasetExist) // 空数据集进入新建
                {
                    string path = ConfigParser.AddInHomePath + @"\Assemblies\net472\BplmSw.Launcher.exe";
                    string args = @"BSWLauncher://" + SessionContext.Token + "," + id + "," + type;
                    Process.Start(path, args);
                    this.Close();
                    return;
                }
                string topFileName = topDataset.strObjectName;
                string topFullPath = Path.Combine(Constants.SW_CACHE_PATH, topFileName);

                // 3. 下载
                if (DATASET_PRT_MASTER == fileExtType || DATASET_DRW_MASTER == fileExtType)
                {
                    await MD5Util.OpenDocDownLoadFromPLM(topFileName, topFullPath, topDataset);
                } else if (DATASET_ASM_MASTER == fileExtType)
                {
                    BomLineResponse bomLineRes = (BomLineResponse)await HttpRequest.getAllBomLineByParent(id, type, "defalut_rev_rule_name");
                    if (!bomLineRes.isSuccess)
                        throw new Exception("查询Bom行失败: " + bomLineRes.mesg);
                    Dictionary<string, BomLineResponse.FatherObject> uniqueParts = new Dictionary<string, BomLineResponse.FatherObject>();
                    //去重下载清单
                    MD5Util.TraverseBOMTree(bomLineRes.fatherObject, uniqueParts);
                    // 1. 组装批量查询的参数
                    Dictionary<string, string> batchQueryParams = new Dictionary<string, string>();
                    foreach (var part in uniqueParts.Values)
                    {
                        batchQueryParams.Add(part.revision_puid, part.revision_type);
                    }
                    // 2. 批量查询零部件数据集
                    ItemDataSetResponse idsRes = (ItemDataSetResponse)await HttpRequest.getPartAttachmentByPartIdBatch(batchQueryParams);
                    if (!idsRes.isSuccess)
                        throw new Exception("查询零部件数据集失败: " + idsRes.mesg);
                    // 3. 构建并发下载任务
                    List<Task> downloadTasks = new List<Task>();
                    Dictionary<string, string> toNewDoc = new Dictionary<string, string>();
                    if (idsRes.itemDataSets != null)
                    {
                        ItemDataSetResponse.DataSet childDataset = default(ItemDataSetResponse.DataSet);
                        foreach (var ids in idsRes.itemDataSets)
                        {
                            foreach (var ds in ids.dataSets)
                            {
                                if (DATASET_ASM_MASTER == ds.strDatasetType ||
                                DATASET_PRT_MASTER == ds.strDatasetType ||
                                DATASET_DRW_MASTER == ds.strDatasetType)
                                {
                                    childDataset = ds;
                                    break;
                                }
                            }
                            // 如果找到了符合条件的数据集，则创建下载Task
                            if (!string.IsNullOrEmpty(childDataset.strObjectName))
                            {
                                string childFileName = childDataset.strObjectName;
                                string childFullPath = Path.Combine(Constants.SW_CACHE_PATH, childFileName);
                                Task downloadTask = MD5Util.OpenDocDownLoadFromPLM(childFileName, childFullPath, childDataset);
                                downloadTasks.Add(downloadTask);
                            }
                            else if(!string.IsNullOrEmpty(childDataset.strPuid) && !string.IsNullOrEmpty(childDataset.strType))
                            {
                                toNewDoc[childDataset.strPuid] = childDataset.strType;
                            }
                            childDataset = default(ItemDataSetResponse.DataSet);
                        }
                    }
                    // 等待所有子件和子装配体下载完成
                    if (downloadTasks.Count > 0) await Task.WhenAll(downloadTasks);
                    // 对空数据集顺序打开新建窗口（通过接口调用，避免循环依赖）
                    if (toNewDoc.Count > 0 && NewDocServiceLocator.Instance != null)
                    {
                        foreach (var kvp in toNewDoc)
                        {
                            bool created = NewDocServiceLocator.Instance.ShowNewDocDialog(Sw, kvp.Key, kvp.Value);
                            if (!created) // 用户取消新建，终止整个打开流程
                            {
                                this.Close();
                                return;
                            }
                        }
                    }
                    // 文件下载和新建结束后，自底向上重组更新装配体
                    if (bomLineRes.fatherObject != null)
                    {
                        Dictionary<string, bool> processedDict = new Dictionary<string, bool>();
                        await UpdateAssemblyBottomUp(Sw, bomLineRes.fatherObject, processedDict);
                    }
                }

                // 4. 打开
                int docType = (int)swDocumentTypes_e.swDocPART;
                string fileExt = Path.GetExtension(topFullPath).ToLower();
                if (fileExt == ".sldasm")
                {
                    docType = (int)swDocumentTypes_e.swDocASSEMBLY;
                }
                else if (fileExt == ".slddrw")
                {
                    docType = (int)swDocumentTypes_e.swDocDRAWING;
                }
                if (!File.Exists(topFullPath)) return;
                int err = 0, warn = 0;
                ModelDoc2 doc = Sw.OpenDoc6(topFullPath, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
                this.Close();

                //// 签出
                //var baseRes = await HttpRequest.checkOut(item.Uid, item.ObjectType);
                //if (!baseRes.isSuccess)
                //    throw new Exception("签出失败: " + baseRes.mesg);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开过程出错: " + ex.Message);
            }
        }
        public static async Task SyncAttrs(ISldWorks sw, ModelDoc2 doc, string id, string type)
        {
            int err = 0;
            if (doc != null)
            {// 激活并前台显示
                sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swUserDecision, ref err);

                // 同步属性
                var attrRes = (AttributesResponse)await HttpRequest.queryItemAttributes(id, type);
                if (attrRes.isSuccess && attrRes.attributes != null)
                {
                    CustomPropertyManager propMgr = doc.Extension.CustomPropertyManager[""];
                    // 根据属性配置进行属性同步
                    string objectType = type.Substring(0, type.Length - REVISION_SUFFIX.Length);
                    List<SwAttr> attrList;
                    ConfigParser.SwAttrs.TryGetValue(objectType, out attrList);
                    if (null == attrList)
                        throw new Exception("未匹配的项类型: " + objectType);

                    foreach (SwAttr i in attrList)
                    {
                        string oldVal, newVal;
                        string resolvedVal = "";
                        propMgr.Get6(i.Description, false, out oldVal, out resolvedVal, out bool wasResolved, out bool linkToProp);
                        attrRes.attributes.TryGetValue(i.AttrId, out newVal);

                        if (!string.IsNullOrEmpty(newVal) && newVal != oldVal) // 只有值不同才写
                        {
                            propMgr.Add3(i.Description, 30, newVal, 2);
                        }
                    }
                }
            }
            else Console.WriteLine($"打开失败，错误代码: {err}");
        }
        /// <summary>
        /// 自底向上递归重组装配体
        /// </summary>
        public static async Task UpdateAssemblyBottomUp(ISldWorks sw, BomLineResponse.FatherObject node, Dictionary<string, bool> processedDict)
        {
            int err = 0, warn = 0;
            // 跳过空节点或零件，但会打开并同步属性
            if (node == null || !node.has_children || node.children == null || node.children.Count == 0) {
                if (node != null && node.has_children == false)
                {
                    if (processedDict.ContainsKey(node.revision_puid)) return; // 防止重复处理
                    
                    string prtName = node.item_id + '_' + node.item_revision_id + '_' + node.object_name + ".sldprt";
                    string prtPath = Path.Combine(Constants.SW_CACHE_PATH, prtName);

                    if (!File.Exists(prtPath)) return;
                    ModelDoc2 doc = sw.OpenDoc6(prtPath, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
                    await SyncAttrs(sw, doc, node.revision_puid, node.revision_type);
                    sw.CloseDoc(doc.GetTitle());
                    processedDict[node.revision_puid] = true;
                }
                return;
            };

            // 先递归处理子装配
            foreach (var child in node.children) await UpdateAssemblyBottomUp(sw, child, processedDict);

            // 打开装配
            string asmName = node.item_id + '_' + node.item_revision_id + '_' + node.object_name + ".sldasm";
            string asmPath = Path.Combine(Constants.SW_CACHE_PATH, asmName);
            if (!File.Exists(asmPath)) return;
            ModelDoc2 asmDoc = sw.OpenDoc6(asmPath, (int)swDocumentTypes_e.swDocASSEMBLY, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
            await SyncAttrs(sw, asmDoc, node.revision_puid, node.revision_type);
            // 更新组件（删除并插入）
            if (asmDoc != null)
            {
                AssemblyDoc swAsm = (AssemblyDoc)asmDoc;
                // 1、统计 PLM BOM 预期的组件及其数量
                Dictionary<string, int> expectedBom = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var child in node.children)
                {
                    string ext = child.has_children ? ".sldasm" : ".sldprt";
                    string childFileName = $"{child.item_id}_{child.item_revision_id}_{child.object_name}{ext}";

                    if (expectedBom.ContainsKey(childFileName))
                        expectedBom[childFileName]++;
                    else
                        expectedBom[childFileName] = 1;
                }
                // 2、统计 SolidWorks 现有的组件及其数量
                Dictionary<string, List<Component2>> existingBom = new Dictionary<string, List<Component2>>(StringComparer.OrdinalIgnoreCase);
                object[] existingComps = (object[])swAsm.GetComponents(true); // true 表示只获取当前装配体的顶层子件
                if (existingComps != null)
                {
                    foreach (Component2 comp in existingComps)
                    {
                        string pathName = comp.GetPathName();
                        if (string.IsNullOrEmpty(pathName)) continue; // 忽略虚拟组件或未保存的组件

                        string fileName = Path.GetFileName(pathName);
                        if (!existingBom.ContainsKey(fileName))
                            existingBom[fileName] = new List<Component2>();

                        existingBom[fileName].Add(comp);
                    }
                }
                // 3、执行删除逻辑 (现有数量 > 预期数量) 
                foreach (var kvp in existingBom)
                {
                    string fileName = kvp.Key;
                    List<Component2> comps = kvp.Value;

                    int expectedQty = expectedBom.ContainsKey(fileName) ? expectedBom[fileName] : 0;
                    int existingQty = comps.Count;

                    if (existingQty > expectedQty)
                    {
                        int toDelete = existingQty - expectedQty;
                        for (int i = 0; i < toDelete; i++)
                        {
                            Component2 compToDelete = comps[i];
                            asmDoc.ClearSelection2(true);
                            bool isSelected = compToDelete.Select4(false, null, false);

                            if (isSelected)
                            {
                                bool delResult = asmDoc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                                if (!delResult) Console.WriteLine($"删除组件失败: {compToDelete.Name2}");
                            }
                            else Console.WriteLine($"无法删除幽灵组件: {compToDelete.Name2}");
                        }
                    }
                }
                // 4、执行插入逻辑 (预期数量 > 现有数量) 
                foreach (var kvp in expectedBom)
                {
                    string fileName = kvp.Key;
                    int expectedQty = kvp.Value;
                    int existingQty = existingBom.ContainsKey(fileName) ? existingBom[fileName].Count : 0;

                    if (expectedQty > existingQty)
                    {
                        int toAdd = expectedQty - existingQty;
                        string childPath = Path.Combine(Constants.SW_CACHE_PATH, fileName);
                        if (File.Exists(childPath))
                        {
                            for (int i = 0; i < toAdd; i++)
                            {
                                string ext = Path.GetExtension(fileName).ToLower();
                                int docType = ".sldasm" == ext ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
                                ModelDoc2 doc = sw.OpenDoc6(childPath, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
                                Component2 newComp = swAsm.AddComponent5(
                                    childPath,
                                    (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                                    "", false, "", 0, 0, 0);
                                sw.CloseDoc(doc.GetTitle());
                                if (newComp == null) Console.WriteLine($"插入组件失败，文件可能损坏或版本不兼容: {childPath}");
                            }
                        }
                        else Console.WriteLine($"新增组件时找不到本地文件: {childPath}");
                    }
                }
                //保存并关闭
                asmDoc.ForceRebuild3(true);
                asmDoc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref err, ref warn);
                sw.CloseDoc(asmDoc.GetTitle());
            }
        }

    }
    public class AttrItem : INotifyPropertyChanged, IDataErrorInfo // 属性列表项
    {
        private string attrId;

        public string AttrId
        {
            get { return attrId; }
            set { attrId = value; OnPropertyChanged(); }
        }

        private string description;

        public string Description
        {
            get { return description; }
            set { description = value; OnPropertyChanged(); }
        }

        private string val;

        public string Value
        {
            get { return val; }
            set { val = value; OnPropertyChanged(); }
        }

        private bool isReadOnly;

        public bool IsReadOnly
        {
            get { return isReadOnly; }
            set { isReadOnly = value; OnPropertyChanged(); }
        }

        private bool isRequired;

        public bool IsRequired
        {
            get { return isRequired; }
            set { isRequired = value; }
        }

        private string mode;

        public string Mode
        {
            get { return mode; }
            set { mode = value; }
        }


        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(Value) && IsRequired)
                {
                    if (string.IsNullOrWhiteSpace(Value))
                        return "此行为必填项";
                }
                return null;
            }
        }

        public string Error => null;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}