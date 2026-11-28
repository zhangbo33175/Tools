/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.Panels.cs
 * author:    云毅
 * created:   2026
 * descrip:   面板色：物件类型标签、资源配置表、资源整理、滚动条与滑块、表单与两态配色。
 ***************************************************************/

using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public static partial class UiTheme
{
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
        public static readonly IBrush CellBorder = Rgb(0xC5, 0xDD, 0xE8);

        /// <summary>表头与表格外框线。</summary>
        public static readonly IBrush Line = Rgb(0x5B, 0x78, 0x85);

        /// <summary>标题行底色（浅青，替代原来的蓝绿拼色表头）。</summary>
        public static readonly IBrush TitleRow = Rgb(0xE4, 0xF4, 0xFA);

        /// <summary>注释行底色（比标题行更浅，保持整片表头同色系）。</summary>
        public static readonly IBrush CommentRow = Rgb(0xF4, 0xFA, 0xFD);

        /// <summary>名称列文字。</summary>
        public static readonly IBrush NameText = Rgb(0x17, 0x32, 0x3D);

        /// <summary>类型列文字。</summary>
        public static readonly IBrush TypeText = Rgb(0x5B, 0x78, 0x85);

        /// <summary>只读单元格文字（序号等不可编辑列）。</summary>
        public static readonly IBrush ReadOnlyText = Rgb(0x55, 0x73, 0x80);

        /// <summary>数据行斑马纹底色（偶数行留白）；太浅等于没有，长表滚动时需要能一眼分行。</summary>
        public static readonly IBrush RowAlt = Rgb(0xEF, 0xF7, 0xFB);

        /// <summary>数据行鼠标悬停底色，比斑马纹再深一档，用于整行高亮。</summary>
        public static readonly IBrush HoverRow = Rgb(0xDF, 0xF1, 0xFA);

        /// <summary>行内编辑按钮文字。</summary>
        public static readonly IBrush EditText = Rgb(0x07, 0x5E, 0x7C);

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
        public static readonly IBrush PreviewSurface = Rgb(0xEB, 0xF6, 0xFA);

        /// <summary>配置表滚动条槽底色。</summary>
        public static readonly IBrush ScrollLane = Rgb(0xE7, 0xF3, 0xF8);

        /// <summary>表单字段标签文字。</summary>
        public static readonly IBrush FieldLabel = Rgb(0x57, 0x73, 0x80);
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
        public static readonly IBrush ThumbFill = Rgb(0x8F, 0xC9, 0xDC);

        /// <summary>滑块描边。</summary>
        public static readonly IBrush ThumbBorder = Rgb(0x5F, 0xA6, 0xBF);

        /// <summary>滑块轨道底色。</summary>
        public static readonly IBrush Track = Rgb(0xF5, 0xFB, 0xFD);

        /// <summary>滚动条整体底色。</summary>
        public static readonly IBrush Surface = Rgb(0xE7, 0xF3, 0xF8);

        /// <summary>滚动条整体描边。</summary>
        public static readonly IBrush Border = Rgb(0xC5, 0xDD, 0xE8);

        /// <summary>箭头填充。</summary>
        public static readonly IBrush Arrow = Rgb(0x35, 0x56, 0x63);

        /// <summary>箭头按钮底色。</summary>
        public static readonly IBrush ArrowSurface = Rgb(0xE3, 0xF1, 0xF6);

        /// <summary>箭头按钮描边。</summary>
        public static readonly IBrush ArrowBorder = Rgb(0xC5, 0xDD, 0xE8);
    }

    /// <summary>
    /// 预览窗左侧列表的自绘滚动条配色：轨道更浅、滑块悬浮与按下逐级加深，
    /// 与 <see cref="ScrollBar"/> 的经典滚动条区分。
    /// </summary>
    public static class ScrollRail
    {
        /// <summary>轨道底色。</summary>
        public static readonly IBrush Track = Rgb(0xE3, 0xF1, 0xF6);

        /// <summary>滑块常态。</summary>
        public static readonly IBrush Thumb = Rgb(0xA9, 0xCB, 0xD8);

        /// <summary>滑块悬浮态。</summary>
        public static readonly IBrush ThumbHover = Rgb(0x73, 0xAE, 0xC4);

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
        public static readonly IBrush Track = Rgb(0xD7, 0xE8, 0xEF);

        /// <summary>已选区间填充。</summary>
        public static readonly IBrush Fill = Accent;

        /// <summary>滑块底色。</summary>
        public static readonly IBrush Thumb = White;

        /// <summary>禁用态轨道。</summary>
        public static readonly IBrush DisabledTrack = Rgb(0xEA, 0xF2, 0xF5);

        /// <summary>禁用态填充。</summary>
        public static readonly IBrush DisabledFill = Rgb(0xB9, 0xCD, 0xD7);
    }

    #endregion

    #region 表单与列表色

    /// <summary>编辑器表单/分组共用的结构色。</summary>
    #region 开关态

    /// <summary>开关型控件（页签 / 筛选按钮 / 浮窗开关）的两态配色表。</summary>
    public static class Toggle
    {
        /// <summary>顶部导航页签：选中铺白卡 + 主色描边 + 深青加粗，未选中只留灰字，与下方工作区同深度的白。</summary>
        public static readonly ToggleVisual NavTab = new(
            Card, AccentDark, Accent, FontWeight.Bold,
            Transparent, TextSecondary, Transparent, FontWeight.Normal);

        /// <summary>卡片内分段页签：选中浅青胶囊 + 主色描边 + 深青半粗。</summary>
        public static readonly ToggleVisual SegTab = new(
            Form.ActiveTabSurface, AccentDark, Accent, FontWeight.SemiBold,
            Transparent, TextSecondary, Transparent, FontWeight.Normal);

        /// <summary>工具条浮窗开关：打开实心青，关闭浅青幽灵。</summary>
        public static readonly ToggleVisual ToolToggle = new(
            Accent, White, Accent, FontWeight.SemiBold,
            PreviewAccentSoft, AccentDark, PanelBorder, FontWeight.Normal);

        /// <summary>下拉菜单行：常态透明吃菜单底色，打开关联浮窗时铺浅青并加粗。</summary>
        public static readonly ToggleVisual MenuRow = new(
            PreviewAccentSoft, AccentDark, null, FontWeight.SemiBold,
            Transparent, TextPrimary, null, FontWeight.Normal);

        /// <summary>资源分类筛选按钮：选中浅青底 + 青字加粗 + 青描边，未选中白底细描边。</summary>
        public static readonly ToggleVisual Filter = new(
            PreviewAccentSoft, AccentDark, Accent, FontWeight.Bold,
            Card, TextSecondary, PanelBorder, FontWeight.Normal);

        /// <summary>左栏模式页签：选中实心蓝白字，未选中浅灰底深字。</summary>
        public static readonly ToggleVisual ModeTab = new(
            Accent, White, null, null,
            Form.IdleTab, TextPrimary, null, null);

        /// <summary>左栏资源类型页签：选中实心蓝白字，未选中完全透明（悬停态由样式类负责）。</summary>
        public static readonly ToggleVisual SideTab = new(
            Accent, White, null, null,
            Transparent, Chrome.SideTabText, null, null);
    }

    #endregion

    public static class Form
    {
        /// <summary>输入框/下拉框描边。</summary>
        public static readonly IBrush FieldBorder = Rgb(0xA9, 0xC0, 0xCB);

        /// <summary>分组卡片描边。</summary>
        public static readonly IBrush GroupBorder = Rgb(0xD8, 0xE9, 0xF0);

        /// <summary>只读字段底。</summary>
        public static readonly IBrush FieldReadOnly = Rgb(0xEF, 0xF6, 0xF9);

        /// <summary>下拉字段底。</summary>
        public static readonly IBrush FieldCombo = Rgb(0xEF, 0xF6, 0xF9);

        /// <summary>分组标题常态底。</summary>
        public static readonly IBrush GroupHeader = Rgb(0xF2, 0xFA, 0xFD);

        /// <summary>分组标题悬停底。</summary>
        public static readonly IBrush GroupHeaderHover = Rgb(0xE1, 0xF4, 0xFA);

        /// <summary>未激活标签底。</summary>
        public static readonly IBrush IdleTab = Rgb(0xE4, 0xE4, 0xE4);

        /// <summary>激活标签底（浅青，卡片内分段页签与路径 / 动画页签共用）。</summary>
        public static readonly IBrush ActiveTabSurface = Rgb(0xDD, 0xF2, 0xF9);

        /// <summary>未选中筛选按钮底。</summary>
        public static readonly IBrush FilterIdle = Rgb(0xE3, 0xE3, 0xE3);
    }

    #endregion

}