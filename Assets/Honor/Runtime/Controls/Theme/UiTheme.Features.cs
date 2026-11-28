/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.Features.cs
 * author:    云毅
 * created:   2026
 * descrip:   功能页色：交通规则与剧情编辑。
 ***************************************************************/

using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public static partial class UiTheme
{
    #region 交通规则面板色

    /// <summary>
    /// 交通规则面板的语义色：相位色、绑定状态徽标、统计胶囊与自检徽标。
    /// 面板列表的相位条与表单的时序条共用这里的红 / 绿 / 黄，避免两处色值漂移。
    /// </summary>
    public static class Traffic
    {
        /// <summary>红灯相位。</summary>
        public static readonly IBrush PhaseRed = Rgb(0xE5, 0x48, 0x4D);

        /// <summary>绿灯相位。</summary>
        public static readonly IBrush PhaseGreen = Rgb(0x43, 0xA0, 0x47);

        /// <summary>黄灯相位。</summary>
        public static readonly IBrush PhaseYellow = Rgb(0xF6, 0xC4, 0x45);

        /// <summary>黄灯相位上的文字色（浅底上保证对比度）。</summary>
        public static readonly IBrush PhaseYellowText = Rgb(0x5A, 0x43, 0x00);

        /// <summary>已绑定的绿色文字。</summary>
        public static readonly IBrush BoundText = Rgb(0x1B, 0x7F, 0x5F);

        /// <summary>已绑定的绿色浅底。</summary>
        public static readonly IBrush BoundSurface = Rgb(0xE3, 0xF7, 0xF2);

        /// <summary>未绑定的琥珀文字。</summary>
        public static readonly IBrush UnboundText = Rgb(0xA9, 0x78, 0x31);

        /// <summary>未绑定的琥珀浅底。</summary>
        public static readonly IBrush UnboundSurface = Rgb(0xFF, 0xF4, 0xE2);

        /// <summary>统计胶囊底色。</summary>
        public static readonly IBrush StatPillSurface = Rgb(0xEE, 0xF7, 0xFB);

        /// <summary>统计胶囊描边。</summary>
        public static readonly IBrush StatPillBorder = Rgb(0xD2, 0xE6, 0xF0);

        /// <summary>统计胶囊里的数值文字。</summary>
        public static readonly IBrush StatValueText = Rgb(0x07, 0x5E, 0x7C);

        /// <summary>列表行相位条的衬底（无相位时显示）。</summary>
        public static readonly IBrush PhaseStripTrack = Rgb(0xE9, 0xF2, 0xF6);

        /// <summary>规则齐全徽标文字。</summary>
        public static readonly IBrush ReadyText = Rgb(0x14, 0x7A, 0x69);

        /// <summary>规则齐全徽标底色。</summary>
        public static readonly IBrush ReadySurface = Rgb(0xE1, 0xF6, 0xF0);

        /// <summary>待补徽标文字。</summary>
        public static readonly IBrush GapText = Rgb(0xA9, 0x78, 0x31);

        /// <summary>待补徽标底色。</summary>
        public static readonly IBrush GapSurface = Rgb(0xFF, 0xF1, 0xDC);
    }

    #endregion

    #region 剧情编辑色

    /// <summary>
    /// 剧情编辑面板与剧情预览窗的语义色：节点类型色点、舞台、对话框与选项。
    /// 底色沿用低饱和青蓝，只有节点类型用低饱和的点色区分，避免整屏跳色。
    /// </summary>
    public static class Story
    {
        /// <summary>对白节点色点（青蓝）。</summary>
        public static readonly IBrush KindLine = Rgb(0x2B, 0x8C, 0xB4);

        /// <summary>旁白节点色点（灰蓝）。</summary>
        public static readonly IBrush KindNarration = Rgb(0x8D, 0xA9, 0xB6);

        /// <summary>选项节点色点（青绿）。</summary>
        public static readonly IBrush KindChoice = Rgb(0x2E, 0x9E, 0x86);

        /// <summary>章节标题色点（琥珀）。</summary>
        public static readonly IBrush KindChapter = Rgb(0xC2, 0x92, 0x3C);

        /// <summary>结束节点色点（砖红）。</summary>
        public static readonly IBrush KindEnd = Rgb(0xB5, 0x6A, 0x5E);

        /// <summary>节点列表选中行底。</summary>
        public static readonly IBrush NodeSelectedSurface = Rgb(0xDD, 0xF2, 0xF9);

        /// <summary>节点列表选中行描边。</summary>
        public static readonly IBrush NodeSelectedBorder = Rgb(0x70, 0xB4, 0xDA);

        /// <summary>起始节点徽标文字。</summary>
        public static readonly IBrush StartText = Rgb(0x14, 0x7A, 0x69);

        /// <summary>起始节点徽标底。</summary>
        public static readonly IBrush StartSurface = Rgb(0xE1, 0xF6, 0xF0);

        /// <summary>预览舞台渐变顶色（深青）。</summary>
        public static readonly Color StageTop = Color.FromRgb(0x10, 0x3B, 0x4A);

        /// <summary>预览舞台渐变底色（近黑青）。</summary>
        public static readonly Color StageBottom = Color.FromRgb(0x07, 0x1C, 0x26);

        /// <summary>预览对话框底。</summary>
        public static readonly IBrush DialogSurface = Argb(0xF2, 0x0C, 0x24, 0x2E);

        /// <summary>预览对话框描边。</summary>
        public static readonly IBrush DialogBorder = Argb(0x99, 0x6E, 0xBB, 0xCB);

        /// <summary>预览对话框说话人文字。</summary>
        public static readonly IBrush SpeakerText = Rgb(0x8F, 0xE0, 0xE8);

        /// <summary>预览对话框正文。</summary>
        public static readonly IBrush DialogText = Rgb(0xEA, 0xF6, 0xFA);

        /// <summary>选项按钮底。</summary>
        public static readonly IBrush ChoiceSurface = Argb(0xE6, 0x14, 0x4C, 0x5C);

        /// <summary>选项按钮悬停底。</summary>
        public static readonly IBrush ChoiceHover = Argb(0xF2, 0x1B, 0x5F, 0x73);

        /// <summary>选项按钮描边。</summary>
        public static readonly IBrush ChoiceBorder = Argb(0xCC, 0x9F, 0xDD, 0xE6);

        /// <summary>选项按钮文字。</summary>
        public static readonly IBrush ChoiceText = Rgb(0xF2, 0xFB, 0xFE);

        /// <summary>预览里的进度文字。</summary>
        public static readonly IBrush ProgressText = Argb(0xCC, 0xC5, 0xDD, 0xE8);

        /// <summary>演出字段所在的浅底条。</summary>
        public static readonly IBrush EffectStrip = Rgb(0xF4, 0xFA, 0xFD);
    }

    #endregion

}