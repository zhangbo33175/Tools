/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementPanel.Form.cs
 * author:    云毅
 * created:   2026
 * descrip:   元件面板表单骨架：折叠分组、字段行与九宫格编辑区。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool.Controls;

public sealed partial class ItemElementPanel : UserControl
{
    #region 表单骨架

    private TextBox _idBox = null!;
    private ComboBox _typeBox = null!;
    private TextBox _nameBox = null!;
    private TextBox _iconBox = null!;
    private TextBox _resNameBox = null!;
    private TextBox _widthBox = null!;
    private TextBox _heightBox = null!;
    private ComboBox _layerKeyBox = null!;
    private TextBox _animBox = null!;
    private CheckBox _loopBox = null!;
    private TextBox _speedBox = null!;
    private CheckBox _interactableBox = null!;
    private ComboBox _interactTypeBox = null!;
    private TextBox _bubbleTextBox = null!;
    private TextBox _bubbleIdBox = null!;
    private TextBox _sortOrderBox = null!;
    private ComboBox _facingBox = null!;
    private CheckBox _collisionBox = null!;
    private CheckBox _shadowBox = null!;
    private ComboBox _displayLayerBox = null!;
    private ComboBox _trafficBox = null!;
    private CheckBox _collectibleBox = null!;
    private TextBox _respawnBox = null!;
    private CheckBox _ySortBox = null!;
    private TextBox _ySortOffsetBox = null!;
    private CheckBox _movableBox = null!;
    private TextBox _moveAnimBox = null!;
    private TextBox _loopRestartBox = null!;
    private CheckBox _hideOffMapBox = null!;
    private CheckBox _teleportBox = null!;
    private TextBox _teleportSceneBox = null!;
    private TextBox _teleportXBox = null!;
    private TextBox _teleportYBox = null!;
    private CheckBox _nineSliceBox = null!;
    private TextBox _nineLeftBox = null!;
    private TextBox _nineRightBox = null!;
    private TextBox _nineTopBox = null!;
    private TextBox _nineBottomBox = null!;
    private ComboBox _nineCenterBox = null!;
    private StackPanel _nineSliceFields = null!;
    private NineSlicePreview _nineSlicePreview = null!;

    /// <summary>构建整张属性表单：一次性创建控件、挂载分组与字段绑定，之后只做回填。</summary>
    private void BuildFormSkeleton()
    {
        _idBox = ReadOnlyBox(FieldWidth);
        _typeBox = Combo(ObjectTypeNames.EditableChoices);
        _nameBox = Text(FieldWidth);
        _iconBox = ReadOnlyBox(FieldWidth);
        _resNameBox = Text(FieldWidth);
        _widthBox = Text(NumberWidth);
        _heightBox = Text(NumberWidth);
        _layerKeyBox = Combo(new[] { TemplateLayers.FollowType }.Concat(TemplateLayers.DisplayChoices).ToList());
        _animBox = Text(FieldWidth);
        _loopBox = Check("循环播放");
        _speedBox = Text(NumberWidth);
        _interactableBox = Check("可交互");
        _interactTypeBox = Combo(TemplateInteractTypes.DisplayChoices);
        _bubbleTextBox = Text(FieldWidth);
        _bubbleIdBox = Text(NumberWidth);
        _sortOrderBox = Text(NumberWidth);
        _facingBox = Combo(FacingNames.DisplayChoices);
        _collisionBox = Check("启用碰撞");
        _shadowBox = Check("显示阴影");
        _displayLayerBox = Combo(TemplateLayers.DisplayChoices.ToList());
        _trafficBox = Combo(TemplateTrafficCategories.DisplayChoices);
        _collectibleBox = Check("可收集");
        _respawnBox = Text(NumberWidth);
        _ySortBox = Check("自动深度排序");
        _ySortOffsetBox = Text(NumberWidth);
        _movableBox = Check("允许移动");
        _moveAnimBox = Text(FieldWidth);
        _loopRestartBox = Text(NumberWidth);
        _hideOffMapBox = Check("自动隐藏");
        _teleportBox = Check("启用传送门");
        _teleportSceneBox = Text(FieldWidth);
        _teleportXBox = Text(NumberWidth);
        _teleportYBox = Text(NumberWidth);
        _nineSliceBox = Check("需要九宫格");
        _nineLeftBox = Text(NumberWidth);
        _nineRightBox = Text(NumberWidth);
        _nineTopBox = Text(NumberWidth);
        _nineBottomBox = Text(NumberWidth);
        _nineCenterBox = Combo(NineSliceModes.DisplayChoices);

        _nineSlicePreview = new NineSlicePreview
        {
            Width = NineSlicePreviewWidth,
            Height = NineSlicePreviewHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = NineSlicePreviewMargin,
        };
        _nineSlicePreview.Changed += (_, _) => OnNineSlicePreviewChanged();

        _groupsHost.Children.Add(BuildGroup("基础信息", content =>
        {
            content.Children.Add(Row(("ID：", _idBox), ("类型：", _typeBox)));
            content.Children.Add(Row(("模板名称：", _nameBox)));
            content.Children.Add(Row(("Icon：", _iconBox)));
            content.Children.Add(Row(("ResName：", _resNameBox)));
            content.Children.Add(Row(("占格宽：", _widthBox), ("占格高：", _heightBox)));
            content.Children.Add(Row(("图层归属：", _layerKeyBox)));
        }));

        _groupsHost.Children.Add(BuildGroup("动画设置", content =>
        {
            content.Children.Add(Row(("默认动画：", _animBox)));
            content.Children.Add(Row(("循环：", _loopBox), ("播放速度：", _speedBox)));
        }));

        _nineSliceFields = new StackPanel { Spacing = GroupContentSpacing };
        _nineSliceFields.Children.Add(new TextBlock
        {
            Text = "仅对勾选「是否缩放」的实例生效：四角保持原尺寸，边与中心按以下方式填充；边距单位是素材原始像素。可直接拖动预览里的绿色边距线，数字框会同步更新。",
            Foreground = UiTheme.TextSecondary,
            FontSize = FormHintFontSize,
            TextWrapping = TextWrapping.Wrap,
            Width = FieldWidth * 2 + LabelWidth + PairGap,
        });
        _nineSliceFields.Children.Add(Row(("左边距：", _nineLeftBox), ("右边距：", _nineRightBox)));
        _nineSliceFields.Children.Add(Row(("上边距：", _nineTopBox), ("下边距：", _nineBottomBox)));
        _nineSliceFields.Children.Add(Row(("中心填充：", _nineCenterBox)));

        _groupsHost.Children.Add(BuildGroup("九宫格设置", content =>
        {
            content.Children.Add(Row(("九宫格：", _nineSliceBox)));
            content.Children.Add(_nineSlicePreview);
            content.Children.Add(_nineSliceFields);
        }));

        _groupsHost.Children.Add(BuildGroup("交互设置", content =>
        {
            content.Children.Add(Row(("可交互：", _interactableBox)));
            content.Children.Add(Row(("交互类型：", _interactTypeBox)));
            content.Children.Add(Row(("对话气泡：", _bubbleTextBox)));
            content.Children.Add(Row(("气泡ID：", _bubbleIdBox)));
        }));

        _groupsHost.Children.Add(BuildGroup("层级与碰撞", content =>
        {
            content.Children.Add(Row(("渲染层级：", _sortOrderBox), ("初始朝向：", _facingBox)));
            content.Children.Add(Row(("碰撞：", _collisionBox), ("阴影：", _shadowBox)));
            content.Children.Add(Row(("层级档：", _displayLayerBox)));
        }));

        _groupsHost.Children.Add(BuildGroup("交通规则", content =>
        {
            content.Children.Add(Row(("交通类别：", _trafficBox)));
        }));

        _groupsHost.Children.Add(BuildGroup("收集物设置", content =>
        {
            content.Children.Add(RowUnit("是否收集物", _collectibleBox, "重生时间", _respawnBox, "秒"));
        }));

        _groupsHost.Children.Add(BuildGroup("Y 坐标深度排序", content =>
        {
            content.Children.Add(Row(("启用 YSort：", _ySortBox), ("偏移：", _ySortOffsetBox)));
        }));

        _groupsHost.Children.Add(BuildGroup("移动与循环", content =>
        {
            content.Children.Add(RowUnit("可移动", _movableBox, "重启延迟", _loopRestartBox, "秒"));
            content.Children.Add(Row(("移动动画：", _moveAnimBox)));
            content.Children.Add(Row(("离开地图隐藏：", _hideOffMapBox)));
        }));

        _groupsHost.Children.Add(BuildGroup("传送门设置", content =>
        {
            content.Children.Add(Row(("是否传送门：", _teleportBox), ("目标场景：", _teleportSceneBox)));
            content.Children.Add(Row(("目标X：", _teleportXBox), ("目标Y：", _teleportYBox)));
        }));

        BindFields();
    }

    /// <summary>构建一个可折叠属性分组：标题行点击切换展开，箭头随之翻转。</summary>
    /// <param name="title">分组标题。</param>
    /// <param name="build">字段构建回调，向传入的面板追加字段行。</param>
    /// <returns>分组容器，由调用方追加到表单。</returns>
    private Control BuildGroup(string title, Action<StackPanel> build)
    {
        // 折叠箭头放在标题左侧，卡片拉满宽度时也不会被甩到最右边
        var arrow = new TextBlock
        {
            Text = ExpandGlyph,
            Foreground = UiTheme.AccentDark,
            FontSize = GroupArrowFontSize,
            Width = GroupArrowWidth,
            TextAlignment = TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var titleText = new TextBlock
        {
            Text = title,
            Foreground = UiTheme.AccentDark,
            FontSize = FormFontSize,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = GroupTitleMargin,
        };

        // 标题左侧一根竖条，比纯文字标题更容易看出分组边界
        var accentBar = new Border
        {
            Width = GroupAccentWidth,
            Height = GroupAccentHeight,
            CornerRadius = new CornerRadius(GroupAccentRadius),
            Background = UiTheme.AccentDark,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var headerContent = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
        headerContent.Children.Add(arrow);
        Grid.SetColumn(accentBar, 1);
        headerContent.Children.Add(accentBar);
        Grid.SetColumn(titleText, 2);
        headerContent.Children.Add(titleText);

        var header = new Border
        {
            Background = GroupHeaderBrush,
            Padding = GroupHeaderPadding,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = headerContent,
        };

        var content = new StackPanel
        {
            Spacing = GroupContentSpacing,
            Margin = GroupContentMargin,
        };
        build(content);

        // 折叠时内容整体隐藏，卡片只留标题条（否则白底空卡片会很突兀）
        var body = new Border
        {
            Background = GroupCardBrush,
            Child = content,
        };

        header.PointerPressed += (_, _) =>
        {
            body.IsVisible = !body.IsVisible;
            arrow.Text = body.IsVisible ? ExpandGlyph : CollapseGlyph;
        };

        header.PointerEntered += (_, _) => header.Background = GroupHeaderHoverBrush;
        header.PointerExited += (_, _) => header.Background = GroupHeaderBrush;

        var card = new StackPanel();
        card.Children.Add(header);
        card.Children.Add(body);

        return new Border
        {
            Background = GroupCardBrush,
            BorderBrush = GroupBorderBrush,
            BorderThickness = new Thickness(GroupCardBorderThickness),
            CornerRadius = new CornerRadius(GroupCardCornerRadius),
            ClipToBounds = true,
            Child = card,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    private Control Row(params (string Label, Control Control)[] cells)
    {
        var columns = string.Join(",", Enumerable.Repeat($"{LabelWidth},{FieldWidth}", cells.Length));
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns + "," + PairGap + ",*"),
            Margin = new Thickness(0),
        };

        for (var index = 0; index < cells.Length; index++)
        {
            AddFieldCell(
                grid,
                cells[index].Label,
                cells[index].Control,
                index * 2,
                index * 2 + 1,
                index == 0 ? 0 : PairGap);
        }

        return grid;
    }

    /// <summary>
    /// 两列且第二列带单位后缀的行（如「重生时间 1 秒」）。
    /// </summary>
    /// <param name="leftLabel">左侧标签。</param>
    /// <param name="leftControl">左侧控件。</param>
    /// <param name="rightLabel">右侧标签。</param>
    /// <param name="rightControl">右侧控件。</param>
    /// <param name="unit">右侧单位文本。</param>
    private Control RowUnit(string leftLabel, Control leftControl, string rightLabel, Control rightControl, string unit)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                $"{LabelWidth},{NumberWidth},{PairGap},{LabelWidth},{NumberWidth},Auto,*"),
            Margin = new Thickness(0),
        };

        AddFieldCell(grid, leftLabel + "：", leftControl, 0, 1, 0);
        AddFieldCell(grid, rightLabel + "：", rightControl, 3, 4, 0);

        var unitText = new TextBlock
        {
            Text = unit,
            Foreground = UiTheme.TextSecondary,
            FontSize = FormFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = UnitLabelMargin,
        };
        Grid.SetColumn(unitText, 5);
        grid.Children.Add(unitText);
        return grid;
    }

    /// <summary>把「标签 + 控件」按固定列位放入行网格，统一两列与单位行的对齐规则。</summary>
    /// <param name="grid">目标行网格。</param>
    /// <param name="labelText">标签文本（调用方负责是否带冒号）。</param>
    /// <param name="control">字段控件。</param>
    /// <param name="labelColumn">标签所在列。</param>
    /// <param name="controlColumn">控件所在列。</param>
    /// <param name="leadingGap">标签左侧额外留白，用于第二列及之后。</param>
    private static void AddFieldCell(
        Grid grid,
        string labelText,
        Control control,
        int labelColumn,
        int controlColumn,
        double leadingGap)
    {
        var label = new TextBlock
        {
            Text = labelText,
            Foreground = UiTheme.TextSecondary,
            FontSize = FormFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(leadingGap, 0, LabelRightGap, 0),
        };

        control.VerticalAlignment = VerticalAlignment.Center;
        control.HorizontalAlignment = HorizontalAlignment.Left;

        Grid.SetColumn(label, labelColumn);
        Grid.SetColumn(control, controlColumn);
        grid.Children.Add(label);
        grid.Children.Add(control);
    }

    private static TextBox Text(double minWidth) => new()
    {
        MinWidth = minWidth,
        Height = FieldHeight,
        Padding = FieldTextPadding,
        FontSize = FormFontSize,
        CornerRadius = new CornerRadius(FieldCornerRadius),
        VerticalContentAlignment = VerticalAlignment.Center,
        Background = FieldFillBrush,
        BorderBrush = FieldBorderBrush,
        BorderThickness = new Thickness(FieldBorderThickness),
    };

    private static TextBox ReadOnlyBox(double minWidth)
    {
        var box = Text(minWidth);
        box.IsReadOnly = true;
        box.Foreground = UiTheme.TextSecondary;
        box.Background = FieldReadOnlyBrush;
        return box;
    }

    private static ComboBox Combo(IReadOnlyList<string> items) => new()
    {
        ItemsSource = items,
        MinWidth = FieldWidth,
        Height = FieldHeight,
        FontSize = FormFontSize,
        Padding = FieldComboPadding,
        CornerRadius = new CornerRadius(FieldCornerRadius),
        HorizontalAlignment = HorizontalAlignment.Left,
        Background = FieldComboBrush,
        BorderBrush = FieldBorderBrush,
        BorderThickness = new Thickness(FieldBorderThickness),
    };

    /// <summary>表单里的勾选框：方框与文字之间固定留一个字的空隙，避免「☑可交互」糊成一团。</summary>
    /// <param name="text">勾选框右侧文字。</param>
    /// <returns>勾选框控件。</returns>
    private static CheckBox Check(string text) => new()
    {
        Content = new TextBlock
        {
            Text = text,
            Margin = new Thickness(CheckLabelGap, 0, 0, 0),
        },
        MinWidth = 0,
        Padding = new Thickness(0),
        FontSize = FormFontSize,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = UiTheme.TextPrimary,
    };

    #endregion
}
