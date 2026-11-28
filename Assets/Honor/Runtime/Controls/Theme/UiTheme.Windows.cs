/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.Windows.cs
 * author:    云毅
 * created:   2026
 * descrip:   弹窗色：预览/向导、发布窗深色壳与九宫格预览。
 ***************************************************************/

using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public static partial class UiTheme
{
    #region 预览与向导色

    /// <summary>预览窗/向导窗底色。</summary>
    public static readonly IBrush PreviewPanel = Rgb(0xEA, 0xF6, 0xFA);

    /// <summary>预览窗主文字。</summary>
    public static readonly IBrush PreviewText = Rgb(0x17, 0x32, 0x3D);

    /// <summary>预览窗提示文字。</summary>
    public static readonly IBrush PreviewHint = Rgb(0x5B, 0x78, 0x85);

    /// <summary>预览窗更浅的次要文字。</summary>
    public static readonly IBrush PreviewMuted = Rgb(0x93, 0xAC, 0xB8);

    /// <summary>预览窗强调蓝的浅底。</summary>
    public static readonly IBrush PreviewAccentSoft = Rgb(0xDD, 0xF2, 0xF9);

    /// <summary>收集物向导的槽位浅底。</summary>
    public static readonly IBrush PreviewSlot = Rgb(0xF6, 0xFB, 0xFD);

    /// <summary>收集物向导的绿色强调。</summary>
    public static readonly IBrush WizardAccent = Rgb(0x1E, 0x9E, 0x86);

    /// <summary>收集物向导的深绿描边。</summary>
    public static readonly IBrush WizardAccentDark = Rgb(0x14, 0x7A, 0x69);

    /// <summary>收集物向导标题绿。</summary>
    public static readonly IBrush WizardTitle = Rgb(0x11, 0x6B, 0x60);

    /// <summary>收集物向导说明卡底色。</summary>
    public static readonly IBrush WizardInfoSurface = Rgb(0xF7, 0xFC, 0xFE);

    /// <summary>收集物向导说明卡描边。</summary>
    public static readonly IBrush WizardInfoBorder = Rgb(0xD4, 0xE9, 0xF2);

    /// <summary>收集物向导说明文字。</summary>
    public static readonly IBrush WizardInfoText = Rgb(0x40, 0x63, 0x74);

    /// <summary>收集物向导地图预览底色。</summary>
    public static readonly IBrush WizardMapSurface = Rgb(0xF7, 0xFC, 0xFE);

    /// <summary>收集物向导的绿色浅底。</summary>
    public static readonly IBrush WizardAccentSoft = Rgb(0xE3, 0xF7, 0xF2);

    /// <summary>收集物向导素材卡未选中时的描边：比通用分隔线更清晰，又不与选中态抢。</summary>
    public static readonly IBrush WizardCardBorder = Rgb(0xD7, 0xE6, 0xEC);

    /// <summary>收集物向导素材卡悬停底色，只做轻微提亮，不改变尺寸。</summary>
    public static readonly IBrush WizardCardHover = Rgb(0xF3, 0xFB, 0xFA);

    /// <summary>收集物向导「已选素材」区的槽位底色：比整块强调底更淡，让缩略图成为焦点。</summary>
    public static readonly IBrush WizardSlotSurface = Rgb(0xF2, 0xFA, 0xF9);

    /// <summary>收集物向导的移除/危险操作色。</summary>
    public static readonly IBrush WizardDanger = Rgb(0xFF, 0x6B, 0x6B);

    /// <summary>收集物向导空槽描边。</summary>
    public static readonly IBrush WizardEmptySlotBorder = Rgb(0xCC, 0xE2, 0xEC);

    /// <summary>收集物向导空槽提示色。</summary>
    public static readonly IBrush WizardEmptySlotHint = Rgb(0xA4, 0xBC, 0xC8);

    /// <summary>收集物向导缩略图底色。</summary>
    public static readonly IBrush WizardThumbSurface = Rgb(0xF4, 0xFA, 0xFD);

    /// <summary>收集物向导无图占位底色。</summary>
    public static readonly IBrush WizardThumbFallback = Rgb(0xEA, 0xF5, 0xFA);

    /// <summary>收集物向导窗体渐变顶色。</summary>
    public static readonly Color WizardWindowTop = Color.FromRgb(0xF4, 0xFB, 0xFD);

    /// <summary>收集物向导窗体渐变底色。</summary>
    public static readonly Color WizardWindowBottom = Color.FromRgb(0xE7, 0xF6, 0xFA);

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
        public static readonly IBrush EmptyHint = Rgb(0x9F, 0xBB, 0xC6);

        /// <summary>「场景预览」标签底。</summary>
        public static readonly IBrush TagSurface = Argb(0x33, 0x00, 0x00, 0x00);

        /// <summary>「场景预览」标签描边。</summary>
        public static readonly IBrush TagBorder = Argb(0x33, 0xFF, 0xFF, 0xFF);

        /// <summary>底部场景信息条底。</summary>
        public static readonly IBrush InfoSurface = Argb(0xE6, 0x0B, 0x14, 0x1C);

        /// <summary>尺寸信息胶囊底。</summary>
        public static readonly IBrush StatChipSurface = Argb(0x1F, 0xFF, 0xFF, 0xFF);

        /// <summary>尺寸信息胶囊文字。</summary>
        public static readonly IBrush StatChipText = Rgb(0xC5, 0xDD, 0xE8);
    }

    #endregion

    #region 网格设置弹窗色

    /// <summary>
    /// 网格设置弹窗的浅青配色：窗体渐变、卡片与描边、字段三态、预览格面与底栏按钮。
    /// 弹窗内所有色值集中在这里，装配代码只表达结构与间距。
    /// </summary>
    public static class GridDialog
    {
        /// <summary>窗体渐变顶色（近白）。</summary>
        public static readonly Color BackgroundTop = Color.FromRgb(0xFA, 0xFD, 0xFE);

        /// <summary>窗体渐变收束色（浅青）。</summary>
        public static readonly Color BackgroundBottom = Color.FromRgb(0xE7, 0xF4, 0xF9);

        /// <summary>卡片描边：比通用分隔线略深一档，白卡落在浅青底上才有边界。</summary>
        public static readonly IBrush CardBorder = Rgb(0xDC, 0xEB, 0xF2);

        /// <summary>卡片投影：低透明度青灰，只做轻微离地感，不抢内容。</summary>
        public static readonly BoxShadows CardShadow = BoxShadows.Parse("0 4 14 0 #120B5568");

        /// <summary>字段常态描边。</summary>
        public static readonly IBrush FieldBorder = Rgb(0xC6, 0xDC, 0xE6);

        /// <summary>字段悬停描边。</summary>
        public static readonly IBrush FieldBorderHover = Rgb(0x9C, 0xC7, 0xD8);

        /// <summary>字段常态底色。</summary>
        public static readonly IBrush FieldSurface = Rgb(0xFF, 0xFF, 0xFF);

        /// <summary>字段聚焦底色：极浅青，与描边一起表达「正在编辑」。</summary>
        public static readonly IBrush FieldFocusSurface = Rgb(0xFB, 0xFE, 0xFF);

        /// <summary>下拉框底色：比输入框浅一档，暗示「可选」。</summary>
        public static readonly IBrush ComboSurface = Rgb(0xF7, 0xFB, 0xFD);

        /// <summary>同步开关条底色。</summary>
        public static readonly IBrush SyncSurface = Rgb(0xF1, 0xFA, 0xFD);

        /// <summary>同步开关条描边。</summary>
        public static readonly IBrush SyncBorder = Rgb(0xD6, 0xEB, 0xF4);

        /// <summary>预览舞台渐变顶色。</summary>
        public static readonly Color PreviewTop = Color.FromRgb(0xFF, 0xFF, 0xFF);

        /// <summary>预览舞台渐变收束色。</summary>
        public static readonly Color PreviewBottom = Color.FromRgb(0xEF, 0xF8, 0xFC);

        /// <summary>预览棋盘格深色块。</summary>
        public static readonly IBrush PreviewCellA = Rgb(0xEE, 0xF8, 0xFC);

        /// <summary>预览棋盘格浅色块。</summary>
        public static readonly IBrush PreviewCellB = Rgb(0xFF, 0xFF, 0xFF);

        /// <summary>预览网格线。</summary>
        public static readonly IBrush PreviewStroke = Rgb(0x93, 0xCC, 0xDE);

        /// <summary>底部摘要胶囊底色。</summary>
        public static readonly IBrush SummarySurface = Rgb(0xF7, 0xFB, 0xFD);

        /// <summary>底栏底色。</summary>
        public static readonly IBrush FooterSurface = Rgb(0xFF, 0xFF, 0xFF);

        /// <summary>幽灵按钮文字（恢复默认）。</summary>
        public static readonly IBrush GhostText = Rgb(0x54, 0x70, 0x7C);

        /// <summary>主按钮渐变起色。</summary>
        public static readonly Color PrimaryTop = Color.FromRgb(0x17, 0xA4, 0xC9);

        /// <summary>主按钮渐变收束色。</summary>
        public static readonly Color PrimaryBottom = Color.FromRgb(0x0B, 0x84, 0xAE);

        /// <summary>尺寸不整除等需要注意的提示色（暖棕，不用纯红）。</summary>
        public static readonly IBrush WarnText = Rgb(0xA9, 0x70, 0x2F);
    }

    #endregion

    #region 发布窗色

    /// <summary>
    /// 发布地图弹窗的深色配色：窗口壳、输入/预览卡片、说明文字与错误提示。
    /// 集中在这里后，界面装配代码只表达控件结构，不再夹带色值。
    /// </summary>
    public static class PublishWindow
    {
        /// <summary>窗口与内容根底色（深蓝夜空）。</summary>
        public static readonly IBrush Background = Rgb(0x0B, 0x24, 0x30);

        /// <summary>发布窗自绘标题栏渐变起始色：比窗体底色略亮的深海蓝。</summary>
        public static readonly Color TitleBarBackgroundLight = Color.FromRgb(0x1C, 0x52, 0x67);

        /// <summary>发布窗自绘标题栏渐变收束色，与窗体深蓝背景一致。</summary>
        public static readonly Color TitleBarBackground = Color.FromRgb(0x0B, 0x24, 0x30);

        /// <summary>发布窗标题栏文字 / 图标颜色。</summary>
        public static readonly Color TitleBarText = Color.FromRgb(0xD6, 0xEC, 0xF5);

        /// <summary>发布窗标题栏边框颜色。</summary>
        public static readonly Color TitleBarBorder = Color.FromRgb(0x2C, 0x4F, 0x60);

        /// <summary>副标题等次要文字。</summary>
        public static readonly IBrush SubtitleText = Rgb(0xA7, 0xC2, 0xCF);

        /// <summary>标题下方分隔线。</summary>
        public static readonly IBrush Divider = Rgb(0x22, 0x41, 0x4F);

        /// <summary>预览卡片底色。</summary>
        public static readonly IBrush PreviewSurface = Rgb(0x10, 0x2C, 0x3A);

        /// <summary>预览卡片描边。</summary>
        public static readonly IBrush PreviewBorder = Rgb(0x2C, 0x4F, 0x60);

        /// <summary>预览内容文字。</summary>
        public static readonly IBrush PreviewText = Rgb(0xD6, 0xEC, 0xF5);

        /// <summary>底部说明与路径提示文字。</summary>
        public static readonly IBrush NoteText = Rgb(0x8F, 0xB2, 0xC0);

        /// <summary>发布失败/校验错误文字。</summary>
        public static readonly IBrush ErrorText = Rgb(0xFF, 0x8A, 0x8A);

        /// <summary>表单左侧字段标签文字。</summary>
        public static readonly IBrush LabelText = Rgb(0xC5, 0xE2, 0xEC);
    }

    #endregion

    #region 九宫格预览色

    /// <summary>九宫格预览的棋盘格、切片填充与拖拽手柄配色。</summary>
    public static class NineSlice
    {
        /// <summary>预览背景底色（深蓝）。</summary>
        public static readonly IBrush Backdrop = Rgb(0x10, 0x2A, 0x33);

        /// <summary>棋盘格深色块，与背景形成透明区参照。</summary>
        public static readonly IBrush CheckerDark = Rgb(0x0A, 0x1F, 0x26);

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
        public static readonly IBrush Frame = Rgb(0x5B, 0x78, 0x85);

        /// <summary>底部尺寸与边距说明文字。</summary>
        public static readonly IBrush Hint = Rgb(0xD8, 0xD8, 0xD8);

        /// <summary>边距越界提示文字。</summary>
        public static readonly IBrush Error = Rgb(0xFF, 0x9A, 0x8A);

        /// <summary>拖拽手柄描边，保证手柄在浅色素材上也可见。</summary>
        public static readonly IBrush HandleBorder = Rgb(0x0F, 0x23, 0x2B);
    }

    #endregion

}



