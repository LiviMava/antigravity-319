using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BplmSw
{
    // 定义通用常量
    public static class Constants
    {
        public const string SW_CACHE_PATH = "D:\\BplmSwData\\";
        public const string PUID = "PUID";
        public const string ITEMPUID = "ITEMPUID";
        public const string BOMLINEUID = "BomLineUid";
        public const string OBJECT_NAME = "object_name";
        public const string OBJECT_NAME_CHINESE = "名称";
        public const string ITEM_REVISION_ID = "item_revision_id";
        public const string ITEM_REVISION_ID_CHINESE = "版本号";
        public const string ITEM_ID = "item_id";
        public const string ITEM_ID_CHINESE = "编码";
        public const string REVISION_SUFFIX = "Revision";
        public const string ITEM_TYPE = "object_type";
        public const string ITEM_TYPE_CHINESE = "项类型";
        public const string BOMLINE_UID = "BomLineUid";
        public const string DOUBLE_CLICK_TO_ASSIGN = "双击以指派";
        public const string ITEM_ATTRIBUTE_CATEGORY = "BPLM零部件属性";
        public const string ITEMREVISION_ATTRIBUTE_CATEGORY = "BPLM零部件版本属性";
        public const string IMPORT_OPERATE_DEFAULT = "默认";
        public const string IMPORT_OPERATE_COVER = "覆盖";
        public const string IMPORT_OPERATE_USEEXISTING = "使用现有的";
        public const string DATASET = "Dataset";
        public const string DATASET_ASM_MASTER = "ASMMaster";
        public const string DATASET_PRT_MASTER = "PRTMaster";
        public const string DATASET_DRW_MASTER = "DRWMaster";
        public const string DATASET_UGMASTER = "UGMaster";
        public const string DATASET_IMAGE = "Image";
        public const string DATASET_UGPART = "UGPart";
        public const string DATASET_PDF = "PDF";
        public const string Z9_MOLD_MANAGEMENT_NUMBER = "z9_mold_management_number";
        public const string Z9_MOLD_MANAGEMENT_NUMBER_CHINESE = "模具管理号";
        public const string Z9_CASTING_MOLDS_NUMBER = "z9_casting_molds_number";
        public const string Z9_CASTING_MOLDS_NUMBER_CHINESE = "压铸模编号";
        public const string Z9_JIG_SERIAL_NUMBER = "z9_jig_serial_number";
        public const string Z9_JIG_SERIAL_NUMBER_CHINESE = "检具编号";
        public const string Z9_PRODUCT_NO = "z9_product_no";
        public const string Z9_PRODUCT_NO_CHINESE = "产品管理号";
        public const string Z9_VERSION = "z9_version";
        public const string Z9_VERSION_CHINESE = "版本";
        public const string Z9_VERSION_ID = "z9_version_id";
        public const string Z9_VERSION_ID_CHINESE = "版号";
        public const string Z9_PART_NUMBER = "z9_part_number";
        public const string Z9_PART_NUMBER_CHINESE = "件号";
        public const string Z9_PRESS_MOLD = "Z9_press_mold";
        public const string Z9_SEQUENCE_NUMBER = "z9_sequence_number";
        public const string Z9_SEQUENCE_NUMBER_CHINESE = "零件顺序号";
        public const string Z9_PRESS_MOLD_CHINESE = "冲压模具";
        public const string Z9_INJECTION_SURFACE = "Z9_injection_surface";
        public const string Z9_INJECTION_SURFACE_CHINESE = "注塑模具";
        public const string Z9_CASTING_MOLDS = "Z9_casting_molds";
        public const string Z9_CASTING_MOLDS_CHINESE = "压铸模";
        public const string Z9_INSPECTION_JIGS = "Z9_inspection_jigs";
        public const string Z9_INSPECTION_JIGS_CHINESE = "检具";
        public const string Z9_DL_DIAGRAM = "Z9_dl_diagram";
        public const string Z9_DL_DIAGRAM_CHINESE = "DL图";
        public const string Z9_STAMPING_SURFACE = "Z9_stamping_surface";
        public const string Z9_STAMPING_SURFACE_CHINESE = "冲压模面";
        public static readonly HashSet<string> FOLDER_ITEMS = new HashSet<string>
        {
            "Folder", "Home", "MEActivity", "Stuff", "Work", "Z9_Collab_Folder"
        }; // 文件夹类型
    }
}