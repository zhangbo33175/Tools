/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.PropertyEvents.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口属性事件：属性输入、区域与变换字段、层级排序。
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
    private void WireProperties()
    {
        // ID / Tag 是只读展示：ID 取配置表数字 ID（缺失回退 PSD 切片序号），Tag 取分层技术键。
        // 不能把 TemplateId / Name 绑到这两个展示框上，否则改数字 ID 会让实例找不到模板。
        BindGridCellField(PropCellX, isX: true);
        BindGridCellField(PropCellY, isX: false);

        BindNumber(PropSortOrder, value => ApplySortOrderValue((int)value));

        // 层级输入框旁的 − / ＋：每次点按 ±1，和手输层级走同一个写回口径（0–79 夹紧）
        BtnLayerMinus.Click += (_, _) => NudgeSortOrderValue(-1);
        BtnLayerPlus.Click += (_, _) => NudgeSortOrderValue(1);

        BindNumber(PropBubble, value =>
        {
            if (!_suppressSync && Canvas.Selected is not null)
            {
                Canvas.Selected.BubbleId = (int)value;
            }
        });

        BindText(PropDialogBubble, text =>
        {
            if (!_suppressSync && Canvas.Selected is not null)
            {
                Canvas.Selected.DialogBubble = text;
            }
        });

        // 物体编辑：旋转（↺ / ↻ / 重置，与「物体变换」角度行共用同一份 rotation）
        BtnEditRotateLeft.Click += (_, _) => StepRotation(-15);
        BtnEditRotateRight.Click += (_, _) => StepRotation(15);
        BtnEditRotateReset.Click += (_, _) => ApplyProperty(item => item.Rotation = 0);

        BindNumber(PropPosX, value => ApplyObjectPosition(value, null));
        BindNumber(PropPosY, value => ApplyObjectPosition(null, value));
        BindNumber(PropSizeW, value => ApplyObjectSize(value, null));
        BindNumber(PropSizeH, value => ApplyObjectSize(null, value));

        // Unity 式拖动改值：按住 X / Y 标签或数值框左键左右拖动 → 世界坐标连续加减
        BindScrub(PropPosX, PropPosXLabel, value => ApplyObjectPosition(value, null));
        BindScrub(PropPosY, PropPosYLabel, value => ApplyObjectPosition(null, value));
        BindScrub(PropSizeW, PropSizeW, value => ApplyObjectSize(value, null));
        BindScrub(PropSizeH, PropSizeH, value => ApplyObjectSize(null, value));

        PropStretch.IsCheckedChanged += (_, _) =>
            ApplyProperty(item => item.Stretch = PropStretch.IsChecked == true);

        PropCollision.IsCheckedChanged += (_, _) =>
        {
            ApplyProperty(item => item.CollisionEnabled = PropCollision.IsChecked == true);
            UpdateAreaSectionVisibility();
        };

        PropShadow.IsCheckedChanged += (_, _) =>
        {
            ApplyProperty(item => item.ShadowEnabled = PropShadow.IsChecked == true);
            UpdateAreaSectionVisibility();
        };

        PropCollectible.IsCheckedChanged += (_, _) =>
        {
            ApplyProperty(item => item.IsCollectible = PropCollectible.IsChecked == true);
            UpdateAreaSectionVisibility();
        };
        PropTraffic.IsCheckedChanged += (_, _) =>
        {
            ApplyProperty(item => item.Interactable = PropTraffic.IsChecked == true);
        };

        // 「编辑」只控制右栏「物体编辑」分组的显隐，不写进地图数据
        PropShowObjectEdit.IsCheckedChanged += (_, _) =>
        {
            _showObjectEdit = PropShowObjectEdit.IsChecked == true;
            UpdateObjectEditVisibility();
        };

        PropFacing.SelectionChanged += (_, _) => ApplyProperty(item =>
            item.FacingDirection = FacingCodeFromDisplay(ReadCombo(PropFacing, "下")));

        PropInteractType.SelectionChanged += (_, _) => ApplyProperty(item =>
            item.InteractType = InteractCodeFromDisplay(ReadCombo(PropInteractType, "无")));

        // 「遮挡带」是这张贴图的层级开关：自动（按图层类型 / 名字判定）或硬指定地表 / 立体 / 叠加；
        // 硬指定后会自动挪到目标带的最上层（sortOrder = 带号 × 1000 + 带内最大序位 + 1）
        PropBand.SelectionChanged += (_, _) =>
        {
            if (_suppressBandMove)
            {
                return;
            }

            ApplyProperty(item => LayerBands.MoveToBand(_config, item, PropBand.SelectedIndex - 1));
        };

        // 「图层」下拉改的是这张图在地图里的归属层与显示层：Unity 改层后 SortOrder 会重算成
        // 「新层基线 + 原档位」，这里必须同步重算，否则画布仍按旧层的号段出图（层级显示也会错位）
        PropDisplayLayer.SelectionChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is not { } item)
            {
                return;
            }

            var index = PropDisplayLayer.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            var newLayer = TemplateLayers.ToKey(TemplateLayers.DisplayChoices[index]);
            if (item.DisplayLayer == newLayer)
            {
                return;
            }

            // 原档位按旧层基线取，改层后落在新层同一档位（0–9），不会越界压到相邻层
            var slot = LayerOrder.SlotOf(item);
            item.DisplayLayer = newLayer;

            // 换了图层键就回到「自动判定」，否则手定的带会一直压住新的图层
            item.BandOverride = -1;
            item.BandOverrideAuto = false;
            item.SortOrder = LayerOrder.Build(LayerOrder.EffectiveLayer(item), slot);

            _suppressBandMove = true;
            PropBand.SelectedIndex = 0;
            _suppressBandMove = false;

            Canvas.Refresh();
            SyncProperties();
        };

        BtnDeleteObject.Click += (_, _) =>
        {
            if (Canvas.Selected is not { } selected)
            {
                return;
            }

            // 物件就是交通标记的锚点：删物件时把灯 / 横道 / 区域一起清掉，避免留下指向空物件的交通数据
            TrafficMarkService.RemoveObjectTrafficMarks(_config, selected.Id);
            _config.Objects.Remove(selected);
            Canvas.Select(null);
            Canvas.Refresh();
            Canvas.NotifyDocumentChanged();
        };

        SetPropertyPanelEnabled(false);
        SwitchObjectEditTab(true);
    }

    /// <summary>
    /// 把层级号写回当前实例。层级号就是 Unity 的全局 SortOrder：0–79，十位是层、个位是层内档位
    /// （background 0 / greenery 10 / road 20 / building 30 / prop 40 / character 50 / foreground 60 / bubble 70）。
    /// 手改 40 → 60 等于把这张贴图从道具层挪到前景层，同时把显示层钉成目标层、清掉遮挡带手改。
    /// 手输与 − / ＋ 按钮共用这里，避免两条路径算出的层号不一致。
    /// </summary>
    private void ApplySortOrderValue(int raw)
    {
        if (_suppressSync || Canvas.Selected is not { } item)
        {
            return;
        }

        var max = LayerOrder.BaselineStep * MapLayerKeys.AllKeys.Length - 1;
        raw = Math.Clamp(raw, 0, max);
        var layerIndex = Math.Clamp(raw / LayerOrder.BaselineStep, 0, MapLayerKeys.AllKeys.Length - 1);
        var layerKey = MapLayerKeys.AllKeys[layerIndex];
        var slot = Math.Clamp(raw % LayerOrder.BaselineStep, 0, LayerOrder.SlotCount - 1);
        item.DisplayLayer = layerKey;
        item.BandOverride = -1;
        item.BandOverrideAuto = false;
        item.SortOrder = LayerOrder.Build(layerKey, slot);

        // 夹紧后回填输入框，点按到 0 / 79 时界面上也能看到已经停住
        PropSortOrder.Text = item.SortOrder.ToString(CultureInfo.InvariantCulture);

        _suppressBandMove = true;
        PropBand.SelectedIndex = 0;
        _suppressBandMove = false;
        ApplyLayerOptions();
        PropDisplayLayer.SelectedIndex = LayerSelectionIndex(item);

        Canvas.Refresh();
        UpdateBandInfo();
    }

    /// <summary>层级点按微调：在当前 SortOrder 上 ±1，越界由 <see cref="ApplySortOrderValue"/> 夹紧。</summary>
    private void NudgeSortOrderValue(int delta)
    {
        if (_suppressSync || Canvas.Selected is not { } item)
        {
            return;
        }

        ApplySortOrderValue(item.SortOrder + delta);
    }

    private void ApplyProperty(Action<PlacedObject> mutate)
    {
        if (_suppressSync || Canvas.Selected is null)
        {
            return;
        }

        mutate(Canvas.Selected);
        Canvas.Refresh();
        SyncProperties();
    }

    #region 区域与变换（碰撞 / 阴影 / 收集物层级比例角度）

    /// <summary>装配「碰撞区域」「阴影区域」「物体变换」三块控件。</summary>
    private void WireAreaAndTransform()
    {
        BindAreaField(CollisionXField, (item, value) => item.CollisionX = value);
        BindAreaField(CollisionYField, (item, value) => item.CollisionY = value);
        BindAreaField(CollisionWField, (item, value) => item.CollisionWidth = value);
        BindAreaField(CollisionHField, (item, value) => item.CollisionHeight = value);

        BindAreaField(ShadowXField, (item, value) => item.ShadowX = value);
        BindAreaField(ShadowYField, (item, value) => item.ShadowY = value);
        BindAreaField(ShadowWField, (item, value) => item.ShadowWidth = value);
        BindAreaField(ShadowHField, (item, value) => item.ShadowHeight = value);

        BtnCollisionReset.Click += (_, _) => ApplyProperty(item =>
        {
            item.CollisionX = 0;
            item.CollisionY = 0;
            item.CollisionWidth = 0;
            item.CollisionHeight = 0;
        });

        BtnShadowReset.Click += (_, _) => ApplyProperty(item =>
        {
            item.ShadowX = 0;
            item.ShadowY = 0;
            item.ShadowWidth = 0;
            item.ShadowHeight = 0;
        });

        BtnOrderUp.Click += (_, _) => StepSortOrder(1);
        BtnOrderDown.Click += (_, _) => StepSortOrder(-1);
        BtnOrderTop.Click += (_, _) => MoveSortOrderToEdge(true);
        BtnOrderBottom.Click += (_, _) => MoveSortOrderToEdge(false);

        BtnResortPicture.Click += (_, _) => ResortOcculsion(true);
        BtnResortPsd.Click += (_, _) => ResortOcculsion(false);

        BtnScaleMinus.Click += (_, _) => StepScale(-0.1);
        BtnScalePlus.Click += (_, _) => StepScale(0.1);
        BtnScaleReset.Click += (_, _) => ApplyProperty(item => item.Scale = 1.0);

        BtnRotateLeft.Click += (_, _) => StepRotation(-15);
        BtnRotateRight.Click += (_, _) => StepRotation(15);
        BtnRotateReset.Click += (_, _) => ApplyProperty(item => item.Rotation = 0);
    }

    /// <summary>区域数值输入：回车或失焦提交，写回选中实例后重画画布。</summary>
    /// <param name="box">输入框。</param>
    /// <param name="setter">写回动作。</param>
    private void BindAreaField(TextBox box, Action<PlacedObject, double> setter)
    {
        void Commit()
        {
            if (_suppressSync)
            {
                return;
            }

            var text = box.Text?.Trim() ?? string.Empty;
            var value = text.Length > 0 && double.TryParse(text, out var parsed) ? Math.Round(parsed, 2) : 0;
            ApplyProperty(item => setter(item, value));
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };
    }

    /// <summary>同带内层级微调：+1 上移、-1 下移（仅改 SortOrder，不跨遮挡带）。</summary>
    /// <param name="delta">增量。</param>
    private void StepSortOrder(int delta)
    {
        ApplyProperty(item => item.SortOrder += delta);
    }

    /// <summary>同带内置顶 / 置底：取同遮挡带实例的层序号极值再外推一档。</summary>
    /// <param name="toFront">true=置顶（最上层）。</param>
    private void MoveSortOrderToEdge(bool toFront)
    {
        var selected = Canvas.Selected;
        if (selected is null)
        {
            return;
        }

        var layerKey = LayerOrder.EffectiveLayer(selected);
        var peers = _config.Objects.Where(other => LayerOrder.EffectiveLayer(other) == layerKey).ToList();
        if (peers.Count == 0)
        {
            return;
        }

        // 层内只有 10 档（0–9）：置顶 / 置底落到本层最高 / 最低档，不会越界压到相邻层
        var slot = toFront ? LayerOrder.SlotCount - 1 : 0;
        ApplyProperty(item =>
        {
            item.BandOverride = -1;
            item.BandOverrideAuto = false;
            item.SortOrder = LayerOrder.Build(layerKey, slot);
        });
    }

    /// <summary>
    /// 重排全部层序号（地表 / 立体 / 叠加三带）：只改 Unity 要用的 <c>sortOrder</c> 数据。
    /// 画面前后已由「带 + 脚底 Y」决定，重排不会再改变遮挡关系。
    /// </summary>
    /// <param name="byPicture">
    /// true = 立体带的层内档位按「脚底 Y」重写（与当前画面顺序一致）；
    /// false = 带内还原 PSD 层序（手改过层序号时用来找回画稿顺序）。
    /// </param>
    private void ResortOcculsion(bool byPicture)
    {
        if (_config.Objects.Count == 0)
        {
            SetMessage("当前地图是空的，没有可重排的实例");
            return;
        }

        var before = new int[_config.Objects.Count];
        for (var i = 0; i < before.Length; i++)
        {
            before[i] = _config.Objects[i].SortOrder;
        }

        // 「刷新层级」不再只重写层号：先按统一遮挡策略重新分析整张地图——
        // 背景组立体美术提层、平面件窄缝贴合；再做立体带的层号兜底。两项互不替代。
        SceneDisplayReport? displayReport = null;
        if (byPicture)
        {
            displayReport = SceneDisplayAudit.Apply(_config, _assets);
        }

        var bands = TemplateCatalog.RefreshSortOrders(_config, byPicture);
        var changed = 0;
        for (var i = 0; i < before.Length; i++)
        {
            if (before[i] != _config.Objects[i].SortOrder)
            {
                changed++;
            }
        }

        Canvas.Refresh();
        SyncProperties();
        UpdateBandInfo();
        RefreshStatus();

        var policy = displayReport is null
            ? string.Empty
            : new OcclusionAdjustmentResult(displayReport.BandPromoted, displayReport.SeamSnapped).Summary;
        if (displayReport is { PixelSizeCompleted: > 0 } pixelReport)
        {
            var sizeFix = $"尺寸补全{pixelReport.PixelSizeCompleted}";
            policy = string.IsNullOrEmpty(policy) ? sizeFix : $"{policy}｜{sizeFix}";
        }

        var policyText = string.IsNullOrEmpty(policy)
            ? "遮挡巡检：无需修复；"
            : $"遮挡巡检：{policy.Replace(" ", string.Empty)}；";
        var extraText = displayReport is { } report
            ? (report.Missing > 0 ? $"｜缺图{report.Missing}" : string.Empty)
              + (report.PixelSizeMismatch > 0 ? $"｜尺寸差{report.PixelSizeMismatch}(重导PSD)" : string.Empty)
              + (report.OffMap > 0
                  ? $"｜越界{report.OffMap}" + (Canvas.ShowClipOutsideMap ? "(已裁)" : "(未裁)")
                  : string.Empty)
            : string.Empty;

        var total = _config.Objects.Count;
        if (changed == 0)
        {
            SetMessage(byPicture
                ? $"{policyText}层号已一致（地/立/叠 {bands[0]}/{bands[1]}/{bands[2]}）{extraText}｜保存到JSON"
                : $"层号已与 PSD 层序一致，无需改动（{total} 个实例）");
            return;
        }

        SetMessage(byPicture
            ? $"{policyText}层号 {changed}/{total}（地/立/叠 {bands[0]}/{bands[1]}/{bands[2]}）{extraText}｜保存到JSON"
            : $"已还原 PSD 层序：{changed}/{total} 个实例层号变化（地/立/叠 {bands[0]}/{bands[1]}/{bands[2]}）；画面遮挡不变");
    }

    /// <summary>比例微调：在当前倍率上叠加，夹在 20% ~ 300%。</summary>
    /// <param name="delta">增量倍率。</param>
    private void StepScale(double delta)
    {
        ApplyProperty(item => item.Scale = Math.Clamp(Math.Round(item.Scale + delta, 2), 0.2, 3.0));
    }

    /// <summary>角度微调：在当前角度上叠加，归一到 [-180, 180)。</summary>
    /// <param name="delta">增量（度，正=顺时针）。</param>
    private void StepRotation(double delta)
    {
        ApplyProperty(item => item.Rotation = NormalizeAngle180(item.Rotation + delta));
    }

    /// <summary>把角度归一到 [-180, 180)，便于角度行连续左旋 / 右旋不越界。</summary>
    /// <param name="degrees">输入角度。</param>
    /// <returns>归一后的角度。</returns>
    private static double NormalizeAngle180(double degrees)
    {
        var value = Math.Round(degrees, 2) % 360;
        if (value >= 180)
        {
            value -= 360;
        }
        else if (value < -180)
        {
            value += 360;
        }

        return value;
    }

    /// <summary>回填碰撞 / 阴影区域：偏移取原始值，尺寸取换算后的世界像素（0 尺寸显示为铺满后的值）。</summary>
    /// <param name="item">选中实例。</param>
    private void SyncAreaFields(PlacedObject item)
    {
        var collision = item.CollisionBounds;
        CollisionXField.Text = item.CollisionX.ToString("0.##", CultureInfo.InvariantCulture);
        CollisionYField.Text = item.CollisionY.ToString("0.##", CultureInfo.InvariantCulture);
        CollisionWField.Text = collision.Width.ToString("0.##", CultureInfo.InvariantCulture);
        CollisionHField.Text = collision.Height.ToString("0.##", CultureInfo.InvariantCulture);

        var shadow = item.ShadowBounds;
        ShadowXField.Text = item.ShadowX.ToString("0.##", CultureInfo.InvariantCulture);
        ShadowYField.Text = item.ShadowY.ToString("0.##", CultureInfo.InvariantCulture);
        ShadowWField.Text = shadow.Width.ToString("0.##", CultureInfo.InvariantCulture);
        ShadowHField.Text = shadow.Height.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>清空碰撞 / 阴影区域输入框（未选中实例时调用）。</summary>
    private void ClearAreaFields()
    {
        CollisionXField.Text = string.Empty;
        CollisionYField.Text = string.Empty;
        CollisionWField.Text = string.Empty;
        CollisionHField.Text = string.Empty;
        ShadowXField.Text = string.Empty;
        ShadowYField.Text = string.Empty;
        ShadowWField.Text = string.Empty;
        ShadowHField.Text = string.Empty;
        ScaleValueText.Text = "100%";
        AngleValueText.Text = "0°";
    }

    /// <summary>三块的显隐：碰撞区域看「碰撞」、阴影区域看「阴影」、物体变换看「收集」，未选中时全部收起。</summary>
    private void UpdateAreaSectionVisibility()    {
        var item = Canvas.Selected;
        var collision = item?.CollisionEnabled == true;
        var shadow = item?.ShadowEnabled == true;
        var transform = item?.IsCollectible == true;

        if (CollisionAreaSection.IsVisible != collision)
        {
            CollisionAreaSection.IsVisible = collision;
        }

        if (ShadowAreaSection.IsVisible != shadow)
        {
            ShadowAreaSection.IsVisible = shadow;
        }

        if (TransformSection.IsVisible != transform)
        {
            TransformSection.IsVisible = transform;
            TrafficScroll.Offset = default;
        }
    }

    #endregion

    /// <summary>
    /// 改「位置」：直接写实例的世界坐标（像素，相对地图左上角），和 Unity 版 RectTransform 的 Pos 同口径。
    /// 未传的一边沿用当前坐标，避免只改 X 时 Y 被清成 0。
    /// </summary>
    /// <param name="x">新的 X（像素）；null 表示不改。</param>
    /// <param name="y">新的 Y（像素）；null 表示不改。</param>
    private void ApplyObjectPosition(double? x, double? y)
    {
        var item = Canvas.Selected;
        if (_suppressSync || item is null || (x is null && y is null))
        {
            return;
        }

        item.X = x ?? item.X;
        item.Y = y ?? item.Y;
        Canvas.Refresh();
        SyncProperties();
    }

    /// <summary>
    /// 改「大小」：写像素尺寸，并把缩放倍率归 1，保证输入值就是画布上的显示尺寸。
    /// 未传的一边沿用当前显示尺寸，避免只改宽时高被折算成格尺寸。
    /// </summary>
    /// <param name="width">新宽度（像素）；null 表示不改。</param>
    /// <param name="height">新高度（像素）；null 表示不改。</param>
    private void ApplyObjectSize(double? width, double? height)
    {
        var item = Canvas.Selected;
        if (_suppressSync || item is null || (width is null && height is null))
        {
            return;
        }

        var bounds = item.Bounds;
        item.PixelWidth = Math.Max(1, width ?? bounds.Width);
        item.PixelHeight = Math.Max(1, height ?? bounds.Height);
        item.Scale = 1.0;
        Canvas.Refresh();
        SyncProperties();
    }
}

