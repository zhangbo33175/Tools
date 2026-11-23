/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器统一视觉主题：集中维护编辑器/预览/向导语义色与地图网格画笔，
 *            避免各控件重复写死色值，保持一致的外观口径。
 ***************************************************************/

using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 编辑器统一视觉主题。颜色按使用场景分组：
/// <list type="bullet">
/// <item><description>编辑器面板：浅灰面板、白卡片、深灰文字；</description></item>
/// <item><description>预览/向导：蓝灰底、深蓝文字、蓝色或绿色强调；</description></item>
/// <item><description>地图画布：摆放网格、路径网格、地图外框。</description></item>
/// </list>
/// 所有画刷均为静态只读，可在绘制循环中直接复用，避免每帧重复创建。
/// </summary>
public static class UiTheme
{
    #region 通用色

    /// <summary>纯白，用于主按钮文字、白色底等。</summary>
    public static readonly IBrush White = Brushes.White;

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

    /// <summary>收集物向导的绿色浅底。</summary>
    public static readonly IBrush WizardAccentSoft = Rgb(0xE8, 0xFA, 0xF3);

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
