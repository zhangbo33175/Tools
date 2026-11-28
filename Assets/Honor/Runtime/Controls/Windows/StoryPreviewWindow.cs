/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryPreviewWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情预览窗：立绘舞台 + 对话框推进 + 选项分支，验证写好的剧情走不走得通
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 剧情预览窗。把 <see cref="StoryDocument"/> 从起始节点一路播下去：
/// 立绘、说话人、正文、选项都按编辑器里的字段呈现，点击对话框或按空格推进。
/// </summary>
public sealed partial class StoryPreviewWindow : Window
{
    #region 常量

    /// <summary>推进一次的震动帧数。</summary>
    private const int ShakeFrames = 12;

    #endregion

    #region 字段

    private readonly IReadOnlyList<StorySpeaker> _speakers;

    private readonly StoryDocument _doc;
    private readonly MapConfigData? _config;
    private readonly AssetLibrary? _assets;
    private readonly Image _portrait = new();
    private readonly TextBlock _speakerText = new();
    private readonly TextBlock _dialogText = new();
    private readonly TextBlock _progressText = new();
    private readonly TextBlock _hintText = new();
    private readonly StackPanel _choicesHost = new();
    private readonly ScaleTransform _portraitScale = new(1, 1);
    private readonly TranslateTransform _stageShift = new();
    private readonly DispatcherTimer _autoTimer;
    private DispatcherTimer? _shakeTimer;
    private StoryNode? _current;
    private int _step;
    private bool _waitingChoice;

    #endregion

    #region 构造

    /// <summary>构造剧情预览窗。</summary>
    /// <param name="doc">剧情文档。</summary>
    /// <param name="config">当前地图，用于把说话人数字 ID 解析到角色图片。</param>
    /// <param name="assets">资源库。</param>
    /// <param name="speakers">编辑器算好的角色索引，按数字 ID 查立绘。</param>
    public StoryPreviewWindow(
        StoryDocument doc,
        MapConfigData? config,
        AssetLibrary? assets,
        IReadOnlyList<StorySpeaker>? speakers = null)
    {
        _doc = doc;
        _config = config;
        _assets = assets;
        _speakers = speakers ?? Array.Empty<StorySpeaker>();

        Title = $"剧情预览 - {doc.Title}";
        Width = 1120;
        Height = 780;
        MinWidth = 860;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiTheme.PreviewPanel;

        _autoTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _autoTimer.Tick += (_, _) =>
        {
            _autoTimer.Stop();
            Advance();
        };

        Content = PreviewWindowChrome.BuildShell(BuildHeader(), BuildStage(), BuildFooter());
        KeyDown += OnKeyDown;
        Opened += (_, _) => Restart();
        WindowChromeTheme.Attach(this);
    }

    #endregion

    #region 布局

    /// <summary>头部：剧情标题与进度。</summary>
    /// <returns>头部内容。</returns>
    private Control BuildHeader()
    {
        var title = new TextBlock
        {
            Text = _doc.Title,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var sub = new TextBlock
        {
            Text = $"{_doc.MapFile} · 共 {_doc.Nodes.Count} 个节点",
            FontSize = 10.5,
            Foreground = UiTheme.PreviewMuted,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _progressText.Text = "第 0 / 0 句";
        _progressText.FontSize = UiMetrics.FontBody;
        _progressText.Foreground = UiTheme.InfoText;
        _progressText.VerticalAlignment = VerticalAlignment.Center;

        var restart = UiFactory.CompactButton("重新播放", UiTheme.PreviewAccentSoft, UiTheme.InfoText);
        restart.CornerRadius = new CornerRadius(UiMetrics.ButtonCornerRadius);
        restart.BorderBrush = UiTheme.Form.FieldBorder;
        restart.BorderThickness = new Thickness(1);
        restart.Margin = new Thickness(8, 0, 0, 0);
        restart.Click += (_, _) => Restart();

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _progressText, restart },
        };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { title, sub },
        });
        Grid.SetColumn(right, 1);
        head.Children.Add(right);
        return head;
    }

    /// <summary>舞台：深青渐变背景 + 立绘 + 底部对话框与选项。</summary>
    /// <returns>舞台内容。</summary>
    private Control BuildStage()
    {
        _portrait.Stretch = Stretch.Uniform;
        _portrait.MaxHeight = 400;
        _portrait.HorizontalAlignment = HorizontalAlignment.Left;
        _portrait.Margin = new Thickness(70, 24, 0, 200);
        _portrait.RenderTransform = _portraitScale;
        _portrait.RenderTransformOrigin = RelativePoint.Center;

        _speakerText.FontSize = 12.5;
        _speakerText.FontWeight = FontWeight.SemiBold;
        _speakerText.Foreground = UiTheme.Story.SpeakerText;
        _speakerText.Margin = new Thickness(0, 0, 0, 6);

        _dialogText.FontSize = 14;
        _dialogText.Foreground = UiTheme.Story.DialogText;
        _dialogText.TextWrapping = TextWrapping.Wrap;
        _dialogText.LineHeight = 24;
        _dialogText.MaxHeight = 150;

        _hintText.Text = "点击对话框 / 空格 继续";
        _hintText.FontSize = UiMetrics.FontCaption;
        _hintText.Foreground = UiTheme.Story.ProgressText;
        _hintText.HorizontalAlignment = HorizontalAlignment.Right;
        _hintText.Margin = new Thickness(0, 8, 0, 0);

        _choicesHost.Spacing = 6;
        _choicesHost.Margin = new Thickness(0, 0, 0, 10);
        _choicesHost.IsVisible = false;

        var dialog = new Border
        {
            Background = UiTheme.Story.DialogSurface,
            BorderBrush = UiTheme.Story.DialogBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
            Padding = new Thickness(22, 16, 22, 12),
            Child = new StackPanel
            {
                Children = { _speakerText, _dialogText, _hintText },
            },
        };
        dialog.PointerPressed += (_, _) => Advance();

        var bottom = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(56, 0, 56, 24),
            Children = { _choicesHost, dialog },
        };

        var stage = new Grid
        {
            Children = { _portrait, bottom },
        };
        stage.RenderTransform = _stageShift;
        stage.PointerPressed += (_, _) => Advance();

        return new Border
        {
            CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
            ClipToBounds = true,
            Margin = new Thickness(10, 0, 10, 8),
            BorderBrush = UiTheme.Form.GroupBorder,
            BorderThickness = new Thickness(1),
            Child = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(UiTheme.Story.StageTop, 0),
                        new GradientStop(UiTheme.Story.StageBottom, 1),
                    },
                },
                Child = stage,
            },
        };
    }

    /// <summary>底部：一句操作提示。</summary>
    /// <returns>底部内容。</returns>
    private Control BuildFooter() => new TextBlock
    {
        Text = "空格 / 回车：下一句　　数字键 1-9：选分支　　Esc：关闭",
        FontSize = UiMetrics.FontCaption,
        Foreground = UiTheme.PreviewHint,
        Margin = new Thickness(12, 4, 12, 0),
    };

    #endregion
}
