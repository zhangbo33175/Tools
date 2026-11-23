/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.PsdImport.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口 PSD 导入域：源探测、重导入、切片导出、失败回滚与导入日志。
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

    /// <summary>弹出文件选择器导入 PSD / PSB（按钮 <c>BtnImportPsd</c> 入口）。</summary>

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
}

