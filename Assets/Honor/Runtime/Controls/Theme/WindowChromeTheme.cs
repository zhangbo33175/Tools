/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WindowChromeTheme.cs
 * author:    云毅
 * created:   2026
 * descrip:   Avalonia 自绘窗口标题栏：在客户区顶部铺冰青渐变，保留系统
 *            拖动 / 双击最大化 / 边框缩放，并统一主窗与全部弹窗的标题区。
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace MapEditorTool.Controls
{
    /// <summary>
    /// 统一给应用内窗口装配自绘标题栏。标题栏颜色来自 <see cref="UiTheme"/>，
    /// 通过 Avalonia 的扩展客户区与 ElementRole 命中测试保留原生窗口行为。
    /// </summary>
    public static class WindowChromeTheme
    {
        #region 常量与配色

        /// <summary>标题栏高度（DIP），与系统标题栏接近。</summary>
        private const double TitleBarHeight = 32;

        /// <summary>与 exe 共用的多尺寸应用图标资源。</summary>
        private const string BrandIconUri = "avares://MapEditorTool/Assets/App/MapEditorTool.ico";

        /// <summary>浅色窗口标题栏的渐变、文字与底边。</summary>
        private sealed class Palette
        {
            public required IBrush Background { get; init; }
            public required IBrush Text { get; init; }
            public required IBrush Border { get; init; }
            public required bool Dark { get; init; }
        }

        /// <summary>标题栏品牌立方体的三个可见面与棱线。</summary>
        private static readonly IBrush BrandTop = HorizontalGradient(
            UiTheme.TitleBarBackgroundLight,
            UiTheme.TitleBarBackground,
            UiTheme.TitleBarBackgroundDeep);

        private static readonly IBrush BrandLeft = new SolidColorBrush(Color.FromRgb(0x0E, 0x5B, 0x70));
        private static readonly IBrush BrandRight = new SolidColorBrush(Color.FromRgb(0x09, 0x40, 0x52));
        private static readonly IBrush BrandEdge = new SolidColorBrush(Color.FromRgb(0xF2, 0xFD, 0xFF));

        /// <summary>标题栏与任务栏共用的应用图标；首次装配时从同一套矢量生成。</summary>
        private static WindowIcon? _brandWindowIcon;

        private static readonly Palette LightPalette = new()
        {
            Background = HorizontalGradient(
                UiTheme.TitleBarBackgroundLight,
                UiTheme.TitleBarBackground,
                UiTheme.TitleBarBackgroundDeep),
            Text = new SolidColorBrush(UiTheme.TitleBarText),
            Border = new SolidColorBrush(UiTheme.TitleBarBorder),
            Dark = false,
        };

        private static readonly Palette DarkPalette = new()
        {
            Background = HorizontalGradient(
                UiTheme.PublishWindow.TitleBarBackgroundLight,
                UiTheme.PublishWindow.TitleBarBackground,
                UiTheme.PublishWindow.TitleBarBackground),
            Text = new SolidColorBrush(UiTheme.PublishWindow.TitleBarText),
            Border = new SolidColorBrush(UiTheme.PublishWindow.TitleBarBorder),
            Dark = true,
        };

        #endregion

        #region 对外入口

        /// <summary>把窗口的系统标题栏替换为冰青渐变自绘标题栏。</summary>
        /// <param name="window">目标窗口，需已设置 Content。</param>
        /// <param name="dark">是否使用发布窗深蓝配色。</param>
        /// <param name="closeAction">关闭按钮行为；对话框可传“取消”语义的回调。</param>
        public static void Attach(Window window, bool dark = false, Action? closeAction = null)
        {
            if (window.Content is not Control content)
            {
                return;
            }

            // ---TODO 自绘标题栏：BorderOnly 保留系统边框与缩放区域，标题区由客户区接管。
            window.WindowDecorations = WindowDecorations.BorderOnly;
            window.ExtendClientAreaToDecorationsHint = true;
            window.ExtendClientAreaTitleBarHeightHint = TitleBarHeight;

            var palette = dark ? DarkPalette : LightPalette;
            var titleBar = BuildTitleBar(window, palette, closeAction ?? window.Close);

            var brandIcon = ResolveBrandWindowIcon();
            if (brandIcon is not null && window.Icon is null)
            {
                window.Icon = brandIcon;
            }

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            root.RowDefinitions.Add(new RowDefinition(
                window.SizeToContent == SizeToContent.Manual
                    ? new GridLength(1, GridUnitType.Star)
                    : GridLength.Auto));

            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            // 先把旧内容从窗口逻辑树摘下来，再放进新的根 Grid，避免重复父级。
            window.Content = null;
            Grid.SetRow(content, 1);
            root.Children.Add(content);

            window.Content = root;
        }

        #endregion

        #region 标题栏装配

        /// <summary>
        /// 加载 exe 同款多尺寸图标；首次成功后缓存，供所有窗口复用。
        /// 图标仅影响任务栏 / Alt+Tab，不影响窗口内容。
        /// </summary>
        private static WindowIcon? ResolveBrandWindowIcon()
        {
            if (_brandWindowIcon is not null)
            {
                return _brandWindowIcon;
            }

            try
            {
                using var stream = AssetLoader.Open(new Uri(BrandIconUri));
                _brandWindowIcon = new WindowIcon(stream);
                return _brandWindowIcon;
            }
            catch
            {
                // 图标加载失败不能阻断窗口装配；回退为系统默认图标。
                return null;
            }
        }

        /// <summary>
        /// 构造标题栏左侧的 MT 立方体标识：顶面冰青渐变、两个深青侧面、浅色棱线，
        /// 与空态立方体保持同一套视觉语言，缩到标题栏尺寸仍可辨识。
        /// </summary>
        private static Viewbox BuildBrandMark(double size = 24)
        {
            var canvas = new Canvas { Width = 32, Height = 32 };

            canvas.Children.Add(CreateBrandFacet("M 16,1.5 L 30,9.5 L 16,17.5 L 2,9.5 Z", BrandTop));
            canvas.Children.Add(CreateBrandFacet("M 2,9.5 L 16,17.5 L 16,30.5 L 2,22.5 Z", BrandLeft));
            canvas.Children.Add(CreateBrandFacet("M 16,17.5 L 30,9.5 L 30,22.5 L 16,30.5 Z", BrandRight));

            var label = new TextBlock
            {
                Text = "MT",
                Foreground = Brushes.White,
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 0.2,
            };
            Canvas.SetLeft(label, 7.0);
            Canvas.SetTop(label, 18.8);
            canvas.Children.Add(label);

            return new Viewbox
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Child = canvas,
            };
        }

        /// <summary>构建立方体的单个面，统一浅色棱线。</summary>
        private static Avalonia.Controls.Shapes.Path CreateBrandFacet(string data, IBrush fill)
        {
            return new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Fill = fill,
                Stroke = BrandEdge,
                StrokeThickness = 0.9,
            };
        }

        /// <summary>构造标题栏：左侧主题标记与标题，右侧系统按钮。</summary>
        private static Border BuildTitleBar(Window window, Palette palette, Action closeAction)
        {
            var bar = new Border
            {
                Height = TitleBarHeight,
                Background = palette.Background,
                BorderBrush = palette.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            WindowDecorationProperties.SetElementRole(bar, WindowDecorationsElementRole.TitleBar);

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            layout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var heading = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            heading.Children.Add(BuildBrandMark());
            heading.Children.Add(new TextBlock
            {
                Text = window.Title ?? string.Empty,
                Foreground = palette.Text,
                FontSize = UiMetrics.FontTitle,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 640,
            });

            Grid.SetColumn(heading, 0);
            layout.Children.Add(heading);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            Grid.SetColumn(buttons, 2);

            buttons.Children.Add(CreateCaptionButton(
                CaptionGlyph.Minimize, "最小化", palette, () => window.WindowState = WindowState.Minimized));

            Button? maximizeButton = null;
            if (window.CanResize)
            {
                maximizeButton = CreateCaptionButton(
                    window.WindowState == WindowState.Maximized ? CaptionGlyph.Restore : CaptionGlyph.Maximize,
                    "最大化 / 还原",
                    palette,
                    () => window.WindowState = window.WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized);
                buttons.Children.Add(maximizeButton);
            }

            buttons.Children.Add(CreateCaptionButton(CaptionGlyph.Close, "关闭", palette, closeAction, isClose: true));

            if (maximizeButton is not null)
            {
                // ---TODO 自绘标题栏：窗口状态变化时同步最大化 / 还原字形，避免按钮状态失真。
                window.PropertyChanged += (_, args) =>
                {
                    if (args.Property == Window.WindowStateProperty)
                    {
                        var kind = window.WindowState == WindowState.Maximized
                            ? CaptionGlyph.Restore
                            : CaptionGlyph.Maximize;
                        maximizeButton.Content = BuildCaptionGlyph(kind);
                        ToolTip.SetTip(maximizeButton, kind == CaptionGlyph.Restore ? "还原" : "最大化");
                    }
                };
            }

            layout.Children.Add(buttons);
            bar.Child = layout;
            return bar;
        }

        /// <summary>标题栏系统按钮的字形种类。</summary>
        private enum CaptionGlyph
        {
            /// <summary>最小化：一根短横。</summary>
            Minimize,

            /// <summary>最大化：一个单框。</summary>
            Maximize,

            /// <summary>还原：两个错位的框。</summary>
            Restore,

            /// <summary>关闭：一个叉。</summary>
            Close,
        }

        /// <summary>
        /// 构造系统按钮用的矢量字形。
        /// 不用 Segoe UI Symbol 字符：小字号符号在 ClearType 下会带彩边、笔画粗细也不一致；
        /// 统一 1.4px 圆头描边的几何路径在任意 DPI 下都干净，且能与悬停色对齐。
        /// </summary>
        private static Control BuildCaptionGlyph(CaptionGlyph kind)
        {
            var geometry = kind switch
            {
                CaptionGlyph.Minimize => Geometry.Parse("M 0,0 L 10,0"),
                CaptionGlyph.Maximize => Geometry.Parse("M 0,0 L 10,0 L 10,10 L 0,10 Z"),
                CaptionGlyph.Restore => Geometry.Parse("M 0,3 L 7,3 L 7,10 L 0,10 Z M 3,0 L 10,0 L 10,7 L 7,7"),
                _ => Geometry.Parse("M 0,0 L 10,10 M 10,0 L 0,10"),
            };

            return new Path
            {
                Data = geometry,
                Stroke = new SolidColorBrush(UiTheme.TitleBarGlyph),
                StrokeThickness = 1.4,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };
        }

        /// <summary>构造一个贴边系统按钮；交互仍由 Avalonia 按钮处理。</summary>
        private static Button CreateCaptionButton(
            CaptionGlyph kind,
            string tooltip,
            Palette palette,
            Action action,
            bool isClose = false)
        {
            var button = new Button
            {
                Content = BuildCaptionGlyph(kind),
                Width = 46,
                Height = TitleBarHeight,
                MinWidth = 46,
                MinHeight = TitleBarHeight,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Focusable = false,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            button.Classes.Add("caption");
            if (palette.Dark)
            {
                button.Classes.Add("dark");
            }

            if (isClose)
            {
                button.Classes.Add("close");
            }

            // ---TODO 自绘标题栏：按钮标记为用户交互区，避免整条标题栏的 HTCAPTION 吞掉点击。
            WindowDecorationProperties.SetElementRole(button, WindowDecorationsElementRole.User);

            ToolTip.SetTip(button, tooltip);
            button.Click += (_, _) => action();
            return button;
        }

        /// <summary>构造与卡面标题条同向的横向渐变。</summary>
        private static LinearGradientBrush HorizontalGradient(Color from, Color middle, Color to)
        {
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(from, 0),
                    new GradientStop(middle, 0.55),
                    new GradientStop(to, 1),
                },
            };
        }

        #endregion
    }
}
