/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NumberStepper.cs
 * author:    云毅
 * created:   2026
 * descrip:   统一的数字步进输入控件：主题化输入框 + 贴合的上下箭头，
 *            替代各窗口分散实现的原生 NumericUpDown 与独立加减按钮。
 ***************************************************************/

using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls
{
    /// <summary>
    /// 主题化数字步进器。输入框与箭头共用同一外框，箭头固定在输入框右侧，
    /// 支持直接输入、失焦提交、Enter 提交以及键盘上下键调整。
    /// </summary>
    public sealed class NumberStepper : UserControl
    {
        #region 字段与部件

        private readonly TextBox _field = new();
        private readonly Button _increaseButton = new();
        private readonly Button _decreaseButton = new();
        private decimal _value;
        private bool _synchronizing;

        #endregion

        #region 配置属性

        /// <summary>允许的最小值。</summary>
        public decimal Minimum { get; set; }

        /// <summary>允许的最大值。</summary>
        public decimal Maximum { get; set; } = 9999m;

        /// <summary>每次点击箭头或按上下键的步长。</summary>
        public decimal Step { get; set; } = 1m;

        /// <summary>当前值，写入时会自动夹紧到 <see cref="Minimum"/> 与 <see cref="Maximum"/> 之间。</summary>
        public decimal Value
        {
            get => _value;
            set => SetValue(value);
        }

        /// <summary>值发生变化时触发。</summary>
        public event EventHandler<decimal>? ValueChanged;

        #endregion

        #region 构造

        /// <summary>创建数字步进器。</summary>
        public NumberStepper()
        {
            Height = 34;
            MinWidth = 96;

            _field.Classes.Add("field");
            _field.Background = UiTheme.Transparent;
            _field.BorderThickness = new Thickness(0);
            _field.Padding = new Thickness(8, 0, 6, 0);
            _field.FontSize = 15;
            _field.TextAlignment = TextAlignment.Right;
            _field.VerticalContentAlignment = VerticalAlignment.Center;
            _field.LostFocus += (_, _) => CommitText();
            _field.KeyDown += OnFieldKeyDown;

            ConfigureSpinButton(_increaseButton, "▲");
            ConfigureSpinButton(_decreaseButton, "▼");
            _increaseButton.Click += (_, _) =>
            {
                CommitText();
                SetValue(_value + Step);
            };
            _decreaseButton.Click += (_, _) =>
            {
                CommitText();
                SetValue(_value - Step);
            };

            var spinner = new Grid
            {
                RowDefinitions = new RowDefinitions("*,*"),
                Background = UiTheme.Card,
            };
            spinner.Children.Add(_increaseButton);
            spinner.Children.Add(_decreaseButton);
            Grid.SetRow(_decreaseButton, 1);

            var spinnerFrame = new Border
            {
                Background = UiTheme.Card,
                BorderBrush = UiTheme.Divider,
                BorderThickness = new Thickness(1, 0, 0, 0),
                Child = spinner,
            };

            var layout = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,26"),
            };
            layout.Children.Add(_field);
            layout.Children.Add(spinnerFrame);
            Grid.SetColumn(spinnerFrame, 1);

            Content = new Border
            {
                Background = UiTheme.Card,
                BorderBrush = UiTheme.PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = layout,
            };

            SetValue(_value, notify: false, forceText: true);
        }

        #endregion

        #region 交互

        /// <summary>把输入框文本提交为数值；非法文本回退到当前值。</summary>
        public void CommitText()
        {
            if (_synchronizing)
            {
                return;
            }

            var text = _field.Text?.Trim();
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
            {
                SetValue(parsed);
                return;
            }

            SetValue(_value, notify: false, forceText: true);
        }

        /// <summary>让输入框获得焦点，便于外部在打开表单时直接定位。</summary>
        public void FocusField() => _field.Focus();

        private void OnFieldKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    CommitText();
                    e.Handled = true;
                    break;
                case Key.Up:
                    CommitText();
                    SetValue(_value + Step);
                    e.Handled = true;
                    break;
                case Key.Down:
                    CommitText();
                    SetValue(_value - Step);
                    e.Handled = true;
                    break;
            }
        }

        private void ConfigureSpinButton(Button button, string glyph)
        {
            button.Content = glyph;
            button.Classes.Add("spin");
            button.Width = double.NaN;
            button.Height = double.NaN;
            button.MinWidth = 0;
            button.MinHeight = 0;
            button.FontSize = 9;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Stretch;
            button.BorderThickness = new Thickness(0);
        }

        #endregion

        #region 数值同步

        private void SetValue(decimal value, bool notify = true, bool forceText = false)
        {
            var clamped = Math.Max(Minimum, Math.Min(Maximum, value));
            var changed = clamped != _value;
            _value = clamped;

            if (changed || forceText)
            {
                _synchronizing = true;
                _field.Text = clamped.ToString("0", CultureInfo.CurrentCulture);
                _synchronizing = false;
            }

            if (notify && changed)
            {
                ValueChanged?.Invoke(this, clamped);
            }
        }

        #endregion
    }
}
