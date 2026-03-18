using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BplmSw
{
    public struct SWTemplate
    {
        public string Name;
        public string Description;
        public string DatasetType;
        public string Suffix;
        public string Filename;
        public string Preview;
        public List<string> ItemTypes;
    }

    public struct SwAttr
    {
        public string AttrId;
        public string Description;
        public bool ReadOnly;
        public bool Required;
        public string Mode;
    }

    public class SwItemTypeMap
    {
        public Dictionary<string, string> CN2Ens; // 中文名称->英文名称
        public Dictionary<string, string> Ens2CN; // 英文名称->中文名称
    }

    public struct ServerConfig
    {
        public string Environment; // 例如: prod, dev
        public string Domain;      // 例如: ubplm.byd.com/bplmPro
        public string FullUrl;     // 例如: https://ubplm.byd.com/bplmPro
    }

    /// <summary>
    /// 配置文件解析类
    /// </summary>
    public class ConfigParser
    {

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        private static string ReadIniValue(string section, string key, string filePath)
        {
            var retVal = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", retVal, 255, filePath);
            return retVal.ToString();
        }
        public static ServerConfig ParseServerConfigFromFile(string filePath)
        {
            ServerConfig config = new ServerConfig();

            if (!File.Exists(filePath))
            {
                return config; // 或者抛出异常，视你的错误处理策略而定
            }

            // 1. 读取环境设置 [Server] -> env
            config.Environment = ReadIniValue("Server", "env", filePath);

            // 2. 如果环境配置存在，读取对应的域名
            if (!string.IsNullOrEmpty(config.Environment))
            {
                // 使用 config.Environment 作为 Section 名 (例如 "prod")
                config.Domain = ReadIniValue(config.Environment, "bplm-domain", filePath);

                if (!string.IsNullOrEmpty(config.Domain))
                {
                    // 3. 拼接完整的 HTTP URL
                    config.FullUrl = $"https://{config.Domain}";
                }
            }

            return config;
        }

        // 插件根目录
        private static string addInHomePath;

        public static string AddInHomePath
        {
            get
            {
                if (addInHomePath == null)
                {
                    // 获取当前执行程序集的位置
                    string assemblyLocation = Assembly.GetExecutingAssembly().Location;

                    // 转换为文件信息
                    FileInfo assemblyFile = new FileInfo(assemblyLocation);

                    // 获取目录信息
                    DirectoryInfo assemblyDir = assemblyFile.Directory;

                    if (assemblyDir?.Parent?.Parent != null)
                    {
                        return assemblyDir.Parent.Parent.FullName;
                    }

                    throw new DirectoryNotFoundException("获取插件根目录失败");

                }
                return addInHomePath;
            }
        }

        /// <summary>
        /// 模板
        /// </summary>
        private static List<SWTemplate> swTemplates;

        public static List<SWTemplate> SwTemplates
        {
            get
            {
                if (null == swTemplates)
                {
                    swTemplates = new List<SWTemplate>();

                    // 从文件加载XML文档
                    XDocument doc = XDocument.Load(AddInHomePath + @"\Config\templates.xml");

                    // 遍历所有template元素
                    foreach (XElement templateElement in doc.Descendants("template"))
                    {
                        SWTemplate template = new SWTemplate();

                        // 解析presentation元素的属性
                        XElement presentation = templateElement.Element("presentation");
                        if (presentation != null)
                        {
                            template.Name = presentation.Attribute("name")?.Value ?? string.Empty;
                            template.Description = presentation.Attribute("description")?.Value ?? string.Empty;
                        }

                        // 解析其他子元素
                        template.DatasetType = templateElement.Element("dataset_type")?.Value ?? string.Empty;
                        template.Suffix = templateElement.Element("suffix")?.Value ?? string.Empty;
                        template.Filename = templateElement.Element("filename")?.Value ?? string.Empty;
                        template.Preview = templateElement.Element("preview")?.Value ?? string.Empty;
                        string ItemType = templateElement.Element("item_type")?.Value ?? string.Empty;
                        template.ItemTypes = ItemType.Split(',').ToList();

                        swTemplates.Add(template);
                    }
                }
                return swTemplates;
            }
        }

        /// <summary>
        /// 零部件类型中英文映射表
        /// </summary>
        private static SwItemTypeMap swItemTypeMap;

        public static SwItemTypeMap SwItemTypeMap
        {
            get
            {
                if (null == swItemTypeMap)
                {
                    swItemTypeMap = new SwItemTypeMap();
                    XDocument doc = XDocument.Load(AddInHomePath + @"\Config\templates.xml");
                    Dictionary<string, string> CN2Ens = new Dictionary<string, string>();
                    Dictionary<string, string> Ens2CN = new Dictionary<string, string>();

                    // 使用LINQ查询
                    var entities = from entity in doc.Descendants("entity")
                                   select new
                                   {
                                       CnName = (string)entity.Attribute("cn_name"),
                                       EnsName = (string)entity.Attribute("ens_name")
                                   };

                    foreach (var entity in entities)
                    {
                        if (!string.IsNullOrEmpty(entity.CnName) && !string.IsNullOrEmpty(entity.EnsName))
                        {
                            CN2Ens[entity.CnName] = entity.EnsName;
                            Ens2CN[entity.EnsName] = entity.CnName;
                        }
                    }
                    swItemTypeMap.Ens2CN = Ens2CN;
                    swItemTypeMap.CN2Ens = CN2Ens;

                }
                return swItemTypeMap;
            }
        }

        /// <summary>
        /// 零部件类型对应属性
        /// </summary>
        private static Dictionary<string, List<SwAttr>> swAttrs;

        public static Dictionary<string, List<SwAttr>> SwAttrs
        {
            get
            {
                if (null == swAttrs)
                {
                    swAttrs = new Dictionary<string, List<SwAttr>>();

                    // 从文件加载XML文档
                    XDocument doc = XDocument.Load(AddInHomePath + @"\Config\attributes.xml");
                    var itemAttributes = doc.Descendants("ITEM_ATTRIBUTE");

                    foreach (var itemAttr in itemAttributes)
                    {
                        var itemTable = itemAttr.Attribute("item_table")?.Value;

                        if (!string.IsNullOrEmpty(itemTable))
                        {
                            List<SwAttr> attributes = new List<SwAttr>();
                            foreach (var attr in itemAttr.Elements("attrbute"))
                            {
                                var attributeInfo = new SwAttr
                                {
                                    AttrId = attr.Attribute("attr_id")?.Value,
                                    Description = attr.Attribute("description")?.Value,
                                    ReadOnly = attr.Attribute("readonly")?.Value == "Y",
                                    Required = attr.Attribute("required")?.Value == "Y",
                                    Mode = attr.Attribute("mode")?.Value
                                };

                                attributes.Add(attributeInfo);
                            }

                            swAttrs[itemTable] = attributes;
                        }
                    }
                }
                return swAttrs;
            }
        }

    }
}