/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.EventWiring.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口核心事件装配：模式、雾效、资源与物品元件面板的控件接线。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    #region 事件装配

    private void WireTabs()
    {
        TabScene.Click += (_, _) => SwitchMode(0);
        TabItemElement.Click += (_, _) => SwitchMode(1);
        TabFog.Click += (_, _) => SwitchMode(2);
        // 交通规则已从顶栏页签下移到场景工具条（排在「添加收集」之后），
        // 语义从「切页」变成「开/关浮窗」，所以这里做成可再次点击收起的开关。
        TabTraffic.Click += (_, _) =>
        {
            if (TrafficRulePanelHost.IsVisible)
            {
                HideTrafficRuleDock();
            }
            else
            {
                OpenTrafficRuleDock();
            }

            SwitchMode(0);
            UpdateActiveTabVisual();
        };
        TabResource.Click += (_, _) => SwitchMode(3);
        TabStory.Click += (_, _) => SwitchMode(4);

        TabBuilding.Click += (_, _) => SwitchResourceType(MapObjectType.Building);
        TabGround.Click += (_, _) => SwitchResourceType(MapObjectType.Character);
        TabProp.Click += (_, _) => SwitchResourceType(MapObjectType.Prop);

        BtnRefresh.Click += (_, _) =>
        {
            var loaded = _templateStore.LoadOrCreate(() => TemplateCatalog.CreateBuiltIn());
            _templates.Clear();
            _templates.AddRange(loaded);

            // 模板重读后重建朝向索引：新配的 facingImages 立刻生效，不用重启编辑器
            _facingImages.Rebuild(_templates);
            Canvas.FacingImages = _facingImages;

            // 配置表也一起重读，场景资源列表 / 物品元件编辑都按表最新内容刷新
            _itemTable = LoadItemElementTable();
            _itemPanel?.SetItemTable(_itemTable);
            _resourcePanel?.SetItemTable(_itemTable);
            _storyPanel?.SetItemTable(_itemTable);
            Canvas.ItemTable = _itemTable;
            Canvas.Refresh();

            _pendingTemplate = null;
            Canvas.PendingTemplate = null;
            _itemPanel?.RefreshAll();
            BuildResourceList();
            SetMessage(_itemTable is null
                ? $"资源列表已刷新：{_templates.Count} 个元件模板"
                : $"资源列表已刷新：配置表 {_itemTable.Files.Count} 张 / {_itemTable.RowCount} 行");
        };

        BtnConfig.Click += async (_, _) => await OpenMapConfigAsync();

        BtnGrid.Click += async (_, _) => await OpenGridSettingsAsync();

        BtnDelete.Click += (_, _) =>
        {
            if (_pendingTemplate is null || _pendingTemplate.IsBuiltIn)
            {
                SetMessage("内置模板不可删除");
                return;
            }

            _templates.Remove(_pendingTemplate);
            _pendingTemplate = null;
            Canvas.PendingTemplate = null;
            _itemPanel?.RefreshAll();
            BuildResourceList();
            SetMessage("已删除元件模板");
        };
    }


    /// <summary>挂载迷雾编辑面板：笔刷参数、绘制开关、区域列表。</summary>
    private void WireFogPanel()
    {
        _fogPanel = new FogEditPanel(SetMessage);
        _fogPanel.Attach(_config, _fogDoc, Canvas);
        _fogPanel.DrawingChanged += (_, _) =>
        {
            Canvas.FogDrawing = _fogPanel.Drawing && _mode == 2;
            if (_fogPanel.Drawing)
            {
                Canvas.PathDrawing = false;
            }
        };
        _fogPanel.ResetRequested += (_, _) => ReloadFogFromDisk();

        _fogPanel.FogChanged += (_, _) =>
        {
            Canvas.Refresh();
            PersistFog();
        };
        FogPanelHost.Child = _fogPanel;
    }

    /// <summary>
    /// 挂载交通规则编辑面板：面板放进画布上的悬浮窗容器，可拖动 / 贴边停靠，
    /// 不再占用主工作区，编辑规则的同时地图与效果始终可见。
    /// </summary>
    private void WireTrafficRulePanel()
    {
        _trafficPanel = new TrafficRulePanel(SetMessage);
        _trafficPanel.Attach(_config, Canvas);
        _trafficPanel.DemoRequested += (_, _) => OpenTrafficDemo();
        _trafficPanel.PreviewRequested += (_, _) =>
        {
            OpenMapPreview();
            SetMessage("已打开地图预览：『交通仿真』默认开启，规则改动即时生效");
        };
        TrafficRuleContent.Child = _trafficPanel;

        // 右栏的交通标记 / 实例属性整体并入面板「物件标记」页，右栏只留物件自身编辑项
        MoveTrafficControlsToRulePanel(_trafficPanel);
        WireTrafficRuleDock();
    }

    /// <summary>挂载资源整理面板：扫描未配置 PNG、补字段、写入配置表。</summary>
    private void WireResourcePanel()
    {
        _resourcePanel = new ResourceOrganizePanel(_templateStore, SetMessage);
        _resourcePanel.SetMapDirectory(ResolveResourceDirectory());
        _resourcePanel.SetItemTable(_itemTable);
        _resourcePanel.Attach(_templates, _config);
        _resourcePanel.CatalogChanged += (_, _) =>
        {
            _itemPanel?.RefreshAll();
            BuildResourceList();
            // 补齐 / 新建行后表内容变了，画布上的「未登记」标记要跟着消失
            Canvas.ItemTable = _resourcePanel.ItemTable;
            Canvas.Refresh();
        };
        ResourcePanelHost.Child = _resourcePanel;
    }

    /// <summary>挂载剧情编辑面板：剧情文件与地图同名同目录，切页时按地图路径同步。</summary>
    private void WireStoryPanel()
    {
        _storyPanel = new StoryEditorPanel(SetMessage);
        _storyPanel.SetItemTable(_itemTable);
        _storyPanel.Attach(_config, Canvas.Assets, _currentMapPath);
        StoryPanelHost.Child = _storyPanel;
    }

    /// <summary>把迷雾文档写回磁盘（与地图 JSON 分离的 sidecar 文件）。</summary>
    /// <summary>迷雾 sidecar 的基名：优先用地图文件名，未载入文件时退回 sceneId。</summary>
    private string FogFileName()
    {
        if (_currentMapPath.Length > 0)
        {
            return Path.GetFileNameWithoutExtension(_currentMapPath);
        }

        return string.IsNullOrWhiteSpace(_config.SceneId) ? "scene" : _config.SceneId;
    }

    /// <summary>把迷雾恢复到上次保存的状态（重新读 sidecar）。</summary>
    private void ReloadFogFromDisk()
    {
        LoadFogForScene();
        _fogPanel?.Attach(_config, _fogDoc, Canvas);
        Canvas.ClearFogHistory();
        Canvas.Refresh();
        SetMessage("已重置迷雾为上次保存的状态");
    }

    private void PersistFog()
    {
        try
        {
            _fogDoc.SceneId = _config.SceneId;
            var path = Path.Combine(_saveDirectory, MapFileNames.FogForMap(FogFileName()));
            FileSystemIo.EnsureDirectory(_saveDirectory);
            FileSystemIo.WriteText(
                path,
                System.Text.Json.JsonSerializer.Serialize(
                    _fogDoc,
                    JsonFileOptions.Indented));
        }
        catch (Exception ex)
        {
            SetMessage(UiFeedback.Failed("迷雾保存", ex));
        }
    }

    private void WireItemElementPanel()
    {
        _itemPanel = new ItemElementPanel(_templateStore, SetMessage);
        _itemPanel.CatalogChanged += (_, _) => BuildResourceList();
        _itemPanel.TemplatesSaved += (_, _) =>
        {
            Canvas.NineSlices = _itemPanel.BuildNineSliceCatalog();
            SyncTemplatesToPlacements();
            BuildResourceList();
        };
        _itemPanel.NineSliceChanged += (_, _) =>
        {
            Canvas.NineSlices = _itemPanel.BuildNineSliceCatalog();
            Canvas.Refresh();
        };
        _itemPanel.TemplateVisibilityChanged += (_, e) =>
        {
            if (e.Visible)
            {
                Canvas.HiddenTemplateIds.Remove(e.TemplateId);
            }
            else
            {
                Canvas.HiddenTemplateIds.Add(e.TemplateId);
            }

            Canvas.Refresh();
            SetMessage(e.Visible ? $"已显示模板 {e.TemplateId} 的摆放实例" : $"已隐藏模板 {e.TemplateId} 的摆放实例");
        };
        ItemPanelHost.Child = _itemPanel;
        _itemPanel.Attach(_templates, _activeType);
        _itemPanel.SetItemTable(_itemTable);
    }

    /// <summary>模板保存后，把模板属性同步到已摆放实例并刷新画布与资源列表。</summary>
    private void SyncTemplatesToPlacements()
    {
        var updated = TemplateSyncService.Apply(_config, _templates);
        Canvas.Refresh();
        BuildResourceList();
        RefreshStatus();
        SetMessage($"元件模板已保存，同步更新 {updated} 个摆放实例");
    }

    private void SwitchMode(int mode)
    {
        _mode = mode;
        ScenePanel.IsVisible = mode == 0;
        ItemPanelHost.IsVisible = mode == 1;
        FogPanelHost.IsVisible = mode == 2;
        ResourcePanelHost.IsVisible = mode == 3;
        StoryPanelHost.IsVisible = mode == 4;

        // 交通规则悬浮窗只在场景编辑里可见：切到其它页签先收起，回到场景再恢复。
        TrafficRuleOverlay.IsVisible = mode == 0 && TrafficRulePanelHost.IsVisible;

        // 路径绘制只在场景模式生效，切走后复位，避免在别的面板误加点
        if (mode != 0)
        {
            SetPathEditing(false, notify: false);
        }

        if (mode == 1)
        {
            _itemPanel?.RefreshAll();
        }
        else if (mode == 2)
        {
            _fogPanel?.RefreshAll();
        }
        else if (mode == 3)
        {
            _resourcePanel?.RefreshAll();
        }
        else if (mode == 4)
        {
            // 剧情跟着地图走：换了地图才重新读文件，同一张地图只同步上下文，避免丢掉未保存改动
            _storyPanel?.SyncMap(_config, Canvas.Assets, _currentMapPath);
        }

        Canvas.FogDrawing = mode == 2 && _fogPanel?.Drawing == true;
        Canvas.Refresh();
        UpdateActiveTabVisual();
    }

    /// <summary>按当前激活的工作区刷新顶部四枚模式 Tab 的底色与文字色。</summary>
    private void UpdateActiveTabVisual()
    {
        ApplyTabVisual(TabScene, _mode == 0);
        ApplyTabVisual(TabItemElement, _mode == 1);
        ApplyTabVisual(TabFog, _mode == 2);
        ApplyTabVisual(TabTraffic, TrafficRulePanelHost.IsVisible);
        ApplyTabVisual(TabResource, _mode == 3);
        ApplyTabVisual(TabStory, _mode == 4);
    }

    private void SwitchResourceType(MapObjectType type)
    {
        _activeType = type;
        _pendingTemplate = null;
        Canvas.PendingTemplate = null;
        Canvas.PendingCollectibleTemplate = null;
        BuildResourceList();
    }

    #endregion
}
