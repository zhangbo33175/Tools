/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.EventWiring.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口事件装配：模式/资源/交通/雾效/属性/画布的控件接线。
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
    #region 事件装配

    private void WireTabs()
    {
        TabScene.Click += (_, _) => SwitchMode(0);
        TabItemElement.Click += (_, _) => SwitchMode(1);
        TabFog.Click += (_, _) => SwitchMode(2);
        TabResource.Click += (_, _) => SwitchMode(3);

        TabBuilding.Click += (_, _) => SwitchResourceType(MapObjectType.Building);
        TabGround.Click += (_, _) => SwitchResourceType(MapObjectType.Character);
        TabProp.Click += (_, _) => SwitchResourceType(MapObjectType.Prop);

        BtnRefresh.Click += (_, _) =>
        {
            var loaded = _templateStore.LoadOrCreate(() => TemplateCatalog.CreateBuiltIn());
            _templates.Clear();
            _templates.AddRange(loaded);

            // 配置表也一起重读，场景资源列表 / 物品元件编辑都按表最新内容刷新
            _itemTable = LoadItemElementTable();
            _itemPanel?.SetItemTable(_itemTable);
            _resourcePanel?.SetItemTable(_itemTable);

            _pendingTemplate = null;
            Canvas.PendingTemplate = null;
            _itemPanel?.RefreshAll();
            BuildResourceList();
            SetMessage(_itemTable is null
                ? $"资源列表已刷新：{_templates.Count} 个元件模板"
                : $"资源列表已刷新：配置表 {_itemTable.Files.Count} 张 / {_itemTable.RowCount} 行");
        };

        BtnConfig.Click += async (_, _) => await OpenMapConfigAsync();

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
            Canvas.Refresh();
        };
        ResourcePanelHost.Child = _resourcePanel;
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
            var path = Path.Combine(_saveDirectory, FogFileName() + ".fog.json");
            Directory.CreateDirectory(_saveDirectory);
            File.WriteAllText(
                path,
                System.Text.Json.JsonSerializer.Serialize(
                    _fogDoc,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            SetMessage($"迷雾保存失败：{ex.Message}");
        }
    }

    private void WireItemElementPanel()
    {
        _itemPanel = new ItemElementPanel(_templateStore, DescribeRemoveBlocker, SetMessage);
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

    /// <summary>元件删除前置校验：仍被摆放实例引用时不允许删除。</summary>
    /// <param name="template">待删除模板。</param>
    /// <returns>null 表示允许删除，否则返回拒绝原因。</returns>
    private string? DescribeRemoveBlocker(ObjectTemplate template)
    {
        var count = _config.Objects.Count(instance => instance.TemplateId == template.TemplateId);
        return count > 0
            ? $"元件 {template.TemplateId} 仍被 {count} 个摆放实例引用，请先删除这些实例。"
            : null;
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
        ApplyTabVisual(TabResource, _mode == 3);
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
