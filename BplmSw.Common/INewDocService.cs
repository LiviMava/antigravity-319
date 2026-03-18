namespace BplmSw.Common
{
    /// <summary>
    /// 新建文档服务接口，用于解耦 OpenDoc 与 NewDoc 的循环依赖。
    /// sw 参数为 object 类型以避免 Common 层依赖 SolidWorks 互操作程序集。
    /// </summary>
    public interface INewDocService
    {
        /// <summary>
        /// 以模态方式显示新建文档窗口（ShowDialog 阻塞，关闭后才返回）
        /// </summary>
        /// <param name="sw">ISldWorks 实例（传 object 避免 Common 引用 SW 互操作）</param>
        /// <param name="puid">零部件版本对象 UID</param>
        /// <param name="objectType">对象类型</param>
        void ShowNewDocDialog(object sw, string puid, string objectType);
    }
}
