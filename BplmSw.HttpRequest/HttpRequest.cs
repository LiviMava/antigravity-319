using BplmSw.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Xml.Linq;
using static BplmSw.CheckInStatusResponse;
using static BplmSw.FolderResponse;
using static BplmSw.ItemDataSetResponse;
using static BplmSw.ItemRevisionResponse;
using static BplmSw.LastItemRevisionResponse;

namespace BplmSw
{
    public class HttpRequest
    {
        private static readonly string BPLM_PROTOCOL = "https://";
        private static readonly string BPLM_DOMAIN = "dmc13-uat.a.com/bplmUat";
        //private static readonly string BPLM_DOMAIN = "ubplm-test.a.com/bplmTest";
        private static readonly HttpClient _client = new HttpClient();
        private static string tenantCode;

        static HttpRequest()
        {
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        private static async Task<string> getTenantCodeAsync()
        {
            if (null == tenantCode || 0 == tenantCode.Length)
            {
                PersonalInfoResponse res = (PersonalInfoResponse)await getPersonalInfo();
                tenantCode = res.tenantCode;
            }
            return tenantCode;
        }
        public static async Task<LoginResponse> login(string userId, string password)
        {

            string strUrl, strData;
            strUrl = BPLM_PROTOCOL + BPLM_DOMAIN + "/bidp-system/tSysUser/login";
            strData = "?userId=" + userId + "&password=" + password;
            strUrl += strData;

            LoginResponse res = new LoginResponse();

            try
            {
                HttpResponseMessage response = await _client.GetAsync(strUrl);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                if (res.isSuccess)
                {
                    res.token = (string)jsonObj["data"]["token"];
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Request Error: {e.Message}");
                res.isSuccess = false;
                res.mesg = e.Message;
            }

            return res;
        }

        public static async Task<BoolResponse> isLogin(string token)
        {
            token = "Bearer " + token;
            string strUrl, strData;
            strUrl = BPLM_PROTOCOL + BPLM_DOMAIN + "/bidp-system/iam/isLogin";
            JObject json = new JObject();
            json["token"] = token;
            strData = json.ToString();
            BoolResponse res = new BoolResponse();

            try
            {
                var content = new StringContent(strData, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _client.PostAsync(strUrl, content);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.val = (bool)jsonObj["data"];
            }
            catch (Exception e)
            {
                Console.WriteLine($"Request Error: {e.Message}");
                res.isSuccess = false;
                res.mesg = e.Message;
            }

            return res;
        }

        public static async Task<BaseResponse> getPersonalInfo()
        {
            string strUrl = BPLM_PROTOCOL + BPLM_DOMAIN + "/bidp-system/workbench/getPersonalInfo";
            PersonalInfoResponse res = new PersonalInfoResponse();

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, strUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionContext.Token);

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.tenantCode = (string)jsonObj["data"]["tenantCode"];
                res.puserId = (string)jsonObj["data"]["puserId"];
                res.puserName = (string)jsonObj["data"]["puserName"];
            }
            catch (Exception e)
            {
                Console.WriteLine($"Request Error: {e.Message}");
                res.isSuccess = false;
                res.mesg = e.Message;
            }

            return res;
        }

        /// <summary>
        /// 指派零部件编码(item_id)
        /// </summary>
        /// <param name="objectType">零部件类型</param>
        /// <returns></returns>
        public static async Task<BaseResponse> assignID(string objectType)
        {
            StringResponse res = new StringResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/spareParts/getItemCode";
                string data = "objectType=" + objectType;
                var content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());
                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.str = (string)jsonObj["data"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 指派零部件版本号
        /// </summary>
        /// <param name="objectType">零部件类型</param>
        /// <returns></returns>
        public static async Task<BaseResponse> assignRev(string objectType)
        {
            StringResponse res = new StringResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/spareParts/getItemVersion";
                string data = "objectType=" + objectType;
                var content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());
                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.str = (string)jsonObj["data"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        public static void dfsParse(JObject jObj, ref FolderResponse.Folder parent)
        {
            if(null != jObj)
            {
                parent.strUid = (string)jObj["uid"];
                parent.strObjectType = (string)jObj["objectType"];
                parent.strObjectString = (string)jObj["objectString"];
                parent.strOwningUserInfo = (string)jObj["owningUserInfo"];
                parent.strWorkflowStatus = (string)jObj["workflowStatus"];
                parent.strCheckInOutStatus = (string)jObj["checkInOutStatus"];
                parent.strPfigureIdentification = (string)jObj["pfigureIdentification"];
                parent.children = new List<FolderResponse.Folder>();
                var temp = jObj["childList"];
                if(temp.HasValues)
                {
                    JArray array = (JArray)temp;
                    foreach (JObject i in array)
                    {
                        FolderResponse.Folder folder= new FolderResponse.Folder();
                        dfsParse(i, ref folder);
                        parent.children.Add(folder);
                    }
                }
            }
        }

        /// <summary>
        /// 查询Home及其子文件夹结构树
        /// </summary>
        /// <returns></returns>
        public static async Task<BaseResponse> getCurrentUserHomeDirectory()
        {
            FolderResponse res = new FolderResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/getCurrentUserHomeDirectory";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());
                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                JObject dataObj = (JObject)jsonObj["data"];
                dfsParse(dataObj, ref res.folder);
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 查询文件夹的子文件夹列表
        /// </summary>
        /// <param name="uid">文件夹uid</param>
        /// <param name="objectType">文件夹类型</param>
        /// <returns></returns>
        public static async Task<BaseResponse> getChildFolderListByParentFolder(string uid, string objectType)
        {
            FolderResponse res = new FolderResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/getChildFolderListByParentFolder";
                string data = "?uid=" + uid + "&objectType=" + objectType;
                url += data;
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());
                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                var dataObj = jsonObj["data"];
                res.folder.children = new List<FolderResponse.Folder>();
                if (dataObj.HasValues)
                {
                    JArray array = (JArray)dataObj;
                    foreach (JObject i in array)
                    {
                        FolderResponse.Folder folder = new FolderResponse.Folder();
                        folder.strUid = (string)i["uid"];
                        folder.strObjectType = (string)i["objectType"];
                        folder.strObjectString = (string)i["objectString"];
                        folder.strOwningUserInfo = (string)i["owningUserInfo"];
                        folder.strWorkflowStatus = (string)i["workflowStatus"];
                        folder.strCheckInOutStatus = (string)i["checkInOutStatus"];
                        folder.strPfigureIdentification = (string)i["pfigureIdentification"];
                        res.folder.children.Add(folder);
                    }
                }
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 根据父查询子结构（懒加载）
        /// </summary>
        /// <param name="uid">父级对象uid</param>
        /// <param name="objectType">父级对象类型</param>
        /// <returns></returns>
        public static async Task<BaseResponse> getChildListByParentObject(string uid, string objectType)
        {
            FatherObjectResponse res = new FatherObjectResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/getChildListByParentObject";
                string data = "?uid=" + uid + "&objectType=" + objectType;
                url += data;
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());
                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                var dataObj = jsonObj["data"];
                res.fatherObject.children = new List<FatherObjectResponse.FatherObject>();
                if (dataObj.HasValues)
                {
                    JArray array = (JArray)dataObj;
                    foreach (JObject i in array)
                    {
                        FatherObjectResponse.FatherObject fatherObject = new FatherObjectResponse.FatherObject();
                        fatherObject.strUid = (string)i["uid"];
                        fatherObject.strObjectType = (string)i["objectType"];
                        fatherObject.strObjectString = (string)i["objectString"];
                        fatherObject.strOwningUserInfo = (string)i["owningUserInfo"];
                        fatherObject.strWorkflowStatus = (string)i["workflowStatus"];
                        fatherObject.strCheckInOutStatus = (bool?)i["isCheckOut"] ?? false;
                        fatherObject.strPfigureIdentification = (string)i["pfigureIdentification"];
                        fatherObject.strItemRevisionId = (string)i["itemRevisionId"];
                        res.fatherObject.children.Add(fatherObject);
                    }
                }
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }
        public static async Task<BomLineResponse> getAllBomLineByParent(string uid, string objectType, string revisionRuleName)
        {
            BomLineResponse res = new BomLineResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/getAllBomLineByParent";
                string data = "?itemRevisionType=" + objectType + "&itemRevisionPuid=" + uid + "&revisionRuleName=" + revisionRuleName;
                url += data;
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];

                var dataObj = jsonObj["data"];
                if (dataObj.HasValues)
                {
                    res.fatherObject = dataObj.ToObject<BomLineResponse.FatherObject>();
                }
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }
        /// <summary>
        /// 创建零部件对象
        /// </summary>
        /// <param name="objectType">零部件类型</param>
        /// <param name="attrs">属性</param>
        /// <param name="folderId">文件夹uid</param>
        /// <param name="folderType">文件夹类型</param>
        /// <returns></returns>
        public static async Task<BaseResponse> createItem(string objectType, Dictionary<string, string> attrs, string folderId, string folderType)
        {
            CreateItemResponse res = new CreateItemResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/spareParts/partsDataManageAdd";
                // 构造json请求体
                JObject data = new JObject();
                // 类型
                data["type"] = objectType;
                // 属性
                JArray fieldList = new JArray();
                foreach (KeyValuePair<string, string> kvp in attrs)
                {
                    JObject field = new JObject();
                    field["fieldName"] = kvp.Key;
                    field["fieldValue"] = kvp.Value;
                    fieldList.Add(field);
                }
                data["fieldList"] = fieldList;
                // 文件夹
                JObject reletionNode = new JObject();
                reletionNode["uid"] = folderId;
                reletionNode["type"] = folderType;
                data["relationNode"] = reletionNode;

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.uid = (string)jsonObj["data"]["uid"];
                res.revUid = (string)jsonObj["data"]["revisionUid"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 签入（零部件/文件夹/数据集)
        /// </summary>
        /// <param name="uid">零部件版本对象uid</param>
        /// <param name="objectType">零部件版本对象类型</param>
        /// <returns></returns>
        public static async Task<BaseResponse> checkIn(string uid, string objectType)
        {
            BaseResponse res = new BaseResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/checkIn";
                string data = "[{\"puid\": \"" + uid + "\", \"type\": \"" + objectType + "\"}]";

                var content = new StringContent(data, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }
        public static async Task<CheckInStatusResponse> getCheckInStatus(string uid, string objectType)
        {
            CheckInStatusResponse res = new CheckInStatusResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/spareParts/batchGetCheckInStatus";
                var requestData = new[]
                {
                    new { uid = uid, type = objectType }
                };
                string data = JsonConvert.SerializeObject(requestData);

                var content = new StringContent(data, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];

                var dataToken = jsonObj["data"];
                if (dataToken != null)
                {
                    res.data = dataToken.ToObject<List<CheckInStatusResponse.CheckInStatus>>();
                }
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 签出
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public static async Task<BaseResponse> checkOut(string uid, string objectType)
        {
            BoolResponse res = new BoolResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/checkOutSingle";
                string data = "{\"puid\": \"" + uid + "\", \"type\": \"" + objectType + "\"}";

                var content = new StringContent(data, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.val = (bool)jsonObj["data"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 批量查询零部件数据集
        /// </summary>
        /// <param name="mapUid2Type">零部件版本对象uid -> 零部件版本对象类型</param>
        /// <returns></returns>
        public static async Task<BaseResponse> getPartAttachmentByPartIdBatch(Dictionary<string, string> mapUid2Type)
        {
            ItemDataSetResponse res = new ItemDataSetResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/partAttachment/getPartAttachmentByPartIdBatch";
                // 构造json请求体
                JArray data = new JArray();
                foreach (KeyValuePair<string, string> kvp in mapUid2Type)
                {
                    JObject item = new JObject();
                    item["uid"] = kvp.Key;
                    item["type"] = kvp.Value;
                    data.Add(item);
                }
                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                var dataObj = jsonObj["data"];
                if (dataObj.HasValues)
                {
                    JArray array = (JArray)dataObj;
                    foreach (JObject i in array)
                    {
                        ItemDataSetResponse.ItemDataSet itemDataSet = new ItemDataSet();
                        itemDataSet.strType = (string)i["type"];
                        itemDataSet.strPuid = (string)i["uid"]; 
                        itemDataSet.strItemId = (string)i["item_id"];
                        itemDataSet.strItemRevisionId = (string)i["item_revision_id"];
                        itemDataSet.strObjectString = (string)i["object_name"];
                        itemDataSet.isCheckOut = "1" == (string)i["is_checkout"];
                        itemDataSet.dataSets = new List<DataSet>();
                        var dataSetObj = i["partAttachmentBOList"];
                        if (dataSetObj.HasValues)
                        {
                            JArray array1 = (JArray)dataSetObj;
                            foreach (JObject j in array1)
                            {
                                ItemDataSetResponse.DataSet dataSet = new DataSet();
                                dataSet.strPuid = (string)j["dataSetPuid"];
                                dataSet.strObjectName = (string)j["objectName"];
                                dataSet.strDatasetType = (string)j["datasetType"];
                                dataSet.strFileId = (string)j["fileId"];
                                dataSet.strMd5 = (string)j["md5"];
                                dataSet.strType = (string)j["type"];
                                dataSet.strOwningUserInfo = (string)j["owningUser"];

                                itemDataSet.dataSets.Add(dataSet);
                            }
                        }
                        res.itemDataSets.Add(itemDataSet);
                    }
                }

            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }


        public struct PartAttachmentDeleteBatchParams
        {
            public string uid; // 零部件uid
            public string objectType; // 零部件类型
            public Dictionary<string, string> mapUid2Type; // 数据集 uid -> type
        };
        /// <summary>
        /// 零部件数据集批量删除
        /// </summary>
        /// <param name="itemDatasets"></param>
        /// <returns></returns>
        public static async Task<BaseResponse> partAttachmentDeleteBatch(List<PartAttachmentDeleteBatchParams> itemDatasets)
        {
            BaseResponse res = new BaseResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/partAttachment/partAttachmentDeleteBatch";
                // 构造json请求体
                JArray data = new JArray();
                foreach(var i in itemDatasets)
                {
                    JObject item = new JObject();
                    item["uid"] = i.uid;
                    item["type"] = i.objectType;
                    JArray array = new JArray();
                    foreach (KeyValuePair<string, string> kvp in i.mapUid2Type)
                    {
                        JObject item1 = new JObject();
                        item1["dataSetPuid"] = kvp.Key;
                        item1["objectType"] = kvp.Value;
                        array.Add(item1);
                    }
                    item["attachementDateaSetDTOList"] = array;
                    data.Add(item);
                }
                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        public struct PartAttachmentUploadBatchParams
        {
            public string uid; // 零部件uid
            public string objectType; // 实体类型
            public string datasetType; // 数据集类型
            public string fileId; // 文件ID
            public string fileName; // 文件名称
            public string fileSize; // 文件大小
            public string fileExt; // 文件后缀
            public string md5;
        }
        /// <summary>
        /// 零部件数据集批量创建
        /// </summary>
        /// <param name="itemDatasets"></param>
        /// <returns></returns>
        public static async Task<BaseResponse> partAttachmentUploadBatch(List<PartAttachmentUploadBatchParams> itemDatasets)
        {
            BaseResponse res = new BaseResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/partAttachment/partAttachmentUploadBatch";
                // 构造json请求体
                JArray data = new JArray();
                foreach (var i in itemDatasets)
                {
                    JObject item = new JObject();
                    item["uid"] = i.uid;
                    item["type"] = i.objectType;
                    item["datasetType"] = i.datasetType;
                    item["file_id"] = i.fileId;
                    item["file_name"] = i.fileName;
                    item["file_size"] = i.fileSize;
                    item["file_ext"] = i.fileExt;
                    item["md5"] = i.md5;
                    data.Add(item);
                }
                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 下载零部件附件到本地
        /// </summary>
        /// <param name="puid">零部件Uid</param>
        /// <param name="attachmentIds">数据集ID</param> 
        /// <param name="fileName">文件名称</param>
        /// <returns>Local File Path</returns>
        public static async Task<StringResponse> partAttachmentDownload(string fileId, string fileName)
        {
            StringResponse res = new StringResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-file/bidp-file/downLoad";
                string data = "?fileId=" + fileId;
                var request = new HttpRequestMessage(HttpMethod.Get, url+data);

                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                if (!Directory.Exists(Constants.SW_CACHE_PATH))
                {
                    Directory.CreateDirectory(Constants.SW_CACHE_PATH);
                }

                string localPath = Path.Combine(Constants.SW_CACHE_PATH, fileName);
                using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }

                res.isSuccess = true;
                res.str = localPath;
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 上传文件至文件服务模块
        /// </summary>
        /// <param name="filePath">本地路径</param>
        /// <returns></returns>
        public static async Task<BaseResponse> uploadFile(string filePath)
        {
            UploadFileResponse res = new UploadFileResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-file/bidp-file/upload";
                MultipartFormDataContent formData = new MultipartFormDataContent();
                FileStream fileStream = File.OpenRead(filePath);
                StreamContent fileContent = new StreamContent(fileStream);
                // 第三个参数是文件名，如果不指定，可能会使用默认名，这里我们显式指定
                formData.Add(fileContent, "file", Path.GetFileName(filePath));
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = formData;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.strFileId = (string)jsonObj["data"]["id"];
                res.strFileName = (string)jsonObj["data"]["fileName"];
                res.strFileSize = (string)jsonObj["data"]["fileSize"];
                res.strFileExt = (string)jsonObj["data"]["fileExt"];

            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 查询零部件属性
        /// </summary>
        /// <param name="uid">版本uid</param>
        /// <param name="objectType">对象类型</param>
        /// <returns></returns>
        public static async Task<BaseResponse> queryItemAttributes(string uid, string objectType)
        {
            AttributesResponse res = new AttributesResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/queryItemDetail";
                string data = "?puid=" + uid + "&type=" + objectType;
                url += data;
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.attributes = jsonObj["data"].ToObject<Dictionary<string, string>>();
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="folderType">文件夹类型</param>
        /// <param name="name">文件夹名称</param>
        /// <param name="desc">描述</param>
        /// <param name="pFolderType">父节点类型</param>
        /// <param name="pUid">父节点uid</param>
        /// <returns></returns>
        public static async Task<BaseResponse> createFolder(string folderType, string name, string desc, string pFolderType, string pUid)
        {
            StringResponse res = new StringResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/saveItemAndFolder";
                // 构造json请求体
                JObject data = new JObject();
                // 文件夹类型
                data["type"] = folderType;

                JObject fieldsNode = new JObject();
                fieldsNode["object_name"] = name;
                fieldsNode["object_desc"] = desc;
                data["fields"] = fieldsNode; 
                // 父文件夹
                JObject reletionNode = new JObject();
                reletionNode["uid"] = pUid;
                reletionNode["type"] = pFolderType;
                data["relationNode"] = reletionNode;
                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.str = (string)jsonObj["data"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        public static async Task<BaseResponse> presignedUrl(string fileId)
        {
            StringResponse res = new StringResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-file/bidp-file/presignedUrl";
                string data = "?fileId=" + fileId;
                url += data;
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.str = (string)jsonObj["data"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        public static async Task<BaseResponse> editFolder(string uid, string folderType, string name, string desc)
        {
            BaseResponse res = new BaseResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/checkInAndSave";
                // 构造json请求体
                JObject data = new JObject();
                data["type"] = folderType;
                data["puid"] = uid;
                JObject fieldsNode = new JObject();
                fieldsNode["object_name"] = name;
                fieldsNode["object_desc"] = desc;
                data["fields"] = fieldsNode;

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 删除对象
        /// </summary>
        /// <param name="items">uid -> objectType</param>
        /// <returns></returns>
        public static async Task<BaseResponse> deleteItem(Dictionary<string, string> items)
        {
            BaseResponse res = new BaseResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemDelete/deleteItem";
                // 构造json请求体
                JArray data = new JArray();
                foreach (var i in items)
                {
                    JObject itemNode = new JObject();
                    itemNode["uid"] = i.Key;
                    itemNode["type"] = i.Value;
                    data.Add(itemNode);
                }

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        public struct FolderSearchParams
        {
            public string objectName;
            public string objectType;
            public string owningUser;
            public int pageNum;
            public int pageSize;

            public FolderSearchParams()
            {
                objectName = "";
                objectType = "Folder";
                owningUser = "";
                pageNum = 1;
                pageSize = 10;
            }
        }
        public static async Task<BaseResponse> folderSearch(FolderSearchParams folderParams)
        {
            FolderSearchResponse res = new FolderSearchResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemQuery/folderSearch";
                // 构造json请求体
                JObject data = new JObject();
                data["objectName"] = folderParams.objectName;
                data["objectType"] = folderParams.objectType;
                data["owningUser"] = folderParams.owningUser;
                data["pageNum"] = folderParams.pageNum;
                data["pageSize"] = folderParams.pageSize;

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.totalNum = (int)jsonObj["data"]["totalNum"];
                var listObj = jsonObj["data"]["list"];
                if (listObj.HasValues)
                {
                    JArray array = (JArray)listObj;
                    foreach (JObject i in array)
                    {
                        FolderSearchResponse.Folder folder = new FolderSearchResponse.Folder();
                        folder.uid = (string)i["uid"];
                        folder.objectName = (string)i["objectName"];
                        folder.objectDesc = (string)i["objectDesc"];
                        folder.owningUser = (string)i["owningUser"];
                        folder.objectType = (string)i["objectType"];
                        folder.displayName = (string)i["displayName"];
                        res.folders.Add(folder);
                    }
                }
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }
        /// <summary>
        /// 零部件版本记录查询
        /// </summary>
        /// <param name="objectType">类型</param>
        /// <param name="uid">Uid</param>
        /// <returns></returns>
        public static async Task<BaseResponse> getItemRevision(string objectType, string uid)
        {
            ItemRevisionResponse res = new ItemRevisionResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/spareParts/getItemRevision";

                // 使用键值对字典
                var formData = new Dictionary<string, string>
                {
                    { "objectType", objectType },
                    { "uid", uid }
                };
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new FormUrlEncodedContent(formData);

                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];

                var dataObj = jsonObj["data"];
                if (dataObj != null && dataObj.Type == JTokenType.Array)
                {
                    res.data = dataObj.ToObject<List<ItemRevisionResponse.ItemRevision>>();
                }
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 高级检索
        /// </summary>
        /// <returns></returns>
        public static async Task<BaseResponse> advancedSearch(string id, string name, string type, int pageNum, int pageSize)
        {
            LastItemRevisionResponse res = new LastItemRevisionResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemQuery/advancedSearch";

                JObject data = new JObject();
                data["id"] = id;
                data["name"] = name;
                data["type"] = type;
                data["cancelAuth"] = false;
                data["pageNum"] = pageNum;
                data["pageSize"] = pageSize;

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];

                var dataToken = jsonObj["data"];
                if (dataToken != null)
                {
                    res.data = dataToken.ToObject<LastItemRevisionResponse.Data>();
                }
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        public static async Task<BaseResponse> editItemAttributes(string objectType, string uid, Dictionary<string, string> attrs)
        {
            BaseResponse res = new BaseResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/itemManager/editItem";

                JObject data = new JObject();
                data["type"] = objectType;
                data["puid"] = uid;
                data["fields"] = new JObject();
                foreach(var kvp in attrs)
                    data["fields"][kvp.Key] = kvp.Value;

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /// <summary>
        /// 提交轻量化任务
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public static async Task<BaseResponse> submitLightTask(string uid, string objectType)
        {
            BaseResponse res = new AttributesResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/lightIntegration/submitTask";
                string data = "?puid=" + uid + "&type=" + objectType;
                url += data;
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        /*
        /// <summary>
        /// 已发布最新版本零部件分页查询
        /// </summary>
        /// <returns></returns>
        public static async Task<BaseResponse> getLastItemRevision(string id, string type, string name, string owner, string ownOrg, string project, int pageNum, int pageSize)
        {
            LastItemRevisionResponse res = new LastItemRevisionResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/openApi/item/getLastItemRevision";

                JObject data = new JObject();
                data["ID"] = id;
                data["type"] = type;
                data["name"] = name;
                data["owner"] = owner;
                data["ownOrg"] = ownOrg;
                data["project"] = project;
                data["pageNum"] = pageNum;
                data["pageSize"] = pageSize;

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];

                var dataToken = jsonObj["data"];
                if (dataToken != null)
                {
                    res.data = dataToken.ToObject<LastItemRevisionResponse.Data>();
                }
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }*/

        public struct SaveFileParameters
        {
            public string filePath;
            public string fileName;
            public string fileSize;
            public string fileExt;
            public string businessId;
        }
        /// <summary>
        /// 将oss文件保存至文件服务模块
        /// </summary>
        /// <param name="saveParams"></param>
        /// <returns></returns>
        public static async Task<BaseResponse> saveFile(SaveFileParameters saveParams)
        {
            StringResponse res = new StringResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-file/bidp-file/saveInfo";
                // 构造json请求体
                JObject data = new JObject();
                data["filePath"] = saveParams.filePath;
                data["fileName"] = saveParams.fileName;
                data["fileSize"] = saveParams.fileSize;
                data["fileExt"] = saveParams.fileExt;
                data["businessId"] = saveParams.businessId;

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
                res.str = (string)jsonObj["data"]["fileId"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

        public struct UpdateDatasetsParams
        {
            public string type;
            public string uid;
            public string datasetType;
            public string file_id;
            public string file_name;
            public string file_size;
            public string file_ext;
            public string md5;
            public string pre_file_id;
            public string datasetUid;
        }
        /// <summary>
        /// 更新数据集
        /// </summary>
        /// <param name="datasets"></param>
        /// <returns></returns>
        public static async Task<BaseResponse> updateDatasets(List<UpdateDatasetsParams> datasets)
        {
            BaseResponse res = new BaseResponse();
            try
            {
                string url = BPLM_PROTOCOL + BPLM_DOMAIN + "/bplm-pdm/partAttachment/updateDatasets";
                // 构造json请求体
                JArray data = new JArray();
                foreach(var i in datasets)
                {
                    JObject item = new JObject();
                    item["type"] = i.type;
                    item["uid"] = i.uid;
                    item["datasetType"] = i.datasetType;
                    item["file_id"] = i.file_id;
                    item["file_name"] = i.file_name;
                    item["file_size"] = i.file_size;
                    item["file_ext"] = i.file_ext;
                    item["md5"] = i.md5;
                    item["pre_file_id"] = i.pre_file_id;
                    item["datasetUid"] = i.datasetUid;
                    data.Add(item);
                }

                var content = new StringContent(data.ToString(), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                string token = "Bearer " + SessionContext.Token;
                request.Headers.Add("token", token);
                request.Headers.Add("tenantcode", await getTenantCodeAsync());

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject jsonObj = JObject.Parse(responseBody);
                res.isSuccess = "000000" == (string)jsonObj["code"];
                res.mesg = (string)jsonObj["mesg"];
            }
            catch (Exception e)
            {
                res.isSuccess = false;
                res.mesg = e.Message;
            }
            return res;
        }

    }
}