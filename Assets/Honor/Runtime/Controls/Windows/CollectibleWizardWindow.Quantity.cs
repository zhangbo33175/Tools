/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CollectibleWizardWindow.Quantity.cs
 * author:    云毅
 * created:   2026
 * descrip:   收集物向导第一步：目标数量与显示层级。
 ***************************************************************/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls;

public sealed partial class CollectibleWizardWindow : Window
{
    #region 第一步：数量与层级

    // ---TODO 视觉改版：左图右表单卡片、地图空态与字段分组。
    private Control BuildQuantityStep()
    {
        DetachFromParent(_totalBox);
        DetachFromParent(_layerCombo);

        var card = UiFactory.PaneCard(cornerRadius: 16, padding: 20);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 16,
        };

        var sectionHead = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        sectionHead.Children.Add(new TextBlock
        {
            Text = "第一步 · 收集规模",
            Foreground = UiTheme.WizardAccent,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var hint = new TextBlock
        {
            Text = "之后仍可在左侧「收集物」切页调整",
            Foreground = UiTheme.PreviewHint,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(hint, 1);
        sectionHead.Children.Add(hint);
        root.Children.Add(sectionHead);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.18*,1*"),
            ColumnSpacing = 20,
        };
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var preview = BuildMapPreview();
        body.Children.Add(preview);

        var form = UiFactory.PaneCard(
            background: UiTheme.WizardInfoSurface,
            border: UiTheme.WizardInfoBorder,
            cornerRadius: 14,
            padding: 18);
        form.VerticalAlignment = VerticalAlignment.Stretch;
        var formStack = new StackPanel
        {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        form.Child = formStack;
        Grid.SetColumn(form, 1);
        body.Children.Add(form);

        formStack.Children.Add(new TextBlock
        {
            Text = "收集规模",
            Foreground = UiTheme.WizardTitle,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
        });
        formStack.Children.Add(new TextBlock
        {
            Text = "这张地图最多能收集多少个收集物？",
            Foreground = UiTheme.PreviewText,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        formStack.Children.Add(new TextBlock
        {
            Text = "数量用于随机生成和进度统计，之后仍可手动增删。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        var fields = UiFactory.PaneCard(cornerRadius: 12, padding: 14);
        fields.Margin = new Avalonia.Thickness(0, 2, 0, 0);
        var fieldStack = new StackPanel { Spacing = 8 };
        fields.Child = fieldStack;

        fieldStack.Children.Add(new TextBlock
        {
            Text = "目标数量",
            Foreground = UiTheme.PreviewText,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
        });
        fieldStack.Children.Add(new TextBlock
        {
            Text = "随机生成时使用的收集物总数，范围 1–9999。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        });
        _totalBox.Margin = new Avalonia.Thickness(0, 2, 0, 0);
        ApplyWizardFieldStyle(_totalBox);
        fieldStack.Children.Add(_totalBox);

        fieldStack.Children.Add(new Border
        {
            Height = 1,
            Background = UiTheme.Divider,
            Margin = new Avalonia.Thickness(0, 8),
        });

        fieldStack.Children.Add(new TextBlock
        {
            Text = "显示层级",
            Foreground = UiTheme.PreviewText,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
        });
        _layerCombo.Margin = new Avalonia.Thickness(0, 2, 0, 0);
        ApplyWizardFieldStyle(_layerCombo);
        fieldStack.Children.Add(_layerCombo);
        fieldStack.Children.Add(new TextBlock
        {
            Text = "「物件下」会被建筑、树、道具挡住；「物件上」始终盖在普通物件之上、前景之下。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        });
        formStack.Children.Add(fields);

        var tip = new Border
        {
            Background = UiTheme.WizardAccentSoft,
            BorderBrush = UiTheme.WizardAccent,
            BorderThickness = new Avalonia.Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(8),
            Padding = new Avalonia.Thickness(12, 9),
        };
        tip.Child = new TextBlock
        {
            Text = "完成后左侧会出现「收集物」切页；点素材再点地图，或直接把素材拖进地图，也可一次随机补齐。",
            Foreground = UiTheme.WizardInfoText,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        };
        formStack.Children.Add(tip);

        card.Child = root;
        return card;
    }

    /// <summary>应用收集规模表单字段的统一尺寸与描边。</summary>
    /// <param name="field">数量框或层级下拉框。</param>
    private static void ApplyWizardFieldStyle(TemplatedControl field)
    {
        field.Width = double.NaN;
        field.Height = 38;
        field.HorizontalAlignment = HorizontalAlignment.Stretch;
        field.Background = UiTheme.Card;
        field.BorderBrush = UiTheme.Divider;
        field.BorderThickness = new Avalonia.Thickness(1);
    }

    #endregion
}
