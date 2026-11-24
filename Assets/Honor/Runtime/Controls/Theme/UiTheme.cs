/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器统一视觉主题：集中维护编辑器/预览/向导语义色、滚动条与滑块、
 *            发布窗深色主题及地图网格画笔，避免各控件重复写死色值，保持一致的外观口径。
 ***************************************************************/

using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 编辑器统一视觉主题。颜色按使用场景分组：
/// <list type="bullet">
/// <item><description>编辑器面板：浅灰面板、白卡片、深灰文字；</description></item>
/// <item><description>预览/向导：蓝灰底、深蓝文字、蓝色或绿色强调；</description></item>
/// <item><description>滚动条与滑块：经典箭头条、预览列表滚动轨、速度滑块；</description></item>
/// <item><description>发布窗：深色窗口壳、输入/预览卡片与错误提示；</description></item>
/// <item><description>地图画布：摆放网格、路径网格、地图外框。</description></item>
/// </list>
/// 所有画刷均为静态只读，可在绘制循环中直接复用，避免每帧重复创建。
/// </summary>
public static class UiTheme
{
    #region 通用色

    /// <summary>纯白，用于主按钮文字、白色底等。</summary>
    public static readonly IBrush White = Brushes.White;

    /// <summary>透明画刷，用于列表行常态底等需要完全透出的场景。</summary>
    public static readonly IBrush Transparent = Brushes.Transparent;

    /// <summary>卡片/输入框白底。</summary>
    public static readonly IBrush Card = Rgb(0xFF, 0xFF, 0xFF);

    /// <summary>主强调蓝，选中态、主按钮、活动标签。</summary>
    public static readonly IBrush Accent = Rgb(0x13, 0x87, 0xBE);

    /// <summary>深强调蓝，普通强调文字与图标。</summary>
    public static readonly IBrush AccentDark = Rgb(0x0B, 0x6E, 0x9B);

    /// <summary>错误/危险态文字与描边。</summary>
    public static readonly IBrush Error = Rgb(0xC0, 0x50, 0x4D);

    /// <summary>预览窗分隔线。</summary>
    public static readonly IBrush Divider = Rgb(0xE3, 0xE9, 0xF0);

    #endregion

    #region 编辑器面板色

    /// <summary>编辑器面板底色。</summary>
    public static readonly IBrush PanelSurface = Rgb(0xF7, 0xF7, 0xF7);

    /// <summary>编辑器面板外框。</summary>
    public static readonly IBrush PanelBorder = Rgb(0xC9, 0xC9, 0xC9);

    /// <summary>编辑器主文字。</summary>
    public static readonly IBrush TextPrimary = Rgb(0x26, 0x26, 0x26);

    /// <summary>编辑器次要文字/提示。</summary>
    public static readonly IBrush TextSecondary = Rgb(0x6C, 0x6C, 0x6C);

    /// <summary>信息提示文字（蓝色说明行）。</summary>
    public static readonly IBrush InfoText = Rgb(0x0B, 0x6E, 0x9B);

    /// <summary>对话框底色。</summary>
    public static readonly IBrush DialogSurface = Rgb(0xF4, 0xF6, 0xF8);

    /// <summary>对话框次按钮底。</summary>
    public static readonly IBrush DialogSecondary = Rgb(0xE8, 0xEC, 0xF0);

    #endregion

    #region 预览与向导色

    /// <summary>预览窗/向导窗底色。</summary>
    public static readonly IBrush PreviewPanel = Rgb(0xEE, 0xF2, 0xF7);

    /// <summary>预览窗主文字。</summary>
    public static readonly IBrush PreviewText = Rgb(0x1F, 0x29, 0x37);

    /// <summary>预览窗提示文字。</summary>
    public static readonly IBrush PreviewHint = Rgb(0x78, 0x86, 0x95);

    /// <summary>预览窗更浅的次要文字。</summary>
    public static readonly IBrush PreviewMuted = Rgb(0xA8, 0xB1, 0xBB);

    /// <summary>预览窗强调蓝的浅底。</summary>
    public static readonly IBrush PreviewAccentSoft = Rgb(0xE8, 0xF5, 0xFB);

    /// <summary>收集物向导的槽位浅底。</summary>
    public static readonly IBrush PreviewSlot = Rgb(0xF3, 0xF6, 0xFA);

    /// <summary>收集物向导的绿色强调。</summary>
    public static readonly IBrush WizardAccent = Rgb(0x2F, 0xC5, 0x8B);

    /// <summary>收集物向导的深绿描边。</summary>
    public static readonly IBrush WizardAccentDark = Rgb(0x23, 0xA3, 0x71);

    /// <summary>收集物向导标题绿。</summary>
    public static readonly IBrush WizardTitle = Rgb(0x1B, 0x8F, 0x66);

    /// <summary>收集物向导说明卡底色。</summary>
    public static readonly IBrush WizardInfoSurface = Rgb(0xF7, 0xFB, 0xFD);

    /// <summary>收集物向导说明卡描边。</summary>
    public static readonly IBrush WizardInfoBorder = Rgb(0xD8, 0xEA, 0xF3);

    /// <summary>收集物向导说明文字。</summary>
    public static readonly IBrush WizardInfoText = Rgb(0x46, 0x66, 0x78);

    /// <summary>收集物向导地图预览底色。</summary>
    public static readonly IBrush WizardMapSurface = Rgb(0xF7, 0xFA, 0xFD);

    /// <summary>收集物向导地图预览深色背景。</summary>
    public static readonly IBrush WizardMapBackdrop = Rgb(0x1C, 0x25, 0x33);

    /// <summary>收集物向导的绿色浅底。</summary>
    public static readonly IBrush WizardAccentSoft = Rgb(0xE8, 0xFA, 0xF3);

    /// <summary>收集物向导的移除/危险操作色。</summary>
    public static readonly IBrush WizardDanger = Rgb(0xFF, 0x6B, 0x6B);

    /// <summary>收集物向导空槽描边。</summary>
    public static readonly IBrush WizardEmptySlotBorder = Rgb(0xD4, 0xDD, 0xE7);

    /// <summary>收集物向导空槽提示色。</summary>
    public static readonly IBrush WizardEmptySlotHint = Rgb(0xB4, 0xC1, 0xCE);

    /// <summary>收集物向导缩略图底色。</summary>
    public static readonly IBrush WizardThumbSurface = Rgb(0xF5, 0xF8, 0xFB);

    /// <summary>收集物向导无图占位底色。</summary>
    public static readonly IBrush WizardThumbFallback = Rgb(0xEE, 0xF3, 0xF8);

    /// <summary>收集物向导窗体渐变顶色。</summary>
    public static readonly Color WizardWindowTop = Color.FromRgb(0xF4, 0xF8, 0xFB);

    /// <summary>收集物向导窗体渐变底色。</summary>
    public static readonly Color WizardWindowBottom = Color.FromRgb(0xEA, 0xF6, 0xF1);

    /// <summary>收集物向导地图预览卡的深色叠层与信息条配色。</summary>
    public static class WizardMapPreview
    {
        /// <summary>预览背景渐变顶色。</summary>
        public static readonly Color BackdropTop = Color.FromRgb(0x14, 0x1D, 0x29);

        /// <summary>预览背景渐变底色。</summary>
        public static readonly Color BackdropBottom = Color.FromRgb(0x1F, 0x38, 0x43);

        /// <summary>底图上的暗色压罩。</summary>
        public static readonly IBrush ImageShade = Argb(0x66, 0x0B, 0x12, 0x1B);

        /// <summary>空态图标底。</summary>
        public static readonly IBrush EmptyGlyphSurface = Argb(0x22, 0xFF, 0xFF, 0xFF);

        /// <summary>空态图标描边。</summary>
        public static readonly IBrush EmptyGlyphBorder = Argb(0x44, 0xFF, 0xFF, 0xFF);

        /// <summary>空态图标文字。</summary>
        public static readonly IBrush EmptyGlyph = Rgb(0xD7, 0xE8, 0xE2);

        /// <summary>空态说明文字。</summary>
        public static readonly IBrush EmptyHint = Rgb(0xA9, 0xBC, 0xC7);

        /// <summary>「场景预览」标签底。</summary>
        public static readonly IBrush TagSurface = Argb(0x33, 0x00, 0x00, 0x00);

        /// <summary>「场景预览」标签描边。</summary>
        public static readonly IBrush TagBorder = Argb(0x33, 0xFF, 0xFF, 0xFF);

        /// <summary>底部场景信息条底。</summary>
        public static readonly IBrush InfoSurface = Argb(0xE6, 0x0B, 0x14, 0x1C);

        /// <summary>尺寸信息胶囊底。</summary>
        public static readonly IBrush StatChipSurface = Argb(0x1F, 0xFF, 0xFF, 0xFF);

        /// <summary>尺寸信息胶囊文字。</summary>
        public static readonly IBrush StatChipText = Rgb(0xC5, 0xD5, 0xDE);
    }

    #endregion

    #region 物件类型标签

    /// <summary>物件类型标签色：建筑。</summary>
    public static class ObjectTag
    {
        /// <summary>建筑标签（琥珀）。</summary>
        public static readonly IBrush Building = Rgb(0xD9, 0x8B, 0x2B);

        /// <summary>角色标签（紫）。</summary>
        public static readonly IBrush Character = Rgb(0x8A, 0x6F, 0xD1);

        /// <summary>物件标签（蓝）。</summary>
        public static readonly IBrush Prop = Rgb(0x3F, 0x8E, 0xD4);

        /// <summary>按物件类型取标签色；未知类型回退物件色。</summary>
        /// <param name="type">物件类型。</param>
        /// <returns>标签色画刷。</returns>
        public static IBrush Of(MapObjectType type) => type switch
        {
            MapObjectType.Building => Building,
            MapObjectType.Character => Character,
            _ => Prop,
        };
    }

    /// <summary>动画预览列表的标签色。</summary>
    public static class AnimationTag
    {
        /// <summary>序列帧标签（绿）。</summary>
        public static readonly IBrush Sequence = Rgb(0x2E, 0x9E, 0x6E);

        /// <summary>路径点标签（紫）。</summary>
        public static readonly IBrush PathPoint = Rgb(0x9A, 0x6B, 0xD6);
    }

    #endregion

    #region 资源配置表

    /// <summary>已配置资源表的表头、行底色与描边。</summary>
    public static class ResourceTable
    {
        /// <summary>单元格描边。</summary>
        public static readonly IBrush CellBorder = Rgb(0xD6, 0xD6, 0xD6);

        /// <summary>表头与表格外框线。</summary>
        public static readonly IBrush Line = Rgb(0x80, 0x80, 0x80);

        /// <summary>标题行底色。</summary>
        public static readonly IBrush TitleRow = Rgb(0xC5, 0xD9, 0xF1);

        /// <summary>注释行底色。</summary>
        public static readonly IBrush CommentRow = Rgb(0x92, 0xD0, 0x50);

        /// <summary>名称列文字。</summary>
        public static readonly IBrush NameText = Rgb(0x26, 0x26, 0x26);

        /// <summary>类型列文字。</summary>
        public static readonly IBrush TypeText = Rgb(0x80, 0x80, 0x80);

        /// <summary>只读单元格文字（序号等不可编辑列）。</summary>
        public static readonly IBrush ReadOnlyText = Rgb(0x55, 0x55, 0x55);

        /// <summary>行内编辑按钮底色。</summary>
        public static readonly IBrush EditSurface = Rgb(0xE8, 0xF3, 0xFA);

        /// <summary>行内编辑按钮描边。</summary>
        public static readonly IBrush EditBorder = Rgb(0x13, 0x87, 0xBE);

        /// <summary>行内编辑按钮文字。</summary>
        public static readonly IBrush EditText = Rgb(0x0B, 0x6E, 0x9B);

        /// <summary>行内删除按钮底色。</summary>
        public static readonly IBrush DeleteSurface = Rgb(0xFD, 0xEC, 0xEA);

        /// <summary>行内删除按钮描边。</summary>
        public static readonly IBrush DeleteBorder = Rgb(0xC0, 0x50, 0x4D);

        /// <summary>行内删除按钮文字。</summary>
        public static readonly IBrush DeleteText = Rgb(0xB0, 0x36, 0x2F);
    }

    #endregion

    #region 资源整理面板

    /// <summary>资源整理面板的预览底、滚动条槽与字段标签色。</summary>
    // ---TODO 重构：资源整理面板的局部颜色集中到主题，面板代码只引用语义名。
    public static class ResourcePanel
    {
        /// <summary>选中素材预览区底色。</summary>
        public static readonly IBrush PreviewSurface = Rgb(0xE9, 0xE9, 0xE9);

        /// <summary>配置表滚动条槽底色。</summary>
        public static readonly IBrush ScrollLane = Rgb(0xED, 0xED, 0xED);

        /// <summary>表单字段标签文字。</summary>
        public static readonly IBrush FieldLabel = Rgb(0x56, 0x56, 0x56);
    }

    #endregion

    #region 滚动条色

    /// <summary>
    /// 经典滚动条配色，与 <c>App.axaml</c> 中系统滚动条模板保持一致：
    /// 浅灰轨道、蓝色滑块、灰色箭头。自定义控件只引用这里的画刷，避免再写十六进制色值。
    /// </summary>
    public static class ScrollBar
    {
        /// <summary>滑块填充。</summary>
        public static readonly IBrush ThumbFill = Rgb(0xA9, 0xC6, 0xD8);

        /// <summary>滑块描边。</summary>
        public static readonly IBrush ThumbBorder = Rgb(0x7F, 0xA8, 0xC0);

        /// <summary>滑块轨道底色。</summary>
        public static readonly IBrush Track = Rgb(0xF4, 0xF4, 0xF4);

        /// <summary>滚动条整体底色。</summary>
        public static readonly IBrush Surface = Rgb(0xED, 0xED, 0xED);

        /// <summary>滚动条整体描边。</summary>
        public static readonly IBrush Border = Rgb(0xC6, 0xC6, 0xC6);

        /// <summary>箭头填充。</summary>
        public static readonly IBrush Arrow = Rgb(0x5A, 0x5A, 0x5A);

        /// <summary>箭头按钮底色。</summary>
        public static readonly IBrush ArrowSurface = Rgb(0xE8, 0xE8, 0xE8);

        /// <summary>箭头按钮描边。</summary>
        public static readonly IBrush ArrowBorder = Rgb(0xD4, 0xD4, 0xD4);
    }

    /// <summary>
    /// 预览窗左侧列表的自绘滚动条配色：轨道更浅、滑块悬浮与按下逐级加深，
    /// 与 <see cref="ScrollBar"/> 的经典滚动条区分。
    /// </summary>
    public static class ScrollRail
    {
        /// <summary>轨道底色。</summary>
        public static readonly IBrush Track = Rgb(0xE6, 0xEC, 0xF2);

        /// <summary>滑块常态。</summary>
        public static readonly IBrush Thumb = Rgb(0xB7, 0xC8, 0xD6);

        /// <summary>滑块悬浮态。</summary>
        public static readonly IBrush ThumbHover = Rgb(0x8F, 0xB4, 0xC9);

        /// <summary>滑块拖动态，复用全局强调蓝。</summary>
        public static readonly IBrush ThumbActive = Accent;
    }

    /// <summary>
    /// 预览窗自绘速度滑块的配色：轨道、已选填充、滑块与禁用态。
    /// 填充与滑块描边复用全局强调蓝，避免与按钮强调色漂移。
    /// </summary>
    public static class PlaybackSlider
    {
        /// <summary>轨道底色。</summary>
        public static readonly IBrush Track = Rgb(0xDD, 0xE5, 0xEC);

        /// <summary>已选区间填充。</summary>
        public static readonly IBrush Fill = Accent;

        /// <summary>滑块底色。</summary>
        public static readonly IBrush Thumb = White;

        /// <summary>禁用态轨道。</summary>
        public static readonly IBrush DisabledTrack = Rgb(0xE8, 0xEA, 0xED);

        /// <summary>禁用态填充。</summary>
        public static readonly IBrush DisabledFill = Rgb(0xC8, 0xD3, 0xDC);
    }

    #endregion

    #region 发布窗色

    /// <summary>
    /// 发布地图弹窗的深色配色：窗口壳、输入/预览卡片、说明文字与错误提示。
    /// 集中在这里后，界面装配代码只表达控件结构，不再夹带色值。
    /// </summary>
    public static class PublishWindow
    {
        /// <summary>窗口与内容根底色。</summary>
        public static readonly IBrush Background = Rgb(0x1B, 0x1B, 0x1F);

        /// <summary>副标题等次要文字。</summary>
        public static readonly IBrush SubtitleText = Rgb(0xA8, 0xA8, 0xB3);

        /// <summary>标题下方分隔线。</summary>
        public static readonly IBrush Divider = Rgb(0x34, 0x34, 0x3C);

        /// <summary>预览卡片底色。</summary>
        public static readonly IBrush PreviewSurface = Rgb(0x25, 0x25, 0x2C);

        /// <summary>预览卡片描边。</summary>
        public static readonly IBrush PreviewBorder = Rgb(0x3A, 0x3A, 0x44);

        /// <summary>预览内容文字。</summary>
        public static readonly IBrush PreviewText = Rgb(0xD6, 0xD6, 0xDE);

        /// <summary>底部说明与路径提示文字。</summary>
        public static readonly IBrush NoteText = Rgb(0x8C, 0x8C, 0x97);

        /// <summary>发布失败/校验错误文字。</summary>
        public static readonly IBrush ErrorText = Rgb(0xFF, 0x8A, 0x8A);

        /// <summary>表单左侧字段标签文字。</summary>
        public static readonly IBrush LabelText = Rgb(0xC8, 0xC8, 0xD2);
    }

    #endregion

    #region 九宫格预览色

    /// <summary>九宫格预览的棋盘格、切片填充与拖拽手柄配色。</summary>
    public static class NineSlice
    {
        /// <summary>预览背景底色。</summary>
        public static readonly IBrush Backdrop = Rgb(0x2C, 0x2C, 0x2C);

        /// <summary>棋盘格深色块，与背景形成透明区参照。</summary>
        public static readonly IBrush CheckerDark = Rgb(0x24, 0x24, 0x24);

        /// <summary>九宫格非中心区域的半透明填充。</summary>
        public static readonly IBrush SliceFill = Argb(0x24, 0x4C, 0xD9, 0x64);

        /// <summary>九宫格中心区域的更浅填充，用于区分边与中心。</summary>
        public static readonly IBrush CenterSliceFill = Argb(0x14, 0x4C, 0xD9, 0x64);

        /// <summary>常规参考线、切片描边与手柄填充。</summary>
        public static readonly IBrush Guide = Rgb(0x4C, 0xD9, 0x64);

        /// <summary>当前悬停 / 拖动参考线的黄色高亮。</summary>
        public static readonly IBrush ActiveGuide = Rgb(0xFF, 0xD2, 0x4A);

        /// <summary>边距越界时的红色参考线。</summary>
        public static readonly IBrush InvalidGuide = Rgb(0xE5, 0x53, 0x3D);

        /// <summary>素材外框线。</summary>
        public static readonly IBrush Frame = Rgb(0x80, 0x80, 0x80);

        /// <summary>底部尺寸与边距说明文字。</summary>
        public static readonly IBrush Hint = Rgb(0xD8, 0xD8, 0xD8);

        /// <summary>边距越界提示文字。</summary>
        public static readonly IBrush Error = Rgb(0xFF, 0x9A, 0x8A);

        /// <summary>拖拽手柄描边，保证手柄在浅色素材上也可见。</summary>
        public static readonly IBrush HandleBorder = Rgb(0x1A, 0x1A, 0x1A);
    }

    #endregion

    #region 列表行色

    /// <summary>列表行常态底色。</summary>
    public static readonly IBrush RowBack = Rgb(0xE3, 0xE3, 0xE3);

    /// <summary>列表行常态描边。</summary>
    public static readonly IBrush RowBorder = Rgb(0xAD, 0xAD, 0xAD);

    /// <summary>列表行悬停底色。</summary>
    public static readonly IBrush RowHoverBack = Rgb(0xED, 0xED, 0xED);

    /// <summary>列表行选中底色（带透明叠加）。</summary>
    public static readonly IBrush RowSelectedBack = Argb(0x38, 0x13, 0x87, 0xBE);

    /// <summary>列表行选中描边。</summary>
    public static readonly IBrush RowSelectedBorder = Rgb(0x70, 0xB4, 0xDA);

    #endregion

    #region 主窗口框架色

    /// <summary>主窗口标题、左侧类型页签与状态提示的专用色。</summary>
    public static class Chrome
    {
        /// <summary>左侧类型页签未选中时的文字色。</summary>
        public static readonly IBrush SideTabText = Rgb(0x52, 0x61, 0x6B);

        /// <summary>状态栏错误提示文字（深色状态条上的浅红）。</summary>
        public static readonly IBrush MessageErrorText = Rgb(0xFF, 0xD7, 0xD7);

        /// <summary>状态栏普通提示文字（深色状态条上的浅蓝）。</summary>
        public static readonly IBrush MessageInfoText = Rgb(0xE8, 0xF4, 0xFA);

        /// <summary>保存成功提示文字。</summary>
        public static readonly IBrush SaveSuccessText = Rgb(0x2E, 0x7D, 0x32);

        /// <summary>保存中性提示文字（当前文件/未改动）。</summary>
        public static readonly IBrush SaveNeutralText = Rgb(0x6B, 0x72, 0x80);

        /// <summary>属性卡未选中对象时的图层色点。</summary>
        public static readonly IBrush EmptyLayerDot = Rgb(0xC9, 0xD2, 0xD8);
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
            Rgb(0x7C, 0x87, 0x90),
            Rgb(0xEE, 0xF1, 0xF3),
            Rgb(0xDC, 0xE2, 0xE6));

        /// <summary>数字 ID 来自物件配置表。</summary>
        public static readonly Style Config = new(
            Rgb(0x3A, 0x7F, 0x9E),
            Rgb(0xEA, 0xF4, 0xFA),
            Rgb(0xD2, 0xE6, 0xF1));

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
        public static readonly IBrush StatValueText = Rgb(0x0C, 0x6F, 0x9E);

        /// <summary>统计项标题。</summary>
        public static readonly IBrush StatLabelText = Rgb(0x7A, 0x8A, 0x95);

        /// <summary>统计项之间的细分隔线。</summary>
        public static readonly IBrush StatSeparator = Rgb(0xD9, 0xE8, 0xF0);

        /// <summary>汇总卡底色。</summary>
        public static readonly IBrush SummarySurface = Rgb(0xF3, 0xF9, 0xFC);

        /// <summary>汇总卡描边。</summary>
        public static readonly IBrush SummaryBorder = Rgb(0xD7, 0xE8, 0xF1);

        /// <summary>分区标题文字。</summary>
        public static readonly IBrush SectionText = Rgb(0x42, 0x51, 0x5C);

        /// <summary>素材行右侧已放数量。</summary>
        public static readonly IBrush CountText = Rgb(0x0F, 0x6B, 0x96);

        /// <summary>素材行描边。</summary>
        public static readonly IBrush RowBorder = Rgb(0xE1, 0xE8, 0xEE);
    }

    /// <summary>左侧章节树、关卡行与资源缩略图的专用色。</summary>
    public static class LeftPanel
    {
        /// <summary>章节标题行常态底。</summary>
        public static readonly IBrush ChapterHeaderSurface = Rgb(0xF1, 0xF3, 0xF5);

        /// <summary>章节标题行底部分隔线。</summary>
        public static readonly IBrush ChapterHeaderBorder = Rgb(0xDD, 0xE2, 0xE7);

        /// <summary>关卡已落盘时的文件大小文字。</summary>
        public static readonly IBrush LevelFileSizeText = Rgb(0x2B, 0xA6, 0x6A);

        /// <summary>资源缩略图占位描边。</summary>
        public static readonly IBrush ThumbnailBorder = Rgb(0xD2, 0xD2, 0xD2);

        /// <summary>资源缩略图未加载时的占位底。</summary>
        public static readonly IBrush ThumbnailFill = Rgb(0xE0, 0xE0, 0xE0);
    }

    #endregion

    #region 表单与列表色

    /// <summary>编辑器表单/分组共用的结构色。</summary>
    public static class Form
    {
        /// <summary>输入框/下拉框描边。</summary>
        public static readonly IBrush FieldBorder = Rgb(0xB4, 0xBE, 0xC4);

        /// <summary>分组卡片描边。</summary>
        public static readonly IBrush GroupBorder = Rgb(0xD6, 0xDE, 0xE3);

        /// <summary>面板/字段条浅底。</summary>
        public static readonly IBrush SubtleSurface = Rgb(0xF0, 0xF0, 0xF0);

        /// <summary>只读字段底。</summary>
        public static readonly IBrush FieldReadOnly = Rgb(0xEF, 0xF0, 0xF1);

        /// <summary>下拉字段底。</summary>
        public static readonly IBrush FieldCombo = Rgb(0xEE, 0xF0, 0xF2);

        /// <summary>分组标题常态底。</summary>
        public static readonly IBrush GroupHeader = Rgb(0xF1, 0xF7, 0xFA);

        /// <summary>分组标题悬停底。</summary>
        public static readonly IBrush GroupHeaderHover = Rgb(0xE3, 0xEF, 0xF6);

        /// <summary>未激活标签底。</summary>
        public static readonly IBrush IdleTab = Rgb(0xE4, 0xE4, 0xE4);

        /// <summary>未选中筛选按钮底。</summary>
        public static readonly IBrush FilterIdle = Rgb(0xE3, 0xE3, 0xE3);
    }

    #endregion

    #region 地图画布色

    /// <summary>主地图画布的语义色：底色、图层、编辑提示、碰撞/阴影占位、路径与红绿灯。</summary>
    public static class MapView
    {
        /// <summary>画布外底色（视口空白处）。</summary>
        public static readonly IBrush Backdrop = Rgb(0x06, 0x5A, 0x7A);

        /// <summary>地图有效区域底色。</summary>
        public static readonly IBrush Surface = Rgb(0x07, 0x84, 0xB6);

        /// <summary>地图图层叠加色。</summary>
        public static readonly Color Layer = Color.FromRgb(0x14, 0x96, 0xCB);

        /// <summary>框选矩形填充。</summary>
        public static readonly IBrush Marquee = Argb(0x38, 0xFF, 0xFF, 0xFF);

        /// <summary>白色编辑提示线。</summary>
        public static readonly IBrush Accent = Brushes.White;

        /// <summary>控制点/标记黄色。</summary>
        public static readonly IBrush Handle = Rgb(0xFF, 0xD2, 0x4A);

        /// <summary>画布内文字。</summary>
        public static readonly IBrush Label = Rgb(0xF2, 0xFB, 0xFF);

        /// <summary>选中格填充。</summary>
        public static readonly IBrush SelectedCellFill = Argb(0x66, 0x4C, 0xD9, 0x64);

        /// <summary>选中格描边。</summary>
        public static readonly IBrush SelectedCellStroke = Argb(0xFF, 0x2E, 0xC4, 0x4A);

        /// <summary>碰撞区填充。</summary>
        public static readonly IBrush CollisionFill = Argb(0x33, 0xE5, 0x53, 0x3D);

        /// <summary>碰撞区描边。</summary>
        public static readonly IBrush CollisionStroke = Rgb(0xE5, 0x53, 0x3D);

        /// <summary>阴影区填充。</summary>
        public static readonly IBrush ShadowFill = Argb(0x33, 0x7A, 0x5C, 0xD6);

        /// <summary>阴影区描边。</summary>
        public static readonly IBrush ShadowStroke = Rgb(0x7A, 0x5C, 0xD6);

        /// <summary>占用格填充。</summary>
        public static readonly IBrush OccupancyFill = Argb(0x28, 0xFF, 0xD2, 0x4A);

        /// <summary>占用格描边。</summary>
        public static readonly IBrush OccupancyStroke = Argb(0xE6, 0xFF, 0xC8, 0x1E);

        /// <summary>未选中旋转图元的淡占格描边。</summary>
        public static readonly IBrush OccupancyIdleStroke = Argb(0x55, 0xFF, 0xD2, 0x4A);

        /// <summary>整图包围盒绿线。</summary>
        public static readonly IBrush Boundary = Rgb(0x00, 0xFF, 0x00);

        /// <summary>未旋转图元的占格足迹描边。</summary>
        public static readonly IBrush Footprint = Argb(0x40, 0xFF, 0xFF, 0xFF);

        /// <summary>路径轨迹主色。</summary>
        public static readonly IBrush Path = Rgb(0xBF, 0xF0, 0x6B);

        /// <summary>路径节点填充。</summary>
        public static readonly IBrush PathPoint = Rgb(0x62, 0xD9, 0x8B);

        /// <summary>预览路径节点填充（略透明，避免遮住底图）。</summary>
        public static readonly IBrush PathPointFill = Argb(0xEE, 0x62, 0xD9, 0x8B);

        /// <summary>路径轨迹阴影。</summary>
        public static readonly IBrush PathShadow = Argb(0xB8, 0x10, 0x25, 0x18);

        /// <summary>路径节点描边。</summary>
        public static readonly IBrush PathPointBorder = Rgb(0x0F, 0x6B, 0x42);

        /// <summary>路径起终点标记。</summary>
        public static readonly IBrush PathMarker = Rgb(0xFF, 0xD2, 0x4A);

        /// <summary>路径起终点标记描边。</summary>
        public static readonly IBrush PathMarkerBorder = Rgb(0x3A, 0x2B, 0x00);

        /// <summary>人行道描边。</summary>
        public static readonly IBrush Sidewalk = Argb(0xC8, 0x6F, 0xD9, 0xE8);

        /// <summary>道路中心线。</summary>
        public static readonly IBrush Road = Argb(0xD8, 0xDC, 0xE6, 0xF2);

        /// <summary>斑马线填充。</summary>
        public static readonly IBrush Crosswalk = Argb(0xCC, 0xF7, 0xF7, 0xF7);

        /// <summary>斑马线边线。</summary>
        public static readonly IBrush CrosswalkEdge = Argb(0xEE, 0xFF, 0xD2, 0x4A);

        /// <summary>红灯。</summary>
        public static readonly IBrush TrafficRed = Rgb(0xE0, 0x3A, 0x3A);

        /// <summary>黄灯。</summary>
        public static readonly IBrush TrafficYellow = Rgb(0xE8, 0xC5, 0x45);

        /// <summary>绿灯。</summary>
        public static readonly IBrush TrafficGreen = Rgb(0x4C, 0xC0, 0x69);

        /// <summary>交通停止线。</summary>
        public static readonly IBrush StopLine = Argb(0xFF, 0xFF, 0x5A, 0x5A);

        /// <summary>红绿灯灯体。</summary>
        public static readonly IBrush TrafficBody = Rgb(0x1B, 0x1B, 0x1B);

        /// <summary>灯体到斑马线的关联引导线。</summary>
        public static readonly IBrush TrafficGuide = Argb(0x88, 0xFF, 0xFF, 0xFF);

        /// <summary>战争迷雾编辑面板的中性底。</summary>
        public static readonly IBrush FogNeutral = Rgb(0xCF, 0xD8, 0xDC);

        /// <summary>迷雾覆盖层基色；实际绘制按区域浓度叠加透明度。</summary>
        public static readonly Color FogOverlay = Color.FromRgb(0x07, 0x0C, 0x14);

        /// <summary>迷雾笔刷范围填充。</summary>
        public static readonly IBrush FogBrushFill = Argb(0x1F, 0xFF, 0xD2, 0x4A);

        /// <summary>迷雾笔刷范围描边。</summary>
        public static readonly IBrush FogBrushStroke = Argb(0xCC, 0xFF, 0xD2, 0x4A);
    }

    #endregion

    #region 预览画布色

    /// <summary>动画/整图预览共用的深色舞台色。</summary>
    public static class PreviewView
    {
        /// <summary>预览窗深色舞台底。</summary>
        public static readonly IBrush Stage = Rgb(0x0F, 0x3A, 0x4A);

        /// <summary>动画预览画布背景。</summary>
        public static readonly IBrush CanvasBackdrop = Rgb(0x10, 0x4A, 0x5E);

        /// <summary>动画预览网格。</summary>
        public static readonly IBrush Grid = Argb(0x18, 0xBF, 0xE8, 0xF0);

        /// <summary>动画预览路径轨迹。</summary>
        public static readonly IBrush Path = Rgb(0xBF, 0xF0, 0x6B);

        /// <summary>动画预览路径节点。</summary>
        public static readonly IBrush PathPoint = Rgb(0x62, 0xD9, 0x8B);

        /// <summary>脚底阴影。</summary>
        public static readonly IBrush Shadow = Argb(0x55, 0x00, 0x00, 0x00);

        /// <summary>脚底落点填充。</summary>
        public static readonly IBrush FootprintFill = Argb(0x33, 0x62, 0xD9, 0x8B);

        /// <summary>脚底落点描边。</summary>
        public static readonly IBrush FootprintStroke = Argb(0x99, 0x9C, 0xE8, 0xB0);

        /// <summary>预览画布文字。</summary>
        public static readonly IBrush Label = Argb(0xE6, 0xFF, 0xFF, 0xFF);

        /// <summary>预览画布次要文字。</summary>
        public static readonly IBrush Muted = Argb(0xB0, 0xDD, 0xEE, 0xF0);

        /// <summary>预览画布空态提示。</summary>
        public static readonly IBrush Empty = Argb(0xCC, 0xFF, 0xD2, 0x4A);

        /// <summary>预览画布外框。</summary>
        public static readonly IBrush FrameBorder = Rgb(0xA9, 0xB7, 0xBE);
    }

    #endregion

    #region 地图网格色

    /// <summary>地图画布统一网格主题。</summary>
    public static class MapGrid
    {
        /// <summary>摆放网格弱线。</summary>
        public static readonly IBrush MinorLine = Argb(0x50, 0x38, 0xFF, 0x86);

        /// <summary>摆放网格强线（每若干格一条）。</summary>
        public static readonly IBrush MajorLine = Argb(0x9A, 0x16, 0xD9, 0x66);

        /// <summary>路径编辑网格线。</summary>
        public static readonly IBrush PathLine = Argb(0x9A, 0x3D, 0x9B, 0xFF);

        /// <summary>地图外框线。</summary>
        public static readonly IBrush MapFrameLine = Argb(0xC0, 0x8A, 0x93, 0xA8);
    }

    #endregion

    #region 构造

    /// <summary>按 RGB 分量构造不透明画刷。</summary>
    /// <param name="red">红分量。</param>
    /// <param name="green">绿分量。</param>
    /// <param name="blue">蓝分量。</param>
    /// <returns>静态可复用的画刷。</returns>
    private static IBrush Rgb(byte red, byte green, byte blue) =>
        new SolidColorBrush(Color.FromRgb(red, green, blue));

    /// <summary>按 ARGB 分量构造带透明度画刷。</summary>
    /// <param name="alpha">透明度。</param>
    /// <param name="red">红分量。</param>
    /// <param name="green">绿分量。</param>
    /// <param name="blue">蓝分量。</param>
    /// <returns>静态可复用的画刷。</returns>
    private static IBrush Argb(byte alpha, byte red, byte green, byte blue) =>
        new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));

    #endregion
}
