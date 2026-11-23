/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementPanel.Events.cs
 * author:    云毅
 * created:   2026
 * descrip:   物品元件面板事件参数：模板可见性变化
 ***************************************************************/

using System;

namespace MapEditorTool.Controls;

/// <summary>模板可见性变化事件参数。</summary>
public sealed class TemplateVisibilityEventArgs : EventArgs
{
    /// <summary>初始化事件参数。</summary>
    /// <param name="templateId">模板 ID。</param>
    /// <param name="visible">是否显示。</param>
    public TemplateVisibilityEventArgs(string templateId, bool visible)
    {
        TemplateId = templateId;
        Visible = visible;
    }

    /// <summary>模板 ID。</summary>
    public string TemplateId { get; }

    /// <summary>是否显示。</summary>
    public bool Visible { get; }
}
