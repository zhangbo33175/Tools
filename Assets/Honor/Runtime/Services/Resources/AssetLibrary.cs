/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetLibrary.cs
 * author:    云毅
 * created:   2026
 * descrip:   本地资源索引：把地图/模板里的 image 字段解析成 PNG，并缓存位图
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;

namespace MapEditorTool.Services;

/// <summary>
/// 本地资源索引。资源根目录取自 <c>output\Config\resRoots.txt</c>（每行一个目录或 <c>project=目录</c>），
/// 缺省时使用程序目录下的 <c>output\Res</c>、<c>output\Resources</c>。
/// <para>地图 JSON 里的 <c>image</c> 是 Unity 工程内的相对路径（如 <c>Assets/Res/Textures/Map/.../bg_1.png</c>），
/// 这里按「工程根 + 相对路径」优先解析，其次按文件名在资源根里查找，最后回退到同名文件。</para>
/// </summary>
public sealed class AssetLibrary
{
    #region 常量与字段

    private const string RootsFileName = "resRoots.txt";

    /// <summary>位图缓存上限：超过后按「先进先出」淘汰最旧的四分之一，不再整体清空重解码。</summary>
    private const int BitmapCacheLimit = 1200;

    private readonly string _configDirectory;

    private readonly Dictionary<string, Bitmap> _bitmaps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>位图写入顺序（用于 LRU 淘汰）。</summary>
    private readonly Queue<string> _bitmapOrder = new();

    /// <summary>「资源名 → 绝对路径」解析缓存：同一帧内 500+ 物件反复查同一个名字不再走磁盘。</summary>
    private readonly Dictionary<string, string> _pathCache = new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, string>? _index;

    #endregion

    #region 构造

    /// <summary>初始化资源索引。</summary>
    /// <param name="configDirectory">配置目录（<c>output\Config</c>）。</param>
    public AssetLibrary(string configDirectory)
    {
        _configDirectory = configDirectory;
        LoadRoots();
    }

    #endregion

    #region 属性

    /// <summary>资源根目录集合（绝对路径）。</summary>
    public IReadOnlyList<string> Roots { get; private set; } = Array.Empty<string>();

    /// <summary>Unity 工程根（用于解析 <c>Assets/…</c> 相对路径），未配置时为 null。</summary>
    public string? ProjectRoot { get; private set; }

    #endregion

    #region 公开方法

    /// <summary>按资源名（不含扩展名）在资源根目录里找图片。</summary>
    /// <param name="name">资源名、相对路径或工程内路径。</param>
    /// <returns>命中的绝对路径；找不到返回 null。</returns>
    public string? FindImage(string name) => Resolve(name);

    /// <summary>解析任意 image 字段为绝对路径（带「名字 → 路径」缓存，供画布逐帧调用）。</summary>
    /// <param name="name">资源名、相对路径或工程内路径。</param>
    /// <returns>存在的文件路径；找不到返回 null。</returns>
    public string? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Replace('\\', '/').Trim();
        if (_pathCache.TryGetValue(trimmed, out var cached))
        {
            return cached.Length == 0 ? null : cached;
        }

        var resolved = ResolveCore(trimmed);
        _pathCache[trimmed] = resolved ?? string.Empty;
        return resolved;
    }

    /// <summary>真正的解析逻辑：绝对路径 → 工程内相对路径 → 文件名索引 → 资源根同名。</summary>
    /// <param name="trimmed">已把反斜杠换成斜杠并去空白后的路径。</param>
    /// <returns>命中路径；找不到返回 null。</returns>
    private string? ResolveCore(string trimmed)
    {

        // 1) 本身就是绝对路径
        if (Path.IsPathRooted(trimmed) && File.Exists(trimmed))
        {
            return trimmed;
        }

        // 2) 工程内相对路径：工程根 / Assets/… 或任意资源根 / 相对路径
        var relative = trimmed.StartsWith("./", StringComparison.Ordinal) ? trimmed[2..] : trimmed;
        foreach (var candidate in PathCandidates(relative))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 3) 按文件名索引查找（忽略扩展名与大小写）
        var stem = Path.GetFileNameWithoutExtension(trimmed);
        if (stem.Length == 0)
        {
            return null;
        }

        EnsureIndex();
        if (_index!.TryGetValue(stem, out var hit))
        {
            return hit;
        }

        // 4) 保底：资源根下直接同名
        foreach (var root in Roots)
        {
            foreach (var extension in new[] { ".png", ".jpg", ".jpeg" })
            {
                var candidate = Path.Combine(root, stem + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>加载并缓存位图。</summary>
    /// <param name="name">资源名、相对路径或工程内路径。</param>
    /// <returns>位图；找不到或解码失败返回 null。</returns>
    public Bitmap? Load(string name)
    {
        var path = Resolve(name);
        if (path is null)
        {
            return null;
        }

        if (_bitmaps.TryGetValue(path, out var cached))
        {
            return cached;
        }

        // 超过上限时只淘汰最旧的一批（先进先出），保留刚用到的图，避免整表清空后每帧重新解码
        if (_bitmaps.Count >= BitmapCacheLimit)
        {
            var target = BitmapCacheLimit / 4 * 3;
            while (_bitmaps.Count > target && _bitmapOrder.Count > 0)
            {
                var oldest = _bitmapOrder.Dequeue();
                if (_bitmaps.Remove(oldest))
                {
                    // 只丢掉引用，交给 GC 回收像素
                }
            }
        }

        try
        {
            var bitmap = new Bitmap(path);
            _bitmaps[path] = bitmap;
            _bitmapOrder.Enqueue(path);
            return bitmap;
        }
        catch (Exception)
        {
            // 图片损坏或格式不支持时静默回退到色块，不打断绘制
            return null;
        }
    }

    /// <summary>
    /// 按 Unity 的命名关联加载：<c>Name + 层键 + 章节 / 关卡</c> → 本关目录 → 章节 Shared → 全局 Shared → 旧单数目录。
    /// 用于 <c>image</code> 为空（Unity 导出的 JSON 只存资源名）时的兜底。
    /// </summary>
    /// <param name="name">资源名（物件 Name）。</param>
    /// <param name="layerType">归属层键（旧键会归一）。</param>
    /// <param name="chapterId">章节 ID，可空。</param>
    /// <param name="levelId">关卡 ID，可空。</param>
    /// <returns>位图；找不到返回 null。</returns>
    public Bitmap? LoadByLayer(string? name, string? layerType, string? chapterId, string? levelId)
    {
        var path = MapTextureResolver.Resolve(ProjectRoot, name, layerType, chapterId, levelId);
        return path is null ? null : Load(path);
    }

    /// <summary>清空位图缓存、路径解析缓存与索引（资源根变更 / 新烘焙落盘后调用）。</summary>
    public void ClearCache()
    {
        _bitmaps.Clear();
        _bitmapOrder.Clear();
        _pathCache.Clear();
        _index = null;
    }

    /// <summary>追加运行时搜索根（地图配置里的预览图目录 / 分区背景图目录），重复与不存在的目录自动跳过。</summary>
    /// <param name="directories">候选目录的绝对路径。</param>
    public void AddSearchRoots(IEnumerable<string> directories)
    {
        var roots = Roots.ToList();
        var changed = false;
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            if (roots.Any(existing => string.Equals(existing, directory, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            roots.Add(directory);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        Roots = roots;
        ClearCache();
    }

    /// <summary>
    /// 把当前关卡的 <c>Chapter{n}\Level{n}</c> 插到搜索根最前：烘焙切片优先于 Unity 工程里的同名原图。
    /// 同名 <c>bg_ONE.png</c> 在 Unity 工程里是整张合成图、在关卡目录里是底图切片，顺序错了会整张画面显示成另一张图。
    /// </summary>
    /// <param name="directory">关卡绝对目录。</param>
    public void PrependSearchRoot(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var roots = Roots.ToList();
        roots.RemoveAll(existing => string.Equals(existing, directory, StringComparison.OrdinalIgnoreCase));
        roots.Insert(0, directory);
        Roots = roots;
        ClearCache();
    }

    /// <summary>
    /// 移除一个运行时搜索根。切换关卡时先摘掉上一个关卡的 <c>Chapter{n}\Level{n}</c>，
    /// 避免两张关卡有同名切片时按“先入为主”的根解析到别的关卡图片。
    /// </summary>
    /// <param name="directory">要移除的绝对目录。</param>
    /// <returns>true = 找到并移除。</returns>
    public bool RemoveSearchRoot(string directory)
    {
        var roots = Roots.ToList();
        var removed = roots.RemoveAll(existing => string.Equals(existing, directory, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return false;
        }

        Roots = roots;
        ClearCache();
        return true;
    }

    /// <summary>扫描资源根目录中的图片文件。</summary>
    /// <param name="configuredNames">已被配置表占用的资源名集合（不含扩展名，忽略大小写）。</param>
    /// <param name="root">
    /// 只扫这一棵目录（递归）；为空时扫所有资源根。
    /// 资源整理面板传「地图保存目录」，只整理 Map 里的烘焙图，不把工程里其它 PNG 全倒出来。
    /// </param>
    /// <returns>资源名 → 绝对路径。</returns>
    public IReadOnlyDictionary<string, string> ScanUnconfigured(
        IReadOnlyCollection<string> configuredNames,
        string? scanRoot = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var configured = new HashSet<string>(configuredNames, StringComparer.OrdinalIgnoreCase);
        var roots = string.IsNullOrWhiteSpace(scanRoot) ? Roots : new[] { scanRoot };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (configured.Contains(name) || result.ContainsKey(name))
                {
                    continue;
                }

                result[name] = file;
            }
        }

        return result;
    }

    #endregion

    #region 内部实现

    /// <summary>把相对路径展开成若干可能的绝对路径。</summary>
    private IEnumerable<string> PathCandidates(string relative)
    {
        if (ProjectRoot is not null)
        {
            yield return Path.Combine(ProjectRoot, relative);

            // 允许省略 Assets/ 前缀写成 Res/Textures/…
            if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(ProjectRoot, relative["Assets/".Length..]);
            }
        }

        foreach (var root in Roots)
        {
            yield return Path.Combine(root, relative);

            if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(root, relative["Assets/".Length..]);
            }
        }
    }

    /// <summary>惰性建立「文件名 → 路径」索引，供只写了资源名的情况使用。</summary>
    private void EnsureIndex()
    {
        if (_index is not null)
        {
            return;
        }

        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Scan(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.png", SearchOption.AllDirectories))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (!index.ContainsKey(stem))
                {
                    index[stem] = file;
                }
            }
        }

        if (ProjectRoot is not null)
        {
            Scan(Path.Combine(ProjectRoot, "Assets", "Res"));
        }

        foreach (var root in Roots)
        {
            Scan(root);
        }

        _index = index;
    }

    /// <summary>读取资源根配置（<c>project=</c> 行声明 Unity 工程根）。</summary>
    private void LoadRoots()
    {
        var defaults = new List<string>
        {
            EditorPaths.FallbackResDirectory,
            EditorPaths.FallbackResourcesDirectory,
        };

        var path = Path.Combine(_configDirectory, RootsFileName);
        if (!File.Exists(path))
        {
            Roots = defaults;
            return;
        }

        var roots = new List<string>();
        foreach (var line in FileSystemIo.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var isProject = trimmed.StartsWith("project=", StringComparison.OrdinalIgnoreCase);
            var value = isProject ? trimmed["project=".Length..].Trim() : trimmed;
            if (value.Length == 0)
            {
                continue;
            }

            var absolute = Path.IsPathRooted(value)
                ? value
                : Path.Combine(AppContext.BaseDirectory, value);

            if (isProject)
            {
                ProjectRoot = absolute;
            }
            else
            {
                roots.Add(absolute);
            }
        }

        Roots = roots.Count > 0 ? roots : defaults;
    }

    #endregion
}
