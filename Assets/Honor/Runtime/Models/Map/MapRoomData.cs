/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapRoomData.cs
 * author:    云毅
 * created:   2026
 * descrip:   房间模型：剖面场景的触发范围与「进屋即隐藏」策略
 ***************************************************************/

using System;

namespace MapEditorTool.Models;

/// <summary>
/// 一个房间（剖面式等距场景里的单间）。
/// <para>
/// 房间是<b>地图级实体</b>，不是物件上的开关：触发范围只有一份，玩家进没进去由它判定；
/// 墙 / 屋顶 / 楼板这些要隐藏的零件通过 <see cref="PlacedObject.RoomId"/> 挂靠过来。
/// 这样一颗「房间」改动（改名、挪范围、调淡出时长）不需要逐个物件重设，
/// 也不会出现「半面墙忘了勾隐藏」的破洞。
/// </para>
/// <para>
/// 范围用格坐标：<see cref="CellX"/> / <see cref="CellY"/> 是左上角格，<see cref="CellWidth"/> /
/// <see cref="CellHeight"/> 是占格数，运行时乘格子边长还原成世界矩形，和物件占格同一口径。
/// </para>
/// </summary>
public sealed class MapRoomData
{
    #region 常量

    /// <summary>默认淡出时长（秒）：短到不挡视线，长到不突兀。</summary>
    public const double DefaultFadeSeconds = 0.15;

    /// <summary>淡出时长上限（秒）。</summary>
    public const double MaxFadeSeconds = 2.0;

    /// <summary>房间占格的最小边长。</summary>
    public const int MinExtent = 1;

    #endregion

    #region 字段

    /// <summary>房间 ID（<c>r_</c> + 6 位数字），全图唯一，导出给运行时做触发判定。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>房间显示名（「1F 客厅」「后院长廊」），只用于编辑与日志。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>所属楼层，0 = 地面层；触发区只在同一楼层生效。</summary>
    public int Level { get; set; }

    /// <summary>触发区左上角格坐标 X。</summary>
    public int CellX { get; set; }

    /// <summary>触发区左上角格坐标 Y。</summary>
    public int CellY { get; set; }

    /// <summary>触发区占格宽。</summary>
    public int CellWidth { get; set; } = MinExtent;

    /// <summary>触发区占格高。</summary>
    public int CellHeight { get; set; } = MinExtent;

    /// <summary>进屋隐藏屋顶（山墙 / 屋面 / 顶楼板）。</summary>
    public bool HideRoof { get; set; } = true;

    /// <summary>进屋隐藏前墙（挡在玩家与室内之间的那面墙）。</summary>
    public bool HideFrontWall { get; set; } = true;

    /// <summary>进屋隐藏上层楼板下沿，避免站在地板下抬头就顶到一块板。</summary>
    public bool HideFloorUnder { get; set; } = true;

    /// <summary>淡出时长（秒）：运行时用它在「进入 / 离开」时做透明度过渡。</summary>
    public double FadeSeconds { get; set; } = DefaultFadeSeconds;

    #endregion

    #region 派生属性

    /// <summary>触发区右下角格坐标 X（不含）。</summary>
    public int CellRight => CellX + Math.Max(MinExtent, CellWidth);

    /// <summary>触发区右下角格坐标 Y（不含）。</summary>
    public int CellBottom => CellY + Math.Max(MinExtent, CellHeight);

    /// <summary>房间在编辑器里的完整显示名：名字为空时退回 ID。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

    #endregion

    #region 公开方法

    /// <summary>把范围夹成合法值：负宽高按 1 处理，淡出时长夹到上限内。</summary>
    public void Normalize()
    {
        CellWidth = Math.Max(MinExtent, CellWidth);
        CellHeight = Math.Max(MinExtent, CellHeight);
        FadeSeconds = Math.Clamp(FadeSeconds, 0, MaxFadeSeconds);
        Name ??= string.Empty;
        Id ??= string.Empty;
    }

    /// <summary>世界坐标是否落在触发区内。</summary>
    /// <param name="worldX">世界 X（像素）。</param>
    /// <param name="worldY">世界 Y（像素）。</param>
    /// <param name="gridSize">格子边长（像素）。</param>
    /// <returns>落在触发区返回 true。</returns>
    public bool ContainsWorld(double worldX, double worldY, double gridSize)
    {
        if (gridSize <= 0)
        {
            return false;
        }

        var left = CellX * gridSize;
        var top = CellY * gridSize;
        return worldX >= left
            && worldX < CellRight * gridSize
            && worldY >= top
            && worldY < CellBottom * gridSize;
    }

    #endregion
}
