/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceRowData.cs
 * author:    云毅
 * created:   2026
 * descrip:   已配置资源表的共享行视图：统一配置表行与模板库的字段口径
 ***************************************************************/

namespace MapEditorTool.Models;

/// <summary>
/// 已配置资源表的一行数据视图。
/// 配置表行（<c>ItemElementRow</c>）与模板库（<c>ObjectTemplate</c>）字段同名同型，
/// 差异仅在 <c>Id / Icon</c> 与 <c>TemplateId / ImagePath</c> 的命名；
/// 由各自模型实现本接口后，表格渲染只依赖这一份字段契约。
/// </summary>
public interface IResourceRowData
{
    /// <summary>序号（建筑 2xxx / 人物 1xxx / 物件 3xxx）。只读展示。</summary>
    string TemplateId { get; set; }

    /// <summary>图片名称，对应 Map 目录下的 PNG。</summary>
    string ImagePath { get; set; }

    /// <summary>资源 ResDef 名。</summary>
    string ResName { get; set; }

    /// <summary>建筑名称 / 预制体名。</summary>
    string TemplateName { get; set; }

    /// <summary>默认动画名。</summary>
    string AnimName { get; set; }

    /// <summary>动画是否循环。</summary>
    bool Loop { get; set; }

    /// <summary>动画播放速度。</summary>
    double Speed { get; set; }

    /// <summary>渲染层级排序。</summary>
    int SortOrder { get; set; }

    /// <summary>对话气泡内容。</summary>
    string DialogBubble { get; set; }

    /// <summary>是否显示阴影。</summary>
    bool ShadowEnabled { get; set; }

    /// <summary>是否收集物。</summary>
    bool IsCollectible { get; set; }

    /// <summary>重生时间（秒）。</summary>
    double RespawnTime { get; set; }
}
