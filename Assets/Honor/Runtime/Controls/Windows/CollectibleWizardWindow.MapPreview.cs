/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CollectibleWizardWindow.MapPreview.cs
 * author:    云毅
 * created:   2026
 * descrip:   收集物向导第一步的地图预览卡与尺寸信息胶囊。
 ***************************************************************/
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;
namespace MapEditorTool.Controls;

public sealed partial class CollectibleWizardWindow : Window
{
    // ---TODO 重构：地图预览卡独立成 partial，第一步表单不再承担绘制职责。
    #region 地图预览

    private Control BuildMapPreview()
    {
        Bitmap? bitmap = _assets.LoadByLayer(
            _config.BgName,
            MapLayerKeys.Background,
            _config.ChapterId,
            _config.SceneId);
        bitmap ??= _assets.Load(_config.BgName);

        var sceneName = string.IsNullOrWhiteSpace(_config.SceneName)
            ? "当前关卡"
            : _config.SceneName;

        var card = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(UiTheme.WizardMapPreview.BackdropTop, 0),
                    new GradientStop(UiTheme.WizardMapPreview.BackdropBottom, 1),
                },
            },
            BorderBrush = UiTheme.WizardAccentDark,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            MinHeight = 360,
        };

        var frame = new Grid();
        card.Child = frame;

        if (bitmap is not null)
        {
            frame.Children.Add(new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            frame.Children.Add(new Border
            {
                Background = UiTheme.WizardMapPreview.ImageShade,
            });
        }
        else
        {
            var empty = new StackPanel
            {
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(28, 0, 28, 38),
            };
            empty.Children.Add(new Border
            {
                Width = 62,
                Height = 62,
                CornerRadius = new CornerRadius(18),
                Background = UiTheme.WizardMapPreview.EmptyGlyphSurface,
                BorderBrush = UiTheme.WizardMapPreview.EmptyGlyphBorder,
                BorderThickness = new Avalonia.Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "◇",
                    Foreground = UiTheme.WizardMapPreview.EmptyGlyph,
                    FontSize = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
            empty.Children.Add(new TextBlock
            {
                Text = "地图底图暂未载入",
                Foreground = UiTheme.White,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            empty.Children.Add(new TextBlock
            {
                Text = "仍可继续设置收集物；进入地图后会在这里显示场景预览。",
                Foreground = UiTheme.WizardMapPreview.EmptyHint,
                FontSize = UiMetrics.FontBody,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 330,
            });
            frame.Children.Add(empty);
        }

        var previewTag = new Border
        {
            Background = UiTheme.WizardMapPreview.TagSurface,
            BorderBrush = UiTheme.WizardMapPreview.TagBorder,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Avalonia.Thickness(9, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(14),
        };
        var tagRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        tagRow.Children.Add(UiFactory.MarkerDot(UiTheme.WizardAccent));
        tagRow.Children.Add(new TextBlock
        {
            Text = "场景预览",
            Foreground = UiTheme.White,
            FontSize = UiMetrics.FontCaption,
            FontWeight = FontWeight.SemiBold,
        });
        previewTag.Child = tagRow;
        frame.Children.Add(previewTag);

        var info = new Border
        {
            Background = UiTheme.WizardMapPreview.InfoSurface,
            Padding = new Avalonia.Thickness(16, 12),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        var infoStack = new StackPanel { Spacing = 7 };
        info.Child = infoStack;
        infoStack.Children.Add(new TextBlock
        {
            Text = sceneName,
            Foreground = UiTheme.White,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var stats = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
        };
        AddStatChip(stats, $"{_config.Width} × {_config.Height}");
        AddStatChip(stats, $"{_config.GridCols} × {_config.GridRows} 格");
        AddStatChip(stats, $"{_config.Objects.Count} 个物件");
        infoStack.Children.Add(stats);
        frame.Children.Add(info);

        return card;
    }

    /// <summary>添加地图尺寸/网格/物件数量信息胶囊。</summary>
    /// <param name="host">目标容器。</param>
    /// <param name="text">胶囊文案。</param>
    private static void AddStatChip(Panel host, string text)
    {
        host.Children.Add(new Border
        {
            Background = UiTheme.WizardMapPreview.StatChipSurface,
            CornerRadius = new CornerRadius(7),
            Padding = new Avalonia.Thickness(8, 3),
            Margin = new Avalonia.Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = text,
                Foreground = UiTheme.WizardMapPreview.StatChipText,
                FontSize = UiMetrics.FontCaption,
            },
        });
    }

    #endregion
}
