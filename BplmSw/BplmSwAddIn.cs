using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Base.Attributes;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.UI.Commands;
using BplmSw.NewDoc;
using BplmSw.OpenDoc;
using Xarial.XCad.SolidWorks.UI;
using Xarial.XCad.UI;
using System.IO;
using System.Threading;
using System.Net;
using SolidWorks.Interop.sldworks;
using System.Windows.Interop;
using BplmSw.Login;
using Environment = System.Environment;
using BplmSw.Common;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Security.Cryptography;
using static BplmSw.Constants;
using System.Diagnostics;
using System.Windows.Controls;
using System.Runtime.InteropServices.ComTypes;
using SolidWorks.Interop.swconst;
using MessageBox = System.Windows.MessageBox;
using BplmSw.Assembly;
using Xarial.XCad.Documents;
using Xarial.XCad.Base.Enums;
using Xarial.XCad.Data;
using Xarial.XCad.UI.Commands.Structures;
using BplmSw.Utils;

namespace BplmSw
{
    [ComVisible(true)]
    [Title("在线设计")]
    [Guid("1D0270F5-4BE9-4199-A4EE-7C9B0F5634AE")]
    public class BplmSwAddIn : SwAddInEx
    {
        private ISwTaskPane<TaskPanelControl> iSwTaskPane;
        private TaskPanelControl taskPanelControl;
        private TaskPanelVM _taskPanelVM;
        private FileSystemWatcher _watcher;
        private SynchronizationContext _uiContext;
        // 本地状态缓存
        private bool _canCheckIn = false;
        private bool _canCheckOut = false;
        //全局单例
        public static BplmSwAddIn Instance { get; private set; }
        public override void OnConnect()
        {
            Instance = this;
            NewDocServiceLocator.Instance = new NewDocService();
            this.Application.Documents.RegisterHandler<MyDocumentHandler>();

            // 捕获当前的 UI 线程上下文
            _uiContext = SynchronizationContext.Current;

            // 如果上面是 null 创建一个默认的 WinForms 上下文
            if (_uiContext == null) _uiContext = new System.Windows.Forms.WindowsFormsSynchronizationContext();

            //工具栏命令
            var _cmdGroupTyped = this.CommandManager.AddCommandGroup<SwCommands>();
            _cmdGroupTyped.CommandClick += BplmSwAddIn_CommandClick;
            // 动态刷新按钮状态
            _cmdGroupTyped.CommandStateResolve += OnCommandStateResolve;
            this.Application.Documents.DocumentOpened += OnDocumentOpened;
            this.Application.Documents.DocumentActivated += OnDocumentActivated;
            //右键菜单命令
            this.CommandManager.AddContextMenu<MyFileContextMenu_e>(SelectType_e.Components).CommandClick += OnMyFileContextMenuClick;
            InitialTaskPane();

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            TokenManager.LoadToken();
            //url唤醒（未启动
            if (SessionContext.IsWeb == "1")
                CheckTokenAndLoginAsync();
            //url唤醒(已启动，置顶
            StartFileWatcher();
        }
        private void OnDocumentOpened(IXDocument doc)
        {
            UpdateCommandStateAsync(doc);
        }

        private void OnDocumentActivated(IXDocument doc)
        {
            UpdateCommandStateAsync(doc);
        }
        public async Task UpdateCommandStateAsync(IXDocument doc)
        {
            // 如果没有打开文档，或者为空白未保存的文档，默认置灰
            if (doc == null || string.IsNullOrEmpty(doc.Path))
            {
                _canCheckIn = false;
                _canCheckOut = false;
                return;
            }
            var (uid, objectType) = GetIdTypeFromDoc();
            CheckInStatusResponse response = await HttpRequest.getCheckInStatus(uid, objectType);
            CheckStatusResponse(response);
        }
        // 控制命令的激活和置灰
        private void OnCommandStateResolve(SwCommands cmd, CommandState state)
        {
            if (cmd == SwCommands.CheckInDoc) state.Enabled = _canCheckIn;
            else if (cmd == SwCommands.CheckOutDoc) state.Enabled = _canCheckOut;
        }
        //右键菜单
        private async void OnMyFileContextMenuClick(MyFileContextMenu_e cmd)
        {
            switch (cmd)
            {
                case MyFileContextMenu_e.checkout:
                    await HandleCheckOutDoc();
                    break;

                case MyFileContextMenu_e.checkin:
                    await HandleCheckInDoc();
                    break;
            }
        }
        public override void OnDisconnect()
        {
            //销毁文件监听
            if (_watcher != null)
            {
                try
                {
                    //防止在销毁过程中又进来一个新的事件
                    _watcher.EnableRaisingEvents = false;
                    //归还文件句柄给操作系统
                    _watcher.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("销毁 Watcher 异常: " + ex.Message);
                }
                finally
                {
                    _watcher = null;
                }
            }
            //关闭sw后isWeb置0
            TokenManager.SaveToken("");
        }

        private async Task CheckTokenAndLoginAsync()
        {
            try
            {
                TokenManager.LoadToken();
                if (!string.IsNullOrEmpty(SessionContext.Token))
                {
                    var res = await HttpRequest.isLogin(SessionContext.Token);
                    if (res.val)
                    {
                        await PerformLoginSuccess();
                    }
                    else
                    {
                        UpdateStatusBar($"当前工号: Unknown");
                        MessageBox.Show("自动登录失败，请重新登录，" + Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    }
                }
                if (!string.IsNullOrEmpty(SessionContext.Puid) && !string.IsNullOrEmpty(SessionContext.ObjectType))
                {
                    if (FOLDER_ITEMS.Contains(SessionContext.ObjectType))
                    {
                        SessionContext.Puid = null;
                        SessionContext.ObjectType = null;
                        return;
                    }

                    // 查询零部件数据集，（附件）存在走集成打开场景，不存在走集成新建场景
                    Dictionary<string, string> mapUid2Type = new Dictionary<string, string> { { SessionContext.Puid, SessionContext.ObjectType } };
                    var baseRes = await HttpRequest.getPartAttachmentByPartIdBatch(mapUid2Type);
                    if (!baseRes.isSuccess)
                        throw new Exception("查询零部件数据集失败: " + baseRes.mesg);

                    ItemDataSetResponse queryDatasetRes = (ItemDataSetResponse)baseRes;
                    bool isPartExist = false;
                    ItemDataSetResponse.DataSet targetDataset = default(ItemDataSetResponse.DataSet);
                    ItemDataSetResponse.ItemDataSet targetItemDataset = queryDatasetRes.itemDataSets[0];
                    foreach (var ds in targetItemDataset.dataSets)
                    {
                        if (DATASET_ASM_MASTER == ds.strDatasetType)
                        {
                            targetDataset = ds;
                            isPartExist = true;
                            break;
                        }
                        else if (DATASET_PRT_MASTER == ds.strDatasetType)
                        {
                            targetDataset = ds;
                            isPartExist = true;
                            break;
                        }
                        else if (DATASET_DRW_MASTER == ds.strDatasetType)
                        {
                            targetDataset = ds;
                            isPartExist = true;
                            break;
                        }
                    }
                    if (!isPartExist)
                        await HandleNewDoc();
                    else
                    {
                        SessionContext.Puid = null;
                        SessionContext.ObjectType = null;
                        // 下载
                        string fileName = targetDataset.strObjectName;
                        string fullPath = Path.Combine(Constants.SW_CACHE_PATH, fileName);
                        await MD5Util.OpenDocDownLoadFromPLM(fileName, fullPath, targetDataset);
                        // 打开
                        await OpenAndSyncAttr(fullPath, targetItemDataset);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
            }
        }

        private async Task OpenAndSyncAttr(string fullPath, ItemDataSetResponse.ItemDataSet targetItemDataset)
        {
            try
            {
                int errors = 0;
                int warnings = 0;
                int docType = (int)swDocumentTypes_e.swDocPART;
                string fileExt = Path.GetExtension(fullPath).ToLower();
                if (fileExt == ".sldasm")
                {
                    docType = (int)swDocumentTypes_e.swDocASSEMBLY;
                }
                else if (fileExt == ".slddrw")
                {
                    docType = (int)swDocumentTypes_e.swDocDRAWING;
                }

                var doc = this.Application.Sw.OpenDoc6(fullPath, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
                if (doc != null)
                {
                    // 同步属性
                    AttributesResponse attrRes = (AttributesResponse)await HttpRequest.queryItemAttributes(targetItemDataset.strPuid, targetItemDataset.strType);
                    if (attrRes.isSuccess && attrRes.attributes != null)
                    {
                        ModelDoc2 swDoc = (ModelDoc2)doc;
                        ModelDocExtension docExten = swDoc.Extension;
                        CustomPropertyManager propMgr = docExten.CustomPropertyManager[""];

                        // 根据属性配置进行属性同步
                        string objectType = targetItemDataset.strType.Substring(0, targetItemDataset.strType.Length - REVISION_SUFFIX.Length);
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

                    //// 签出
                    //var baseRes = await HttpRequest.checkOut(targetItemDataset.strPuid, targetItemDataset.strType);
                    //if (!baseRes.isSuccess)
                    //    throw new Exception("签出失败: " + baseRes.mesg);
                }
            }
            catch (Exception openEx)
            {
                MessageBox.Show("打开文件失败: " + openEx.Message + Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
            }
        }

        private async Task PerformLoginSuccess()
        {
            try
            {
                PersonalInfoResponse userInfo = (PersonalInfoResponse)await HttpRequest.getPersonalInfo();

                if (userInfo.isSuccess && userInfo.puserId != null)
                {
                    SessionContext.UserId = userInfo.puserId;
                    SessionContext.UserName = userInfo.puserName;
                    UpdateStatusBar($"当前工号: {SessionContext.UserId} ({SessionContext.UserName})");
                }
                else
                {
                    UpdateStatusBar($"当前工号: Unknown");
                    MessageBox.Show("自动登录失败，请重新登录" + Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetPersonalInfo Error: " + ex.Message);
            }
        }

        private void UpdateStatusBar(string message)
        {
            try
            {
                // 获取原生 ISldWorks 对象来设置状态栏
                var swApp = this.Application.Sw;
                IFrame f = swApp.Frame() as IFrame;
                f.SetStatusBarText(message);
            }
            catch (Exception)
            {
            }
        }

        private void InitialTaskPane()
        {
            iSwTaskPane = this.CreateTaskPaneWpf<TaskPanelControl>();
            taskPanelControl = iSwTaskPane.Control;
            _taskPanelVM = new TaskPanelVM(this);
            taskPanelControl.DataContext = _taskPanelVM;
        }

        private async void BplmSwAddIn_CommandClick(SwCommands spec)
        {
            switch (spec)
            {
                case SwCommands.Login:
                    await HandleManualLoginAsync();
                    break;
                case SwCommands.Logout:
                    HandleLogout();
                    break;
                case SwCommands.NewDoc:
                    await HandleNewDoc();
                    break;
                case SwCommands.Save:
                    HandleSave();
                    break;
                case SwCommands.SaveOption:
                    SaveOptionWindow window = new SaveOptionWindow();
                    window.ShowDialog();
                    break;
                case SwCommands.OpenDoc:
                    await HandleOpenDoc();
                    break;
                case SwCommands.AddComp:
                    HandleAddComp();
                    break;
                case SwCommands.NewComp:
                    HandleNewComp();
                    break;
                case SwCommands.ReplaceComp:
                    HandleReplaceComp();
                    break;
                case SwCommands.CheckOutDoc:
                    await HandleCheckOutDoc();
                    break;
                case SwCommands.CheckInDoc:
                    await HandleCheckInDoc();
                    break;
            }
        }
        
        public async Task HandleCheckOutDoc()
        {
            var (uid, objectTypeRev) = GetIdTypeFromDoc();
            var resOut = await HttpRequest.checkOut(uid, objectTypeRev);
            if (resOut.isSuccess) MessageBox.Show("签出成功！");
            else MessageBox.Show($"签出失败: {resOut.mesg}");
            // 如果没有打开文档，或者为空白未保存的文档，默认置灰
            var doc = this.Application.Documents.Active;
            if (doc == null || string.IsNullOrEmpty(doc.Path))
            {
                _canCheckIn = false;
                _canCheckOut = false;
                return;
            }
            CheckInStatusResponse response = await HttpRequest.getCheckInStatus(uid, objectTypeRev);
            CheckStatusResponse(response);
        }
        public async Task HandleCheckInDoc()
        {
            var (uid, objectTypeRev) = GetIdTypeFromDoc();
            var resIn = await HttpRequest.checkIn(uid, objectTypeRev);
            if (resIn.isSuccess) MessageBox.Show("签入成功！");
            else MessageBox.Show($"签入失败: {resIn.mesg}");
            // 如果没有打开文档，或者为空白未保存的文档，默认置灰
            var doc = this.Application.Documents.Active;
            if (doc == null || string.IsNullOrEmpty(doc.Path))
            {
                _canCheckIn = false;
                _canCheckOut = false;
                return;
            }
            CheckInStatusResponse response = await HttpRequest.getCheckInStatus(uid, objectTypeRev);
            CheckStatusResponse(response);
        }
        private void CheckStatusResponse(CheckInStatusResponse response)//根据文件的签入状态修改内存中的布尔值
        {
            if (response != null && response.isSuccess && response.data != null && response.data.Count > 0)
            {
                var statusData = response.data[0];
                if (statusData.checkIn.status == "1")
                {
                    _canCheckIn = false;
                    _canCheckOut = true;
                }
                else if (statusData.checkIn.status == "0")
                {
                    _canCheckIn = true;
                    _canCheckOut = false;
                }
            }
            else
            {
                _canCheckIn = false;
                _canCheckOut = false;
            }

            // 同步状态到TaskPane
            _taskPanelVM?.UpdateButtonState(_canCheckIn, _canCheckOut);
        }
        private (string uid, string objectTypeRev) GetIdTypeFromDoc()
        {
            // 获取当前活动文档
            IXDocument model = this.Application.Documents.Active;

            if (model == null)
            {
                MessageBox.Show("当前没有活动文档！");
                return (null, null);
            }
            try
            {
                // 获取 UID 和 ObjectTypeRev
                string uid = model.Properties.GetOrPreCreate(PUID).Value?.ToString();
                string objectType_CN = model.Properties.GetOrPreCreate(ITEM_TYPE_CHINESE).Value?.ToString();

                // 校验
                if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrEmpty(objectType_CN))
                {
                    MessageBox.Show($"缺少必要属性：{PUID} 或 {ITEM_TYPE_CHINESE}");
                    return (null, null);
                }
                // 转换
                if (!ConfigParser.SwItemTypeMap.CN2Ens.ContainsKey(objectType_CN))
                {
                    MessageBox.Show($"未知的中文类型: {objectType_CN}");
                    return (null, null);
                }
                string objectTypeRev = ConfigParser.SwItemTypeMap.CN2Ens[objectType_CN] + REVISION_SUFFIX;
                return (uid, objectTypeRev);
            }
            catch (Exception ex)
            {
                this.Application.ShowMessageBox($"操作异常: {ex.Message}");
                return (null, null);
            }
        }

        private async void HandleReplaceComp()
        {
            try
            {
                if (!await IsTokenValidAsync())
                {
                    await ShowLoginWindowAsync();
                    return;

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
            }
            var window = new ReplaceCompWindow(Application.WindowHandle, Application.Sw);
            window.Show();
        }

        private async void HandleNewComp()
        {
            try
            {
                if (!await IsTokenValidAsync())
                {
                    await ShowLoginWindowAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
            }
            NewComp newComp = new NewComp(Application.Sw);
            newComp.execute();
        }

        private async void HandleAddComp()
        {
            try
            {
                if (!await IsTokenValidAsync())
                {
                    await ShowLoginWindowAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
            }
            AddComp addComp = new AddComp(Application.Sw);
            addComp.Execute();
        }

        private async void HandleSave()
        {
            try
            {
                if (!await IsTokenValidAsync())
                {
                    await ShowLoginWindowAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
            }
            SaveDoc saveDoc = new SaveDoc(Application.Sw);
            saveDoc.excute();
            await UpdateCommandStateAsync(this.Application.Documents.Active);
        }

        public async Task HandleManualLoginAsync()
        {
            try
            {
                if (await IsTokenValidAsync())
                {
                    MessageBox.Show($"{SessionContext.UserName} 已登录，请勿重复登录");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
            }

            await ShowLoginWindowAsync();
        }
        public void HandleLogout()
        {
            TokenManager.ClearToken();
            SessionContext.Token = null;
            SessionContext.Puid = null;
            SessionContext.ObjectType = null;
            SessionContext.IsWeb = null;
            SessionContext.UserId = null;
            SessionContext.UserName = null;
            MessageBox.Show("已注销");
            UpdateStatusBar("未登录");
        }

        public async Task HandleNewDoc()
        {
            try
            {
                if (!await IsTokenValidAsync())
                {
                    await ShowLoginWindowAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
            }
            ShowNewDocWindow();
            UpdateCommandStateAsync(this.Application.Documents.Active);
        }
        public async Task HandleOpenDoc()
        {
            try
            {
                if (!await IsTokenValidAsync())
                {
                    await ShowLoginWindowAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
            }
            ShowOpenDocWindow();
        }
        private async Task<bool> IsTokenValidAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SessionContext.Token))
                {
                    return false;
                }
                var res = await HttpRequest.isLogin(SessionContext.Token);
                if (res == null)
                {
                    return false;
                }
                return res.val;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token验证发生异常: {ex.Message}");
                return false;
            }
        }


        private async Task ShowLoginWindowAsync()
        {
            try
            {
                var loginWin = new LoginWindow();
                var result = loginWin.ShowDialog();
                var res = await HttpRequest.isLogin(SessionContext.Token);
                if (res.val)
                {
                    await PerformLoginSuccess();
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("UI Error: " + ex.Message);
            }
        }
        private void ShowNewDocWindow()
        {
            var window = new NewDocWindow(Application.WindowHandle, Application.Sw);
            window.ShowDialog();
        }
        private void ShowOpenDocWindow()
        {
            var window = new OpenDocWindow(Application.Sw, OpenDocWindowMode.Open);
            window.Show();
        }
        // 文件监听与自动登录 (场景 1 )
        private void StartFileWatcher()
        {
            try
            {
                string tokenPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BplmSwLauncher",
                    "LoginArgs.json"
                );
                string dir = Path.GetDirectoryName(tokenPath);

                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                _watcher = new FileSystemWatcher(dir, "LoginArgs.json");
                _watcher.NotifyFilter = NotifyFilters.LastWrite;

                Debug.WriteLine($"开始监听LoginArgs.json");

                _watcher.Changed += (s, e) =>
                {
                    _watcher.EnableRaisingEvents = false;

                    Task.Run(async () =>
                    {
                        try
                        {
                            Debug.WriteLine($"监听到LoginArgs.json被修改");
                            int maxRetries = 10;
                            int delay = 100;
                            bool isReady = false;
                            for (int i = 0; i < maxRetries; i++)
                            {
                                try
                                {
                                    using (FileStream fs = File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
                                    {
                                        isReady = true;
                                        break;
                                    }
                                }
                                catch (IOException)
                                {
                                    await Task.Delay(delay);
                                }
                            }

                            if (isReady)
                            {
                                await CheckTokenAndLoginAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("StartFileWatcher processing error: " + ex.Message);
                        }
                        finally
                        {
                            //确保监听器恢复工作。
                            if (_watcher != null)
                            {
                                // await Task.Delay(100); 
                                _watcher.EnableRaisingEvents = true;
                            }
                        }
                    });
                };
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Watcher Error: " + ex.Message);
            }
        }
    }
}