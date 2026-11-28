/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.Tiles.cs
 * author:    云毅
 * created:   2026
 * descrip:   瓦片调色板面板配色：缩略图选中态、九宫格三态、规则行底色
 ***************************************************************/

using Avalonia.Media;

namespace MapEditorTool.Controls;

public static partial class UiTheme
{
    #region 瓦片调色板

    /// <summary>瓦片面板配色：延续低饱和青蓝，与资源列表、网格窗保持同一套色阶。</summary>
    public static class Tiles
    {
        /// <summary>缩略图格底。</summary>
        public static readonly IBrush ThumbSurface = Rgb(0xF7, 0xFC, 0xFE);

        /// <summary>缩略图描边。</summary>
        public static readonly IBrush ThumbBorder = Rgb(0xD8, 0xE5, 0xEE);

        /// <summary>缩略图选中描边（强调青）。</summary>
        public static readonly IBrush ThumbSelected = Rgb(0x0D, 0x8C, 0xB6);

        /// <summary>缩略图选中底。</summary>
        public static readonly IBrush ThumbSelectedSurface = Rgb(0xDD, 0xF2, 0xF9);

        /// <summary>规则行交替底。</summary>
        public static readonly IBrush RuleRow = Rgb(0xF7, 0xFB, 0xFD);

        /// <summary>九宫格「必须有」：绿，与画布落位格同源。</summary>
        public static readonly IBrush NeighborRequired = Rgb(0x2E, 0x9E, 0x6E);

        /// <summary>九宫格「必须没有」：红。</summary>
        public static readonly IBrush NeighborForbidden = Rgb(0xC0, 0x50, 0x4D);

        /// <summary>九宫格「无所谓」：灰。</summary>
        public static readonly IBrush NeighborAny = Rgb(0xA9, 0xC0, 0xCB);

        /// <summary>九宫格空格底。</summary>
        public static readonly IBrush NeighborSurface = Rgb(0xFF, 0xFF, 0xFF);

        /// <summary>编辑态激活时的按钮底（Unity 里 Edit* 的高亮观感）。</summary>
        public static readonly IBrush EditActiveSurface = Rgb(0xDD, 0xF2, 0xF9);

        /// <summary>编辑态激活时的按钮描边。</summary>
        public static readonly IBrush EditActiveBorder = Rgb(0x0D, 0x8C, 0xB6);
    }

    #endregion
}
