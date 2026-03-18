using SolidWorks.Interop.sldworks;
using System;
using System.IO;
using BplmSw.Common;
using BplmSw.NewDoc;
using BplmSw.OpenDoc;
using SolidWorks.Interop.swconst;
using System.Collections.Generic;
using System.Diagnostics;
using static BplmSw.Constants;
using System.Linq;
using BplmSw.Utils;

namespace BplmSw.Assembly
{
    /// <summary>
    /// 装配体添加组件功能类
    /// </summary>
    public class AddComp
    {
        private ISldWorks Sw;
        private IFrame iFrame;
        private ModelDoc2 activedDoc;

        public AddComp(ISldWorks sw)
        {
            this.Sw = sw;
            this.iFrame = Sw.Frame() as IFrame;
        }

        /// <summary>
        /// 执行添加组件操作
        /// </summary>
        public void Execute()
        {
            try
            {
                activedDoc = (ModelDoc2)Sw.ActiveDoc;
                if (2 != activedDoc.GetType())
                    throw new Exception("非装配体不允许添加组件");

                var window = new OpenDocWindow(Sw, OpenDocWindowMode.Selection);
                window.DataPassed_Selection += OpenDocWindow_DataPassed;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                iFrame.SetStatusBarText("添加组件失败: " + ex.Message);
            }
        }

        private async void OpenDocWindow_DataPassed(object sender, KeyValuePair<string, string> kvp)
        {
            try
            {
                // 查询零部件数据集
                var dsRes = (ItemDataSetResponse)await HttpRequest.getPartAttachmentByPartIdBatch(new Dictionary<string, string> { { kvp.Key, kvp.Value } });
                if (!dsRes.isSuccess)
                    throw new Exception("查询零部件数据集失败: " + dsRes.mesg);

                if (activedDoc.GetTitle().Contains(dsRes.itemDataSets[0].strItemId))
                    throw new Exception("正在尝试搭建循环结构");

                // 过滤出零件、装配类型
                bool isPrtOrAsmExist = false;
                ItemDataSetResponse.DataSet targetDataset = default(ItemDataSetResponse.DataSet);
                foreach (var ds in dsRes.itemDataSets[0].dataSets)
                {
                    if (DATASET_ASM_MASTER == ds.strDatasetType || DATASET_PRT_MASTER == ds.strDatasetType)
                    {
                        targetDataset = ds;
                        isPrtOrAsmExist = true;
                        break;
                    }
                }

                if (!isPrtOrAsmExist)
                {
                    // 转新建窗口
                    SessionContext.Puid = kvp.Key;
                    SessionContext.ObjectType = kvp.Value;
                    var window = new NewDoc.NewDocWindow(Sw, NewDocWindowMode.NoDrawingTemplate);

                    // 注册事件、回调处理
                    window.DataPassed += NewDocWindow_DataPassed;
                    window.ShowDialog();
                    return;
                }

                // 下载
                string fileName = targetDataset.strObjectName;
                string fullPath = Path.Combine(Constants.SW_CACHE_PATH, fileName);
                await MD5Util.OpenDocDownLoadFromPLM(fileName, fullPath, targetDataset);

                // 打开
                int errors = 0;
                int warnings = 0;
                int docType = DATASET_ASM_MASTER == targetDataset.strDatasetType ? (int)swDocumentTypes_e.swDocASSEMBLY :
                    (int)swDocumentTypes_e.swDocPART;
                ModelDoc2 doc = Sw.OpenDoc6(fullPath, docType, (int)swOpenDocOptions_e.swOpenDocOptions_LoadModel, "", ref errors, ref warnings);

                // 添加组件
                AssemblyDoc assemDoc = (AssemblyDoc)activedDoc;
                Component2 insertedComp = assemDoc.AddComponent5(doc.GetPathName(), (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                    "", false, "", 0.0, 0.0, 0.0     
                );
                Sw.CloseDoc(doc.GetTitle());

                // 添加固定约束
                string assemName = activedDoc.GetTitle().Substring(0, activedDoc.GetTitle().LastIndexOf("."));
                activedDoc.ClearSelection2(true);
                activedDoc.Extension.SelectByID2(insertedComp.Name2 + "@" + assemName, "COMPONENT", 0, 0, 0, false, 0, null, 0);
                assemDoc.FixComponent();
            }
            catch (Exception ex)
            {
                iFrame.SetStatusBarText("添加组件过程出错: " + ex.Message);
            }
        }

        private void NewDocWindow_DataPassed(object sender, ModelDoc2 doc)
        {
            try
            {
                // 添加组件
                AssemblyDoc assemDoc = (AssemblyDoc)activedDoc;
                string path = doc.GetPathName();
                Component2 insertedComp = assemDoc.AddComponent5(path, (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                    "", false, "", 0, 0, 0);
                Sw.CloseDoc(doc.GetTitle());

                // 添加固定约束
                string assemName = activedDoc.GetTitle().Substring(0, activedDoc.GetTitle().LastIndexOf("."));
                activedDoc.ClearSelection2(true);
                activedDoc.Extension.SelectByID2(insertedComp.Name2 + "@" + assemName, "COMPONENT", 0, 0, 0, false, 0, null, 0);
                assemDoc.FixComponent();
            }
            catch (Exception ex)
            {
                iFrame.SetStatusBarText(ex.Message);
            }
        }
    }
}