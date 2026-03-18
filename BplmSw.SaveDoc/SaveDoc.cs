using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Shapes;
using static BplmSw.Constants;
using static BplmSw.HttpRequest;
using BplmSw.Utils;
using Path = System.IO.Path;
using System.Security.Cryptography;

namespace BplmSw
{

    public class SaveDoc
    {
        private ISldWorks Sw;
        private IFrame iFrame;
        private string saveOpFilePath = ConfigParser.AddInHomePath + "\\config\\SaveOption.json";
        public bool SaveLightData { get; set; }
        public bool SavePDF { get; set; }

        private void loadOption()
        {
            if (!File.Exists(saveOpFilePath)) return;

            try
            {
                using (var fs = new FileStream(saveOpFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    var json = sr.ReadToEnd();
                    var jObj = JObject.Parse(json);

                    SaveLightData = (bool)jObj["SaveLightData"];
                    SavePDF = (bool)jObj["SavePDF"];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public SaveDoc(ISldWorks Sw)
        {
            this.Sw = Sw;
            iFrame = Sw.Frame() as IFrame;
            loadOption();
        }

        public async void excute()
        {
            try
            {
                ModelDoc2 doc = (ModelDoc2)Sw.ActiveDoc;
                ModelDocExtension docExten = doc.Extension;
                CustomPropertyManager propMgr = docExten.CustomPropertyManager[""];

                int docType = doc.GetType();
                string datasetType = DATASET_PRT_MASTER;
                string suffix = ".sldprt";
                switch (docType)
                {
                    case 1:
                        datasetType = DATASET_PRT_MASTER;
                        suffix = ".sldprt";
                        break;
                    case 2:
                        datasetType = DATASET_ASM_MASTER;
                        suffix = ".sldasm";
                        break;
                    case 3:
                        datasetType = DATASET_DRW_MASTER;
                        suffix = ".slddrw";
                        break;
                    default:
                        break;
                }

                // 从文档属性获取零部件信息
                string uid, objectType_CN, objectType_Ens;
                string resolvedVal;
                bool flag;
                propMgr.Get5(ITEM_TYPE_CHINESE, true, out objectType_CN, out resolvedVal, out flag);
                propMgr.Get5(PUID, true, out uid, out resolvedVal, out flag);
                objectType_Ens = ConfigParser.SwItemTypeMap.CN2Ens[objectType_CN] + REVISION_SUFFIX; // 零部件版本对象类型

                // 另存（处于内存里的数据不能用于上传，所以另存出一份新的用于上传）
                int errors = 0;
                int warnings = 0;
                string filePath = doc.GetPathName();
                string newPath = Path.Combine(Path.GetDirectoryName(filePath), DateTime.Now.ToString("yyyy-MM-dd"));
                if (!Directory.Exists(newPath))
                    Directory.CreateDirectory(newPath);

                // 数模/图纸
                string newFilePath = Path.Combine(newPath, Path.GetFileName(filePath));
                newFilePath = Path.ChangeExtension(newFilePath, suffix);
                doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
                bool status = docExten.SaveAs(newFilePath, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Copy, null, ref errors, ref warnings);

                // 缩略图
                string previewPath = Path.ChangeExtension(filePath, ".png");
                status = docExten.SaveAs(previewPath, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Copy, null, ref errors, ref warnings);

                // PDF
                string pdfPath = "";
                if (docType == 3 && SavePDF)
                {
                    pdfPath = Path.ChangeExtension(filePath, ".pdf");
                    status = docExten.SaveAs(pdfPath, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Copy, null, ref errors, ref warnings);
                }

                // 查询零部件数据集
                Dictionary<string, string> mapUid2Type = new Dictionary<string, string> { { uid, objectType_Ens } };
                var baseRes = await HttpRequest.getPartAttachmentByPartIdBatch(mapUid2Type);
                if(!baseRes.isSuccess)
                    throw new Exception("查询零部件数据集失败: " + baseRes.mesg);
                ItemDataSetResponse queryDatasetRes = (ItemDataSetResponse)baseRes;

                string formatted = DateTime.Now.ToString("yyyy/MM/dd/SW/HH:mm:ss/");
                OssUtil ossUtil = new OssUtil();
                List<Tuple<string, string, string>> datasetInfos = new List<Tuple<string, string, string>>
                { 
                    new Tuple<string, string, string>(datasetType, suffix, newFilePath),
                    new Tuple<string, string, string>(DATASET_IMAGE, ".png", previewPath)
                };
                if (docType == 3 && SavePDF)
                    datasetInfos.Add(new Tuple<string, string, string>(DATASET_PDF, ".pdf", pdfPath));
                var snowflakeIdWorker = new SnowflakeIdWorker(1);
                List<UpdateDatasetsParams> listUpdateDatasetsParams = new List<UpdateDatasetsParams>();
                foreach (var tuple in datasetInfos)
                {
                    string localMd5 = "", remoteMd5 = "";
                    bool isDatasetExist = false;
                    string preFileId = "", datasetUid = "";
                    long fileSize = 0;
                    using (var stream = File.OpenRead(tuple.Item3))
                    {
                        localMd5 = MD5Util.ComputeMD5FromStream(stream);
                        fileSize = stream.Length;
                    }
                    foreach (var i in queryDatasetRes.itemDataSets[0].dataSets)
                        if (i.strDatasetType == tuple.Item1)
                        {
                            remoteMd5 = i.strMd5;
                            preFileId = i.strFileId;
                            datasetType = i.strPuid;
                            isDatasetExist = true;
                        }

                    if (!isDatasetExist || localMd5 != remoteMd5)
                    {
                        UpdateDatasetsParams updatedDatasetParams = new UpdateDatasetsParams
                        {
                            type = objectType_Ens,
                            uid = uid,
                            datasetType = tuple.Item1,
                            file_name = Path.GetFileName(tuple.Item3),
                            file_size = fileSize.ToString(),
                            file_ext = tuple.Item2,
                            md5 = localMd5,
                            pre_file_id = preFileId,
                            datasetUid = datasetUid
                        };

                        SaveFileParameters saveFileParameters = new SaveFileParameters
                        {
                            fileName = Path.GetFileName(tuple.Item3),
                            filePath = formatted + Path.GetFileName(tuple.Item3),
                            fileSize = fileSize.ToString(),
                            fileExt = tuple.Item2,
                            businessId = snowflakeIdWorker.NextId().ToString()
                        };
                        ossUtil.UploadFile(tuple.Item3, saveFileParameters.filePath);
                        baseRes = await HttpRequest.saveFile(saveFileParameters);
                        if (!baseRes.isSuccess)
                            throw new Exception("文件上传失败: " + baseRes.mesg);
                        StringResponse strRes = (StringResponse)baseRes;
                        updatedDatasetParams.file_size = saveFileParameters.fileSize;
                        updatedDatasetParams.file_id = strRes.str;
                        listUpdateDatasetsParams.Add(updatedDatasetParams);
                    }
                }
                baseRes = await HttpRequest.updateDatasets(listUpdateDatasetsParams);
                if (!baseRes.isSuccess)
                    throw new Exception("数据集更新失败: " + baseRes.mesg);

                //// 删除零部件数据集
                //List<string> datasetTypes = new List<string> { datasetType, DATASET_IMAGE };
                //if(docType == 3 && SavePDF)
                //    datasetTypes.Add(DATASET_PDF);
                //foreach (ItemDataSetResponse.DataSet i in queryDatasetRes.itemDataSets[0].dataSets)
                //{
                //    PartAttachmentDeleteBatchParams partAttachmentDeleteBatchParams =
                //            new PartAttachmentDeleteBatchParams
                //            {
                //                uid = uid,
                //                objectType = objectType_Ens,
                //                mapUid2Type =
                //            new Dictionary<string, string>()
                //            };
                //    if (datasetTypes.Contains(i.strDatasetType))
                //        partAttachmentDeleteBatchParams.mapUid2Type[i.strPuid] = i.strType;
                //    baseRes = await partAttachmentDeleteBatch(new List<PartAttachmentDeleteBatchParams> { partAttachmentDeleteBatchParams });
                //    if (!baseRes.isSuccess)
                //        throw new Exception("删除零部件数据集失败: " + baseRes.mesg);
                //}

                //// 文件上传
                //List<string> filePaths = new List<string> { newFilePath, previewPath };
                //if (docType == 3 && SavePDF)
                //{
                //    filePaths.Add(pdfPath);
                //}
                //List<UploadFileResponse> uploadFileResponses = new List<UploadFileResponse>();
                //foreach(var i in filePaths)
                //{
                //    baseRes = await HttpRequest.uploadFile(i);
                //    if (!baseRes.isSuccess)
                //        throw new Exception("文件上传失败: " + baseRes.mesg);
                //    UploadFileResponse fileRes = (UploadFileResponse)baseRes;
                //    uploadFileResponses.Add(fileRes);
                //}

                //// 创建零部件数据集
                //List<PartAttachmentUploadBatchParams> partAttachmentUploadBatchParams = new List<PartAttachmentUploadBatchParams>();
                //for (int i= 0; i<datasetTypes.Count; i++)
                //{
                //    PartAttachmentUploadBatchParams partAttachmentUploadBatchParams1 = new PartAttachmentUploadBatchParams
                //    {
                //        uid = uid,
                //        objectType = objectType_Ens,
                //        datasetType = datasetTypes[i],
                //        fileId = uploadFileResponses[i].strFileId,
                //        fileName = uploadFileResponses[i].strFileName,
                //        fileSize = uploadFileResponses[i].strFileSize,
                //        fileExt = uploadFileResponses[i].strFileExt,
                //        md5 = ""
                //    };
                //    partAttachmentUploadBatchParams.Add(partAttachmentUploadBatchParams1);
                //}

                //baseRes = await HttpRequest.partAttachmentUploadBatch(partAttachmentUploadBatchParams);
                //if (!baseRes.isSuccess)
                //    throw new Exception("创建零部件数据集失败: " + baseRes.mesg);

                // 提交轻量化任务
                if(SaveLightData)
                {
                    baseRes = await HttpRequest.submitLightTask(uid, objectType_Ens);
                    if (!baseRes.isSuccess)
                        throw new Exception("提交轻量化任务失败: " + baseRes.mesg);
                }

                // 属性同步
                AttributesResponse attrRes = (AttributesResponse)await HttpRequest.queryItemAttributes(uid, objectType_Ens);
                if (!attrRes.isSuccess)
                    throw new Exception("零部件属性查询失败: " + attrRes.mesg);
                List<SwAttr> attrList;
                ConfigParser.SwAttrs.TryGetValue(ConfigParser.SwItemTypeMap.CN2Ens[objectType_CN], out attrList);
                Dictionary<string, string> pdmAttrs, modifiedAttrs = new Dictionary<string, string>();
                pdmAttrs = attrRes.attributes;
                if (null != attrList)
                {
                    foreach(var i in attrList)
                    {
                        if (i.ReadOnly)
                            continue;
                        string str;
                        propMgr.Get5(i.Description, true, out str, out resolvedVal, out flag);
                        if(str != pdmAttrs[i.AttrId])
                            modifiedAttrs[i.AttrId] = str;
                    }
                    baseRes = await HttpRequest.editItemAttributes(objectType_Ens, uid, modifiedAttrs);
                    if (!baseRes.isSuccess)
                        throw new Exception("零部件属性同步失败: " + baseRes.mesg);
                }

                //// 签入
                //baseRes = await HttpRequest.checkIn(uid, objectType_Ens);
                //if (!baseRes.isSuccess)
                //    throw new Exception("零部件签入失败: " + baseRes.mesg);

                iFrame.SetStatusBarText("保存完成");

            }
            catch (Exception ex)
            {
                iFrame.SetStatusBarText(ex.Message);
            }
        }

    }
}