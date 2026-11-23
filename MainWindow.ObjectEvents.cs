/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.ObjectEvents.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口物件事件：物件字段、动画导入、预览窗与路径绘制开关。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool;

public partial class MainWindow : Window
{
    private void WireObjectEdit()
    {
        TabEditPath.Click += (_, _) => SwitchObjectEditTab(true);
        TabEditAnim.Click += (_, _) => SwitchObjectEditTab(false);

        BtnPathDraw.Click += (_, _) =>
        {
            if (Canvas.Selected is null)
            {
                SetMessage("请先选中一个物件再绘制路径");
                SyncPathEditUi();
                return;
            }

            SetPathEditing(!Canvas.PathDrawing);
        };

        BtnPathUndo.Click += (_, _) =>
        {
            if (Canvas.Selected?.PathPoints is { Count: > 0 } points)
            {
                points.RemoveAt(points.Count - 1);
                Canvas.Refresh();
                Canvas.NotifyDocumentChanged();
            }
        };

        BtnPathClear.Click += (_, _) =>
        {
            if (Canvas.Selected is not { } item || item.PathPoints.Count == 0)
            {
                return;
            }

            item.PathPoints.Clear();
            Canvas.Refresh();
            Canvas.NotifyDocumentChanged();
        };

        BtnClearTarget.Click += (_, _) =>
        {
            Canvas.Select(null);
            SetPathEditing(false, notify: false);
        };

        AnimLoopToggle.IsCheckedChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            Canvas.Selected.Loop = AnimLoopToggle.IsChecked == true;
            Canvas.NotifyDocumentChanged();
        };

        AnimEnabledToggle.IsCheckedChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            Canvas.Selected.AnimEnabled = AnimEnabledToggle.IsChecked == true;
            Canvas.RefreshAnimations();
            Canvas.NotifyDocumentChanged();
        };

        AnimKindField.SelectionChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            var item = Canvas.Selected;
            item.AnimationKind = AnimKindField.SelectedIndex == 1 ? AnimationKinds.Spine : AnimationKinds.Sprite;
            if (item.AnimationKind == AnimationKinds.Spine && string.IsNullOrWhiteSpace(item.SpineAsset)
                && _spineAnimations.Names.Count > 0)
            {
                item.SpineAsset = _spineAnimations.Names[0];
                var asset = _spineAnimations.Find(item.SpineAsset);
                item.SpineAnimation = asset?.DefaultAnimation ?? string.Empty;
            }

            UpdateAnimationControlState();
            if (item.AnimationKind == AnimationKinds.Spine)
            {
                SelectSpineAsset(item);
            }

            Canvas.RefreshAnimations();
            Canvas.NotifyDocumentChanged();
        };

        BindComboText(AnimNameField, text =>
        {
            if (!_suppressSync && Canvas.Selected is not null)
            {
                Canvas.Selected.AnimName = text.Trim();
                Canvas.Selected.AnimationKind = AnimationKinds.Sprite;
                Canvas.RefreshAnimations();
                Canvas.NotifyDocumentChanged();
            }
        });

        SpineAssetField.SelectionChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            var item = Canvas.Selected;
            item.SpineAsset = SpineAssetField.SelectedItem as string ?? string.Empty;
            item.AnimationKind = AnimationKinds.Spine;
            var asset = _spineAnimations.Find(item.SpineAsset);
            SpineAnimationField.ItemsSource = asset?.Animations ?? Array.Empty<string>();
            item.SpineAnimation = asset?.DefaultAnimation ?? string.Empty;
            SpineAnimationField.SelectedItem = item.SpineAnimation;
            Canvas.RefreshAnimations();
            Canvas.NotifyDocumentChanged();
        };

        SpineAnimationField.SelectionChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            Canvas.Selected.SpineAnimation = SpineAnimationField.SelectedItem as string ?? string.Empty;
            Canvas.RefreshAnimations();
            Canvas.NotifyDocumentChanged();
        };

        BindNumber(AnimSpeedField, value =>
        {
            if (!_suppressSync && Canvas.Selected is not null)
            {
                Canvas.Selected.Speed = Math.Max(0, value);
                Canvas.RefreshAnimations();
                Canvas.NotifyDocumentChanged();
            }
        });

        BtnImportFrames.Click += async (_, _) => await ImportFrameSequenceAsync();
        BtnImportSpine.Click += async (_, _) => await ImportSpineAsync();
        BtnPreviewAnim.Click += (_, _) => OpenAnimationPreview();

        RefreshAnimationOptions();
        UpdateAnimationControlState();
        SyncPathEditUi();
    }

    /// <summary>把已导入的序列帧 / Spine 名称灌进下拉框，保持当前文本不丢。</summary>
    private void RefreshAnimationOptions()
    {
        var currentText = AnimNameField.Text;
        var currentAsset = SpineAssetField.SelectedItem as string;
        AnimNameField.ItemsSource = _spriteAnimations.Names;
        AnimNameField.Text = currentText;
        SpineAssetField.ItemsSource = _spineAnimations.Names;
        if (!string.IsNullOrWhiteSpace(currentAsset))
        {
            SpineAssetField.SelectedItem = currentAsset;
        }
    }

    /// <summary>回填动画页控件；调用方负责 _suppressSync 范围。</summary>
    /// <param name="item">选中的实例。</param>
    private void RefreshAnimationEditor(PlacedObject item)
    {
        var wasSuppressed = _suppressSync;
        _suppressSync = true;
        try
        {
            RefreshAnimationOptions();
            AnimKindField.SelectedIndex = string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            AnimEnabledToggle.IsChecked = item.AnimEnabled;
            AnimNameField.Text = item.AnimName;
            AnimSpeedField.Text = item.Speed.ToString("0.##", CultureInfo.InvariantCulture);
            AnimLoopToggle.IsChecked = item.Loop;
            SelectSpineAsset(item);
            UpdateAnimationControlState();
        }
        finally
        {
            _suppressSync = wasSuppressed;
        }
    }

    /// <summary>选中实例的 Spine 资产并同步骨架动画下拉。</summary>
    /// <param name="item">选中的实例。</param>
    private void SelectSpineAsset(PlacedObject item)
    {
        var asset = _spineAnimations.Find(item.SpineAsset);
        SpineAssetField.SelectedItem = asset?.Name;
        SpineAnimationField.ItemsSource = asset?.Animations ?? Array.Empty<string>();
        if (asset is not null && string.IsNullOrWhiteSpace(item.SpineAnimation))
        {
            item.SpineAnimation = asset.DefaultAnimation;
        }

        SpineAnimationField.SelectedItem = item.SpineAnimation;
    }

    /// <summary>按动画来源启用 / 禁用对应控件，避免在 Spine 模式下显示无效的序列帧输入。</summary>
    private void UpdateAnimationControlState()
    {
        var spine = AnimKindField.SelectedIndex == 1;
        AnimNameField.IsEnabled = !spine;
        SpineAssetField.IsEnabled = spine;
        SpineAnimationField.IsEnabled = spine;
    }

    /// <summary>导入一个目录下的序列帧：按文件名排序，12 FPS、循环，命名为目录名。</summary>
    private async Task ImportFrameSequenceAsync()
    {
        // 文件选择器会抢走焦点，部分窗口管理器下会导致画布选中态被清掉；
        // 先记下导入目标，选完目录仍能自动绑定到用户刚才选中的实例。
        var target = Canvas.Selected;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择序列帧图片目录",
            AllowMultiple = false,
        });
        if (folders.Count == 0)
        {
            return;
        }

        var local = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(local))
        {
            return;
        }

        var frames = Directory.EnumerateFiles(local)
            .Where(path => new[] { ".png", ".jpg", ".jpeg", ".webp" }
                .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (frames.Count == 0)
        {
            SetMessage("该目录没有 PNG/JPG 序列帧，未导入。");
            return;
        }

        var baseName = Path.GetFileName(local.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var clipName = EnsureUniqueClipName(string.IsNullOrWhiteSpace(baseName) ? "sequence" : baseName);
        var importedFrames = _spriteAnimationStore.ImportFrames(frames, clipName, out var importError);
        if (importedFrames.Count == 0)
        {
            SetMessage(importError);
            return;
        }

        _spriteAnimations.Register(new SpriteAnimationClip
        {
            Name = clipName,
            Fps = 12,
            Loop = true,
            Frames = importedFrames.ToList(),
        });

        if (!_spriteAnimationStore.TrySave(_spriteAnimations, out var saveError))
        {
            SetMessage($"序列帧已载入内存，但写盘失败：{saveError}");
        }
        else
        {
            SetMessage($"已导入序列帧「{clipName}」，{importedFrames.Count} 帧，12 FPS。");
        }

        RefreshAnimationOptions();
        if (target is not null)
        {
            target.AnimationKind = AnimationKinds.Sprite;
            target.AnimName = clipName;
            target.AnimEnabled = true;
            RefreshAnimationEditor(target);
            Canvas.RefreshAnimations();
            Canvas.NotifyDocumentChanged();
        }
    }

    /// <summary>给序列帧生成不覆盖旧片段的名称。</summary>
    /// <param name="baseName">基础名字。</param>
    /// <returns>唯一名字。</returns>
    private string EnsureUniqueClipName(string baseName)
    {
        if (!_spriteAnimations.Names.Contains(baseName, StringComparer.OrdinalIgnoreCase))
        {
            return baseName;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{baseName}_{index}";
            if (!_spriteAnimations.Names.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return $"{baseName}_{Guid.NewGuid():N}";
    }

    /// <summary>选择 Spine 骨架文件，自动找同名 Atlas，导入并绑定到当前实例。</summary>
    private async Task ImportSpineAsync()
    {
        var target = Canvas.Selected;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Spine 骨架（.json / .skel）",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Spine 骨架")
                {
                    Patterns = new[] { "*.json", "*.skel" },
                },
            },
        });
        if (files.Count == 0)
        {
            return;
        }

        var local = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(local))
        {
            return;
        }

        var atlas = Path.ChangeExtension(local, ".atlas");
        if (!File.Exists(atlas))
        {
            atlas = string.Empty;
        }

        var assetName = Path.GetFileNameWithoutExtension(local);
        var asset = _spineAnimationStore.Import(local, atlas, assetName, string.Empty, out var error);
        if (asset is null)
        {
            SetMessage($"Spine 导入失败：{error}");
            return;
        }

        _spineAnimations.Register(asset);
        try
        {
            _spineAnimationStore.Save(_spineAnimations);
        }
        catch (Exception exception)
        {
            SetMessage($"Spine 已载入内存，但写盘失败：{exception.Message}");
        }

        RefreshAnimationOptions();
        if (target is not null)
        {
            target.AnimationKind = AnimationKinds.Spine;
            target.SpineAsset = asset.Name;
            target.SpineAnimation = asset.DefaultAnimation;
            target.AnimEnabled = true;
            RefreshAnimationEditor(target);
            Canvas.RefreshAnimations();
            Canvas.NotifyDocumentChanged();
        }

        SetMessage($"已导入 Spine「{asset.Name}」，{asset.Animations.Count} 个动画，默认「{asset.DefaultAnimation}」。");
    }

    /// <summary>预览窗导入 / 绑定动画后，把最新目录与实例字段同步回主窗口。</summary>
    private void RefreshAfterPreviewChange()
    {
        RefreshAnimationOptions();
        if (Canvas.Selected is { } item)
        {
            RefreshAnimationEditor(item);
        }

        Canvas.RefreshAnimations();
        Canvas.NotifyDocumentChanged();
    }

    /// <summary>打开场景动画预览窗；已打开时先关闭旧窗口，避免两份播放时钟。</summary>
    private void OpenAnimationPreview()
    {
        _animationPreviewWindow?.Close();
        var window = new AnimationPreviewWindow(
            _config,
            _assets,
            _spriteAnimations,
            _spineAnimations,
            _spineAnimationStore,
            Canvas.Selected,
            RefreshAfterPreviewChange);
        _animationPreviewWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_animationPreviewWindow, window))
            {
                _animationPreviewWindow = null;
            }
        };
        window.Show(this);
    }

    /// <summary>打开整张地图的只读预览窗；已打开时先关闭旧窗口，避免两份预览时钟。</summary>
    private void OpenMapPreview()
    {
        _mapPreviewWindow?.Close();
        var window = new MapPreviewWindow(
            _config,
            _assets,
            _spriteAnimations,
            _spineAnimations,
            Canvas.NineSlices);
        _mapPreviewWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_mapPreviewWindow, window))
            {
                _mapPreviewWindow = null;
            }
        };
        window.Show(this);
    }

    /// <summary>切换关卡 / 重载地图前关闭整图预览，避免窗口继续引用上一份地图对象。</summary>
    private void CloseMapPreview()
    {
        _mapPreviewWindow?.Close();
        _mapPreviewWindow = null;
    }


    /// <summary>
    /// 路径绘制的唯一开关入口。路径状态只存 <see cref="MapCanvas.PathDrawing"/>，
    /// 不再在窗口侧维护第二份 bool，避免「按钮显示停止、实际没开启」。
    /// </summary>
    /// <param name="enabled">true = 进入路径绘制。</param>
    /// <param name="notify">true = 往状态栏写一条提示；内部同步状态时传 false，避免刷屏。</param>
    private void SetPathEditing(bool enabled, bool notify = true)
    {
        var hasSelection = Canvas.Selected is not null;
        if (enabled && !hasSelection)
        {
            if (notify)
            {
                SetMessage("请先选中一个物件再绘制路径");
            }

            SyncPathEditUi();
            return;
        }

        Canvas.PathDrawing = enabled;
        SyncPathEditUi();

        if (notify)
        {
            var target = Canvas.Selected?.Name ?? "当前物件";
            SetMessage(enabled
                ? $"路径绘制中（{target}）：左键点地面加点；点到其他物件会忽略，不会切换选中"
                : $"已退出路径绘制；{target} 的路径点仍会显示");
        }
    }

    /// <summary>
    /// 选中变化时同步路径模式：路径编辑对所有已放置物件开放（角色 / 建筑 / 物件 / 载具均可），
    /// 只在选中为空时退出；不再因为类型不是角色就强制切回动画页。
    /// </summary>
    private void SyncPathEditingWithSelection()
    {
        var wasPathEditing = Canvas.PathDrawing;

        if (Canvas.Selected is null)
        {
            SetPathEditing(false, notify: false);
            SyncPathEditUi();

            if (wasPathEditing)
            {
                SetMessage("已退出路径绘制：未选中物件");
            }

            return;
        }

        // 选中新物件后保留用户当前的「路径信息 / 动画信息」标签页，不强行切换；
        // 路径按钮与列表统一按当前选中物件刷新，点击「路径信息」即可查看 / 编辑。
        SyncPathEditUi();
        SetMessage(Canvas.PathDrawing
            ? $"移动路径编辑中（{Canvas.Selected.Name}）：左键点地面加点"
            : $"已选中「{Canvas.Selected.Name}」：点「开始绘制路径」后可编辑移动路径");
    }

    /// <summary>把画布真实状态回写到按钮文案与可用性，任何选中 / 开关变化都走这里。</summary>
    private void SyncPathEditUi()
    {
        var selected = Canvas.Selected;
        BtnPathDraw.IsEnabled = selected is not null;
        BtnPathDraw.Content = selected is not null && Canvas.PathDrawing ? "停止绘制路径" : "开始绘制路径";

        var hasPoints = selected?.PathPoints.Count > 0;
        BtnPathUndo.IsEnabled = hasPoints;
        BtnPathClear.IsEnabled = hasPoints;
    }

    /// <summary>在“路径信息 / 动画信息”之间切换属性区子面板。</summary>
    /// <param name="showPath">true 显示路径信息，false 显示动画信息。</param>
    private void SwitchObjectEditTab(bool showPath)
    {
        PathScroll.IsVisible = showPath;
        AnimPanel.IsVisible = !showPath;
        ApplyTabVisual(TabEditPath, showPath);
        ApplyTabVisual(TabEditAnim, !showPath);
    }
}

