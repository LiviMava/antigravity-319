using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Aliyun.OSS;
using Aliyun.OSS.Common;

namespace BplmSw.Utils
{
    public class OssUtil
    {
        private readonly string _accessKeyId = "o";
        private readonly string _accessKeySecret = "p";
        private readonly string _endpoint = "a.ops.cloud.a.com";
        private readonly string _bucketName = "bucket-bplm-test";
        private readonly OssClient _client;

        public OssUtil()
        {
            // 创建 OssClient 实例
            _client = new OssClient(_endpoint, _accessKeyId, _accessKeySecret);
        }

        /// <summary>
        /// 构造函数，初始化 OSS 客户端
        /// </summary>
        public OssUtil(string accessKeyId, string accessKeySecret, string endpoint, string bucketName)
        {
            _accessKeyId = accessKeyId;
            _accessKeySecret = accessKeySecret;
            _endpoint = endpoint;
            _bucketName = bucketName;

            // 创建 OssClient 实例
            _client = new OssClient(_endpoint, _accessKeyId, _accessKeySecret);
        }

        /// <summary>
        /// 上传文件到 OSS（同步）
        /// </summary>
        /// <param name="localFilePath">本地文件完整路径</param>
        /// <param name="ossObjectKey">OSS 对象键（包含路径）</param>
        public void UploadFile(string localFilePath, string ossObjectKey)
        {
            try
            {
                var result = _client.PutObject(_bucketName, ossObjectKey, localFilePath);
                //var metadata = _client.GetObjectMetadata(_bucketName, ossObjectKey);
                //fileSize = metadata.ContentLength.ToString();
            }
            catch (Exception ex)
            {
                throw;
                //Console.WriteLine($"上传失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 OSS 下载文件到本地（同步）
        /// </summary>
        /// <param name="ossObjectKey">OSS 对象键</param>
        /// <param name="downloadFilePath">本地保存路径</param>
        public void DownloadFile(string ossObjectKey, string downloadFilePath)
        {
            Uri uri = new Uri(ossObjectKey);
            string absolutePath = uri.AbsolutePath;
            string objectKey = absolutePath.TrimStart('/');
            objectKey = WebUtility.UrlDecode(objectKey);
            try
            {
                // 获取对象并写入本地文件
                var obj = _client.GetObject(_bucketName, objectKey);
                using (var fileStream = File.OpenWrite(downloadFilePath))
                {
                    obj.Content.CopyTo(fileStream);
                }
                Console.WriteLine($"下载成功，保存至: {downloadFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"下载失败: {ex.Message}");
            }
        }

    }
}