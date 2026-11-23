/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CollectibleWizardWindow.Quantity.cs
 * author:    云毅
 * created:   2026
 * descrip:   收集物向导第一步：目标数量、层级与地图预览
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class CollectibleWizardWindow : Window
{
    #region 第一步：数量与层级

    private Control BuildQuantityStep()
    {
        var card = new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Avalonia.Thickness(16),
        };

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 14,
        };

        var eyebrow = new TextBlock
        {
            Text = "步骤 1 / 2 · 目标数量",
            Foreground = UiTheme.WizardAccent,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
        };
        root.Children.Add(eyebrow);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.15*,1*"),
            ColumnSpacing = 18,
        };
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var preview = BuildMapPreview();
        body.Children.Add(preview);

        var form = new StackPanel
        {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(form, 1);
        body.Children.Add(form);

        form.Children.Add(new TextBlock
        {
            Text = "这张地图最多能收集多少个收集物？",
            Foreground = UiTheme.PreviewText,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        form.Children.Add(new TextBlock
        {
            Text = "数量用于随机生成和进度统计，之后仍可手动增删。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        var numberRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10,
            Margin = new Avalonia.Thickness(0, 4, 0, 0),
        };
        numberRow.Children.Add(new TextBlock
        {
            Text = "目标数量",
            Foreground = UiTheme.PreviewText,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(_totalBox, 1);
        numberRow.Children.Add(_totalBox);
        form.Children.Add(numberRow);

        var layerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10,
        };
        layerRow.Children.Add(new TextBlock
        {
            Text = "显示层级",
            Foreground = UiTheme.PreviewText,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(_layerCombo, 1);
        layerRow.Children.Add(_layerCombo);
        form.Children.Add(layerRow);

        form.Children.Add(new TextBlock
        {
            Text = "「物件下」会被建筑、树、道具挡住；「物件上」始终盖在普通物件之上、前景之下。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        var tip = new Border
        {
            Background = UiTheme.WizardInfoSurface,
            BorderBrush = UiTheme.WizardInfoBorder,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Avalonia.Thickness(10, 8),
        };
        tip.Child = new TextBlock
        {
            Text = "完成后左侧会出现「收集物」切页；点素材再点地图，或直接把素材拖进地图，也可一次随机补齐。",
            Foreground = UiTheme.WizardInfoText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        form.Children.Add(tip);

        card.Child = root;
        return card;
    }

    private Control BuildMapPreview()
    {
        var card = new Border
        {
            Background = UiTheme.WizardMapSurface,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Avalonia.Thickness(10),
            MinHeight = 300,
        };

        var stack = new StackPanel { Spacing = 8 };
        card.Child = stack;

        Bitmap? bitmap = _assets.LoadByLayer(
            _config.BgName,
            MapLayerKeys.Background,
            _config.ChapterId,
            _config.SceneId);
        bitmap ??= _assets.Load(_config.BgName);

        var imageFrame = new Border
        {
            Background = UiTheme.WizardMapBackdrop,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Height = 255,
        };
        if (bitmap is not null)
        {
            imageFrame.Child = new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else
        {
            imageFrame.Child = new TextBlock
            {
                Text = "当前地图暂无底图预览",
                Foreground = Brushes.White,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        stack.Children.Add(imageFrame);
        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_config.SceneName) ? "当前关卡" : _config.SceneName,
            Foreground = UiTheme.PreviewText,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"{_config.Width} × {_config.Height} · {_config.GridCols} × {_config.GridRows} 格 · {_config.Objects.Count} 个物件",
            Foreground = UiTheme.PreviewHint,
            FontSize = 11,
        });
        return card;
    }

    #endregion
}
