namespace BplmSw.Common
{
    /// <summary>
    /// 全局服务定位器，主项目启动时注册 INewDocService 的实现
    /// </summary>
    public static class NewDocServiceLocator
    {
        public static INewDocService Instance { get; set; }
    }
}
