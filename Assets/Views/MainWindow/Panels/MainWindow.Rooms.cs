/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.Rooms.cs
 * author:    云毅
 * created:   2026
 * descrip:   主界面房间面板：归入房间、进屋预览、房间下拉回填与楼板预设
 ***************************************************************/

using System;
using System.Linq;
using Avalonia.Controls;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool;

/// <summary>
/// 房间相关的主界面接线。房间是地图级实体（<see cref="MapConfigData.Rooms"/>），
/// 这里只处理三件事：把选中物件挂进房间、按房间做进屋预览、以及把下拉与勾选框同步到画布。
/// </summary>
public partial class MainWindow
{
    #region 常量

    /// <summary>房间下拉里「不属于任何房间」的占位文本。</summary>
    private const string NoRoomOption = "（无）";

    /// <summary>立体遮挡带的编号，见 <see cref="LayerBands"/>。</summary>
    private const int StandingBand = 1;

    #endregion

    /// <summary>房间下拉 / 勾选框是否正在被代码回填（防止 SelectionChanged 反向写数据）。</summary>
    private bool _suppressRoomSync;

    #region 接线

    /// <summary>房间下拉、进屋隐藏、归入房间、进屋预览与楼板预设的接线。</summary>
    private void WireRooms()
    {
        // 房间下拉改归属：写 RoomId，并把「进屋隐藏」恢复成默认勾选（墙 / 顶要藏，家具再手工取消）
        PropRoom.SelectionChanged += (_, _) =>
        {
            if (_suppressRoomSync || Canvas.Selected is not { } item)
            {
                return;
            }

            var roomId = SelectedRoomId();
            ApplyProperty(target => target.RoomId = roomId);
            if (!string.IsNullOrEmpty(roomId))
            {
                SetMessage($"已归入房间：{_config?.RoomName(roomId)}");
            }
            else
            {
                SetMessage("已取消房间归属：按独立件绘制");
            }
        };

        PropHideOnEnter.IsCheckedChanged += (_, _) =>
        {
            if (_suppressRoomSync)
            {
                return;
            }

            ApplyProperty(item => item.HideOnEnter = PropHideOnEnter.IsChecked == true);
        };

        BtnMakeRoom.Click += (_, _) => MakeRoomFromSelection();
        BtnRoomPreview.Click += (_, _) => ToggleRoomPreview();
        BtnFloorPreset.Click += (_, _) => ApplyFloorPreset();

        // 房间区显隐：和「包围盒」同性质，只影响编辑态辅助框
        LayerRooms.IsCheckedChanged += (_, _) =>
        {
            Canvas.ShowRooms = LayerRooms.IsChecked == true;
            Canvas.Refresh();
        };
    }

    #endregion

    #region 属性回填

    /// <summary>把选中物件的房间归属回填到下拉与勾选框。</summary>
    /// <param name="item">当前选中的地图实例。</param>
    private void SyncRoomFields(PlacedObject item)
    {
        _suppressRoomSync = true;
        RefreshRoomOptions();
        PropRoom.SelectedIndex = RoomOptionIndex(item.RoomId);
        PropHideOnEnter.IsChecked = item.HideOnEnter;
        PropHideOnEnter.IsEnabled = !string.IsNullOrEmpty(item.RoomId);
        _suppressRoomSync = false;
    }

    /// <summary>未选中物件时把房间字段复位到「无」并置灰。</summary>
    private void ResetRoomFields()
    {
        _suppressRoomSync = true;
        RefreshRoomOptions();
        PropRoom.SelectedIndex = 0;
        PropRoom.IsEnabled = false;
        PropHideOnEnter.IsChecked = true;
        PropHideOnEnter.IsEnabled = false;
        _suppressRoomSync = false;
    }

    /// <summary>重建「房间」下拉：第一项是「（无）」，之后按楼层顺序列出所有房间。</summary>
    private void RefreshRoomOptions()
    {
        var keep = PropRoom.SelectedIndex;
        PropRoom.Items.Clear();
        PropRoom.Items.Add(new ComboBoxItem { Content = NoRoomOption });

        if (_config?.Rooms is { } rooms)
        {
            // 按楼层、再按名字排：多层洋馆里先列 1F 的房间，找起来符合直觉
            foreach (var room in rooms
                         .OrderBy(room => room.Level)
                         .ThenBy(room => room.Name, StringComparer.CurrentCulture))
            {
                PropRoom.Items.Add(new ComboBoxItem
                {
                    Content = $"{room.DisplayName}（{room.Level + 1}F）",
                    Tag = room.Id,
                });
            }
        }

        PropRoom.SelectedIndex = Math.Clamp(keep, 0, PropRoom.Items.Count - 1);
        PropRoom.IsEnabled = Canvas.Selected is not null;
    }

    /// <summary>房间 ID 在下拉里的下标；空 ID 或找不到时回到 0（「（无）」）。</summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>下拉下标。</returns>
    private int RoomOptionIndex(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return 0;
        }

        for (var i = 1; i < PropRoom.Items.Count; i++)
        {
            if (PropRoom.Items[i] is ComboBoxItem { Tag: string tag }
                && string.Equals(tag, roomId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>当前下拉选中的房间 ID；选「（无）」时返回空串。</summary>
    /// <returns>房间 ID 或空串。</returns>
    private string SelectedRoomId() =>
        PropRoom.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : string.Empty;

    #endregion

    #region 动作

    /// <summary>
    /// 「归入房间」：下拉已选房间 → 把当前物件挂进去；下拉是「（无）」→ 以当前物件的占格建一个新房间并挂进去。
    /// <para>用「选中物件的包围盒建房间」而不是另开一个画框工具：墙面本身就是房间的边界，
    /// 选中整排前墙直接生成触发区，比手画一个近似矩形更准。</para>
    /// </summary>
    private void MakeRoomFromSelection()
    {
        if (Canvas.Selected is not { } item || _config is null)
        {
            SetMessageError("先选中一个物件（墙 / 屋顶 / 楼板），再点「归入房间」");
            return;
        }

        var roomId = SelectedRoomId();
        if (string.IsNullOrEmpty(roomId))
        {
            var cell = _config.GridSize > 0 ? _config.GridSize : MapToolDefaults.GridSize;
            var bounds = item.Bounds;
            var room = _config.AddRoom(
                BuildRoomName(item, _config),
                item.Level,
                (int)Math.Floor(bounds.X / cell),
                (int)Math.Floor(bounds.Y / cell),
                Math.Max(MapRoomData.MinExtent, (int)Math.Ceiling(bounds.Width / cell)),
                Math.Max(MapRoomData.MinExtent, (int)Math.Ceiling(bounds.Height / cell)));
            roomId = room.Id;
            SetMessage($"已新建房间「{room.DisplayName}」（{room.Level + 1}F，{room.CellWidth}×{room.CellHeight} 格），并把该物件挂进去");
        }
        else
        {
            SetMessage($"已把「{item.Name}」归入房间「{_config.RoomName(roomId)}」");
        }

        item.RoomId = roomId;
        item.HideOnEnter = true;

        SyncRoomFields(item);
        SyncProperties();
        Canvas.Refresh();
    }

    /// <summary>
    /// 「进屋预览」：把画布切到「玩家站在这个房间」的状态，该房间勾了进屋隐藏的零件压到 22%。
    /// 再点一次退出预览。
    /// </summary>
    private void ToggleRoomPreview()
    {
        if (!string.IsNullOrEmpty(Canvas.RoomPreviewId))
        {
            Canvas.RoomPreviewId = string.Empty;
            Canvas.Refresh();
            SetMessage("已退出进屋预览");
            return;
        }

        var roomId = SelectedRoomId();
        if (string.IsNullOrEmpty(roomId) && Canvas.Selected is { } item)
        {
            roomId = item.RoomId;
        }

        if (string.IsNullOrEmpty(roomId) || _config?.FindRoom(roomId) is null)
        {
            SetMessageError("这个物件还没归入房间：先在「房间」下拉里选一个房间，或点「归入房间」新建");
            return;
        }

        Canvas.RoomPreviewId = roomId;
        Canvas.Refresh();
        SetMessage($"进屋预览：{_config.RoomName(roomId)} —— 该房间勾了「进屋隐藏」的零件已压淡");
    }

    /// <summary>
    /// 「楼板预设」：把选中对象改成楼板口径——遮挡带立体（按脚底 Y 参与遮挡）、不吃挤压、按底边对齐。
    /// <para>楼板不需要专门的物件类型：一张带厚度的菱形精灵摆在 2F，靠既有底边排序就能压住 1F 房间上半部。
    /// 这里只把三个容易设错的开关一次性设好，避免美术出图后还要逐个手调。</para>
    /// </summary>
    private void ApplyFloorPreset()
    {
        if (Canvas.Selected is not { } item || _config is null)
        {
            SetMessageError("先选中要当楼板的图元，再点「套用楼板预设」");
            return;
        }

        item.BandOverride = StandingBand;
        item.BandOverrideAuto = false;
        item.YSortEnabled = true;
        item.Stretch = false;
        LayerBands.MoveToBand(_config, item, StandingBand);

        SyncProperties();
        Canvas.Refresh();
        SetMessage(item.Level == 0
            ? "楼板预设已套用：遮挡带=立体。注意楼板要摆到 2F（用「选中到本层」），并让它底边压在房间前部"
            : $"楼板预设已套用：遮挡带=立体、按脚底 Y 排序，将压在 {item.Level}F 之下的楼层上");
    }

    /// <summary>房间默认名：优先用楼层 + 该层序号，避免一堆「新房间」分不清。</summary>
    /// <param name="item">用作范围的物件。</param>
    /// <param name="config">地图配置。</param>
    /// <returns>房间显示名。</returns>
    private static string BuildRoomName(PlacedObject item, MapConfigData config)
    {
        var index = config.Rooms.Count(room => room.Level == item.Level) + 1;
        return $"{item.Level + 1}F 房间{index}";
    }

    #endregion
}
