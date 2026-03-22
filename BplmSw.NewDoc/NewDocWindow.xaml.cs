using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using BplmSw;
using System.Globalization;
using static BplmSw.Constants;
using System.Windows.Forms;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.Forms.MessageBox;
using Mouse = System.Windows.Input.Mouse;
using BplmSw.Common;
using View = SolidWorks.Interop.sldworks.View;
using BplmSw.OpenDoc;
using Button = System.Windows.Controls.Button;
using DataGrid = System.Windows.Controls.DataGrid;
using DataGridCell = System.Windows.Controls.DataGridCell;

namespace BplmSw.NewDoc
{
    public class TemplateItem // 模板列表项
    {
        public string Name { get; set; } // 模板名称
        public string DatasetType { get; set; } // 数据集类型
        public string Unit { get; set; } // 单位
        public string Preview { get; set; } // 预览图
        public string Suffix { get; set; } // 文件后缀
        public List<string> ItemTypes { get; set; } // 支持的项类型
        public string Filename { get; set; } // 模板物理文件名
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

    public enum NewDocWindowMode
    {
        Normal,
        NoDrawingTemplate // 无图纸模板
    }

    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class NewDocWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ISldWorks Sw { get; set; }
        private IFrame iFrame { get; set; }

        private string folderId; // 文件夹uid
        private string folderType; // 文件夹类型
        private bool isIntegrationLaunch = false; // 是否集成启动
        private string passedUid, passedObjectType;

        // 模板数据源
        private ObservableCollection<TemplateItem> templateItems;

        public ObservableCollection<TemplateItem> TemplateItems
        {
            get { if (null == templateItems) templateItems = new ObservableCollection<TemplateItem>(); return templateItems; }
            set { templateItems = value; OnPropertyChanged(nameof(TemplateItems)); }
        }


        // 存储零部件必填属性
        private ObservableCollection<AttrItem> requiredAttrItems;
        public ObservableCollection<AttrItem> RequiredAttrItems
        {
            get { if (null == requiredAttrItems) { requiredAttrItems = new ObservableCollection<AttrItem>(); } return requiredAttrItems; }
            set 
            {
                requiredAttrItems = value;
                OnPropertyChanged("RequiredAttrItems");
            }
        }

        // 存储零部件所有属性
        private ObservableCollection<AttrItem> allAttrItems;
        public ObservableCollection<AttrItem> AllAttrItems
        {
            get { if (null == allAttrItems) { allAttrItems = new ObservableCollection<AttrItem>(); } return allAttrItems; }
            set { allAttrItems = value; OnPropertyChanged("AllAttrItems"); }
        }

        private bool isBtnConfirmEnable = false;

        public bool IsBtnConfirmEnable
        {
            get { return isBtnConfirmEnable; }
            set { isBtnConfirmEnable = value; OnPropertyChanged(nameof(IsBtnConfirmEnable)); }
        }

        public event EventHandler<ModelDoc2> DataPassed;

        public NewDocWindow(IntPtr windowHandle, ISldWorks sw) : this(sw)
        {
            InitializeComponent();
            // 设置父窗口
            var interopHelper = new WindowInteropHelper(this);
            interopHelper.Owner = windowHandle;
        }

        public NewDocWindow(ISldWorks sw, NewDocWindowMode mode = NewDocWindowMode.Normal)
        {
            InitializeComponent();
            this.DataContext = this;
            this.Sw = sw;
            this.iFrame = Sw.Frame() as IFrame;

            if (!string.IsNullOrEmpty(SessionContext.Puid) && !string.IsNullOrEmpty(SessionContext.ObjectType)) // 集成新建
            {
                passedUid = SessionContext.Puid;
                passedObjectType = SessionContext.ObjectType;
                SessionContext.Puid = null;
                SessionContext.ObjectType = null;
                // 零部件对象(非版本)类型
                string typeWithoutRevision = passedObjectType.Substring(0, passedObjectType.Length - REVISION_SUFFIX.Length);
                if (!ConfigParser.SwItemTypeMap.Ens2CN.ContainsKey(typeWithoutRevision))
                {
                    MessageBox.Show("未匹配的对象类型: " + passedObjectType);
                    return;
                }

                isIntegrationLaunch = true;

                foreach (SWTemplate i in ConfigParser.SwTemplates)
                {
                    if (!i.ItemTypes.Contains(typeWithoutRevision))
                        continue;

                    if (NewDocWindowMode.NoDrawingTemplate == mode && i.DatasetType == DATASET_DRW_MASTER) // 特定场景下，过滤图纸模板
                        continue;

                    var templateItem = new TemplateItem
                    {
                        Name = i.Name,
                        DatasetType = i.DatasetType,
                        Unit = "毫米",
                        Preview = i.Preview,
                        Suffix = i.Suffix,
                        ItemTypes = i.ItemTypes,
                        Filename = i.Filename
                    };
                    TemplateItems.Add(templateItem);
                }

                comboBoxItemType.ItemsSource = new List<string> { ConfigParser.SwItemTypeMap.Ens2CN[typeWithoutRevision] };
                comboBoxItemType.SelectedIndex = 0;
                comboBoxItemType.IsEnabled = false;
                dataGridInfo.IsEnabled = false;
                expFolder.Visibility = Visibility.Collapsed;
                // 订阅Loaded事件
                this.Loaded += MainWindow_Loaded;
            }
            else
            {
                foreach (SWTemplate i in ConfigParser.SwTemplates)
                {
                    if (NewDocWindowMode.NoDrawingTemplate == mode && i.DatasetType == DATASET_DRW_MASTER) // 特定场景下，过滤图纸模板
                        continue;

                    var templateItem = new TemplateItem
                    {
                        Name = i.Name,
                        DatasetType = i.DatasetType,
                        Unit = "毫米",
                        Preview = i.Preview,
                        Suffix = i.Suffix,
                        ItemTypes = i.ItemTypes,
                        Filename = i.Filename
                    };
                    TemplateItems.Add(templateItem);
                }
            }
            dataGridTemplate.SelectedIndex = 0; // 默认选择零件
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 在Loaded事件处理程序中执行异步初始化
            await integrationInitializeAsync();
        }

        private async Task integrationInitializeAsync()
        {
            // 异步加载数据
            AttributesResponse res = (AttributesResponse)await HttpRequest.queryItemAttributes(passedUid, passedObjectType);
            if (res.isSuccess)
            {
                foreach(var i in RequiredAttrItems)
                    i.Value = res.attributes.ContainsKey(i.AttrId) ? res.attributes[i.AttrId] : "";
                foreach (var i in AllAttrItems)
                    i.Value = res.attributes.ContainsKey(i.AttrId) ? res.attributes[i.AttrId] : "";
            }
            IsBtnConfirmEnable = true;
        }

        /// <summary>
        /// 更新确定按钮的状态
        /// </summary>
        private void updateBtnConfrimStatus()
        {
            if(RequiredAttrItems.Count == 0)
            {
                IsBtnConfirmEnable = false;
                return;
            }

            foreach (var i in RequiredAttrItems)
            {
                if (string.IsNullOrEmpty(i.Value))
                {
                    IsBtnConfirmEnable = false;
                    return;
                }
            }

            IsBtnConfirmEnable=true;
        }

        private void DataGridTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var currentSelected = dataGridTemplate.SelectedItem as TemplateItem;
                if (null == currentSelected)
                    return;

                // 集成新建下无需更新项类型下拉框
                if(!isIntegrationLaunch)
                {
                    // 项类型下拉框
                    List<string> itemTypes_CN = new List<string>();
                    foreach (var i in currentSelected.ItemTypes)
                        itemTypes_CN.Add(ConfigParser.SwItemTypeMap.Ens2CN[i]);
                    comboBoxItemType.ItemsSource = itemTypes_CN;
                    comboBoxItemType.SelectedIndex = 0;
                }

                // 预览图
                string previewUrl = ConfigParser.AddInHomePath + @"\Resources\" + currentSelected.Preview;
                imgPreview.Source = new BitmapImage(new Uri(previewUrl));

                btnBrowse.IsEnabled = DATASET_DRW_MASTER == currentSelected.DatasetType;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ComboBoxItemType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var itemType_CN = comboBoxItemType.SelectedValue as string;
            if (itemType_CN == null)
                return;

            var itemType = ConfigParser.SwItemTypeMap.CN2Ens[itemType_CN];
            List<SwAttr> swAttrs;
            ConfigParser.SwAttrs.TryGetValue(itemType, out swAttrs);
            RequiredAttrItems.Clear();
            AllAttrItems.Clear();

            if (null != swAttrs)
            {
                foreach (SwAttr i in swAttrs)
                {
                    var attrItem = new AttrItem
                    {
                        AttrId = i.AttrId,
                        Description = i.Description,
                        Value = "",
                        IsReadOnly = i.ReadOnly,
                        IsRequired = i.Required,
                        Mode = i.Mode
                    };

                    if (i.Required)
                        RequiredAttrItems.Add(attrItem);
                    else
                        AllAttrItems.Add(attrItem);
                }
            }
            updateBtnConfrimStatus();
        }

        private void dataGridInfo_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var dataItem = e.Row.Item as AttrItem;
            if (dataItem == null || dataItem.IsReadOnly)
            {
                e.Cancel = true;
            }
        }

        private void FolderWindow_DataPassed(object sender, KeyValuePair<string, string> data)
        {
            // 处理从子窗口传递过来的数据
            this.folderId = data.Key;
            this.folderType = data.Value;
        }

        private void AttrWindow_DataPassed(object sender, ObservableCollection<AttrItem> data)
        {
            // 处理从子窗口传递过来的数据
            AllAttrItems = data;

            // 同步新建页面的必填属性
            foreach(var i in RequiredAttrItems)
            {
                if(!i.IsReadOnly)
                {
                    foreach(var j in AllAttrItems)
                    {
                        if(j.AttrId == i.AttrId)
                            i.Value = j.Value;
                    }
                }
            }
        }

        private async void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 新建本地文档
                var currentSelected = dataGridTemplate.SelectedItem as TemplateItem;
                var templatePath = ConfigParser.AddInHomePath + @"\Templates\" + currentSelected.Filename;
                if (!File.Exists(templatePath))
                    throw new Exception("未找到模板");

                var doc = Sw.INewDocument2(templatePath, 0, 300d, 300d);

                // 设置文档属性
                Dictionary<string, string> attrs = new Dictionary<string, string>();
                ModelDocExtension docExten = doc.Extension;
                CustomPropertyManager propMgr = docExten.CustomPropertyManager[""];
                // 必填属性
                foreach (AttrItem i in RequiredAttrItems)
                {
                    propMgr.Add3(i.Description, 30, i.Value, 2);
                    attrs[i.AttrId] = i.Value;
                }
                // 非必填属性
                foreach(AttrItem i in AllAttrItems)
                {
                    if(!i.IsRequired)
                    {
                        propMgr.Add3(i.Description, 30, i.Value, 2);
                        attrs[i.AttrId] = i.Value;
                    }
                }

                var itemType_CN = comboBoxItemType.SelectedValue as string;
                var itemType = ConfigParser.SwItemTypeMap.CN2Ens[itemType_CN];
                if (!isIntegrationLaunch) // 非集成新建
                {
                    // 创建零部件对象
                    var baseRes = await HttpRequest.createItem(itemType, attrs, folderId, folderType);
                    CreateItemResponse res = (CreateItemResponse)baseRes;
                    if (res.isSuccess)
                    {
                        // 零部件uid反写至属性
                        propMgr.Add3(PUID, 30, res.revUid, 2); // 零部件版本对象uid
                        propMgr.Add3(ITEMPUID, 30, res.uid, 2); // 零部件对象uid
                        propMgr.Add3(ITEM_TYPE_CHINESE, 30, itemType_CN, 2); // 项类型

                        // 签出零部件版本对象
                        baseRes = await HttpRequest.checkOut(res.revUid, itemType + REVISION_SUFFIX);
                        BoolResponse boolRes = (BoolResponse)baseRes;
                        //if (!boolRes.isSuccess)
                        //    throw new Exception("零部件签出失败：" + boolRes.mesg);
                    }
                    else
                        throw new Exception("零部件创建失败：" + res.mesg);
                }
                else // 集成新建
                {
                    // 零部件uid反写至属性
                    propMgr.Add3(PUID, 30, passedUid, 2); // 零部件版本对象uid
                    propMgr.Add3(ITEMPUID, 30, "", 2); // 零部件对象uid
                    propMgr.Add3(ITEM_TYPE_CHINESE, 30, itemType_CN, 2); // 项类型

                    // 签出零部件版本对象
                    var baseRes = await HttpRequest.checkOut(passedUid, passedObjectType);
                    BoolResponse boolRes = (BoolResponse)baseRes;
                    //if (!boolRes.isSuccess)
                    //    throw new Exception("零部件签出失败：" + boolRes.mesg);
                }

                // 另存(根据零部件编码规则修改数模名称)
                var currentSelectedTemplate = dataGridTemplate.SelectedItem as TemplateItem;
                int errors = 0;
                int warnings = 0;
                string fileName = SW_CACHE_PATH + attrs[ITEM_ID] + "_" + attrs[ITEM_REVISION_ID] + "_" + attrs[OBJECT_NAME] + "." + currentSelectedTemplate.Suffix;
                bool status = docExten.SaveAs(fileName, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);

                IFrame iFrame = Sw.Frame() as IFrame;
                iFrame.SetStatusBarText("创建成功: " + attrs[ITEM_ID] + "_" + attrs[ITEM_REVISION_ID]);
                this.DialogResult = true;
                this.Close();

                // 图纸
                //ModelDoc2 refModelDoc = Sw.OpenDoc6("C:\\Users\\chen.jianghu1\\Desktop\\零件1.SLDPRT", (int)swDocumentTypes_e.swDocDRAWING, 
                //    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
                //var drawingDoc = (DrawingDoc)doc;
                //double[] view1pos = new double[] { 120 / 1000.0, 100 / 1000.0 };
                //View view1 = drawingDoc.CreateDrawViewFromModelView3("C:\\Users\\chen.jianghu1\\Desktop\\零件1.SLDPRT", "*上视", view1pos[0], view1pos[1], 0);

                // 触发事件，给调用者发送消息
                DataPassed?.Invoke(this, doc);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnAttr_Click(object sender, RoutedEventArgs e)
        {
            var itemType_CN = comboBoxItemType.SelectedValue as string;
            var itemType = ConfigParser.SwItemTypeMap.CN2Ens[itemType_CN];
            List<SwAttr> attrList;
            ConfigParser.SwAttrs.TryGetValue(itemType, out attrList);
            ObservableCollection<AttrItem> attrItems = new ObservableCollection<AttrItem>(RequiredAttrItems);
            if (null != attrList)
            {
                foreach (SwAttr i in attrList)
                {
                    if (!i.Required)
                    {
                        var attrItem = new AttrItem
                        {
                            AttrId = i.AttrId,
                            Description = i.Description,
                            Value = "",
                            IsReadOnly = i.ReadOnly,
                            IsRequired = i.Required,
                            Mode = i.Mode
                        };
                        attrItems.Add(attrItem);
                    }
                }
            }

            var attrWindow = new AttrWindow(attrItems);
            attrWindow.Owner = this;
            attrWindow.DataPassed += AttrWindow_DataPassed; // 注册事件
            attrWindow.ShowDialog();
        }

        private void dataGridInfo_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 获取点击位置
            var position = Mouse.GetPosition(dataGridInfo);

            // 进行命中测试，检查是否点击在行上
            var hitTestResult = VisualTreeHelper.HitTest(dataGridInfo, position);

            if (hitTestResult != null)
            {
                // 查找是否命中了DataGridRow
                var row = FindVisualParent<DataGridRow>(hitTestResult.VisualHit);

                if (row != null)
                {
                    // 如果点击在行上，取消显示菜单
                    e.Handled = true;
                    return;
                }
            }

            // 点击在空白处，正常显示菜单
        }

        private void dataGridInfo_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            updateBtnConfrimStatus();
        }

        // 辅助方法：查找视觉树父元素
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && !(child is T))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as T;
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var window = new OpenDocWindow(Sw, OpenDocWindowMode.Selection);
            window.DataPassed_Selection += OpenDocWindow_DataPassed;
            window.ShowDialog();
        }

        private async void btnAssign_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is AttrItem i)
                {
                    var itemType = comboBoxItemType.SelectedValue as string;
                    if (i.AttrId == ITEM_ID)
                    {
                        var baseRes = await HttpRequest.assignID("");
                        if (!baseRes.isSuccess)
                            throw new Exception("编码指派失败：" + baseRes.mesg);

                        StringResponse res = (StringResponse)baseRes;
                        i.Value = res.str;
                    }
                    else if (i.AttrId == ITEM_REVISION_ID)
                    {
                        var baseRes = await HttpRequest.assignRev(itemType);
                        if (!baseRes.isSuccess)
                            throw new Exception("版本号指派失败：" + baseRes.mesg);

                        StringResponse res = (StringResponse)baseRes;
                        i.Value = res.str;
                    }
                    btn.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                iFrame.SetStatusBarText(ex.Message);
            }
        }

        private void dataGridInfo_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGrid grid = sender as DataGrid;
            DependencyObject dep = e.OriginalSource as DependencyObject;

            // 遍历可视化树找到DataGridCell
            while (dep != null && !(dep is DataGridCell))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep is DataGridCell cell)
            {
                if (cell.DataContext is AttrItem item)
                {
                    if (!item.IsReadOnly  && !cell.IsEditing)
                    {
                        cell.IsEditing = true;
                        e.Handled = true;
                    }
                }
            }
        }

        private async void OpenDocWindow_DataPassed(object sender, KeyValuePair<string, string> pairUid2ObjectType)
        {
            try
            {
                string uid, objectType;
                uid = pairUid2ObjectType.Key;
                objectType = pairUid2ObjectType.Value;

                // 同步PDM属性
                var attrRes = (AttributesResponse)await HttpRequest.queryItemAttributes(uid, objectType);
                if(!attrRes.isSuccess)
                    throw new Exception("零部件属性查询失败: " + attrRes.mesg);

                string attrVal;
                foreach (var i in AllAttrItems)
                {
                    if(!i.IsReadOnly)
                    {
                        attrRes.attributes.TryGetValue(i.AttrId, out attrVal);
                        i.Value = attrVal;
                    }
                }
                foreach (var i in RequiredAttrItems)
                {
                    attrRes.attributes.TryGetValue(i.AttrId, out attrVal);

                    if (i.AttrId == ITEM_ID)
                        i.Value = attrVal + "_2D";
                    else
                        i.Value = attrVal;
                }

                attrRes.attributes.TryGetValue(ITEM_ID, out attrVal);
                txtRefItemId.Text = attrVal;
                attrRes.attributes.TryGetValue(ITEM_REVISION_ID, out attrVal);
                txtRefItemRevId.Text = attrVal;
                attrRes.attributes.TryGetValue(OBJECT_NAME, out attrVal);
                txtRefObjectName.Text = attrVal;

                dataGridTemplate.IsEnabled = false; // 禁用模板选择
                updateBtnConfrimStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

    }
}