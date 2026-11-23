/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileDialogService.cs
 * author:    云毅
 * created:   2026
 * descrip:   统一的文件 / 目录选择入口，隔离窗口与 Avalonia StorageProvider 细节
 ***************************************************************/

using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MapEditorTool.Services;

/// <summary>
/// 文件与目录选择统一入口。调用方只描述「标题 + 过滤类型」，
/// 不再各自处理 <see cref="IStorageFolder"/> 空值、多选和本地路径转换。
/// </summary>
public static class FileDialogService
{
    /// <summary>
    /// 选择单个目录并返回本地绝对路径。
    /// 取消选择、平台不支持或无法转换为本地路径时返回 null。
    /// </summary>
    /// <param name="owner">发起选择的顶层窗口。</param>
    /// <param name="title">对话框标题。</param>
    /// <param name="startPath">可选起始目录；目录不存在时由平台回退到默认位置。</param>
    /// <returns>本地目录全路径，或 null。</returns>
    public static async Task<string?> PickFolderAsync(TopLevel owner, string title, string? startPath = null)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await ResolveStartFolderAsync(owner, startPath),
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    /// <summary>
    /// 选择单个文件并返回本地绝对路径。
    /// <paramref name="filters"/> 为空时允许选择任意文件。
    /// </summary>
    /// <param name="owner">发起选择的顶层窗口。</param>
    /// <param name="title">对话框标题。</param>
    /// <param name="filters">文件类型过滤。</param>
    /// <returns>本地文件全路径，或 null。</returns>
    public static Task<string?> PickFileAsync(
        TopLevel owner,
        string title,
        params FilePickerFileType[] filters) =>
        PickFileFromAsync(owner, title, null, filters);

    /// <summary>
    /// 指定起始目录后选择单个文件并返回本地绝对路径。
    /// </summary>
    /// <param name="owner">发起选择的顶层窗口。</param>
    /// <param name="title">对话框标题。</param>
    /// <param name="startPath">可选起始目录。</param>
    /// <param name="filters">文件类型过滤。</param>
    /// <returns>本地文件全路径，或 null。</returns>
    public static async Task<string?> PickFileFromAsync(
        TopLevel owner,
        string title,
        string? startPath,
        params FilePickerFileType[] filters)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await ResolveStartFolderAsync(owner, startPath),
            FileTypeFilter = filters.Length == 0 ? null : filters,
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <summary>
    /// 选择保存文件位置并返回本地绝对路径。取消时返回 null。
    /// </summary>
    /// <param name="owner">发起选择的顶层窗口。</param>
    /// <param name="title">对话框标题。</param>
    /// <param name="suggestedFileName">默认文件名。</param>
    /// <param name="defaultExtension">默认扩展名，不带点。</param>
    /// <param name="choices">可选文件类型。</param>
    /// <returns>本地文件全路径，或 null。</returns>
    public static async Task<string?> SaveFileAsync(
        TopLevel owner,
        string title,
        string suggestedFileName,
        string defaultExtension,
        params FilePickerFileType[] choices)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = defaultExtension,
            FileTypeChoices = choices.Length == 0 ? null : choices,
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// 把本地目录转换为平台起始位置。目录为空或不存在时返回 null，
    /// 让各平台使用自己的默认起始目录。
    /// </summary>
    private static async Task<IStorageFolder?> ResolveStartFolderAsync(TopLevel owner, string? startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath))
        {
            return null;
        }

        return await owner.StorageProvider.TryGetFolderFromPathAsync(startPath);
    }
}
