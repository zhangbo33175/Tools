/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapConfigWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图配置弹窗（1:1 复刻 Unity MapConfigWindow：六路径 + 基线步长）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

#region 类定义

/// <summary>
/// 地图配置弹窗。对应 Unity 版 <c>MapConfigWindow</c>（uxml 3–46 行）：
/// 六条路径（章节配置表 / 主关卡配置表 / 两个物体预览图目录 / 分区背景图目录 / 地图保存目录）、
/// 基线步长，底部取消 / 确定。确定后把新设置交给调用方落盘与生效。
/// </summary>
public sealed class MapConfigWindow : Window
{
    #region 常量

    /// <summary>标签列宽，六条「…目录：」标签右对齐到同一列。</summary>
    private const double LabelWidth = 128.0;

    /// <summary>输入框高度。</summary>
    private const double FieldHeight = 26.0;

    /// <summary>「浏览…」按钮宽度。</summary>
    private const double BrowseButtonWidth = 64.0;

    /// <summary>基线步长输入框宽度。</summary>
    private const double NumberFieldWidth = 96.0;

    /// <summary>底部按钮宽度。</summary>
    private const double DialogButtonWidth = 78.0;

    /// <summary>弹窗宽度。</summary>
    private const double DialogWidth = 812.0;

    private static readonly IBrush CardBorderBrush = UiTheme.Form.GroupBorder;
    private static readonly IBrush FieldBorderBrush = UiTheme.Form.FieldBorder;

    #endregion

    #region 字段

    private readonly string? _projectRoot;

    private readonly List<PathField> _pathFields = new();

    private readonly TextBox _baselineBox;

    private readonly TextBlock _hintLabel;

    #endregion

    #region 构造

    /// <summary>初始化弹窗。</summary>
    /// <param name="settings">当前设置，窗口内编辑的是它的副本。</param>
    /// <param name="projectRoot">Unity 工程根，用于路径选择器的初始目录与相对化。</param>
    public MapConfigWindow(EditorSettingsData settings, string? projectRoot)
    {
        _projectRoot = projectRoot;
        Settings = settings.Clone();

        Title = "地图配置";
        Width = DialogWidth;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RequestedThemeVariant = ThemeVariant.Light;

        _baselineBox = Field(
            Settings.BaselineStep.ToString(CultureInfo.InvariantCulture),
            NumberFieldWidth);
        _hintLabel = new TextBlock
        {
            Text = "基线步长下限 10：八层基线按 0 / 步长 / 2·步长 … 等差拉开，改完点「刷新层级」：先重分析遮挡，再重排现有实例",
            FontSize = 11,
            Foreground = UiTheme.TextSecondary,
            IsVisible = false,
            TextWrapping = TextWrapping.NoWrap,
        };

        Content = BuildRoot();
    }

    #endregion

    #region 公开属性

    /// <summary>窗口里编辑中的设置（确定后交给调用方）。</summary>
    public EditorSettingsData Settings { get; }

    #endregion

    #region 界面装配

    private Control BuildRoot()
    {
        var content = new StackPanel { Spacing = 12 };

        content.Children.Add(BuildPathCard());
        content.Children.Add(BuildBaselineRow());
        content.Children.Add(_hintLabel);
        content.Children.Add(BuildFooter());

        return new Border
        {
            Background = UiTheme.PanelSurface,
            Padding = new Thickness(14),
            Child = content,
        };
    }

    /// <summary>六条路径的白色卡片（对应 uxml 的 <c>config-box</c>）。</summary>
    private Control BuildPathCard()
    {
        var rows = new StackPanel { Spacing = 8 };

        AddPathRow(rows, "章节配置表：", file: true, Settings.TableChaptersPath);
        AddPathRow(rows, "主关卡配置表：", file: true, Settings.TableMainLevelsPath);
        AddPathRow(rows, "地图物件配置表文件夹：", file: false, Settings.ItemElementTablesDirectory);
        AddPathRow(rows, "物体预览图目录：", file: false, Settings.ObjectPreviewTexturesDirectory);
        AddPathRow(rows, "分区背景图目录：", file: false, Settings.LevelBackgroundDirectory);
        AddPathRow(rows, "地图保存目录：", file: false, Settings.MapSaveDirectory);

        return new Border
        {
            Background = UiTheme.Card,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14, 12, 14, 12),
            Child = rows,
        };
    }

    /// <summary>基线步长行（对应 uxml 的 <c>step-row</c>）。</summary>
    private Control BuildBaselineRow()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},Auto,*") };
        grid.Children.Add(Label("基线步长："));

        _baselineBox.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_baselineBox, 1);
        grid.Children.Add(_baselineBox);

        var hint = new TextBlock
        {
            Text = "相邻显示层 SortOrder 基线间距",
            FontSize = 11,
            Foreground = UiTheme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };
        Grid.SetColumn(hint, 2);
        grid.Children.Add(hint);

        return grid;
    }

    /// <summary>底部取消 / 确定（对应 uxml 的 <c>btn-row</c>）。</summary>
    private Control BuildFooter()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };

        var cancel = SmallButton("取消", DialogButtonWidth);
        cancel.Height = 28;
        cancel.Margin = new Thickness(0, 4, 8, 0);
        cancel.VerticalAlignment = VerticalAlignment.Top;
        cancel.Click += (_, _) => Close(null);
        Grid.SetColumn(cancel, 1);
        grid.Children.Add(cancel);

        var confirm = SmallButton("确定", DialogButtonWidth);
        confirm.Height = 28;
        confirm.Margin = new Thickness(0, 4, 0, 0);
        confirm.VerticalAlignment = VerticalAlignment.Top;
        confirm.Background = UiTheme.Accent;
        confirm.BorderBrush = UiTheme.AccentDark;
        confirm.Foreground = UiTheme.White;
        confirm.Click += (_, _) => Confirm();
        Grid.SetColumn(confirm, 2);
        grid.Children.Add(confirm);

        return grid;
    }

    private void AddPathRow(StackPanel host, string label, bool file, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*,Auto") };
        grid.Children.Add(Label(label));

        var box = Field(value);
        box.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);

        var field = new PathField(box, file);
        _pathFields.Add(field);

        var browse = SmallButton("浏览…", BrowseButtonWidth);
        browse.Margin = new Thickness(8, 0, 0, 0);
        browse.Click += async (_, _) => await PickPathAsync(field);
        Grid.SetColumn(browse, 2);
        grid.Children.Add(browse);

        host.Children.Add(grid);
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = UiTheme.TextPrimary,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
    };

    private static Button SmallButton(string text, double width) => new()
    {
        Content = text,
        Width = width,
        Height = FieldHeight,
        FontSize = 12,
        Padding = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    /// <summary>统一输入框：12px、26 高、白底细边、圆角 2。</summary>
    /// <param name="value">初始文本。</param>
    /// <param name="width">固定宽度，省略时撑满所在列。</param>
    private static TextBox Field(string value, double? width = null) => new()
    {
        Text = value,
        Height = FieldHeight,
        FontSize = 12,
        Padding = new Thickness(6, 2),
        CornerRadius = new CornerRadius(2),
        Background = UiTheme.Card,
        BorderBrush = FieldBorderBrush,
        BorderThickness = new Thickness(1),
        VerticalContentAlignment = VerticalAlignment.Center,
        Width = width ?? double.NaN,
    };

    #endregion

    #region 文件与目录选择

    /// <summary>路径行：文本框 + 该行选的是文件还是目录。</summary>
    private sealed class PathField
    {
        /// <summary>初始化路径行。</summary>
        /// <param name="box">文本框。</param>
        /// <param name="isFile">true 选文件，false 选目录。</param>
        public PathField(TextBox box, bool isFile)
        {
            Box = box;
            IsFile = isFile;
        }

        /// <summary>文本框。</summary>
        public TextBox Box { get; }

        /// <summary>是否文件型配置。</summary>
        public bool IsFile { get; }
    }

    private async Task PickPathAsync(PathField field)
    {
        var start = await ResolveStartFolderAsync(field.Box.Text, field.IsFile);

        if (field.IsFile)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择配置文件",
                AllowMultiple = false,
                SuggestedStartLocation = start,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("配置表")
                    {
                        Patterns = new[] { "*.xlsm", "*.xlsx", "*.csv", "*.json" },
                    },
                },
            });

            if (files.Count == 0)
            {
                return;
            }

            ApplyPicked(field.Box, files[0].TryGetLocalPath());
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择目录",
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        if (folders.Count == 0)
        {
            return;
        }

        ApplyPicked(field.Box, folders[0].TryGetLocalPath());
    }

    /// <summary>把选中的绝对路径按 Unity 口径转成相对路径写回文本框。</summary>
    private void ApplyPicked(TextBox box, string? localPath)
    {
        if (string.IsNullOrEmpty(localPath))
        {
            return;
        }

        box.Text = EditorSettings.MakeProjectRelative(localPath, _projectRoot);
    }

    /// <summary>解析选择器的初始目录：优先当前配置所在目录，其次 Assets 目录。</summary>
    private async Task<IStorageFolder?> ResolveStartFolderAsync(string? current, bool isFile)
    {
        var assets = _projectRoot is null ? null : Path.Combine(_projectRoot, "Assets");
        var absolute = EditorSettings.ResolveProjectPath(current, _projectRoot);
        var target = absolute is null
            ? assets
            : isFile ? Path.GetDirectoryName(absolute) : absolute;

        if (target is null || !Directory.Exists(target))
        {
            return null;
        }

        return await StorageProvider.TryGetFolderFromPathAsync(target);
    }

    #endregion

    #region 确定

    /// <summary>收集六个路径 + 基线步长，校验通过后以设置作为对话框结果关闭。</summary>
    private void Confirm()
    {
        Settings.TableChaptersPath = _pathFields[0].Box.Text?.Trim() ?? string.Empty;
        Settings.TableMainLevelsPath = _pathFields[1].Box.Text?.Trim() ?? string.Empty;
        Settings.ItemElementTablesDirectory = _pathFields[2].Box.Text?.Trim() ?? string.Empty;
        Settings.ObjectPreviewTexturesDirectory = _pathFields[3].Box.Text?.Trim() ?? string.Empty;
        Settings.LevelBackgroundDirectory = _pathFields[4].Box.Text?.Trim() ?? string.Empty;
        Settings.MapSaveDirectory = _pathFields[5].Box.Text?.Trim() ?? string.Empty;

        // 基线步长：非数字按当前值回退，越界按上下限钳制，提示行说明实际结果
        var raw = _baselineBox.Text?.Trim() ?? string.Empty;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var step))
        {
            step = Settings.BaselineStep;
        }

        var clamped = EditorSettings.ClampBaselineStep(step);
        Settings.BaselineStep = clamped;
        _baselineBox.Text = clamped.ToString(CultureInfo.InvariantCulture);

        if (clamped != step)
        {
            _hintLabel.Text = $"基线步长已钳到 {clamped}（允许范围 {EditorSettingsDefaults.BaselineStepMin}–{EditorSettingsDefaults.BaselineStepMax}）";
            _hintLabel.Foreground = UiTheme.Error;
            _hintLabel.IsVisible = true;
            return;
        }

        Close(Settings);
    }

    #endregion
}

#endregion
