/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.cs
 * author:    云毅
 * created:   2026
 * descrip:   主题核心：两态配色类型、通用/面板语义色、列表行色与画刷构造，其余分组按场景拆到分部文件。
 ***************************************************************/

using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 开关型控件的两态配色：选中与未选中各一套底色、文字色、描边与字重。
/// 描边与字重可为 null，表示该控件在这种态下不需要覆盖这两项。
/// </summary>
/// <param name="ActiveSurface">选中态底色。</param>
/// <param name="ActiveText">选中态文字色。</param>
/// <param name="ActiveBorder">选中态描边；null 不覆盖。</param>
/// <param name="ActiveWeight">选中态字重；null 不覆盖。</param>
/// <param name="IdleSurface">未选中态底色。</param>
/// <param name="IdleText">未选中态文字色。</param>
/// <param name="IdleBorder">未选中态描边；null 不覆盖。</param>
/// <param name="IdleWeight">未选中态字重；null 不覆盖。</param>
public readonly record struct ToggleVisual(
    IBrush ActiveSurface,
    IBrush ActiveText,
    IBrush? ActiveBorder,
    FontWeight? ActiveWeight,
    IBrush IdleSurface,
    IBrush IdleText,
    IBrush? IdleBorder,
    FontWeight? IdleWeight);


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
public static partial class UiTheme
{
    #region 通用色

    /// <summary>纯白，用于主按钮文字、白色底等。</summary>
    public static readonly IBrush White = Brushes.White;

    /// <summary>透明画刷，用于列表行常态底等需要完全透出的场景。</summary>
    public static readonly IBrush Transparent = Brushes.Transparent;

    /// <summary>卡片/输入框白底。</summary>
    public static readonly IBrush Card = Rgb(0xFF, 0xFF, 0xFF);

    /// <summary>主强调蓝，选中态、主按钮、活动标签。</summary>
    public static readonly IBrush Accent = Rgb(0x0D, 0x8C, 0xB6);

    /// <summary>深强调蓝，普通强调文字与图标。</summary>
    public static readonly IBrush AccentDark = Rgb(0x07, 0x5E, 0x7C);

    /// <summary>错误/危险态文字与描边。</summary>
    public static readonly IBrush Error = Rgb(0xC0, 0x50, 0x4D);

    /// <summary>预览窗分隔线。</summary>
    public static readonly IBrush Divider = Rgb(0xD8, 0xE9, 0xF0);

    #endregion

    #region 窗口标题栏色

    /// <summary>自绘标题栏渐变起始色：浅青白，标题栏比内容更亮，窗口像是浮在面板之上。</summary>
    public static readonly Color TitleBarBackgroundLight = Color.FromRgb(0xC5, 0xF8, 0xFA);

    /// <summary>自绘标题栏渐变中段色。</summary>
    public static readonly Color TitleBarBackground = Color.FromRgb(0x5A, 0xC4, 0xD9);

    /// <summary>自绘标题栏渐变收束色。</summary>
    public static readonly Color TitleBarBackgroundDeep = Color.FromRgb(0x42, 0xB8, 0xD2);

    /// <summary>标题栏文字颜色：浅青底上必须用深墨，才能保证对比度。</summary>
    public static readonly Color TitleBarText = Color.FromRgb(0x06, 0x32, 0x3F);

    /// <summary>标题栏系统按钮（最小化 / 最大化 / 关闭）的矢量字形颜色：深青墨，与标题文字同族。</summary>
    public static readonly Color TitleBarGlyph = Color.FromRgb(0x0B, 0x42, 0x56);

    /// <summary>标题栏系统按钮悬停时的字形颜色：压到近黑，随底下的白色叠光一起更清楚。</summary>
    public static readonly Color TitleBarGlyphHot = Color.FromRgb(0x04, 0x25, 0x30);

    /// <summary>标题栏底边颜色。</summary>
    public static readonly Color TitleBarBorder = Color.FromRgb(0x2F, 0xA0, 0xBA);

    #endregion

    #region 编辑器面板色

    /// <summary>编辑器面板底色。</summary>
    public static readonly IBrush PanelSurface = Rgb(0xF0, 0xF8, 0xFB);

    /// <summary>编辑器面板外框。</summary>
    public static readonly IBrush PanelBorder = Rgb(0xC5, 0xDD, 0xE8);

    /// <summary>编辑器主文字。</summary>
    public static readonly IBrush TextPrimary = Rgb(0x17, 0x32, 0x3D);

    /// <summary>编辑器次要文字/提示。</summary>
    public static readonly IBrush TextSecondary = Rgb(0x57, 0x73, 0x80);

    /// <summary>信息提示文字（蓝色说明行）。</summary>
    public static readonly IBrush InfoText = Rgb(0x07, 0x5E, 0x7C);

    /// <summary>对话框底色。</summary>
    public static readonly IBrush DialogSurface = Rgb(0xED, 0xF7, 0xFB);

    /// <summary>对话框次按钮底。</summary>
    public static readonly IBrush DialogSecondary = Rgb(0xDF, 0xEE, 0xF5);

    #endregion

    #region 列表行色

    /// <summary>列表行常态底色：接近纯白，靠 1px 青灰描边分行，不再用中性灰。</summary>
    public static readonly IBrush RowBack = Rgb(0xFB, 0xFD, 0xFE);

    /// <summary>列表行常态描边：与卡片描边同源的青灰。</summary>
    public static readonly IBrush RowBorder = Rgb(0xDF, 0xEF, 0xF5);

    /// <summary>列表行悬停底色。</summary>
    public static readonly IBrush RowHoverBack = Rgb(0xE7, 0xF3, 0xF8);

    /// <summary>列表行选中底色（带透明叠加）。</summary>
    public static readonly IBrush RowSelectedBack = Argb(0x38, 0x13, 0x87, 0xBE);

    /// <summary>列表行选中描边。</summary>
    public static readonly IBrush RowSelectedBorder = Rgb(0x70, 0xB4, 0xDA);

    /// <summary>
    /// 编辑器列表行配色：白底细描边，选中蓝底蓝边，整圈描边不做强调线。
    /// 用属性而不是静态字段：三套行配色的色值分散在其它分部文件，字段初始化顺序无法保证，
    /// 早期写成静态字段时 <c>UiTheme.PathPointRow</c> 会抓到还没赋值的 null 画刷，选中行只剩描边没有底色。
    /// </summary>
    public static RowVisual EditorRow => new(
        RowBack, RowBorder, RowHoverBack, RowSelectedBack, RowSelectedBorder);

    /// <summary>预览窗列表配色：默认透明（外观交给主题），选中浅青底 + 3px 左侧强调线。</summary>
    public static RowVisual PreviewRow => new(
        Transparent, Transparent, Transparent, PreviewAccentSoft!, Accent, 3);

    /// <summary>路径点列表行配色：透明底，悬停浅青，选中浅青底 + 3px 左侧强调线，与画布上的选中点同色系。</summary>
    public static RowVisual PathPointRow => new(
        Transparent, Transparent, Form.GroupHeaderHover!, PreviewAccentSoft!, Accent, 3);

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

    /// <summary>按 ARGB 分量构造带透明度的单像素宽度画笔。</summary>
    /// <param name="alpha">透明度。</param>
    /// <param name="red">红分量。</param>
    /// <param name="green">绿分量。</param>
    /// <param name="blue">蓝分量。</param>
    /// <param name="thickness">线宽。</param>
    /// <returns>静态可复用的画笔。</returns>
    private static IPen ArgbPen(byte alpha, byte red, byte green, byte blue, double thickness) =>
        new Pen(Argb(alpha, red, green, blue), thickness);

    #endregion

    #region 分部

    // 主窗口框架见 UiTheme.Chrome.cs，面板与表单见 UiTheme.Panels.cs，
    // 预览/向导与发布窗见 UiTheme.Windows.cs，画布与网格见 UiTheme.Canvas.cs，
    // 交通与剧情见 UiTheme.Features.cs。

    #endregion

}
