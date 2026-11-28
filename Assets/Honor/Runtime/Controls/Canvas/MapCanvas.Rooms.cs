/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.Rooms.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布房间层：触发区辅助框与「进屋预览」淡出
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 画布的房间相关绘制。房间数据本身在地图层（<see cref="MapConfigData.Rooms"/>），
/// 这里只负责两件事：编辑态把触发区画出来、以及「进屋预览」时把该房间要隐藏的零件压淡。
/// </summary>
public partial class MapCanvas
{
    #region 常量

    /// <summary>进屋预览时被隐藏零件的残留不透明度：留一点影子，能看清原来挡了什么。</summary>
    private const double RoomPreviewOpacity = 0.22;

    /// <summary>房间名标签的字号（画布像素，随缩放走）。</summary>
    private const double RoomLabelSize = 26.0;

    #endregion

    #region 公开属性

    /// <summary>是否显示房间触发区（虚线框 + 名字）。编辑态辅助，和「包围盒」同性质的开关。</summary>
    public bool ShowRooms { get; set; } = true;

    /// <summary>
    /// 进屋预览的房间 ID：非空时把该房间里「进屋隐藏」的零件压到
    /// <see cref="RoomPreviewOpacity"/>，等价于运行时的进屋效果，可在编辑器里直接核对数据。
    /// </summary>
    public string RoomPreviewId { get; set; } = string.Empty;

    #endregion

    #region 绘制

    /// <summary>画所有房间的触发区：淡填充 + 虚线框 + 名字，预览中的房间换成高亮描边。</summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图配置。</param>
    private void DrawRooms(DrawingContext context, MapConfigData config)
    {
        if (!ShowRooms || config.Rooms.Count == 0)
        {
            return;
        }

        var cell = (double)(config.GridSize > 0 ? config.GridSize : MapToolDefaults.GridSize);
        var penWidth = 1.5 * Hairline;

        foreach (var room in config.Rooms)
        {
            var previewing = !string.IsNullOrEmpty(RoomPreviewId)
                && string.Equals(RoomPreviewId, room.Id, StringComparison.OrdinalIgnoreCase);
            var brush = previewing ? UiTheme.MapView.RoomPreviewStroke : UiTheme.MapView.RoomStroke;
            var pen = new Pen(brush, previewing ? penWidth * 1.4 : penWidth, new DashStyle(new double[] { 4, 3 }, 0));

            // 房间是「世界坐标上的一块区域」，不是菱形格块：格坐标 × 格子边长就是区域本身。
            // 按菱形格逐格拼会得到锯齿边（索引矩形在等距平面上本来就不是矩形），反倒看不清范围。
            var rect = new Rect(
                room.CellX * cell,
                room.CellY * cell,
                Math.Max(MapRoomData.MinExtent, room.CellWidth) * cell,
                Math.Max(MapRoomData.MinExtent, room.CellHeight) * cell);

            context.FillRectangle(UiTheme.MapView.RoomFill, rect);
            context.DrawRectangle(null, pen, rect);

            // 名字贴在左上角内侧：缩得太小时省掉文字，避免一屏糊满标签
            if (_zoom < 0.2 || string.IsNullOrWhiteSpace(room.DisplayName))
            {
                continue;
            }

            var label = CanvasText.Create(
                room.DisplayName,
                RoomLabelSize * Hairline,
                UiTheme.MapView.Label);
            context.DrawText(label, new Point(rect.X + 6 * Hairline, rect.Y + 5 * Hairline));
        }
    }

    #endregion

    #region 进屋预览

    /// <summary>
    /// 该实例此刻是否需要按「进屋隐藏」压淡：只有预览中的房间、且实例勾了进屋隐藏时成立。
    /// </summary>
    /// <param name="item">地图实例。</param>
    /// <returns>需要压淡返回 true。</returns>
    private bool ShouldFadeForRoomPreview(PlacedObject item) =>
        !string.IsNullOrEmpty(RoomPreviewId)
        && item.HideOnEnter
        && string.Equals(item.RoomId, RoomPreviewId, StringComparison.OrdinalIgnoreCase);

    #endregion
}
