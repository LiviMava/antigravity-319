using BplmSw.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Base.Attributes;
using Xarial.XCad.Base.Enums;
using Xarial.XCad.UI.Commands.Attributes;
using Xarial.XCad.UI.Commands.Enums;

namespace BplmSw
{
    [Title("在线设计")]
    public enum SwCommands
    {
        [Title("登录")]
        [Description("登录")]
        [Icon(typeof(Resources), nameof(Resources.login))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.All, true, RibbonTabTextDisplay_e.TextBelow)]
        Login,

        [Title("注销")]
        [Description("注销")]
        [Icon(typeof(Resources), nameof(Resources.logout))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.All, true, RibbonTabTextDisplay_e.TextBelow)]
        Logout,

        [Title("新建")]
        [Description("新建")]
        [Icon(typeof(Resources), nameof(Resources.new_file))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.All, true, RibbonTabTextDisplay_e.TextBelow)]
        NewDoc,

        [Title("保存并检入")]
        [Description("保存并检入")]
        [Icon(typeof(Resources), nameof(Resources.save))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.All, true, RibbonTabTextDisplay_e.TextHorizontal)]
        Save,

        [Title("保存选项")]
        [Description("保存选项")]
        [Icon(typeof(Resources), nameof(Resources.options))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.All, true, RibbonTabTextDisplay_e.TextHorizontal)]
        SaveOption,

        [Title("打开")]
        [Description("打开")]
        [Icon(typeof(Resources), nameof(Resources.file_open))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.All, true, RibbonTabTextDisplay_e.TextBelow)]
        OpenDoc,

        [Title("签出文件")]
        [Description("签出文件")]
        [Icon(typeof(Resources), nameof(Resources.replace_component))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.All, true, RibbonTabTextDisplay_e.TextBelow)]
        CheckOutDoc,

        [Title("签入文件")]
        [Description("签入文件")]
        [Icon(typeof(Resources), nameof(Resources.replace_component))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.All, true, RibbonTabTextDisplay_e.TextBelow)]
        CheckInDoc,

        [Title("新建组件")]
        [Description("新建组件")]
        [Icon(typeof(Resources), nameof(Resources.new_component))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly, true, RibbonTabTextDisplay_e.TextBelow)]
        NewComp,

        [Title("添加组件")]
        [Description("添加组件")]
        [Icon(typeof(Resources), nameof(Resources.add_component))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly, true, RibbonTabTextDisplay_e.TextBelow)]
        AddComp,

        [Title("替换组件")]
        [Description("替换组件")]
        [Icon(typeof(Resources), nameof(Resources.replace_component))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly, true, RibbonTabTextDisplay_e.TextBelow)]
        ReplaceComp

    }
    [Title("签入&签出")]
    public enum MyFileContextMenu_e
    {
        [Title("签出")]
        [Description("签出")]
        checkout,

        [Title("签入")]
        [Description("签入")]
        checkin
    }
}