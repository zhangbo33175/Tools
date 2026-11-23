/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.SaveImport.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口文件域：保存、重载、PSD 导入/切片导出与发布。
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

        return Path.Combine(AppContext.BaseDirectory, "output", "MapDatas");
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

        return Path.Combine(AppContext.BaseDirectory, "output", "Map");
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
            Directory.CreateDirectory(_saveDirectory);
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

    /// <summary>
    /// PSD 源文件探测结果。<see cref="Changed"/> 为 true 时「更新地图」走重读 PSD 分支。
    /// </summary>
    private sealed record PsdSourceProbe(
        string Path,
        bool Changed,
        string BaselineText,
        string CurrentText);

    /// <summary>
    /// 找出当前关卡对应的 PSD 源文件，并和「上次导入戳 / slices.json 基线」比对。
    /// 优先用本次会话导入时记下的文件戳；重启后没有会话状态时退回切片清单里的 source/length/ticks。
    /// </summary>
    private PsdSourceProbe? ProbePsdSource()
    {
        var sourcePath = _psdSourcePath;
        var baselineLength = _psdImportLength;
        var baselineTicks = _psdImportTicks;

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            var levelDirectory = ResolveLevelResourceDirectory();
            if (PsdSliceBaker.TryReadSourceStamp(levelDirectory, out var manifestSource, out var manifestLength, out var manifestTicks))
            {
                sourcePath = manifestSource;
                baselineLength = manifestLength;
                baselineTicks = manifestTicks;
            }
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var info = new FileInfo(sourcePath);
        var currentLength = info.Length;
        var currentTicks = File.GetLastWriteTimeUtc(sourcePath).Ticks;
        var hasBaseline = baselineLength > 0 && baselineTicks > 0;
        var changed = !hasBaseline || currentLength != baselineLength || currentTicks != baselineTicks;

        var baselineText = hasBaseline
            ? $"{baselineLength:N0} B · {new DateTime(baselineTicks, DateTimeKind.Utc).ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}"
            : "无（清单 / 会话里没有导入戳）";
        var currentText = $"{currentLength:N0} B · {info.LastWriteTime:yyyy-MM-dd HH:mm:ss.fff}";

        return new PsdSourceProbe(sourcePath, changed, baselineText, currentText);
    }

    /// <summary>
    /// 「更新地图」的 PSD 分支：解析最新 PSD → 清空旧摆放物 → 重建 → 保持当前缩放 / 平移。
    /// 解析失败时不破坏当前画面；成功但一个可见图层都没导入时也回退到磁盘 JSON。
    /// </summary>
    private async Task ReimportPsdForUpdateAsync(string psdPath)
    {
        var jsonPath = string.IsNullOrWhiteSpace(_currentMapPath)
            ? Path.Combine(_saveDirectory, CurrentSaveFileName())
            : _currentMapPath;
        var view = Canvas.ViewState;
        var sceneName = _config.SceneName;
        var sceneId = _config.SceneId;
        var chapterId = _config.ChapterId;
        var chapterName = _config.ChapterName;
        var backupObjects = _config.Objects.ToList();
        var cleared = false;

        SetMessage($"正在重新解析 PSD：{Path.GetFileName(psdPath)}…");
        BtnUpdateMap.IsEnabled = false;
        try
        {
            var document = await Task.Run(() => PsdReader.Read(psdPath));
            if (document.Layers.Count == 0)
            {
                SetMessageError("PSD 重读失败：没有解析到任何图层，画面保持原样");
                return;
            }

            // 重建前先清空旧实例：ImportPsdDocument 是「追加」语义，直接调用会把新旧图层叠成两份
            _config.Objects.Clear();
            cleared = true;
            _psdObjectByLayer.Clear();
            _psdPreview = null;
            Canvas.ClearRuntimeSprites();
            Canvas.Config = _config; // 同时清掉选中物与贴图缓存

            var imported = ImportPsdDocument(document, psdPath);
            if (imported == 0)
            {
                SetMessageError("PSD 重读完成，但没有可见图层；已回退到磁盘 JSON");
                if (File.Exists(jsonPath))
                {
                    LoadMapFile(jsonPath, fitToView: false);
                    Canvas.SetView(view.Zoom, view.Offset);
                    RefreshStatus();
                }
                else
                {
                    RestoreObjectsAfterFailedImport(backupObjects);
                }

                return;
            }

            // 场景身份以当前关卡为准，PSD 只负责摆放物 / 底图，不改章节与关卡号
            _config.SceneName = sceneName;
            _config.SceneId = sceneId;
            _config.ChapterId = chapterId;
            _config.ChapterName = chapterName;

            if (Canvas.Bounds.Width > 0 && Canvas.Bounds.Height > 0)
            {
                Canvas.SetView(view.Zoom, view.Offset);
            }

            RefreshSceneFields();
            RefreshStatus();
            SyncProperties();
            SetSaveHint($"PSD 已更新：{Path.GetFileName(psdPath)}（{imported} 层，未保存）", neutral: true);
            SetMessage($"已按最新 PSD 重建：{imported} 层 → {_config.Objects.Count} 实例；先「导出切片」，再「保存」");
        }
        catch (Exception error)
        {
            if (cleared)
            {
                RestoreObjectsAfterFailedImport(backupObjects);
            }

            SetMessageError($"PSD 重读失败：{error.Message}；已恢复重读前的摆放物");
        }
        finally
        {
            BtnUpdateMap.IsEnabled = true;
        }
    }

    /// <summary>PSD 重建中途失败时，把清空前抓下来的实例表放回去，并切回磁盘贴图解析。</summary>
    /// <param name="backupObjects">重读前当前关卡的实例快照。</param>
    private void RestoreObjectsAfterFailedImport(List<PlacedObject> backupObjects)
    {
        ResetPsdSession();
        _config.Objects.Clear();
        _config.Objects.AddRange(backupObjects);
        Canvas.Config = _config;
        Canvas.ClearSpriteCache();
        Canvas.Refresh();
        RefreshStatus();
        SyncProperties();
    }

    /// <summary>载入当前场景的迷雾 sidecar（不存在时清空）。</summary>
    private void LoadFogForScene()
    {
        _fogDoc.Regions.Clear();
        var path = Path.Combine(_saveDirectory, FogFileName() + ".fog.json");
        _fogDoc.SceneId = _config.SceneId;
        if (!File.Exists(path))
        {
            _fogDoc.SceneId = _config.SceneId;
            Canvas.Refresh();
            return;
        }

        try
        {
            var loaded = System.Text.Json.JsonSerializer.Deserialize<MapFogDocument>(File.ReadAllText(path));
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

    private async Task PickSaveDirectoryAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择地图数据输出目录",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return;
        }

        var local = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(local))
        {
            return;
        }

        _saveDirectory = local;
        RefreshStatus();
        SetMessage($"输出目录：{local}");
    }

    /// <summary>弹出文件选择器导入 PSD / PSB（按钮 <c>BtnImportPsd</code> 入口）。</summary>
    /// <summary>
    /// 清掉与某一关 PSD 绑定的会话状态（文档、源路径、内存预览、图层→物件映射）。
    /// 换关卡 / 从 JSON 重载后必须调用，否则「导出切片」会拿上一关的 PSD 去写新关卡目录。
    /// </summary>
    private void ResetPsdSession()
    {
        _psdDocument = null;
        _psdSourcePath = string.Empty;
        _psdImportLength = 0;
        _psdImportTicks = 0;
        _psdManifest = null;
        _psdBackgroundLayers = new List<PsdLayerInfo>();
        _psdObjectByLayer.Clear();
        _psdPreview = null;
        Canvas.ClearRuntimeSprites();
    }

    /// <summary>「添加收集」：两步向导（数量 → 素材），完成后打开收集物浮窗。</summary>
    private async Task AddCollectibleAsync()
    {
        var candidates = BuildCollectibleCandidates();
        if (candidates.Count == 0)
        {
            SetMessageError("没有可用作收集物的物件表素材：请先在「物品元件编辑」里配置物件素材。");
            return;
        }

        var wizard = new CollectibleWizardWindow(_config, candidates, _config.Collectibles, _assets);
        bool confirmed;
        try
        {
            confirmed = await wizard.ShowDialog<bool>(this);
        }
        catch (Exception error)
        {
            SetMessageError($"打开添加收集向导失败：{error.Message}");
            return;
        }

        if (!confirmed)
        {
            return;
        }

        _config.Collectibles.Total = wizard.TotalCount;
        _config.Collectibles.LayerMode = wizard.LayerMode;
        _config.Collectibles.Kinds = wizard.SelectedTemplates
            .Select(CollectibleKindData.FromTemplate)
            .ToList();

        _pendingTemplate = null;
        Canvas.PendingTemplate = null;
        Canvas.PendingCollectibleTemplate = null;
        // 新建计划时确保收集物层处于可见状态：即使用户之前关过该层，也能立刻看到摆放结果
        if (LayerCollectible.IsChecked != true)
        {
            LayerCollectible.IsChecked = true;
        }

        Canvas.HiddenLayers.Remove(MapLayerKeys.Collectible);
        SyncCollectibleEntry();
        ShowCollectiblePanel(CollectibleDockSide.None);
        SetMessage($"收集物计划已建立：目标 {_config.Collectibles.Total} 个、{_config.Collectibles.Kinds.Count} 种素材、{CollectibleLayers.DisplayName(_config.Collectibles.LayerMode)}。可在右侧收集物面板拖放或随机生成。");
    }

    /// <summary>收集物候选池：优先用物件表（与资源列表同源），没有表时退回模板库里的 Prop。</summary>
    /// <returns>去重后的模板列表。</returns>
    private List<ObjectTemplate> BuildCollectibleCandidates()
    {
        var table = _itemTable?.FileForCategory(ObjectTypeNames.Of(MapObjectType.Prop));
        var candidates = table is null
            ? _templates.Where(template => template.Type == MapObjectType.Prop).ToList()
            : table.Rows.Select(row => BuildTablePaletteTemplate(row, MapObjectType.Prop)).ToList();

        return candidates
            .Where(template => !string.IsNullOrWhiteSpace(template.TemplateId))
            .GroupBy(template => template.TemplateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private async Task ImportPsdAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 PSD / PSB 文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Photoshop 文档")
                {
                    Patterns = new[] { "*.psd", "*.psb" },
                },
            },
        });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            SetMessage("无法读取所选文件路径");
            return;
        }

        try
        {
            var document = PsdReader.Read(path);
            ImportPsdDocument(document, path);
            Canvas.FitToView();
            RefreshStatus();
        }
        catch (Exception error)
        {
            SetMessage($"PSD 解析失败：{error.Message}");
        }
    }

    /// <summary>按绝对路径导入 PSD（命令行 <c>--import-psd</c> 用于批处理与回归自测）。</summary>
    /// <param name="path">PSD / PSB 文件绝对路径。</param>
    private void TryImportPsd(string path)
    {
        try
        {
            var document = PsdReader.Read(path);
            ImportPsdDocument(document, path);
            RefreshStatus();
        }
        catch (Exception error)
        {
            SetMessage($"PSD 解析失败：{error.Message}");
        }
    }

    /// <summary>
    /// 把 PSD 图层结构导入当前地图：每层换算成一个 <see cref="PlacedObject"/>。
    /// 优先级：Unity 侧 <c>psd.json</c> 清单（权威层级 + 切片图）→ 层名关键词分类 → 包围盒形状启发，
    /// 贴图按「清单 pngPath → 同目录同名 PNG → 资源库索引」逐层查找。
    /// </summary>
    /// <param name="document">已解析的 PSD 文档。</param>
    /// <param name="psdPath">PSD 源文件路径（用于定位清单、同目录切片图与写诊断日志）。</param>
    /// <returns>成功导入的图层数。</returns>
    private int ImportPsdDocument(PsdDocument document, string psdPath)
    {
        var directory = Path.GetDirectoryName(psdPath) ?? string.Empty;
        var manifest = PsdManifest.TryLoad(psdPath);
        int imported = 0;
        var missing = new List<string>();
        var report = new List<string>();

        // 导入阶段只做「内存预览」：逐层解码、裁透明边、按像素 + 旋转去重，产出 PNG 字节直接喂给画布，
        // 不建目录、不落任何图片；真正写盘只发生在「导出切片」按钮（见 ExportSlices）。
        // 章节归属跟随左侧章节树：空白 / 默认章节时取当前选中的章节，避免切片落到 Chapter1
        // 而存档里留下 DefaultChapter（重开时章节号对不上，只能靠文件名索引兜底）
        if ((string.IsNullOrWhiteSpace(_config.ChapterId) || _config.ChapterId == "DefaultChapter")
            && !string.IsNullOrWhiteSpace(_selectedChapterId))
        {
            _config.ChapterId = _selectedChapterId;
        }

        var levelDirectory = ResolveLevelResourceDirectory();
        var layerKeys = CollectImportLayerKeys(document, manifest);
        var slices = PsdSliceBaker.BuildPreview(document, psdPath, levelDirectory,
            layer => layerKeys.TryGetValue(layer.Index, out var key) ? key : MapLayerKeys.Prop);

        // 本次导入的会话状态：导出按钮据此重新解码 + 回写物件
        _psdDocument = document;
        _psdSourcePath = psdPath;
        try
        {
            var sourceInfo = new FileInfo(psdPath);
            _psdImportLength = sourceInfo.Length;
            _psdImportTicks = File.GetLastWriteTimeUtc(psdPath).Ticks;
        }
        catch (Exception)
        {
            _psdImportLength = 0;
            _psdImportTicks = 0;
        }

        _psdManifest = manifest;
        _psdPreview = slices;
        _psdBackgroundLayers = new List<PsdLayerInfo>();
        _psdObjectByLayer.Clear();

        var backgroundLayers = new List<PsdLayerInfo>();

        // 新 PSD 不一定还有整幅底图层：旧 BgName 指向上一份画稿的合成图，
        // 不清掉会把旧地图整张当成底图继续显示（尤其从一份 PSD 换到另一份时）。
        _config.BgName = string.Empty;

        if (document.Width > 0 && document.Height > 0)
        {
            _config.Width = document.Width;
            _config.Height = document.Height;
        }

        foreach (var layer in document.Layers)
        {
            if (layer.IsGroup || !layer.Visible || !layer.HasBounds || layer.Opacity <= 0.02)
            {
                continue;
            }

            // 整幅铺满的图层属于底图/地块层，Unity 版烘焙进合成背景，不拆成独立物件
            if (PsdLayerRules.IsFullCanvasLayer(layer, document))
            {
                backgroundLayers.Add(layer);

                if (string.IsNullOrWhiteSpace(_config.BgName) || _config.BgName == "bg_grass")
                {
                    _config.BgName = layer.Name;
                }

                report.Add(FormatImportRow(layer, layer.Name, MapLayerKeys.Background, "(整幅图层，并入底图)"));
                continue;
            }

            // 清单命中时用 Unity 侧判定结果（道路层不会被图层名坑成道具/前景）
            var entry = manifest?.Find(layer);

            // Photoshop 默认名常带「拷贝 / 副本 / (1)」后缀，先归一化再做分类与取图
            var cleanName = PsdLayerRules.NormalizeLayerName(layer.Name);
            var layerKey = entry is { LayerType.Length: > 0 } ? PsdLayerRules.NormalizeLayerType(entry.LayerType) : PsdLayerRules.ResolveLayerKey(layer, cleanName);
            var type = MapObjectType.Prop;
            if (layerKey == MapLayerKeys.Building)
            {
                type = MapObjectType.Building;
            }
            else if (layerKey == MapLayerKeys.Character)
            {
                type = MapObjectType.Character;
            }

            // 清单判定「并入底图」的背景地块：只有铺满画布的大底图层才压平进 bg_composite，
            // 普通地块（沙滩 / 海面 / 岩石 / 停车位…）保留成地面实例，否则它们与地面贴图之间的
            // 层序会被烘焙抹平（实测整图偏离原画 0.16%，只压平全幅层后 0.001%）
            if (layerKey == MapLayerKeys.Background && entry is { Decision: "background" } && PsdLayerRules.IsFullCanvasLayer(layer, document))
            {
                backgroundLayers.Add(layer);
                report.Add(FormatImportRow(layer, cleanName, layerKey, "(并入底图)"));
                continue;
            }

            var sliceRecord = slices.RecordFor(layer.Index);
            if (sliceRecord is null)
            {
                missing.Add(cleanName);
            }

            // 导入不落盘：image 留空，画布用内存预览出图；点「导出切片」后才回写相对路径
            var imagePath = sliceRecord?.File;
            report.Add(FormatImportRow(layer, cleanName, layerKey,
                imagePath is null ? "(无像素，仅占位)" : imagePath + "（内存预览）"));

            // 放置层的四边形尺寸才是 PSD 里的显示尺寸：90° 旋转块的图层框宽高互换，
            // 直接用框会把竖路画成横条；但嵌入大画布的智能对象 quad 会虚高几十倍，需要校验后才采信
            PsdLayerRules.ResolveDisplayPixels(layer, entry, out var pixelWidth, out var pixelHeight);
            var rotation = entry?.Rotation ?? 0.0;
            var flipX = entry?.FlipX ?? false;

            if (sliceRecord is not null)
            {
                // 自裁切片就是「图层自身像素裁掉透明边之后」的图：PSD 存盘时已经把旋转/镜像烘进了像素里，
                // 再按清单转一次等于翻两倍，所以变换回到裁剪后的原始状态；
                // 只有「旋转后与已有图相同」而复用同一张 PNG 的图层，才用 rotation 还原（0/90/180/270）
                pixelWidth = sliceRecord.Width;
                pixelHeight = sliceRecord.Height;
                rotation = sliceRecord.Rotation;
                flipX = false;
            }

            // Unity 里物件的 Name 就是贴图资源名（不含扩展名），这里也用切片文件主干，
            // 保证「物体名 ↔ 贴图文件」一一对应，后续换机器 / 换目录还能按三级目录重新命中
            var resourceName = sliceRecord is null
                ? cleanName
                : Path.GetFileNameWithoutExtension(sliceRecord.File.Replace('\\', '/'));

            var instance = new PlacedObject
            {
                TemplateId = layer.FullPath,
                Name = string.IsNullOrWhiteSpace(resourceName) ? cleanName : resourceName,
                Type = type,
                LayerType = layerKey,
                X = sliceRecord?.Left ?? layer.Left,
                Y = sliceRecord?.Top ?? layer.Top,
                PixelWidth = pixelWidth,
                PixelHeight = pixelHeight,
                Width = Math.Max(1, (int)Math.Ceiling(pixelWidth / MapToolDefaults.GridSize)),
                Height = Math.Max(1, (int)Math.Ceiling(pixelHeight / MapToolDefaults.GridSize)),
                Stretch = false,
                Rotation = rotation,
                FlipX = flipX,
                ImagePath = string.Empty,
                SortOrder = LayerOrder.Base(layerKey),
                AnimName = MapToolDefaults.DefaultAnimName,
            };

            _config.Objects.Add(instance);

            // 背景/道路组里也有独立的树、灌木、围栏层：它们不是整幅底图，不能被地表带永远压住。
            // 分带策略统一走 OcclusionAdjustmentStrategy，和「刷新画面 / 刷新层级」保持同一套判定。
            OcclusionAdjustmentStrategy.TryPromoteBackgroundStanding(instance);

            _psdObjectByLayer[layer.Index] = instance;
            imported++;
        }

        // 背景地块 + 整幅图层只在内存里合成一张底图，直接给画布预览；不写 Background\bg_*.png，
        // 真正落盘同样等「导出切片」
        _psdBackgroundLayers = backgroundLayers;
        if (backgroundLayers.Count > 0)
        {
            var composite = PsdSliceBaker.BuildCompositePng(document, psdPath, backgroundLayers);
            if (composite is not null)
            {
                var psdStem = Path.GetFileNameWithoutExtension(psdPath);
                if (!string.IsNullOrWhiteSpace(psdStem))
                {
                    _config.BgName = "bg_" + psdStem;
                }

                slices.CompositePng = composite.Value.Png;
                slices.CompositeWidth = composite.Value.Width;
                slices.CompositeHeight = composite.Value.Height;
                Canvas.SetRuntimeBackground(ToBitmap(composite.Value.Png));
            }
        }

        // 内存预览贴图：按图层序号找到刚建的物件，把 PNG 字节解码成位图交给画布
        foreach (var pair in slices.PngByIndex)
        {
            if (_psdObjectByLayer.TryGetValue(pair.Key, out var instance2))
            {
                Canvas.SetRuntimeSprite(instance2.Id, ToBitmap(pair.Value));
            }
        }


        // 烘焙进地图后：画面前后由「遮挡带 + 脚底 Y」决定，排序时不再看 PSD 层序；
        // sortOrder 只是给 Unity 的数据（这里按带内 PSD 层序写，想换成脚底 Y 就点「按画面重排」）
        var bands = TemplateCatalog.RefreshSortOrders(_config, byPicture: false);
        _assets.ClearCache();
        Canvas.ClearSpriteCache();
        Canvas.Refresh();
        SyncProperties();
        WritePsdImportLog(psdPath, document, report);
        ReportPsdImport(document, imported, missing, slices, bands);
        return imported;
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

    /// <summary>把 PNG 字节解码成位图（导入预览用，不落盘）。</summary>
    /// <param name="png">PNG 文件字节。</param>
    /// <returns>位图。</returns>
    private static Bitmap ToBitmap(byte[] png)
    {
        using var stream = new MemoryStream(png);
        return new Bitmap(stream);
    }

    /// <summary>
    /// 导入前先把每层判成八层键，交给 <see cref="PsdSliceBaker.BuildPreview"/> 决定落哪个目录；
    /// 判定口径与下面的导入循环完全一致（清单 <c>psd.json</c> 优先 → 层名/前缀关键词）。
    /// 返回 <c>null</c> 表示该层是整幅铺满层，压平进整幅底图、不出切片。
    /// </summary>
    /// <param name="document">已解析的 PSD 文档。</param>
    /// <param name="manifest">Unity 侧 <c>psd.json</c> 清单（可空）。</param>
    /// <returns>图层序号 → 八层键（null = 并入底图）。</returns>
    private Dictionary<int, string?> CollectImportLayerKeys(PsdDocument document, PsdManifest? manifest)
    {
        var keys = new Dictionary<int, string?>();
        foreach (var layer in document.Layers)
        {
            if (layer.IsGroup || !layer.HasBounds)
            {
                continue;
            }

            if (PsdLayerRules.IsFullCanvasLayer(layer, document))
            {
                keys[layer.Index] = null;
                continue;
            }

            var entry = manifest?.Find(layer);
            var cleanName = PsdLayerRules.NormalizeLayerName(layer.Name);
            keys[layer.Index] = entry is { LayerType.Length: > 0 }
                ? PsdLayerRules.NormalizeLayerType(entry.LayerType)
                : PsdLayerRules.ResolveLayerKey(layer, cleanName);
        }

        return keys;
    }

    /// <summary>PSD 诊断日志的一行：序号、原始名、归一化名、父组、包围盒、判定层级、贴图。</summary>
    private static string FormatImportRow(PsdLayerInfo layer, string name, string layerKey, string image)
    {
        var group = layer.GroupPath.Length == 0 ? "-" : layer.GroupPath;
        return string.Join('\t', layer.Index.ToString(CultureInfo.InvariantCulture), layer.Name, name, group,
            $"{layer.Left},{layer.Top},{layer.Width}x{layer.Height}", layerKey, image);
    }

    /// <summary>
    /// 把本次导入的逐层判定结果写到 <c>output/Logs/psd-import.log</c>：
    /// 图层名往往看不出问题，靠这份清单能直接看到「哪层判成了哪种层级、贴图找到没有」。
    /// </summary>
    private static void WritePsdImportLog(string psdPath, PsdDocument document, IReadOnlyList<string> rows)
    {
        try
        {
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "output", "Logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "psd-import.log");

            var text = new System.Text.StringBuilder();
            text.AppendLine("// (c) copyright 2026 - 2030, Honor.Runtime");
            text.AppendLine($"// PSD 导入诊断 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine($"// 文件：{psdPath}");
            text.AppendLine($"// 画布：{document.Width}×{document.Height}　通道 {document.Channels}　位深 {document.Depth}　颜色模式 {document.ColorMode}");
            text.AppendLine("// 序号\t原始层名\t归一化名\t父组\t(left,top,w×h)\t判定层级\t贴图");
            foreach (var row in rows)
            {
                text.AppendLine(row);
            }

            File.WriteAllText(logPath, text.ToString(), new System.Text.UTF8Encoding(false));
        }
        catch (IOException)
        {
            // 日志写失败不影响导入，静默忽略
        }
    }

    /// <summary>
    /// 导入结束的提示：「画布尺寸 + 图层数 + 缺像素数 + 内存预览切片数 + 去重统计 + 提示未落盘」。
    /// </summary>
    private void ReportPsdImport(PsdDocument document, int imported, IReadOnlyList<string> missing, PsdSliceSet slices, int[] bands)
    {
        var head = $"PSD {document.Width}×{document.Height} 导入 {imported} 层";
        var tail = missing.Count == 0 ? "像素齐全" : $"缺像素 {missing.Count} 层";

        var trimmed = slices.Records.Values.Count(record => record.Trimmed);
        var trimText = trimmed == 0 ? "无需裁边" : $"裁掉透明边 {trimmed} 张";

        var distinct = slices.Records.Values.Select(record => record.File).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var dedupe = slices.DuplicateCount == 0 ? string.Empty : $"同图合并 {slices.DuplicateCount}";
        var rotated = slices.RotationDuplicateCount == 0 ? string.Empty : $"其中旋转复用 {slices.RotationDuplicateCount}";
        var unique = slices.Records.Count == 0 ? string.Empty : $"预览出图 {distinct} 张（内存 {slices.PngByIndex.Count} 份）";
        var layering = bands.Length >= 3
            ? $"遮挡 地表 {bands[0]} / 立体 {bands[1]} / 叠加 {bands[2]}"
            : string.Empty;
        var target = $"未落盘：点「导出切片」写到 {Path.GetRelativePath(ResolveResourceDirectory(), slices.Directory)}";
        var detail = string.Join("　", new[] { unique, dedupe, rotated, trimText, target, layering }
            .Where(text => text.Length > 0));
        SetMessage($"{head}　{tail}　{detail}".TrimEnd('　'));
    }

    #endregion
}
