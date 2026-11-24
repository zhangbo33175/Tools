/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.SaveImport.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口文件域：保存、重载、发布、保存目录、迷雾载入与切片导出。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool;

public partial class MainWindow : Window
{
    #region 保存与导入

    /// <summary>
    /// 默认地图存档目录：从程序目录向上找工程根（含 <c>MapEditorTool.csproj</c> 的那层），
    /// 存在就用 <c>docs\MapJson</c>，找不到工程根时退回程序目录下的 <c>output\MapDatas</c>。
    /// 地图 JSON 与 <c>.fog.json</c> sidecar 都落在这里。
    /// </summary>
    /// <returns>存档目录绝对路径。</returns>
    private static string ResolveDefaultSaveDirectory()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            if (File.Exists(Path.Combine(cursor.FullName, "MapEditorTool.csproj")))
            {
                return Path.Combine(cursor.FullName, "docs", "MapJson");
            }

            cursor = cursor.Parent;
        }

        return EditorPaths.FallbackMapDataDirectory;
    }

    /// <summary>
    /// 烘焙资源根目录 = 地图配置里的「地图保存目录」（<see cref="EditorSettingsData.MapSaveDirectory"/>），
    /// 不再写死在 <c>MapData\Map</c>：用户在配置弹窗里改了目录，切片就落到那个目录。
    /// <para>相对路径按 Unity 口径解析（<c>Assets/…</c> 相对工程根，其余相对 Assets）；
    /// 解析不出时退回 <c>docs\Map</c>（工程根存在）或 <c>output\Map</c>。</para>
    /// <para>真正落盘的是它下面的 <c>Chapter{章节}\Level{关卡}</c>（见 <see cref="ResolveLevelResourceDirectory"/>），
    /// 再按 Unity 的八层目录 Background / Greenery / Road / Buildings / Props / Characters / Foreground / Bubbles 分文件夹，
    /// 整幅底图 <c>Background\bg_{PSD 名}.png</c>、清单 <c>slices.json</c> 留在关卡目录上。</para>
    /// </summary>
    /// <returns>资源根目录绝对路径。</returns>
    private string ResolveResourceDirectory()
    {
        var configured = EditorSettings.ResolveProjectPath(
            EditorSettings.Current.MapSaveDirectory,
            _assets.ProjectRoot);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured!;
        }

        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            if (File.Exists(Path.Combine(cursor.FullName, "MapEditorTool.csproj")))
            {
                return Path.Combine(cursor.FullName, "docs", "Map");
            }

            cursor = cursor.Parent;
        }

        return EditorPaths.FallbackMapDirectory;
    }

    /// <summary>
    /// 当前关卡资源目录：<c>{地图保存目录}\Chapter{章节}\Level{关卡}</c>。
    /// 章节 / 关卡号取配置里的数字部分（<c>ChapterId</c> / <c>SceneId</c>），没有数字时回退 1，
    /// 与 Unity 侧 <c>Res/Textures/Map/Chapter{n}/Level{n}</c> 一一对应。
    /// </summary>
    /// <returns>关卡资源目录绝对路径。</returns>
    private string ResolveLevelResourceDirectory()
    {
        var chapter = NumericOr(_config.ChapterId, "1");
        var level = NumericOr(_config.SceneId, "1");
        return Path.Combine(ResolveResourceDirectory(), $"Chapter{chapter}", $"Level{level}");
    }

    /// <summary>
    /// 把当前关卡的 <c>Chapter{n}\Level{n}</c> 挂成“唯一关卡根”：先摘上一个关卡的根，
    /// 再挂新的。地图 JSON 里的 image 是关卡内相对路径，若多个关卡根同时存在，
    /// 同名切片会按先挂的根命中，切关卡后就会出现“图是另一关的/缺图”。
    /// </summary>
    private void AttachLevelResourceRoot()
    {
        var levelDirectory = ResolveLevelResourceDirectory();
        if (string.Equals(_levelResourceRoot, levelDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // 空关卡刚导出切片时目录才出现：同一个路径也要补挂一次，否则新图仍按 Unity 原图解析
            if (Directory.Exists(levelDirectory)
                && !_assets.Roots.Any(root => string.Equals(root, levelDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                _assets.PrependSearchRoot(levelDirectory);
            }

            return;
        }

        if (_levelResourceRoot is not null)
        {
            _assets.RemoveSearchRoot(_levelResourceRoot);
        }

        _levelResourceRoot = levelDirectory;
        if (Directory.Exists(levelDirectory))
        {
            _assets.PrependSearchRoot(levelDirectory);
        }
    }

    /// <summary>
    /// 切关卡 / 载入新图时复位“待摆放模板 + 摆放模式”：否则上一关选中的
    /// <c>bg_road_5</c> 会留在画布上作为幽灵预览，还会让右栏看起来像有选中对象。
    /// </summary>
    private void ResetPlacementState()
    {
        _pendingTemplate = null;
        Canvas.PendingTemplate = null;
        PlacementToggle.IsChecked = false;
    }

    /// <summary>取字符串里的数字串（用于章节 ID / 关卡 ID 归一），没有数字时返回兜底值。</summary>
    /// <param name="value">原始标识。</param>
    /// <param name="fallback">兜底值。</param>
    /// <returns>纯数字串。</returns>
    private static string NumericOr(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? fallback : digits;
    }

    /// <summary>
    /// 保存当前地图。文件名：选中关卡时 <c>Map_{章节}m{关卡}.json</c>，否则沿用「场景 ID.json」。
    /// </summary>
    /// <returns>true = 已写盘；false = 被空图保护或异常拦下。</returns>
    private bool SaveMap()
    {
        try
        {
            FileSystemIo.EnsureDirectory(_saveDirectory);
            var target = Path.Combine(_saveDirectory, CurrentSaveFileName());

            // 空场景（默认 sceneId=1）直接覆盖已存档会把整张地图抹掉：非空文件 > 4KB 时拦一次
            if (_config.Objects.Count == 0 && File.Exists(target) && new FileInfo(target).Length > 4096)
            {
                SetMessage("当前地图是空的，已拦住覆盖存档");
                SetSaveHint("未保存：空地图会覆盖已有存档", ok: false);
                FlashSaveButton(false);
                return false;
            }

            // PSD 导入只把切片留在内存预览；此时 image 为空，若直接写 JSON，
            // 切关卡 / 重启后会按空路径解析 → 全图退化成图层色块。必须先「导出切片」回写路径。
            var pendingSlices = CountPendingPsdSlices();
            if (pendingSlices > 0)
            {
                SetMessageError($"保存已拦截：{pendingSlices} 个 PSD 图层还没导出切片，image 为空；请先点「导出切片」");
                SetSaveHint("未保存：先把 PSD 切片导出到磁盘", ok: false);
                FlashSaveButton(false);
                return false;
            }

            MapJsonService.Save(_config, target);
            PersistFog();
            _currentMapPath = target;
            BuildChapters();
            var displayName = Path.GetFileName(target);
            SetMessage($"已保存：{displayName}");
            SetSaveHint($"已保存：{displayName} · {DateTime.Now:HH:mm:ss}", ok: true);
            FlashSaveButton(true);
            return true;
        }
        catch (Exception error)
        {
            SetMessage($"保存失败：{error.Message}");
            SetSaveHint($"保存失败：{error.Message}", ok: false);
            FlashSaveButton(false);
            return false;
        }
    }

    /// <summary>
    /// 统计本次 PSD 导入后、仍只有内存预览而尚未写回磁盘路径的物件数。
    /// 这些物件一旦被保存成 JSON，切关卡 / 重启后就会因 image 为空而丢失贴图。
    /// </summary>
    private int CountPendingPsdSlices()
    {
        if (_psdPreview is null)
        {
            return 0;
        }

        var pending = 0;
        foreach (var pair in _psdObjectByLayer)
        {
            if (!_config.Objects.Contains(pair.Value) ||
                !string.IsNullOrWhiteSpace(pair.Value.ImagePath) ||
                !_psdPreview.PngByIndex.ContainsKey(pair.Key))
            {
                continue;
            }

            pending++;
        }

        return pending;
    }

    /// <summary>
    /// 发布当前地图。与「保存」分离：保存写编辑器源存档，发布只写目标工程接入目录。
    /// </summary>
    private async Task PublishMapAsync()
    {
        if (_config.Objects.Count == 0)
        {
            SetMessage("当前地图是空的，没有可发布内容");
            SetSaveHint("未发布：空地图", ok: false);
            return;
        }

        try
        {
            var dialog = new PublishMapWindow(_config, _fogDoc);
            var result = await dialog.ShowDialog<MapPublishResult?>(this);
            if (result is null)
            {
                SetMessage("已取消发布");
                return;
            }

            var targetName = result.Target == MapPublishTarget.UnityJson ? "Unity JSON" : "Cocos Lua";
            SetMessage($"已发布 {targetName}：{result.Files.Count} 个文件 → {result.OutputDirectory}");
            SetSaveHint($"发布清单：{Path.GetFileName(result.ManifestPath)}", neutral: true);

            if (result.Warnings.Count > 0)
            {
                var message = string.Join("\n", result.Warnings);
                await new ConfirmWindow("发布完成（有提示）", message, "知道了", "关闭").ShowDialog<bool>(this);
            }
        }
        catch (Exception error)
        {
            SetMessage($"发布失败：{error.Message}");
            SetSaveHint($"发布失败：{error.Message}", ok: false);
        }
    }

    /// <summary>按绝对路径载入地图 JSON（命令行 <c>--open-map</c> 与关卡切换共用）。</summary>
    /// <param name="path">地图 JSON 绝对路径。</param>
    /// <param name="fitToView">true = 载入后整图居中；切关卡时传 false，由调用方恢复该关卡自己的视口。</param>
    private void LoadMapFile(string path, bool fitToView = true)
    {
        // 切关卡 / 从磁盘重载后，上一关的 PSD 文档不再对应本关的图层序号：
        // 先清会话状态，避免「导出切片」把旧 PSD 烘到新关卡目录
        ResetPsdSession();

        var name = Path.GetFileNameWithoutExtension(path);
        _currentMapPath = path;
        _saveFileOverride = Path.GetFileName(path);
        try
        {
            var loaded = MapJsonLoader.Load(path);
            CloseMapPreview();
            _config = loaded;
            SyncCollectiblePlanFromMap();

            // 载入后把「这张地图自己的 Chapter{n}\Level{n}」挂成唯一关卡根：
            // 先摘掉上一个关卡的根，避免两张关卡有同名切片时按先入为主的根解析到别的图。
            AttachLevelResourceRoot();

            Canvas.Config = _config;
            Canvas.ClearSpriteCache();
            Canvas.Select(null);
            ResetPlacementState();
            LoadFogForScene();
            _fogPanel?.Attach(_config, _fogDoc, Canvas);
            _resourcePanel?.Attach(_templates, _config);
            RefreshSceneFields();
            RefreshStatus();
            RefreshLightBindList();
            SyncProperties();
            if (fitToView)
            {
                Canvas.FitToView();
            }
            SetMessage($"已载入地图：{name}（{_config.Objects.Count} 个物件、{_config.TrafficLights.Count} 盏灯、{_config.Crosswalks.Count} 条横道）");
            SetSaveHint($"当前：{Path.GetFileName(path)}", neutral: true);
        }
        catch (Exception error)
        {
            SetMessageError($"载入失败：{error.Message}");
        }
    }

    /// <summary>
    /// 「更新地图」：从磁盘 JSON 重新加载当前关卡（Unity 原工具同口径）。
    /// <para>
    /// 用途：外部脚本 / 配置表 / PSD 改过磁盘文件后，让编辑器看到最新数据；
    /// 也用于把上一次误操作（例如旧版刷新层级把地表层序改坏）从磁盘还原回来。
    /// 重载会丢弃当前未保存的修改，所以先二次确认；重载后恢复原来的缩放与平移。
    /// </para>
    /// </summary>
    private async Task ReloadMapFromDiskAsync()
    {
        var path = string.IsNullOrWhiteSpace(_currentMapPath)
            ? Path.Combine(_saveDirectory, CurrentSaveFileName())
            : _currentMapPath;
        var fileName = Path.GetFileName(path);
        var psd = ProbePsdSource();

        // 第一优先级：PSD 源文件真的改过（长度或修改时间与上次导入 / slices.json 基线不同）→ 重读 PSD。
        // 这是「更新地图」与「刷新画面」的职责分界：按钮负责源数据，另一个按钮只负责按规则重绘。
        if (psd is { Changed: true })
        {
            var dialog = new ConfirmWindow(
                "更新地图 · 重读 PSD",
                $"检测到 PSD 源文件已修改：\n{psd.Path}\n\n" +
                $"上次导入：{psd.BaselineText}\n当前文件：{psd.CurrentText}\n\n" +
                "将重新解析 PSD，并按最新图层的坐标 / 尺寸 / 旋转 / 八层归属重建当前关卡摆放物。\n" +
                "编辑器里未保存的修改会被覆盖；重读只重建内存数据，JSON 与切片都不会自动改写。\n" +
                "重建后请先点「导出切片」落盘图片，再点「保存」写 JSON。\n是否继续？",
                "重读 PSD",
                "取消");

            if (!await dialog.ShowDialog<bool>(this))
            {
                SetMessage("已取消更新地图（PSD 未重读）");
                return;
            }

            await ReimportPsdForUpdateAsync(psd.Path);
            return;
        }

        // 没有 PSD 变更（或本关还没有 PSD 记录）时，退回原来的 JSON 重载。
        if (!File.Exists(path))
        {
            SetMessageError($"更新失败：找不到 {fileName}，请先点「保存」把当前地图落盘");
            return;
        }

        var reason = psd is null
            ? "当前关卡没有可用的 PSD 源记录"
            : "PSD 源文件长度与修改时间均未变化";
        var jsonDialog = new ConfirmWindow(
            "更新地图",
            $"{reason}，将从磁盘配置文件重新加载地图：\n{path}\n\n" +
            "当前编辑器里未保存的修改（摆放物、层号、宽高调整等）会被磁盘内容覆盖，是否继续？",
            "重新加载",
            "取消");

        if (!await jsonDialog.ShowDialog<bool>(this))
        {
            SetMessage("已取消更新地图");
            return;
        }

        var view = Canvas.ViewState;
        LoadMapFile(path, fitToView: false);
        if (Canvas.Bounds.Width > 0 && Canvas.Bounds.Height > 0)
        {
            Canvas.SetView(view.Zoom, view.Offset);
        }

        RefreshStatus();
        SetMessage($"已从磁盘重新加载：{fileName}（{_config.Objects.Count} 个物件；{reason}）");
        SetSaveHint($"当前：{fileName}", neutral: true);
    }


    /// <summary>载入当前场景的迷雾 sidecar（不存在时清空）。</summary>
    private void LoadFogForScene()
    {
        _fogDoc.Regions.Clear();
        var path = Path.Combine(_saveDirectory, MapFileNames.FogForMap(FogFileName()));
        _fogDoc.SceneId = _config.SceneId;
        if (!File.Exists(path))
        {
            _fogDoc.SceneId = _config.SceneId;
            Canvas.Refresh();
            return;
        }

        try
        {
            var loaded = System.Text.Json.JsonSerializer.Deserialize<MapFogDocument>(FileSystemIo.ReadText(path));
            if (loaded is not null)
            {
                _fogDoc = loaded;
                Canvas.Fog = _fogDoc;
            }
        }
        catch (Exception error)
        {
            SetMessage($"迷雾数据读取失败：{error.Message}");
        }

        Canvas.Refresh();
    }

    /// <summary>选择地图数据输出目录；取消选择时保留原目录。</summary>
    private async Task PickSaveDirectoryAsync()
    {
        var local = await FileDialogService.PickFolderAsync(this, "选择地图数据输出目录");
        if (string.IsNullOrEmpty(local))
        {
            return;
        }

        _saveDirectory = local;
        RefreshStatus();
        SetMessage($"输出目录：{local}");
    }

    /// <summary>
    /// 「导出切片」：把当前导入的 PSD 逐层烘焙到
    /// <c>{地图保存目录}\Chapter{章节}\Level{关卡}\&lt;八层目录&gt;</c>（目录取地图配置里的「地图保存目录」），并回写物件路径 / 尺寸 / 旋转。
    /// 像素相同（含旋转 90/180/270 后相同）只保留一张图，其余图层复用同一文件、靠 rotation 还原。
    /// </summary>
    private void ExportSlices()
    {
        if (_psdDocument is null || string.IsNullOrWhiteSpace(_psdSourcePath))
        {
            SetMessage("导出切片：请先导入 PSD —— 切片按 PSD 图层像素裁切，不依赖已有图片");
            return;
        }

        var levelDirectory = ResolveLevelResourceDirectory();
        var layerKeys = CollectImportLayerKeys(_psdDocument, _psdManifest);
        PsdSliceSet slices;
        try
        {
            slices = PsdSliceBaker.EnsureSlices(_psdDocument, _psdSourcePath, levelDirectory,
                layer => layerKeys.TryGetValue(layer.Index, out var key) ? key : MapLayerKeys.Prop, force: true);
        }
        catch (Exception error)
        {
            SetMessage($"导出切片失败：{error.Message}");
            return;
        }

        // 整幅底图：与切片同一次导出落 Background\bg_{PSD 名}.png
        if (_psdBackgroundLayers.Count > 0)
        {
            var composite = PsdSliceBaker.BakeComposite(_psdDocument, _psdSourcePath, _psdBackgroundLayers,
                levelDirectory, force: true);
            if (composite is not null)
            {
                _config.BgName = Path.GetFileNameWithoutExtension(composite);
            }
        }

        // 回写物件：落盘路径 + 资源名 + 位置 + 尺寸 + 旋转（旋转复用的图层靠 rotation 还原成原像素朝向）
        foreach (var pair in _psdObjectByLayer)
        {
            var record = slices.RecordFor(pair.Key);
            if (record is null)
            {
                continue;
            }

            var instance = pair.Value;
            instance.ImagePath = record.File;
            instance.Name = Path.GetFileNameWithoutExtension(record.File.Replace('\\', '/'));
            instance.X = record.Left;
            instance.Y = record.Top;
            instance.PixelWidth = record.Width;
            instance.PixelHeight = record.Height;
            instance.Width = Math.Max(1, (int)Math.Ceiling(record.Width / (double)MapToolDefaults.GridSize));
            instance.Height = Math.Max(1, (int)Math.Ceiling(record.Height / (double)MapToolDefaults.GridSize));
            instance.Rotation = record.Rotation;
            instance.FlipX = false;
        }

        // 落盘后画布改走磁盘资源：登记关卡目录 → 清各类缓存 → 丢掉内存预览位图
        _assets.PrependSearchRoot(levelDirectory);
        _assets.ClearCache();
        Canvas.ClearRuntimeSprites();
        Canvas.Refresh();
        SyncProperties();

        var distinct = slices.Records.Values.Select(record => record.File)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var detail = string.Join("　", new[]
        {
            slices.Records.Count == 0 ? "没有可导出的图层" : $"出图 {distinct} 张 / 图层 {slices.Records.Count}",
            slices.BakedCount == 0 ? string.Empty : $"新写 {slices.BakedCount}",
            slices.DuplicateCount == 0 ? string.Empty : $"同图合并 {slices.DuplicateCount}",
            slices.RotationDuplicateCount == 0 ? string.Empty : $"旋转复用 {slices.RotationDuplicateCount}",
            slices.PrunedCount == 0 ? string.Empty : $"清掉旧残留 {slices.PrunedCount}",
            slices.FailedCount == 0 ? string.Empty : $"解码失败 {slices.FailedCount}",
            $"目录 {Path.GetRelativePath(ResolveResourceDirectory(), slices.Directory)}",
        }.Where(text => text.Length > 0));
        SetMessage($"导出切片完成　{detail}".TrimEnd('　'));
    }
    #endregion
}
