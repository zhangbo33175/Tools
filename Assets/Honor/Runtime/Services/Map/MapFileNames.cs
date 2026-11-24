/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapFileNames.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图存档命名：关卡、场景、迷雾 sidecar、发布清单与目录探测模式
 ***************************************************************/

using System.IO;

namespace MapEditorTool.Services;

/// <summary>
/// 地图相关文件名统一入口。编辑器保存、发布与迷雾 sidecar 共用同一套规则，
/// 避免 <c>Map_{章节}m{关卡}.json</c> 在各处手写后出现大小写或分隔符漂移。
/// </summary>
public static class MapFileNames
{
    #region 常量

    /// <summary>地图 JSON 扩展名。</summary>
    public const string JsonExtension = ".json";

    /// <summary>迷雾 sidecar 扩展名。</summary>
    public const string FogExtension = ".fog.json";

    /// <summary>关卡存档前缀。</summary>
    public const string LevelPrefix = "Map_";

    /// <summary>Unity 地图目录探测使用的关卡存档模式。</summary>
    public const string LevelPattern = LevelPrefix + "*.json";

    #endregion

    #region 生成

    /// <summary>关卡存档名：<c>Map_{章节}m{关卡}.json</c>。</summary>
    /// <param name="chapterId">章节 ID。</param>
    /// <param name="levelId">关卡 ID。</param>
    /// <returns>关卡存档文件名。</returns>
    public static string Level(string chapterId, string levelId) =>
        $"{LevelPrefix}{chapterId}m{levelId}{JsonExtension}";

    /// <summary>旧口径场景存档名：<c>{场景 ID}.json</c>。</summary>
    /// <param name="sceneId">场景 ID，调用方需先做非法字符归一。</param>
    /// <returns>场景存档文件名。</returns>
    public static string Scene(string sceneId) => sceneId + JsonExtension;

    /// <summary>关卡迷雾 sidecar：<c>Map_{章节}m{关卡}.fog.json</c>。</summary>
    /// <param name="chapterId">章节 ID。</param>
    /// <param name="levelId">关卡 ID。</param>
    /// <returns>迷雾文件名。</returns>
    public static string Fog(string chapterId, string levelId) =>
        $"{LevelPrefix}{chapterId}m{levelId}{FogExtension}";

    /// <summary>按地图文件名生成同基名迷雾 sidecar。</summary>
    /// <param name="mapFileName">地图文件名或路径。</param>
    /// <returns>迷雾文件名。</returns>
    public static string FogForMap(string mapFileName) =>
        Path.GetFileNameWithoutExtension(mapFileName) + FogExtension;

    /// <summary>发布清单名：<c>PublishManifest_{章节}_{关卡}.json</c>。</summary>
    /// <param name="chapterId">章节 ID。</param>
    /// <param name="levelId">关卡 ID。</param>
    /// <returns>发布清单文件名。</returns>
    public static string Manifest(string chapterId, string levelId) =>
        $"PublishManifest_{chapterId}_{levelId}{JsonExtension}";

    #endregion
}
