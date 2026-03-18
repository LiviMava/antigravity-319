using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static BplmSw.BomLineResponse;

namespace BplmSw.Utils
{
    public class MD5Util
    {
        public static string ComputeMD5FromStream(Stream stream)
        {
            using (var md5 = MD5.Create())
            {
                // 计算哈希（注意：此方法会从流的当前位置读取到末尾）
                byte[] hashBytes = md5.ComputeHash(stream);
                // 转换为十六进制字符串
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        public async static Task OpenDocDownLoadFromPLM(string fileName, string fullPath, ItemDataSetResponse.DataSet targetDataset)
        {
            string localMd5 = "", remoteMd5 = "";
            remoteMd5 = targetDataset.strMd5;
            if (File.Exists(fullPath))
            {
                using (var stream = File.OpenRead(fullPath)) localMd5 = MD5Util.ComputeMD5FromStream(stream);
            }
            if (!File.Exists(fullPath) || localMd5 != remoteMd5)
            {
                //var downloadRes = await HttpRequest.partAttachmentDownload(targetDataset.strFileId, fileName);
                //下载参数1：OSS存储地址
                StringResponse downloadRes = (StringResponse)await HttpRequest.presignedUrl(targetDataset.strFileId);
                OssUtil ossUtil = new OssUtil();
                if (!Directory.Exists(Constants.SW_CACHE_PATH))
                    Directory.CreateDirectory(Constants.SW_CACHE_PATH);
                //下载参数2：本地路径
                string localPath = Path.Combine(Constants.SW_CACHE_PATH, fileName);
                ossUtil.DownloadFile(downloadRes.str, localPath);
            }
        }

        // 用于存放去重后的零部件列表 (Key: RevisionPuid, Value: 节点对象)
        Dictionary<string, FatherObject> uniqueParts = new Dictionary<string, FatherObject>();

        /// <summary>
        /// 递归遍历BOM树，提取所有去重后的节点
        /// </summary>
        /// <param name="node">当前遍历的节点</param>
        /// <param name="uniqueParts">用于存放去重结果的字典</param>
        public static void TraverseBOMTree(BomLineResponse.FatherObject node, Dictionary<string, BomLineResponse.FatherObject> uniqueParts)
        {
            if (node == null) return;

            // 1. 记录唯一节点 (使用 revision_puid 作为唯一键去重)
            if (!string.IsNullOrEmpty(node.revision_puid) && !uniqueParts.ContainsKey(node.revision_puid))
            {
                uniqueParts.Add(node.revision_puid, node);
            }

            // 2. 递归遍历子节点
            if (node.has_children && node.children != null && node.children.Count > 0)
            {
                foreach (var child in node.children)
                {
                    TraverseBOMTree(child, uniqueParts);
                }
            }
        }
    }
}