/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FilePickerCatalog.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器文件选择器清单：Spine、PSD、配置表与 CSV 共用过滤器
 ***************************************************************/

using Avalonia.Platform.Storage;

namespace MapEditorTool.Services;

/// <summary>
/// 文件选择器清单。文件名模式、扩展名与选择器显示名只在这里维护，
/// 各面板只负责自己的对话框标题与选中后的业务处理。
/// </summary>
public static class FilePickerCatalog
{
    #region 对话框标题

    /// <summary>选择 Spine 骨架文件的标题。</summary>
    public const string SpineDialogTitle = "选择 Spine 骨架（.json / .skel）";

    /// <summary>选择 PSD / PSB 文件的标题。</summary>
    public const string PsdDialogTitle = "选择 PSD / PSB 文件";

    /// <summary>选择配置文件或配置目录的标题。</summary>
    public const string ConfigDialogTitle = "选择配置文件";

    #endregion

    #region 共用过滤器

    /// <summary>Spine 骨架：JSON 与二进制 skel 两种格式。</summary>
    public static readonly FilePickerFileType SpineSkeleton = new("Spine 骨架")
    {
        Patterns = new[] { "*.json", "*.skel" },
    };

    /// <summary>Photoshop 文档：PSD 与大型 PSB。</summary>
    public static readonly FilePickerFileType Photoshop = new("Photoshop 文档")
    {
        Patterns = new[] { "*.psd", "*.psb" },
    };

    /// <summary>地图配置表：兼容 xlsm / xlsx / csv / json 四种落盘格式。</summary>
    public static readonly FilePickerFileType ConfigTable = new("配置表")
    {
        Patterns = new[] { "*.xlsm", "*.xlsx", "*.csv", "*.json" },
    };

    /// <summary>物件元件 CSV 配置表。</summary>
    public static readonly FilePickerFileType CsvTable = new("CSV 配置表")
    {
        Patterns = new[] { "*.csv" },
    };

    #endregion
}
