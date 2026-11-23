/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationPreviewWindow.Playback.cs
 * author:    云毅
 * created:   2026
 * descrip:   场景动画预览窗：播放控制、逐帧推进、Spine 导入与解析辅助
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>场景动画预览窗的播放、导入与解析职责。</summary>
public sealed partial class AnimationPreviewWindow : Window
{
    #region 播放与导入

    /// <summary>
    /// 切换播放状态。恢复播放时重置计时基准，避免暂停期间累积的时间造成跳帧；
    /// 暂停时仅停表，画布保持当前帧。
    /// </summary>
    private void TogglePlayback()
    {
        _playing = !_playing;
        _playButton.Content = _playing ? "暂停" : "播放";
        if (_playing)
        {
            _clock.Restart();
            _lastTick = 0;
        }
        else
        {
            _clock.Stop();
        }
    }

    /// <summary>
    /// 定时器推进动画。单帧时长上限 0.1 秒，防止窗口卡顿或后台恢复后一次性跳过大量动画时间；
    /// 时间文本每帧刷新，右侧信息按 10 Hz 刷新，降低文本布局开销。
    /// </summary>
    private void Tick()
    {
        if (!_playing)
        {
            return;
        }

        var now = _clock.Elapsed.TotalSeconds;
        var delta = Math.Min(0.1, Math.Max(0, now - _lastTick));
        _lastTick = now;
        if (delta <= 0)
        {
            return;
        }

        _canvas.Advance(delta * _playbackSpeed);
        _timeText.Text = $"{_canvas.TimeSeconds:0.00}s";
        if (Math.Floor(_canvas.TimeSeconds * 10) % 2 == 0)
        {
            UpdateInfo();
        }
    }

    /// <summary>
    /// 从预览窗导入 Spine 并绑定到当前物体。导入成功后注册目录并持久化；
    /// 只有存在选中物体时才回写实例字段并请求主窗口刷新，取消选择文件不改变任何状态。
    /// </summary>
    private async Task ImportSpineAsync()
    {
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

        var asset = _spineStore.Import(
            local,
            atlas,
            Path.GetFileNameWithoutExtension(local),
            string.Empty,
            out var error);
        if (asset is null)
        {
            _statusText.Text = $"Spine 导入失败：{error}";
            return;
        }

        _spineCatalog.Register(asset);
        try
        {
            _spineStore.Save(_spineCatalog);
        }
        catch (Exception exception)
        {
            _statusText.Text = $"Spine 已载入，但写盘失败：{exception.Message}";
        }

        var target = _canvas.Target;
        if (target is not null)
        {
            target.AnimationKind = AnimationKinds.Spine;
            target.SpineAsset = asset.Name;
            target.SpineAnimation = asset.DefaultAnimation;
            target.AnimEnabled = true;
            _canvas.SetSpineCatalog(_spineCatalog);
            _canvas.Target = target;
            _canvas.RestartPlayback();
            _onSceneChanged?.Invoke();
        }

        RebuildVisibleList();
        UpdateInfo();
        _statusText.Text = $"已导入 Spine「{asset.Name}」，{asset.Animations.Count} 个动画。";
    }

    #endregion

    #region 辅助

    /// <summary>解析序列帧片段。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>片段或 null。</returns>
    private SpriteAnimationClip? ResolveSprite(PlacedObject item)
    {
        return item is { AnimEnabled: true }
            ? _spriteCatalog.Find(item.AnimName)
            : null;
    }

    /// <summary>解析 Spine 资产。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>资产或 null。</returns>
    private SpineAnimationAsset? ResolveSpine(PlacedObject item)
    {
        return item is { AnimEnabled: true }
            && string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase)
            ? _spineCatalog.Find(item.SpineAsset)
            : null;
    }

    /// <summary>将地图类型转换为列表和详情区使用的中文名称。</summary>
    private static string TypeName(MapObjectType type) => type switch
    {
        MapObjectType.Building => "建筑",
        MapObjectType.Character => "人物",
        MapObjectType.Prop => "物件",
        _ => "物体",
    };

    /// <summary>取得类型对应的强调色；未知类型回退到物件色。</summary>
    private static IBrush TypeBrush(MapObjectType type) => type switch
    {
        MapObjectType.Building => BuildingBrush,
        MapObjectType.Character => CharacterBrush,
        _ => PropBrush,
    };

    /// <summary>取得图层中文显示名，统一由地图图层键解析。</summary>
    private static string LayerName(string layer) => MapLayerKeys.DisplayName(layer);

    #endregion
}

