/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PublishMapWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   发布地图：选择 Unity / Cocos 目标并预检
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 地图发布窗。只负责选择目标、输出目录和发布参数；格式转换全部在 <see cref="MapPublishService"/>。
/// </summary>
public sealed class PublishMapWindow : Window
{
    #region 字段

    private readonly MapConfigData _config;
    private readonly MapFogDocument _fog;
    private readonly MapPublishSettings _settings;
    private ComboBox _targetBox;
    private TextBox _outputBox;
    private TextBox _mapNameBox;
    private CheckBox _fogBox;
    private CheckBox _indexBox;
    private TextBlock _previewText;
    private TextBlock _statusText;
    private StackPanel _mapNameRow;
    private StackPanel _fogRow;
    private StackPanel _indexRow;
    private MapPublishTarget _target;

    #endregion

    #region 构造

    /// <summary>创建发布窗。</summary>
    /// <param name="config">当前地图。</param>
    /// <param name="fog">当前迷雾文档。</param>
    public PublishMapWindow(MapConfigData config, MapFogDocument fog)
    {
        _config = config;
        _fog = fog;
        _settings = MapPublishSettingsStore.Load();
        _target = MapPublishTarget.UnityJson;

        Title = "发布地图";
        Width = 760;
        Height = 600;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiTheme.PublishWindow.Background;

        var root = new Border
        {
            Padding = new Thickness(22, 18, 22, 16),
            Background = UiTheme.PublishWindow.Background,
            Child = BuildContent(out _targetBox, out _outputBox, out _mapNameBox, out _fogBox, out _indexBox,
                out _previewText, out _statusText, out _mapNameRow, out _fogRow, out _indexRow),
        };
        Content = root;

        _targetBox.SelectedIndex = 0;
        _outputBox.Text = _settings.UnityOutputDirectory;
        _mapNameBox.Text = string.IsNullOrWhiteSpace(_settings.MapName) ? "GameMainMap" : _settings.MapName;
        _fogBox.IsChecked = _settings.IncludeFog;
        _indexBox.IsChecked = _settings.UpdateCocosIndex;

        _targetBox.SelectionChanged += (_, _) => OnTargetChanged();
        UpdateVisibility();
        UpdatePreview();
    }

    #endregion

    #region 界面装配

    private Control BuildContent(
        out ComboBox targetBox,
        out TextBox outputBox,
        out TextBox mapNameBox,
        out CheckBox fogBox,
        out CheckBox indexBox,
        out TextBlock previewText,
        out TextBlock statusText,
        out StackPanel mapNameRow,
        out StackPanel fogRow,
        out StackPanel indexRow)
    {
        var stack = new StackPanel { Spacing = 10 };

        var title = new TextBlock
        {
            Text = "发布地图",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.White,
        };
        var subtitle = new TextBlock
        {
            Text = $"{_config.SceneName} · 章节 {ChapterText()} · 关卡 {LevelText()} · " +
                   $"{_config.Objects.Count} 个物件（建筑 {Count(MapObjectType.Building)} / 物件 {Count(MapObjectType.Prop)} / 角色 {Count(MapObjectType.Character)}）",
            FontSize = 12,
            Foreground = UiTheme.PublishWindow.SubtitleText,
        };
        stack.Children.Add(title);
        stack.Children.Add(subtitle);
        stack.Children.Add(new Border { Height = 1, Background = UiTheme.PublishWindow.Divider, Margin = new Thickness(0, 4) });

        targetBox = new ComboBox
        {
            ItemsSource = new[]
            {
                "Unity 数据（MapConfigData JSON）",
                "Cocos 数据（Levels Lua 表）",
            },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        stack.Children.Add(LabeledRow("发布目标", targetBox));

        var outputTextBox = new TextBox
        {
            PlaceholderText = "目标工程的接入目录",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var browseButton = new Button
        {
            Content = "浏览…",
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(12, 4, 12, 4),
        };
        var outputGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        Grid.SetColumn(outputTextBox, 0);
        Grid.SetColumn(browseButton, 1);
        outputGrid.Children.Add(outputTextBox);
        outputGrid.Children.Add(browseButton);
        stack.Children.Add(LabeledRow("输出目录", outputGrid));
        outputBox = outputTextBox;
        browseButton.Click += async (_, _) => await BrowseAsync(outputTextBox);

        mapNameBox = new TextBox
        {
            PlaceholderText = "对应 Cocos 地图预制体名，默认 GameMainMap",
        };
        mapNameRow = LabeledRow("地图名（Cocos）", mapNameBox);
        stack.Children.Add(mapNameRow);

        fogBox = new CheckBox
        {
            Content = "同时发布迷雾 sidecar（Map_*.fog.json）",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 0),
        };
        fogRow = LabeledRow("附加输出", fogBox);
        stack.Children.Add(fogRow);

        indexBox = new CheckBox
        {
            Content = "同步更新 TableLevelData.lua.txt 章节索引",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 0),
        };
        indexRow = LabeledRow("Cocos 索引", indexBox);
        stack.Children.Add(indexRow);

        var previewBorder = new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(6),
            Background = UiTheme.PublishWindow.PreviewSurface,
            BorderBrush = UiTheme.PublishWindow.PreviewBorder,
            BorderThickness = new Thickness(1),
        };
        previewText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = UiTheme.PublishWindow.PreviewText,
        };
        previewBorder.Child = previewText;
        stack.Children.Add(previewBorder);

        var note = new TextBlock
        {
            Text = "发布只写目标工程目录，不会修改编辑器源存档 docs\\MapJson；同名文件会原子覆盖。",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiTheme.PublishWindow.NoteText,
        };
        stack.Children.Add(note);

        statusText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiTheme.PublishWindow.ErrorText,
        };
        stack.Children.Add(statusText);

        var cancel = new Button
        {
            Content = "取消",
            Padding = new Thickness(20, 6, 20, 6),
            Margin = new Thickness(0, 0, 8, 0),
        };
        cancel.Click += (_, _) => Close(null);

        var open = new Button
        {
            Content = "打开输出目录",
            Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 0, 8, 0),
        };
        open.Click += (_, _) => OpenDirectory(outputTextBox.Text);

        var publish = new Button
        {
            Content = "发布",
            Padding = new Thickness(26, 6, 26, 6),
            IsDefault = true,
        };
        publish.Click += async (_, _) => await PublishAsync();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 0,
            Margin = new Thickness(0, 4, 0, 0),
        };
        buttons.Children.Add(open);
        buttons.Children.Add(cancel);
        buttons.Children.Add(publish);
        stack.Children.Add(buttons);

        return stack;
    }

    private static StackPanel LabeledRow(string label, Control control)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Width = 110,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = UiTheme.PublishWindow.LabelText,
        };
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        panel.Children.Add(text);
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        panel.Children.Add(control);
        return panel;
    }

    #endregion

    #region 交互

    private void OnTargetChanged()
    {
        var next = _targetBox.SelectedIndex == 1 ? MapPublishTarget.CocosLua : MapPublishTarget.UnityJson;
        if (next == _target)
        {
            UpdateVisibility();
            UpdatePreview();
            return;
        }

        _target = next;
        _outputBox.Text = next == MapPublishTarget.CocosLua
            ? (string.IsNullOrWhiteSpace(_settings.CocosOutputDirectory)
                ? MapPublishService.DefaultOutputRoot(MapPublishTarget.CocosLua)
                : _settings.CocosOutputDirectory)
            : (string.IsNullOrWhiteSpace(_settings.UnityOutputDirectory)
                ? MapPublishService.DefaultOutputRoot(MapPublishTarget.UnityJson)
                : _settings.UnityOutputDirectory);
        UpdateVisibility();
        UpdatePreview();
    }

    private void UpdateVisibility()
    {
        var isCocos = _target == MapPublishTarget.CocosLua;
        _mapNameRow.IsVisible = isCocos;
        _indexRow.IsVisible = isCocos;
        _fogRow.IsVisible = !isCocos;
    }

    private void UpdatePreview()
    {
        var preview = MapPublishService.Preview(_config, _target);
        _previewText.Text = $"{preview.Summary}\n{preview.TargetHint}";
        _statusText.Text = string.Empty;
    }

    /// <summary>选择发布目标工程目录；取消选择时保留输入框原值。</summary>
    private async Task BrowseAsync(TextBox output)
    {
        var path = await FileDialogService.PickFolderAsync(
            this,
            "选择目标工程接入目录",
            output.Text);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        output.Text = path;
        UpdatePreview();
    }

    private async Task PublishAsync()
    {
        var output = _outputBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(output))
        {
            _statusText.Text = "请填写输出目录。";
            return;
        }

        var options = new MapPublishOptions
        {
            Target = _target,
            OutputDirectory = output,
            IncludeFog = _fogBox.IsChecked == true,
            MapName = string.IsNullOrWhiteSpace(_mapNameBox.Text) ? "GameMainMap" : _mapNameBox.Text!.Trim(),
            UpdateCocosIndex = _indexBox.IsChecked == true,
        };

        try
        {
            // 发布前再确认一次输出目录，避免把空目录当工程目录写满文件。
            FileSystemIo.EnsureDirectory(output);
            var result = MapPublishService.Publish(_config, options, _fog);
            _settings.UnityOutputDirectory = _target == MapPublishTarget.UnityJson
                ? output
                : _settings.UnityOutputDirectory;
            _settings.CocosOutputDirectory = _target == MapPublishTarget.CocosLua
                ? output
                : _settings.CocosOutputDirectory;
            _settings.MapName = options.MapName;
            _settings.IncludeFog = options.IncludeFog;
            _settings.UpdateCocosIndex = options.UpdateCocosIndex;
            MapPublishSettingsStore.Save(_settings);
            Close(result);
        }
        catch (Exception error)
        {
            _statusText.Text = $"发布失败：{error.Message}";
        }
    }

    private static void OpenDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch
        {
            // 打不开资源管理器不影响发布。
        }
    }

    #endregion

    #region 辅助

    private int Count(MapObjectType type)
    {
        var count = 0;
        foreach (var item in _config.Objects)
        {
            if (item.Type == type)
            {
                count++;
            }
        }

        return count;
    }

    private string ChapterText()
    {
        var digits = new string((_config.ChapterId ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? "1" : digits;
    }

    private string LevelText()
    {
        var digits = new string((_config.SceneId ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? "1" : digits;
    }

    #endregion
}
