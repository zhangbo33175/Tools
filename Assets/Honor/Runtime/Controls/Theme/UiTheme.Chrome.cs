/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.Chrome.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口框架色：标题栏状态条、左侧列表卡与「数字 ID 来源」徽标。
 ***************************************************************/

using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public static partial class UiTheme
{
    #region 主窗口框架色

    /// <summary>主窗口标题、左侧类型页签与状态提示的专用色。</summary>
    public static class Chrome
    {
        /// <summary>左侧类型页签未选中时的文字色。</summary>
        public static readonly IBrush SideTabText = Rgb(0x3B, 0x5E, 0x6B);

        /// <summary>状态栏错误提示文字（深色状态条上的浅红）。</summary>
        public static readonly IBrush MessageErrorText = Rgb(0xFF, 0xD7, 0xD7);

        /// <summary>状态栏普通提示文字（深色状态条上的浅蓝）。</summary>
        public static readonly IBrush MessageInfoText = Rgb(0xE8, 0xF4, 0xFA);

        /// <summary>保存成功提示文字。</summary>
        public static readonly IBrush SaveSuccessText = Rgb(0x2E, 0x7D, 0x32);

        /// <summary>保存中性提示文字（当前文件/未改动）。</summary>
        public static readonly IBrush SaveNeutralText = Rgb(0x57, 0x73, 0x80);

        /// <summary>属性卡未选中对象时的图层色点。</summary>
        public static readonly IBrush EmptyLayerDot = Rgb(0xB4, 0xC8, 0xD2);
    }

    /// <summary>属性卡「数字 ID 来源」三态徽标：无命中 / 配置表 / PSD 切片。</summary>
    public static class IdSource
    {
        /// <summary>一组徽标色：文字、底色、描边。</summary>
        /// <param name="Text">文字色。</param>
        /// <param name="Surface">底色。</param>
        /// <param name="Border">描边色。</param>
        public readonly record struct Style(IBrush Text, IBrush Surface, IBrush Border);

        /// <summary>配置表与切片都没有命中。</summary>
        public static readonly Style Missing = new(
            Rgb(0x54, 0x70, 0x7C),
            Rgb(0xF3, 0xF8, 0xFA),
            Rgb(0xE6, 0xEF, 0xF4));

        /// <summary>数字 ID 来自物件配置表。</summary>
        public static readonly Style Config = new(
            Rgb(0x09, 0x6A, 0x8E),
            Rgb(0xDD, 0xF2, 0xF9),
            Rgb(0xD8, 0xE5, 0xEE));

        /// <summary>配置表无此行，数字 ID 回退到 PSD 切片序号。</summary>
        public static readonly Style Slice = new(
            Rgb(0xA9, 0x78, 0x31),
            Rgb(0xFF, 0xF4, 0xE2),
            Rgb(0xF0, 0xDF, 0xC2));

        /// <summary>按命中状态选择徽标样式：缺 ID 优先，其次配置表，最后切片回退。</summary>
        /// <param name="missing">当前没有可用数字 ID。</param>
        /// <param name="fromConfig">数字 ID 是否来自配置表。</param>
        /// <returns>对应徽标样式。</returns>
        public static Style For(bool missing, bool fromConfig) =>
            missing ? Missing : fromConfig ? Config : Slice;
    }

    /// <summary>收集物浮窗：目标统计、汇总卡与素材行。</summary>
    public static class CollectiblePanel
    {
        /// <summary>统计数值。</summary>
        public static readonly IBrush StatValueText = Rgb(0x07, 0x5E, 0x7C);

        /// <summary>统计项标题。</summary>
        public static readonly IBrush StatLabelText = Rgb(0x5B, 0x78, 0x85);

        /// <summary>统计项之间的细分隔线。</summary>
        public static readonly IBrush StatSeparator = Rgb(0xD8, 0xE9, 0xF0);

        /// <summary>汇总卡底色。</summary>
        public static readonly IBrush SummarySurface = Rgb(0xF2, 0xFA, 0xFD);

        /// <summary>汇总卡描边。</summary>
        public static readonly IBrush SummaryBorder = Rgb(0xD7, 0xE8, 0xF1);

        /// <summary>分区标题文字。</summary>
        public static readonly IBrush SectionText = Rgb(0x3B, 0x5E, 0x6B);

        /// <summary>素材行右侧已放数量。</summary>
        public static readonly IBrush CountText = Rgb(0x0F, 0x6B, 0x96);

        /// <summary>素材行描边。</summary>
        public static readonly IBrush RowBorder = Rgb(0xDF, 0xEF, 0xF5);
    }

    /// <summary>左侧章节树、关卡行与资源缩略图的专用色。</summary>
    public static class LeftPanel
    {
        /// <summary>章节标题行常态底。</summary>
        public static readonly IBrush ChapterHeaderSurface = Rgb(0xF0, 0xF8, 0xFB);

        /// <summary>章节标题行底部分隔线。</summary>
        public static readonly IBrush ChapterHeaderBorder = Rgb(0xD8, 0xE9, 0xF0);

        /// <summary>关卡已落盘时的文件大小文字。</summary>
        public static readonly IBrush LevelFileSizeText = Rgb(0x2B, 0xA6, 0x6A);

        /// <summary>资源缩略图占位描边。</summary>
        public static readonly IBrush ThumbnailBorder = Rgb(0xC5, 0xDD, 0xE8);

        /// <summary>资源缩略图未加载时的占位底。</summary>
        public static readonly IBrush ThumbnailFill = Rgb(0xD8, 0xE9, 0xF0);
    }

    #endregion

}