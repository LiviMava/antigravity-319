using BplmSw.Common;
using SolidWorks.Interop.sldworks;

namespace BplmSw.NewDoc
{
    /// <summary>
    /// INewDocService 的实现，供 OpenDoc 通过接口调用
    /// </summary>
    public class NewDocService : INewDocService
    {
        public bool ShowNewDocDialog(object sw, string puid, string objectType)
        {
            // 将参数写入 SessionContext，NewDocWindow 构造函数会读取
            SessionContext.Puid = puid;
            SessionContext.ObjectType = objectType;

            var window = new NewDocWindow((ISldWorks)sw, NewDocWindowMode.NoDrawingTemplate);
            return window.ShowDialog() == true; // true=成功新建, false=用户取消
        }
    }
}
