/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.ResourceBinding.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口配置表 ID 绑定：载入 / 保存 / 发布前把实例模板 ID 对到配置表数字 ID
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool;

public partial class MainWindow
{
    #region 绑定入口

    /// <summary>把当前地图所有实例的模板 ID 对到配置表数字 ID。</summary>
    /// <param name="createMissing">true = 为配置表里没有的资源新建行并分配 ID。</param>
    /// <returns>绑定统计。</returns>
    private MapBindResult BindMapTemplates(bool createMissing) =>
        MapTemplateBinder.Bind(
            _config,
            _itemTable,
            createMissing,
            _templates.Select(template => template.TemplateId).ToList());

    /// <summary>载入地图后的绑定：只对号、不建行，缺行时提示去资源整理补齐。</summary>
    /// <returns>绑定统计。</returns>
    private MapBindResult BindAfterMapLoad()
    {
        Canvas.ItemTable = _itemTable;
        var bind = BindMapTemplates(createMissing: false);
        if (bind.Bound > 0)
        {
            SyncProperties();
            Canvas.Refresh();
        }

        return bind;
    }

    /// <summary>把绑定结果拼成一句状态栏后缀。</summary>
    /// <param name="bind">绑定统计。</param>
    /// <returns>后缀文本；无变化时为空串。</returns>
    private static string DescribeBind(MapBindResult bind)
    {
        var parts = new List<string>();
        if (bind.Bound > 0)
        {
            parts.Add($"已对齐配置表 ID：{bind.Bound} 个实例");
        }

        if (bind.Created > 0)
        {
            parts.Add($"新建配置行：{bind.Created} 个资源");
        }

        if (!bind.IsComplete)
        {
            parts.Add($"{bind.Missing.Count} 个资源未在配置表登记（已在地图上红框标出）");
        }

        return parts.Count == 0 ? string.Empty : "；" + string.Join("，", parts);
    }

    #endregion
}
