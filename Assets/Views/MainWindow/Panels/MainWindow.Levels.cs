/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.Levels.cs
 * author:    云毅
 * created:   2026
 * descrip:   主界面楼层下拉：切换当前楼层、整楼透视、把选中物件挪到本层
 ***************************************************************/

using System;
using Avalonia.Controls;
using MapEditorTool.Controls;
using MapEditorTool.Models;

namespace MapEditorTool;

public partial class MainWindow
{
    /// <summary>楼层下拉是否正在被代码回填（防止 SelectionChanged 反向触发画布）。</summary>
    private bool _suppressLevelSync;

    /// <summary>接线：楼层下拉、透视开关、选中到本层。</summary>
    private void WireLevels()
    {
        LevelField.SelectionChanged += (_, _) =>
        {
            if (_suppressLevelSync)
            {
                return;
            }

            var level = Math.Max(0, LevelField.SelectedIndex);
            Canvas.SetCurrentLevel(level);
            SetMessage($"当前楼层：{_config?.LevelName(level) ?? $"{level + 1}F"}");
        };

        ShowOtherLevelsToggle.IsCheckedChanged += (_, _) =>
        {
            var showAll = ShowOtherLevelsToggle.IsChecked == true;
            Canvas.SetShowAllLevels(showAll);
            ApplyLevelDetailVisibility(showAll);
            SetMessage(showAll
                ? "楼层参考：他层 25% 显示，左侧标出本层层高"
                : "只看当前楼层：他层、层高标尺与楼层行已隐藏");
        };

        // 初始态跟勾选态一致：默认勾选，楼层 / 层高两行可见
        ApplyLevelDetailVisibility(ShowOtherLevelsToggle.IsChecked == true);

        BtnMoveToLevel.Click += (_, _) =>
        {
            var moved = Canvas.MoveSelectionToCurrentLevel();
            if (moved == 0)
            {
                SetMessageError(Canvas.Selected is null
                    ? "先选中一个物件，再点「选中到本层」"
                    : "选中的物件已经在当前楼层");
                return;
            }

            SyncProperties();
            SetMessage($"已把选中物件移到 {_config?.LevelName(Canvas.CurrentLevel) ?? $"{Canvas.CurrentLevel + 1}F"}");
        };

        // 画布侧改层（选中 / 快捷键）也把下拉同步回来，避免界面与画布口径不一致
        Canvas.LevelChanged += (_, _) => SyncLevelField();

        BtnAddLevel.Click += (_, _) => AddLevel();
        BtnRemoveLevel.Click += (_, _) => RemoveCurrentLevel();

        UiFactory.CommitOnEnter(LevelElevationField, ApplyLevelElevation);
        LevelElevationField.LostFocus += (_, _) => ApplyLevelElevation();
        BtnLevelElevationStep.Click += (_, _) =>
        {
            var level = Canvas.CurrentLevel;
            var target = level * MapLevelData.DefaultElevation;
            Canvas.SetLevelElevation(level, target);
            SyncLevelElevationField();
            SetMessage($"{_config?.LevelName(level) ?? $"{level + 1}F"} 层高设为 {target}px（{level} × {MapLevelData.DefaultElevation}）");
        };
    }

    /// <summary>
    /// 「楼层 / 层高」两行的显隐：楼层参考未勾选时整块收起，
    /// 面板不留下用不上的控件，当前楼层仍由卡片上的「当前楼层：NF」与状态栏标明。
    /// </summary>
    /// <param name="visible">true = 显示楼层下拉与层高输入，false = 收起。</param>
    private void ApplyLevelDetailVisibility(bool visible)
    {
        if (LevelDetailGroup is null)
        {
            return;
        }

        LevelDetailGroup.IsVisible = visible;
    }

    /// <summary>
    /// 把「层高」输入框落到当前楼层：0 层恒为 0（剖面以地面为基准），其余夹到 0–4096。
    /// </summary>
    private void ApplyLevelElevation()
    {
        if (_config is null)
        {
            return;
        }

        var level = Canvas.CurrentLevel;
        var name = _config.LevelName(level);
        if (!int.TryParse(LevelElevationField.Text?.Trim(), out var wanted))
        {
            SetMessageError($"层高要填整数（px）：{name} 保持 {Canvas.LevelElevationOf(level)}px");
            SyncLevelElevationField();
            return;
        }

        if (level == 0 && wanted != 0)
        {
            SetMessageError("1F 是地面层，层高恒为 0");
            SyncLevelElevationField();
            return;
        }

        // 回车与失焦会各提交一次：值没变就安静收工，别把上一条提示顶掉
        if (wanted == Canvas.LevelElevationOf(level))
        {
            SyncLevelElevationField();
            return;
        }

        var applied = Canvas.SetLevelElevation(level, wanted);
        if (applied < 0)
        {
            return;
        }

        if (applied != wanted)
        {
            SetMessageError($"层高范围 0–{MapLevelData.MaxElevation}px，已按 {applied}px 生效");
        }
        else
        {
            SetMessage($"{name} 层高：{applied}px");
        }

        SyncLevelElevationField();
    }

    /// <summary>把当前楼层的层高回填到输入框，避免输入框和画布说的不是一件事。</summary>
    private void SyncLevelElevationField()
    {
        LevelElevationField.Text = Canvas.LevelElevationOf(Canvas.CurrentLevel).ToString();
    }

    /// <summary>
    /// 在最高处加一层：楼层号自动 +1，加完直接切过去，顺手刷新下拉与状态行。
    /// </summary>
    private void AddLevel()
    {
        switch (Canvas.TryAddLevel(out var newLevel))
        {
            case MapCanvas.LevelEditStatus.NoConfig:
                SetMessageError("先打开一张地图，再添加楼层");
                return;

            case MapCanvas.LevelEditStatus.AtLimit:
                SetMessageError($"楼层上限 {MapLevelData.MaxCount} 层，已到顶");
                return;

            case MapCanvas.LevelEditStatus.Success:
                RefreshLevelField();
                SyncProperties();
                SetMessage($"已新增 {_config?.LevelName(newLevel) ?? MapLevelData.DefaultName(newLevel)}，当前就在这一层");
                return;

            default:
                return;
        }
    }

    /// <summary>
    /// 删掉当前楼层：只允许顶层且该层没有内容，删完回到新的顶层。
    /// 有内容时把「还剩多少物件 / 瓦片」说清楚，而不是干巴巴一句失败。
    /// </summary>
    private void RemoveCurrentLevel()
    {
        var level = Canvas.CurrentLevel;
        var name = _config?.LevelName(level) ?? MapLevelData.DefaultName(level);

        switch (Canvas.TryRemoveCurrentLevel(out _, out var objectCount, out var tileCount))
        {
            case MapCanvas.LevelEditStatus.NoConfig:
                SetMessageError("先打开一张地图，再删除楼层");
                return;

            case MapCanvas.LevelEditStatus.LastLevel:
                SetMessageError("只剩最后一层，至少要留 1F");
                return;

            case MapCanvas.LevelEditStatus.NotTopLevel:
                SetMessageError($"只能从顶层往下删：先切到 {_config?.LevelName((_config?.LevelCount ?? 1) - 1)}");
                return;

            case MapCanvas.LevelEditStatus.HasContent:
                SetMessageError($"{name} 剩 {objectCount} 物件 / {tileCount} 格瓦片，先清空再删");
                return;

            case MapCanvas.LevelEditStatus.Success:
                RefreshLevelField();
                SyncProperties();
                SetMessage($"已删除 {name}，当前回到 {_config?.LevelName(Canvas.CurrentLevel) ?? "1F"}");
                return;

            default:
                return;
        }
    }

    /// <summary>楼层增减一层：PageUp = 上楼，PageDown = 下楼，直达顶部 / 地面时不做任何事。</summary>
    /// <param name="delta">+1 上楼，-1 下楼。</param>
    private void ChangeLevel(int delta)
    {
        var target = Canvas.CurrentLevel + delta;
        if (_config is not null && target >= _config.LevelCount)
        {
            SetMessage($"已经是最高层：{_config.LevelName(Canvas.CurrentLevel)}");
            return;
        }

        if (target < 0)
        {
            SetMessage("已经在最底层：1F");
            return;
        }

        Canvas.SetCurrentLevel(target);
        SyncLevelField();
        SetMessage(delta > 0
            ? $"上一楼：{_config?.LevelName(target) ?? $"{target + 1}F"}（其它层半透明参考）"
            : $"下一楼：{_config?.LevelName(target) ?? $"{target + 1}F"}（其它层半透明参考）");
    }

    /// <summary>按当前地图的楼层表重建下拉项，并把选中项对到画布的当前楼层。</summary>
    private void RefreshLevelField()
    {
        _suppressLevelSync = true;
        try
        {
            LevelField.Items.Clear();
            var count = _config?.LevelCount ?? MapLevelData.DefaultCount;
            for (var i = 0; i < count; i++)
            {
                LevelField.Items.Add(new ComboBoxItem
                {
                    Content = i == 0 ? $"{_config?.LevelName(i) ?? "1F"}（地面）" : _config?.LevelName(i) ?? $"{i + 1}F",
                });
            }

            LevelField.SelectedIndex = Math.Clamp(Canvas.CurrentLevel, 0, count - 1);
            SyncLevelElevationField();
        }
        finally
        {
            _suppressLevelSync = false;
        }
    }

    /// <summary>只同步选中项，不重建列表（画布切层时用）。</summary>
    private void SyncLevelField()
    {
        SyncLevelElevationField();
        var index = Math.Clamp(Canvas.CurrentLevel, 0, Math.Max(0, LevelField.ItemCount - 1));
        if (LevelField.SelectedIndex == index)
        {
            return;
        }

        _suppressLevelSync = true;
        try
        {
            LevelField.SelectedIndex = index;
        }
        finally
        {
            _suppressLevelSync = false;
        }
    }
}


