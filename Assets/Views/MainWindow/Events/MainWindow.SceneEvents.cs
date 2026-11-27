/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.SceneEvents.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口场景事件：工具条、画布交互、图层开关与场景字段。
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
    private void WireToolbar()
    {
        BtnSave.Click += (_, _) => SaveMap();

        // 发布只保留顶部工具条一个入口（物体卡脚部那个重复入口已删掉）
        BtnPublishTop.Click += async (_, _) => await PublishMapAsync();

        BtnExportSettings.Click += (_, _) => SetMessage("导出设置：选择地图 JSON 的输出目录（默认 docs\\MapJson），切片粒度按 32px 格子");
        BtnExportSlice.Click += (_, _) => ExportSlices();

        BtnClearObjects.Click += (_, _) =>
        {
            _config.Objects.Clear();
            Canvas.Select(null);
            Canvas.Refresh();
            BuildPathList();
            RefreshStatus();
            SetMessage("已清空摆放物");
        };

        BtnClearGridData.Click += (_, _) =>
        {
            _config.SidewalkCells.Clear();
            _config.RoadCells.Clear();
            _config.Regions.Clear();
            Canvas.Refresh();
            SetMessage("已清空格子数据");
        };

        BtnImportPsd.Click += async (_, _) => await ImportPsdAsync();
        BtnAddCollectible.Click += async (_, _) =>
        {
            if (HasCollectibleData())
            {
                ToggleCollectiblePanel();
                return;
            }

            await AddCollectibleAsync();
        };
        BtnMapPreview.Click += (_, _) => OpenMapPreview();
    }

    private void WireCanvas()
    {
        Canvas.ViewChanged += (_, _) => RefreshStatus();
        Canvas.SelectionChanged += (_, _) =>
        {
            // 选中物体时格子高亮已被清掉，提示行回到默认文案，避免残留上一次的格子消息
            if (Canvas.Selected is not null)
            {
                RefreshSceneHint();
            }

            SyncProperties();
            BuildPathList();
            SyncPathEditingWithSelection();
        };
        Canvas.DocumentChanged += (_, _) =>
        {
            RefreshStatus();
            SyncProperties();
            BuildPathList();
            SyncPathEditUi();
            RefreshCollectibleEntry();
            RefreshCollectibleSummary();
        };
        Canvas.PathDrawingChanged += (_, _) =>
        {
            // 路径开关影响空列表提示（「开始绘制」/「左键点地面」两种文案），
            // 必须和按钮文案一起刷新，否则会出现按钮写「停止」、列表还叫你去点「开始」。
            // 进入路径编辑时把顶部「路径网格」同步勾上；画布侧已自动置为可见，
            // 这里只负责让勾选框与真实状态一致，退出后保留用户当前选择。
            if (Canvas.PathDrawing && LayerPathGrid.IsChecked != true)
            {
                LayerPathGrid.IsChecked = true;
            }

            SyncPathEditUi();
            BuildPathList();
        };
        Canvas.PathPointRejected += (_, message) => SetMessage(message);
        Canvas.PathPointFeedback += (_, message) => SetMessage(message);
        Canvas.PointerWorldChanged += (_, world) =>
        {
            StatusMouseX.Text = ((int)Math.Round(world.X)).ToString(CultureInfo.InvariantCulture);
            StatusMouseY.Text = ((int)Math.Round(world.Y)).ToString(CultureInfo.InvariantCulture);
            // 格子坐标跟随绘制形状：菱形模式按 45° 菱形格换算，矩形模式按正交格换算
            var cell = Canvas.IndexOfWorld(world);
            StatusTileX.Text = ((int)cell.X).ToString(CultureInfo.InvariantCulture);
            StatusTileY.Text = ((int)cell.Y).ToString(CultureInfo.InvariantCulture);
        };
        Canvas.CellSelectionChanged += (_, cell) =>
        {
            if (cell is null)
            {
                return;
            }

            SetMessage($"已选中格子 [{Math.Round(cell.Value.X)} , {Math.Round(cell.Value.Y)}]");
        };
    }

    private void WireLayerToggles()
    {
        LayerPlaceGrid.IsCheckedChanged += (_, _) =>
        {
            Canvas.ShowGrid = LayerPlaceGrid.IsChecked == true;
            Canvas.Refresh();
        };

        LayerPathGrid.IsCheckedChanged += (_, _) =>
        {
            Canvas.ShowPathGrid = LayerPathGrid.IsChecked == true;
            Canvas.Refresh();
        };

        LayerBackground.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerBackground, MapLayerKeys.Background);
        LayerGreenery.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerGreenery, MapLayerKeys.Greenery);
        LayerRoad.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerRoad, MapLayerKeys.Road);
        LayerBuilding.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerBuilding, MapLayerKeys.Building);
        LayerObject.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerObject, MapLayerKeys.Prop);
        LayerCollectible.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerCollectible, MapLayerKeys.Collectible);
        LayerCharacter.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerCharacter, MapLayerKeys.Character);
        LayerForeground.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerForeground, MapLayerKeys.Foreground);
        LayerBubbles.IsCheckedChanged += (_, _) => ApplyLayerToggle(LayerBubbles, MapLayerKeys.Bubble);

        LayerBoundingBox.IsCheckedChanged += (_, _) =>
        {
            Canvas.ShowBoundingBox = LayerBoundingBox.IsChecked == true;
            Canvas.Refresh();
        };

        ClipOutsideMapToggle.IsCheckedChanged += (_, _) =>
        {
            // 只影响显示，不改实例坐标、尺寸或分层数据；关闭后整张越界图完整露出。
            Canvas.ShowClipOutsideMap = ClipOutsideMapToggle.IsChecked == true;
            Canvas.Refresh();
            SetMessage(Canvas.ShowClipOutsideMap
                ? "裁剪越界：开启（只画地图包围盒内的美术）"
                : "裁剪越界：关闭（完整显示越界图片，便于检查边缘出血）");
        };

        LayerMap.IsCheckedChanged += (_, _) =>
        {
            Canvas.ShowMapLayer = LayerMap.IsChecked == true;
            Canvas.Refresh();
        };
    }

    private void ApplyLayerToggle(CheckBox box, string layerKey)
    {
        if (box.IsChecked == true)
        {
            Canvas.HiddenLayers.Remove(layerKey);
        }
        else
        {
            Canvas.HiddenLayers.Add(layerKey);
        }

        Canvas.Refresh();
    }

    private void WireSceneFields()
    {
        BindText(SceneNameField, text => _config.SceneName = text.Trim());
        BindText(SceneIdField, text => _config.SceneId = text.Trim());
        BindNumber(MapWidthField, value =>
        {
            _config.Width = (int)Math.Clamp(value, 256, 8192);
            Canvas.Refresh();
            RefreshStatus();
        });
        BindNumber(MapHeightField, value =>
        {
            _config.Height = (int)Math.Clamp(value, 256, 8192);
            Canvas.Refresh();
            RefreshStatus();
        });

        // 原 Unity 工具的「更新地图」是从磁盘 JSON 重新加载（外部脚本/配置表改过后能看到最新数据），
        // 不是只把视口 FitToView。这里按同口径重载，并保留当前缩放/平移。
        BtnUpdateMap.Click += async (_, _) => await ReloadMapFromDiskAsync();

        // 工具条的「刷新层级」= 按当前画面（遮挡带 + 脚底 Y）重写立体带的 Unity 层号，
        // 与属性卡的「按画面重排」同一实现；地表/叠加带保留 PSD 层序，画面前后由 LayerOrder.Compare 实时决定。
        BtnRefreshLayer.Click += (_, _) => ResortOcculsion(true);

        // 「刷新画面」不读 PSD、不重建摆放物：按三带遮挡 + 脚底 Y 重新出图，
        // 并做两类窄口自动修复（背景组立体美术提层、池塘/地毯与上方立体件的 1–24px 接缝贴合）。
        // 如果 PSD 源文件已经被改过，这里明确提示先点「更新地图」，避免用户以为刷新能读到新画稿。
        BtnPreviewMap.Click += (_, _) =>
        {
            var runtimeIds = new HashSet<string>();
            if (_psdPreview is not null)
            {
                foreach (var pair in _psdObjectByLayer)
                {
                    if (_psdPreview.PngByIndex.ContainsKey(pair.Key))
                    {
                        runtimeIds.Add(pair.Value.Id);
                    }
                }
            }

            var report = SceneDisplayAudit.Apply(
                _config,
                _assets,
                item => runtimeIds.Contains(item.Id));

            Canvas.ClearSpriteCache();
            Canvas.Refresh();
            RefreshStatus();

            var stalePsd = ProbePsdSource() is { Changed: true };
            var summary =
                $"刷新画面：{report.Instances} 实例｜带 {report.Ground}/{report.Standing}/{report.Overlay}｜" +
                $"缺图 {report.Missing}｜旋转 {report.Rotated} 拉伸 {report.Stretched} 平铺 {report.TileCandidates}";
            if (report.MemoryPreviews > 0)
            {
                summary += $"｜预览 {report.MemoryPreviews}";
            }

            if (report.PixelSizeCompleted > 0 || report.PixelSizeMismatch > 0)
            {
                summary += $"｜尺寸 补{report.PixelSizeCompleted}/差{report.PixelSizeMismatch}";
            }

            if (report.PixelSizeMismatch > 0)
            {
                // 命名修复只保证「以后导出的切片」不撞名；磁盘上的旧产物仍是同名共用一张图，
                // 这里把恢复动作说清楚，避免把「刷新画面」当成会自动重烘 PSD。
                summary += "（旧切片同名，需重导PSD）";
            }

            if (report.OffMap > 0)
            {
                // 越界不等于错误：树/灌木/海滩边缘本来就会出血，默认只裁显示；
                // 这里明确标出有没有被地图包围盒裁掉，便于对照「裁剪越界」开关。
                summary += $"｜越界{report.OffMap}" + (Canvas.ShowClipOutsideMap ? "(已裁)" : "(未裁)");
            }

            if (report.BandPromoted > 0)
            {
                // 背景组里被误判为地表的树/灌木：围栏会压住它，形成硬切边。
                // 导入与「刷新画面」都会自动提升到立体带；再点保存即可把 band 落进 JSON。
                summary += $"｜遮挡 自动提层 {report.BandPromoted}";
            }

            if (report.SeamSnapped > 0)
            {
                // 池塘/地毯这类平面件与上方立体件只差 1–32px 的窄缝，自动贴到上方脚底。
                summary += $"｜接缝 贴合 {report.SeamSnapped}";
            }

            if (stalePsd)
            {
                SetMessageError($"PSD 已修改：请先点「更新地图」重读；{summary}");
            }
            else
            {
                SetMessage(summary);
            }
        };

        PlacementToggle.IsCheckedChanged += (_, _) =>
        {
            Canvas.PlacementEnabled = PlacementToggle.IsChecked == true;
            SetMessage(Canvas.PlacementEnabled ? "摆放模式已开启" : "摆放模式已关闭");
        };

        DrawShapeField.SelectionChanged += (_, _) =>
        {
            if (_suppressSync)
            {
                return;
            }

            _config.DrawShape = DrawShapeField.SelectedIndex == 1 ? DrawShapes.Diamond : DrawShapes.Rect;
            Canvas.Refresh();
            SetMessage($"绘制形状：{(_config.DrawShape == DrawShapes.Rect ? "矩形" : "菱形")}");
        };

        RegionTypeField.SelectionChanged += (_, _) =>
        {
            if (_suppressSync)
            {
                return;
            }

            _config.RegionType = RegionTypeField.SelectedIndex switch
            {
                1 => RegionTypes.Forbid,
                2 => RegionTypes.Common,
                _ => RegionTypes.Place,
            };
        };

        DrawClearToggle.IsCheckedChanged += (_, _) =>
            SetMessage($"DrawClear：{(DrawClearToggle.IsChecked == true ? "开启" : "关闭")}");

        // 图片边框开关：只影响画布上的图元外框/占格描边，不改数据
        DrawBorderToggle.IsCheckedChanged += (_, _) =>
        {
            Canvas.ShowImageBorder = DrawBorderToggle.IsChecked == true;
            Canvas.Refresh();
            SetMessage($"DrawBorder：{(Canvas.ShowImageBorder ? "显示图片边框" : "隐藏图片边框")}");
        };

        BtnPickSaveDir.Click += async (_, _) => await PickSaveDirectoryAsync();
    }
}

