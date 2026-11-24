/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ListRowCard.cs
 * author:    云毅
 * created:   2026
 * descrip:   列表卡片行：统一描边、圆角、选中与悬停配色
 ***************************************************************/

#region 命名空间引用
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
#endregion

namespace MapEditorTool.Controls
{
    #region 列表卡片行
    /// <summary>
    /// 列表条目的「卡片行」：1px 描边 + 2px 圆角 + 浅灰底 + 3px 行距，选中时蓝底蓝边、悬停时略提亮。
    /// 章节/物件列表（Button 行）与物品元件、未配置资源等面板（自定义内容行）共用同一套颜色与尺寸常量，
    /// 避免各面板各写一份色值导致风格漂移。
    /// </summary>
    internal sealed class ListRowCard : Border
    {
        #region 常量
        /// <summary>行距（下外边距）。</summary>
        public const double RowGap = 3;

        /// <summary>行高（单行文字条目）。</summary>
        public const double SingleLineHeight = 32;

        /// <summary>行左右内边距。</summary>
        public const double RowPaddingX = 6;

        private static readonly IBrush NormalBack = UiTheme.RowBack;
        private static readonly IBrush NormalBorder = UiTheme.RowBorder;
        private static readonly IBrush HoverBack = UiTheme.RowHoverBack;
        private static readonly IBrush SelectedBack = UiTheme.RowSelectedBack;
        private static readonly IBrush SelectedBorder = UiTheme.RowSelectedBorder;
        #endregion

        #region 字段
        private bool _isSelected;
        private bool _isHovered;
        #endregion

        #region 构造
        /// <summary>
        /// 创建一张卡片行。
        /// </summary>
        /// <param name="height">行高；需容纳行内最高元素（图标 52 时取 62，缩略图 36 时取 46）。</param>
        /// <param name="isSelected">初始是否选中。</param>
        public ListRowCard(double height, bool isSelected = false)
        {
            Height = height;
            Margin = new Thickness(0, 0, 0, RowGap);
            Padding = new Thickness(RowPaddingX, 0);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(2);
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            Cursor = new Cursor(StandardCursorType.Hand);
            _isSelected = isSelected;
            ApplyState();

            PointerEntered += (_, _) =>
            {
                _isHovered = true;
                ApplyState();
            };
            PointerExited += (_, _) =>
            {
                _isHovered = false;
                ApplyState();
            };
        }
        #endregion

        #region 属性
        /// <summary>选中状态；变更后立即刷新底色与描边。</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                ApplyState();
            }
        }
        #endregion

        #region 内部方法
        /// <summary>按「选中 → 悬停 → 常态」的优先级刷新底色与描边。</summary>
        private void ApplyState()
        {
            if (_isSelected)
            {
                Background = SelectedBack;
                BorderBrush = SelectedBorder;
                return;
            }

            Background = _isHovered ? HoverBack : NormalBack;
            BorderBrush = NormalBorder;
        }
        #endregion
    }
    #endregion
}
