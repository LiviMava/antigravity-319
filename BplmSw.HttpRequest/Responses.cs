using System.Collections.Generic;

namespace BplmSw
{
    public class BaseResponse
    {
        public bool isSuccess;
        public string mesg;
    }

    public class BoolResponse : BaseResponse
    {
        public bool val;
    }

    public class StringResponse : BaseResponse
    {
        public string str;
    }

    public class LoginResponse : BaseResponse
    {
        public string token;
    }

    public class FolderResponse : BaseResponse 
    {
        public struct Folder
        {
            public string strUid; // 对象主键
            public string strObjectType; // 对象类型（项类型）
            public string strObjectString; // 对象显示名称
            public string strOwningUserInfo; // 所有者(格式：姓名（工号）)
            public string strWorkflowStatus; // 流程状态(1：审批中，2：被收回，3：被驳回，4：结案，5：已终止，6：草稿)
            public string strCheckInOutStatus; // 签入签出状态
            public string strPfigureIdentification; // 图标url
            public List<Folder> children; // 子节点
        }

        public Folder folder = new Folder();
    }

    public class FatherObjectResponse : BaseResponse
    {
        public struct FatherObject
        {
            public string strUid; // 对象主键
            public string strObjectType; // 对象类型（项类型）
            public string strObjectString; // 对象显示名称
            public string strOwningUserInfo; // 所有者(格式：姓名（工号）)
            public string strWorkflowStatus; // 流程状态(1：审批中，2：被收回，3：被驳回，4：结案，5：已终止，6：草稿)
            public bool strCheckInOutStatus; // 签入签出状态
            public string strPfigureIdentification; // 图标url
            public string strItemRevisionId; //版本号
            public string parentUid; //父级uid
            public string hasChild; //是否有子级
            public List<FatherObject> children; // 子节点
        }

        public FatherObject fatherObject = new FatherObject();
    }

    public class PersonalInfoResponse : BaseResponse
    {
        public string tenantCode;
        public string puserId;
        public string puserName;
    }

    public class CreateItemResponse : BaseResponse
    {
	    public string uid; // 零部件ID
        public string revUid; // 零部件版本ID
    };

    public class ItemDataSetResponse : BaseResponse
    {
        // 数据集
        public struct DataSet
        {
            public string strType; // 类型
            public string strPuid;
            public string strObjectName; // 名称
            public string strDatasetType; // 数据集类型
            public string strFileId; // 文件ID
            public string strMd5; // md5
            public string strOwningUserInfo; // 所有者(格式：姓名（工号）)
        };

        // 零部件&数据集列表
        public struct ItemDataSet
        {
            public string strType; // 实体类型
            public string strPuid; // 零部件ID
            public string strItemId; // 对象编码
            public string strItemRevisionId; // 版本号
            public string strObjectString; // 中文名称
            public bool isCheckOut; // 是否签出状态
            public List<DataSet> dataSets; // 数据集
        };

        public List<ItemDataSet> itemDataSets = new List<ItemDataSet>();
    };

    public class UploadFileResponse : BaseResponse
    {
        public string strFileId; // 文件ID
        public string strFileName; // 文件名称
        public string strFileSize; // 文件大小
        public string strFileExt; // 文件扩展
    };

    public class AttributesResponse : BaseResponse
    {
        public Dictionary<string, string> attributes;
    }

    public class FolderSearchResponse : BaseResponse
    {
        public struct Folder
        {
            public string uid;
            public string objectName;
            public string objectDesc;
            public string owningUser;
            public string objectType;
            public string displayName;
        }

        public int totalNum;
        public List<Folder> folders = new List<Folder>();
    }

    public class ItemRevisionResponse : BaseResponse
    {
        public struct ItemRevision
        {
            public string DisplayObject => $"{item_id}-{item_revision_id}-{object_name}";
            public string item_id { get; set; }
            public string item_revision_id { get; set; }
            public string object_name { get; set; }
            public string type { get; set; }
            public string isCheckOut { get; set; }
            public string DisplayCheckOutState => isCheckOut == "1" ? "已签出" : "未签出";
            public string z9_effective_status { get; set; }
            public string owning_user { get; set; }
        }
        public List<ItemRevision> data;
    }

    public class LastItemRevisionResponse : BaseResponse
    {
        public struct ItemRevision
        {
            public string id;
            public string versionNum;
            public string name;
            public string itemType;
            public string checkoutStatusName;
            public string owningUserName;
            public string uid;
            public string revisionType;
        }

        public struct Data
        {
            public List<ItemRevision> data;
            public string total;
        }
        public Data data;
    }

    public class CheckInStatusResponse : BaseResponse
    {
        public struct CheckIn
        {
            public string status;
            public string desc;
        }

        public struct CheckInStatus
        {
            public string uid;
            public string objectName;
            public CheckIn checkIn;
        }
        public List<CheckInStatus> data;
    }

    public class BomLineResponse : BaseResponse
    {
        public class FatherObject
        {
            public string bom_line_uid;
            public string revision_puid;
            public string revision_type;
            public string item_id;
            public string item_puid;
            public string item_type;
            public string object_name;
            public string item_revision_id;
            public bool has_children;
            public List<FatherObject> children;
        }
        public FatherObject fatherObject = new FatherObject();
    }
}