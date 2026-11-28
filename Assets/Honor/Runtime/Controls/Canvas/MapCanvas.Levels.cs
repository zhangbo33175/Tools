/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.Levels.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布楼层分区：当前楼层、整楼透视、楼层显隐与选中实例改层
 ***************************************************************/

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class MapCanvas
{
    #region 楼层状态

    /// <summary>非当前楼层在「整楼透视」下的不透明度：看清结构又不干扰手上这一层。</summary>
    public const double OtherLevelOpacity = 0.25;

    /// <summary>层高标尺离地图左边缘的距离（画布像素，不随缩放变粗）。</summary>
    private const double LevelGuideOffset = 18.0;

    /// <summary>楼层牌子文字字号（画布像素）。</summary>
    private const double LevelGuideLabelSize = 20.0;

    /// <summary>段间层高数字字号：比牌子小一档，避免和牌子抢注意力。</summary>
    private const double LevelGuideGapLabelSize = 17.0;

    /// <summary>两枚牌子之间至少要留这么高才写层高数字，否则数字会糊在牌子上。</summary>
    private const double LevelGuideMinGapLabel = 34.0;

    /// <summary>当前编辑楼层（0 = 地面层）。新建物件与落笔瓦片都落在这一层。</summary>
    public int CurrentLevel { get; private set; }

    /// <summary>
    /// 是否同时显示其它楼层（半透明）。
    /// <para>false = 只画当前楼层，等于「拆掉楼上」，适合看一层内部；
    /// true = 整楼透视，当前层不透明、其余层压到 <see cref="OtherLevelOpacity"/>。</para>
    /// </summary>
    public bool ShowAllLevels { get; private set; } = true;

    /// <summary>楼层状态变化（切层、改显隐、选中实例改层后触发）。</summary>
    public event EventHandler? LevelChanged;

    /// <summary>当前楼层的像素抬升：本层画面永远贴基准平面，所以交互坐标不需要做补偿。</summary>
    public int CurrentLevelElevation => _config?.LevelElevation(CurrentLevel) ?? 0;

    /// <summary>取任意楼层的像素抬升（给界面显示「2F · +128px」这类文案用）。</summary>
    /// <param name="level">楼层号（0 起）。</param>
    /// <returns>抬升像素。</returns>
    public int LevelElevationOf(int level) => _config?.LevelElevation(level) ?? 0;

    /// <summary>改某一层的像素抬升并立刻重绘（0 层恒为 0，剖面关系以地面为基准）。</summary>
    /// <param name="level">楼层号（0 起）。</param>
    /// <param name="elevation">抬升像素。</param>
    /// <returns>实际生效的抬升像素；还没打开地图时返回 -1。</returns>
    public int SetLevelElevation(int level, int elevation)
    {
        if (_config is null)
        {
            return -1;
        }

        var applied = _config.SetLevelElevation(level, elevation);
        InvalidateVisual();
        return applied;
    }

    /// <summary>楼层增删的结果：界面据此给不同提示，不靠异常传递「为什么没做成」。</summary>
    public enum LevelEditStatus
    {
        /// <summary>成功。</summary>
        Success,

        /// <summary>还没打开任何地图。</summary>
        NoConfig,

        /// <summary>已达楼层数上限。</summary>
        AtLimit,

        /// <summary>只剩最后一层，不能再删。</summary>
        LastLevel,

        /// <summary>当前层不是最高层，按「从顶楼往下删」的规则拒绝。</summary>
        NotTopLevel,

        /// <summary>该层还有物件或瓦片，删除会丢内容。</summary>
        HasContent,
    }

    /// <summary>在最高处新增一层并切过去，方便立刻往上摆家具。</summary>
    /// <param name="newLevel">新楼层号；失败时为 -1。</param>
    /// <returns>执行结果。</returns>
    public LevelEditStatus TryAddLevel(out int newLevel)
    {
        newLevel = -1;
        if (_config is null)
        {
            return LevelEditStatus.NoConfig;
        }

        var index = _config.AddTopLevel();
        if (index < 0)
        {
            return LevelEditStatus.AtLimit;
        }

        newLevel = index;
        _config.EnsureLevels();
        CurrentLevel = index;
        InvalidateVisual();
        LevelChanged?.Invoke(this, EventArgs.Empty);
        return LevelEditStatus.Success;
    }

    /// <summary>
    /// 删除最高层：只允许「当前层 = 顶层 + 该层没有内容」，
    /// 删完自动切到新的顶层，画布与下拉同时刷新。
    /// </summary>
    /// <param name="removedLevel">被删掉的楼层号；失败时为 -1。</param>
    /// <param name="objectCount">该层遗留的物件数（失败原因提示用）。</param>
    /// <param name="tileCount">该层遗留的瓦片格数（失败原因提示用）。</param>
    /// <returns>执行结果。</returns>
    public LevelEditStatus TryRemoveCurrentLevel(
        out int removedLevel,
        out int objectCount,
        out int tileCount)
    {
        removedLevel = -1;
        objectCount = 0;
        tileCount = 0;
        if (_config is null)
        {
            return LevelEditStatus.NoConfig;
        }

        var count = _config.LevelCount;
        if (count <= 1)
        {
            return LevelEditStatus.LastLevel;
        }

        if (CurrentLevel != count - 1)
        {
            return LevelEditStatus.NotTopLevel;
        }

        objectCount = _config.CountObjectsAt(CurrentLevel);
        tileCount = _config.CountTilesAt(CurrentLevel);
        if (objectCount > 0 || tileCount > 0)
        {
            return LevelEditStatus.HasContent;
        }

        var removed = _config.RemoveTopLevel();
        if (removed < 0)
        {
            return LevelEditStatus.LastLevel;
        }

        removedLevel = removed;
        CurrentLevel = Math.Max(0, _config.LevelCount - 1);
        InvalidateVisual();
        LevelChanged?.Invoke(this, EventArgs.Empty);
        return LevelEditStatus.Success;
    }

    /// <summary>切到指定楼层，并保证它在可见楼层表里被勾选。</summary>
    /// <param name="level">楼层号；负数按 0 处理。</param>
    public void SetCurrentLevel(int level)
    {
        var target = Math.Max(0, level);
        if (CurrentLevel == target)
        {
            return;
        }

        CurrentLevel = target;
        InvalidateVisual();
        LevelChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>切换「整楼透视 / 只看本层」。</summary>
    /// <param name="showAll">true = 其它楼层半透明显示。</param>
    public void SetShowAllLevels(bool showAll)
    {
        if (ShowAllLevels == showAll)
        {
            return;
        }

        ShowAllLevels = showAll;
        InvalidateVisual();
        LevelChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 把选中的实例移到当前楼层；顺带在配置里补齐楼层表。
    /// </summary>
    /// <returns>被移动的实例数；没有选中返回 0。</returns>
    public int MoveSelectionToCurrentLevel()
    {
        var config = _config;
        if (config is null || _selected is null)
        {
            return 0;
        }

        if (_selected.Level == CurrentLevel)
        {
            return 0;
        }

        _selected.Level = CurrentLevel;
        config.EnsureLevels();
        InvalidateVisual();
        LevelChanged?.Invoke(this, EventArgs.Empty);
        return 1;
    }

    #endregion

    #region 绘制辅助

    /// <summary>楼层是否可见（楼层表里取消勾选即视为拆掉该层）。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="level">楼层号。</param>
    /// <returns>可见返回 true。</returns>
    private static bool IsLevelVisible(MapConfigData config, int level)
    {
        foreach (var item in config.Levels)
        {
            if (item.Index == level)
            {
                return item.Visible;
            }
        }

        return true;
    }

    /// <summary>
    /// 参与绘制的楼层号（升序）：图层可见 + 「只看本层」时只留当前层。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <returns>楼层号列表。</returns>
    private List<int> VisibleLevels(MapConfigData config)
    {
        var levels = new List<int>(config.LevelCount);
        for (var i = 0; i < config.LevelCount; i++)
        {
            if (!IsLevelVisible(config, i))
            {
                continue;
            }

            if (!ShowAllLevels && i != CurrentLevel)
            {
                continue;
            }

            levels.Add(i);
        }

        // 当前层被手动关掉时仍要画出来，否则画布会突然空掉
        if (!levels.Contains(CurrentLevel))
        {
            levels.Add(CurrentLevel);
            levels.Sort();
        }

        return levels;
    }

    /// <summary>某一层在「整楼透视」下的不透明度。</summary>
    /// <param name="level">楼层号。</param>
    /// <returns>0–1 之间的不透明度。</returns>
    private double LevelOpacity(int level) =>
        ShowAllLevels && level != CurrentLevel ? OtherLevelOpacity : 1.0;

    /// <summary>
    /// 楼层标尺：在地图左侧立一条竖向虚线，每个可见楼层在地面线高度上挂一枚小牌子，
    /// 写「2F · 128px」（当前层写「本层」），两枚牌子之间再标出这段层高。
    /// 跟着「楼层参考」开关走——取消勾选时他层和整条标尺一起消失，画面回到「只看本层」。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图配置。</param>
    private void DrawLevelElevationGuide(DrawingContext context, MapConfigData config)
    {
        if (PreviewMode || !ShowAllLevels)
        {
            return;
        }

        // 只有一层时没有任何落差可标，标尺反而挡住画面
        var levels = VisibleLevels(config);
        if (levels.Count <= 1)
        {
            return;
        }

        var current = CurrentLevelElevation;
        var marks = new List<(double Y, int Level)>(levels.Count);
        var top = double.MaxValue;
        var bottom = double.MinValue;
        foreach (var level in levels)
        {
            // 本层永远贴在基准平面（偏移 0），其它层按「本层 - 该层」下移，与绘制口径完全一致
            var y = current - config.LevelElevation(level);
            marks.Add((y, level));
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }

        if (bottom - top < 1)
        {
            return;
        }

        // 楼层号是升序，画到屏幕上是自上而下：按 Y 重排一遍，
        // 后面「相邻两枚牌子之间的层高」才能直接用下一项减当前项得到正数
        marks.Sort((a, b) => a.Y.CompareTo(b.Y));

        var spine = -LevelGuideOffset;
        var tick = 6 * Hairline;
        var spinePen = new Pen(UiTheme.MapView.LevelGuide, 1.2 * Hairline, new DashStyle(new double[] { 5, 4 }, 0));
        context.DrawLine(spinePen, new Point(spine, top), new Point(spine, bottom));

        var padX = 6 * Hairline;
        var padY = 3 * Hairline;
        var radius = 4 * Hairline;

        for (var i = 0; i < marks.Count; i++)
        {
            var (y, level) = marks[i];
            context.DrawLine(spinePen, new Point(spine - tick, y), new Point(spine + tick, y));

            var isCurrent = level == CurrentLevel;
            var elevation = config.LevelElevation(level);
            var text = isCurrent
                ? $"{config.LevelName(level)} · 本层"
                : $"{config.LevelName(level)} · {(elevation > 0 ? $"+{elevation}px" : "地面")}";
            var label = CanvasText.Create(
                text,
                LevelGuideLabelSize * Hairline,
                isCurrent ? UiTheme.MapView.Label : UiTheme.MapView.LevelGuide);

            // 牌子挂在标尺左侧（地图外的暗底上），不压住地图美术
            var pillWidth = label.Width + padX * 2;
            var pill = new Rect(
                spine - tick - 3 * Hairline - pillWidth,
                y - label.Height / 2.0 - padY,
                pillWidth,
                label.Height + padY * 2);
            context.DrawRectangle(
                UiTheme.MapView.LevelTagFill,
                new Pen(isCurrent ? UiTheme.MapView.Label : UiTheme.MapView.LevelGuide, Hairline),
                pill,
                radius,
                radius);
            context.DrawText(label, new Point(pill.X + padX, pill.Y + padY));

            // 与下一层之间的层高：两枚牌子正中间写一段淡色数字，直接读出这一段的高度
            if (i + 1 >= marks.Count)
            {
                continue;
            }

            var gap = marks[i + 1].Y - y;
            if (gap < LevelGuideMinGapLabel)
            {
                continue;
            }

            var gapText = CanvasText.Create(
                $"{gap:0}px",
                LevelGuideGapLabelSize * Hairline,
                UiTheme.MapView.LevelGuide);
            context.DrawText(
                gapText,
                new Point(spine - tick - 4 * Hairline - gapText.Width, y + (gap - gapText.Height) / 2.0));
        }
    }

    #endregion
}
