/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapLevelData.cs
 * author:    云毅
 * created:   2026
 * descrip:   楼层模型：剖面式等距场景的层表（名称 / 可见性 / 像素抬升）
 ***************************************************************/

using System.Collections.Generic;

namespace MapEditorTool.Models;

/// <summary>
/// 一个楼层。
/// <para>
/// 剖面式等距布景（小屋 / 洋馆切面）里，楼层由两件事共同决定观感：
/// 一是<b>绘制顺序</b>（上层压在下层之上），二是<b>像素抬升</b>（上层整体比下层高）。
/// 只有顺序没有抬升，两层会完全叠在同一处，剖面就成了穿模；
/// 只有抬升没有顺序，2F 楼板会盖不住 1F 房间的墙头。
/// </para>
/// <para>
/// 字段口径：<see cref="Elevation"/> 是「本层相对地面（0 层）向上抬多少像素」，
/// 由编辑器写入地图数据，运行时按同一数值把楼层根节点往上提，两边才不会各算一套。
/// </para>
/// </summary>
public sealed class MapLevelData
{
    /// <summary>默认楼层数：地面 + 两个楼上层（多数室内场景够用，超出时按需追加）。</summary>
    public const int DefaultCount = 3;

    /// <summary>楼层数上限：再多就不是「剖面小屋」而是摩天楼了，UI 下拉也会失去意义。</summary>
    public const int MaxCount = 12;

    /// <summary>默认层高（像素）：32×16 等距网格下，约等于「一层家具的高度」，够摆柜子和人。</summary>
    public const int DefaultElevation = 128;

    /// <summary>层高上限：再高就飞出画布，纯属误输入。</summary>
    public const int MaxElevation = 4096;

    /// <summary>楼层号，0 起；0 = 地面层。</summary>
    public int Index { get; set; }

    /// <summary>楼层显示名（同时是 Unity 侧楼层根节点的名字，如 <c>1F</c> / <c>2F</c>）。</summary>
    public string Name { get; set; } = "1F";

    /// <summary>是否参与绘制；取消勾选等于临时「拆掉」这一层，方便查看下层内部。</summary>
    public bool Visible { get; set; } = true;

    /// <summary>本层相对地面向上的像素抬升：0 层恒为 0，2F 常规取 <see cref="DefaultElevation"/> 的整数倍。</summary>
    public int Elevation { get; set; }

    /// <summary>楼层序号对应的默认名：0 → 1F，1 → 2F，-1 → B1。</summary>
    /// <param name="index">楼层号。</param>
    /// <returns>默认显示名。</returns>
    public static string DefaultName(int index) => index >= 0 ? $"{index + 1}F" : $"B{-index}";

    /// <summary>楼层序号对应的默认抬升：按 <see cref="DefaultElevation"/> 逐层累加，地下一层算负。</summary>
    /// <param name="index">楼层号。</param>
    /// <returns>默认抬升像素。</returns>
    public static int DefaultElevationOf(int index) => index * DefaultElevation;

    /// <summary>把任意输入夹到合法层高区间。</summary>
    /// <param name="elevation">待校验的层高。</param>
    /// <returns>夹取后的层高。</returns>
    public static int ClampElevation(int elevation) => System.Math.Clamp(elevation, 0, MaxElevation);

    /// <summary>造一份默认楼层表（1F / 2F / 3F），层高按 0 / 128 / 256 递增。</summary>
    /// <returns>楼层集合。</returns>
    public static List<MapLevelData> CreateDefaults()
    {
        var levels = new List<MapLevelData>(DefaultCount);
        for (var i = 0; i < DefaultCount; i++)
        {
            levels.Add(new MapLevelData
            {
                Index = i,
                Name = DefaultName(i),
                Visible = true,
                Elevation = DefaultElevationOf(i),
            });
        }

        return levels;
    }
}
