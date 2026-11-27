/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Unconfigured.cs
 * author:    云毅
 * created:   2026
 * descrip:   未配置资源写入：表单补字段、单条与批量加入配置表。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool.Controls;

public sealed partial class ResourceOrganizePanel : UserControl
{
    #region 添加到配置表

    /// <summary>新增目标表的解析结果。</summary>
    /// <param name="Category">实际落入的分类表名。</param>
    /// <param name="File">目标表；null 表示回落到主表。</param>
    /// <param name="Created">本次是否新建了分类表。</param>
    /// <param name="Failed">分类表创建失败。</param>
    private readonly record struct TargetTable(
        string Category,
        ItemTableFile? File,
        bool Created,
        bool Failed);

    /// <summary>
    /// 解析并准备新增目标表：当前分类优先，必须独立成表的分类缺表时自动创建。
    /// 单条与批量新增共用，避免两条路径各写一套建表判断。
    /// </summary>
    /// <param name="category">资源分类键。</param>
    /// <returns>目标表解析结果。</returns>
    private TargetTable ResolveTargetTable(string category)
    {
        var targetCategory = ResourceCategories.IsAll(_filter)
            ? ResourceCategories.DisplayNameOfKey(category)
            : _filter;
        var file = _itemTable?.FileForCategory(targetCategory);
        if (file is not null
            || _itemTable is null
            || ResourceCategories.FindByDisplay(targetCategory) is not { RequiresOwnTable: true })
        {
            return new TargetTable(targetCategory, file, false, false);
        }

        file = _itemTable.EnsureCategoryTable(targetCategory);
        return file is null
            ? new TargetTable(targetCategory, null, false, true)
            : new TargetTable(targetCategory, file, true, false);
    }

    /// <summary>
    /// 解析新增目标表，并在建表失败时统一给出提示。
    /// 单条新增与批量新增共用，避免两条路径各写一份失败分支。
    /// </summary>
    /// <param name="category">资源分类键。</param>
    /// <returns>可写入的目标表；null 表示建表失败。</returns>
    private TargetTable? ResolveWritableTargetTable(string category)
    {
        var target = ResolveTargetTable(category);
        if (!target.Failed)
        {
            return target;
        }

        _notify($"无法创建「{target.Category}」配置表：配置表目录不可写或同名文件表头无法识别");
        return null;
    }

    /// <summary>配置表发生新增 / 删除后的统一刷新：扫描未配置项、重建表格并广播目录变化。</summary>
    private void RefreshAfterTableChange()
    {
        ScanUnconfigured(silent: true);
        RefreshConfiguredTable();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 查找当前操作要绑定的配置表行。
    /// 新增只认当前资源名；修改才按编辑对象的图片名 / 资源名回查原行。
    /// </summary>
    /// <param name="isUpdate">是否处于修改模式。</param>
    /// <param name="name">当前资源名。</param>
    /// <returns>命中的配置表行；没有为 null。</returns>
    private ItemElementRow? FindBoundRow(bool isUpdate, string name)
    {
        if (_itemTable is null)
        {
            return null;
        }

        if (isUpdate && _editing is not null)
        {
            return _itemTable.FindByTemplate(_editing, name);
        }

        return _itemTable.Find(name);
    }

    /// <summary>未配置资源表单里参与落库的六个可编辑字段。</summary>
    /// <param name="AnimName">默认动画。</param>
    /// <param name="Loop">是否循环。</param>
    /// <param name="Speed">播放速度。</param>
    /// <param name="SortOrder">渲染层级。</param>
    /// <param name="ShadowEnabled">是否显示阴影。</param>
    /// <param name="IsCollectible">是否收集物。</param>
    private readonly record struct ResourceFormValues(
        string AnimName,
        bool Loop,
        double Speed,
        int SortOrder,
        bool ShadowEnabled,
        bool IsCollectible);

    /// <summary>
    /// 读取表单里的可编辑字段。单条新增与批量新增只在这里解析一次，
    /// 差异较大的「层级」与「收集物」默认值由调用方按场景传入。
    /// </summary>
    /// <param name="defaultSortOrder">层级输入为空时的默认值。</param>
    /// <param name="defaultCollectible">收集物输入为空时的默认值。</param>
    /// <returns>归一后的表单值。</returns>
    // ---TODO 重构：表单解析与默认值统一收敛，单条 / 批量新增不再各抄一份。
    private ResourceFormValues ReadFormValues(int defaultSortOrder, bool defaultCollectible) => new(
        TemplateDefaults.AnimNameOrDefault(_fieldAnim.Text?.Trim()),
        TableTextFormat.ParseBool(_fieldLoop.Text, true),
        TableTextFormat.ParseNumber(_fieldSpeed.Text, 1),
        (int)TableTextFormat.ParseNumber(_fieldOrder.Text, defaultSortOrder),
        TableTextFormat.ParseBool(_fieldShadow.Text, true),
        TableTextFormat.ParseBool(_fieldCollectible.Text, defaultCollectible));

    /// <summary>把归一后的表单值写回模板。</summary>
    /// <param name="target">目标模板。</param>
    /// <param name="values">表单值。</param>
    private static void ApplyFormValues(ObjectTemplate target, ResourceFormValues values)
    {
        target.AnimName = values.AnimName;
        target.Loop = values.Loop;
        target.Speed = values.Speed;
        target.SortOrder = values.SortOrder;
        target.ShadowEnabled = values.ShadowEnabled;
        target.IsCollectible = values.IsCollectible;
    }

    /// <summary>把表单里可见的字段写回模板，用户手改优先于配置表原值。</summary>
    /// <param name="target">目标模板。</param>
    /// <param name="name">已校验过的资源名。</param>
    private void ApplyFormFields(ObjectTemplate target, string name)
    {
        target.TemplateName = name;
        target.ImagePath = Path.GetFileNameWithoutExtension(_fieldImagePath.Text?.Trim() ?? _selectedName) ?? name;
        ApplyFormValues(target, ReadFormValues(target.SortOrder, defaultCollectible: false));
    }

    private void AddToConfigTable()
    {
        if (_selectedName is null)
        {
            _notify("请先在左侧选中一个未配置资源，或在下方列表点「编辑」");
            return;
        }

        if (_editing is null && _selection.Count > 1)
        {
            AddSelectionToConfigTable();
            return;
        }

        var name = _fieldName.Text?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            _notify("Name 不能为空");
            return;
        }

        // ---TODO ID 管理：当前选中资源与上一次「编辑」对象不一致时按新增处理，避免把新资源写进旧行。
        if (_editing is not null
            && !TemplateIdCoordinator.MatchesResource(_editing, name, _selectedPath))
        {
            ExitEditMode();
        }

        var category = ResourceCategories.Classify(_selectedPath ?? _selectedName);
        var type = _editing?.Type ?? ResourceCategories.ObjectTypeOfKey(category);
        var isUpdate = _editing is not null;

        // ---TODO 资源整理：模板库里已有同名素材时复用该模板，只补配置表行。
        // 之前把「模板库已登记」当成「已配置」直接拒绝，导致 ObjectTemplates.json 登记过的图（收集物 cl_*）永远加不进表。
        var existingTemplate = isUpdate ? null : _idCoordinator.FindTemplate(name, _selectedPath);
        var reusesTemplate = existingTemplate is not null;
        var target = _editing ?? existingTemplate ?? new ObjectTemplate
        {
            Type = type,
            IsBuiltIn = false,
            TemplateId = string.Empty,
            ResName = name,
            InteractType = TemplateDefaults.InteractType(type),
            CollisionEnabled = TemplateDefaults.CollisionEnabled(type),
        };

        // 当前分类页优先落分类表；收集物这类「必须独立成表」的分类缺表时自动建表
        if (ResolveWritableTargetTable(category) is not { } targetTable)
        {
            return;
        }

        var preferredTable = targetTable.File;
        var createdTable = targetTable.Created;

        // 配置表行数据（同名的行）：先套未在表单里展示的列（预制体 / 碰撞 / 气泡 / 交通 / 传送门…）
        // ---TODO ID 管理：新增只按当前资源名查绑定行；更新才从编辑对象映射回原行，避免旧选中名把新行改写成旧行。
        var boundRow = FindBoundRow(isUpdate, name);
        if (boundRow is not null)
        {
            boundRow.ApplyToTemplate(target);
        }

        if (!isUpdate)
        {
            // ---TODO ID 管理：新增前只在确有重复时修复目标表；正常表不再被整体改号。
            RepairTableIdsIfNeeded(preferredTable ?? _itemTable?.Primary, type);
        }

        // 表单里可见的字段最后写入，用户的手改优先于配置表原值
        // ---TODO 资源整理：复用模板时不覆盖它的模板 ID，避免打乱已有引用。
        var templateId = _fieldId.Text?.Trim() ?? string.Empty;
        if (!reusesTemplate && TemplateIdAllocator.TryParse(templateId, out _))
        {
            target.TemplateId = templateId;
        }

        if (!isUpdate && string.IsNullOrWhiteSpace(target.TemplateId))
        {
            target.TemplateId = _idCoordinator.Allocate(type);
        }
        else if (!isUpdate && !reusesTemplate && _idCoordinator.IsTaken(target.TemplateId))
        {
            target.TemplateId = _idCoordinator.Allocate(type);
        }
        if (reusesTemplate)
        {
            // 复用模板：只保证资源名 / 图片名指向当前素材，表单值仅在字段为空时兜底。
            target.ResName = name;
            if (string.IsNullOrWhiteSpace(target.ImagePath))
            {
                target.ImagePath = name;
            }

            ApplyBatchFormDefaults(target, ReadFormValues(target.SortOrder, target.IsCollectible));
        }
        else
        {
            target.ResName = boundRow is not null && !string.IsNullOrWhiteSpace(boundRow.Icon) ? boundRow.Icon : name;
            ApplyFormFields(target, name);
        }

        if (!isUpdate && !reusesTemplate)
        {
            _templates.Add(target);
        }

        // 同步配置表：新增或更新同名行（新行落到当前分类那张表，和 Unity 一致）
        _itemTable?.Upsert(target, preferredTable);
        _tableDirty = _itemTable is not null;
        ExitEditMode();

        RefreshAfterTableChange();
        var reuseText = reusesTemplate ? $"（复用模板 {target.TemplateId}）" : string.Empty;
        _notify(isUpdate
            ? $"已修改 {target.TemplateId}（{name}），记得点「保存修改」落盘"
            : createdTable
                ? $"已创建 {preferredTable?.Name} 并添加 {target.TemplateId}（{name}）{reuseText}，记得点「保存修改」落盘"
                : $"已添加 {target.TemplateId}（{name}）到配置表{reuseText}，记得点「保存修改」落盘");
    }

    /// <summary>
    /// 多选批量添加：只允许同一资源分类，公共字段取当前表单，ID / Name / ImagePath 按每张图生成。
    /// 已在配置表中的图片直接跳过，避免批量操作覆盖现有行。
    /// </summary>
    private void AddSelectionToConfigTable()
    {
        if (_itemTable is null || _selection.Count == 0)
        {
            _notify("配置表尚未加载，无法批量添加");
            return;
        }

        var categories = _selection
            .Select(item => ResourceCategories.Classify(item.Path))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (categories.Count > 1)
        {
            _notify("多选批量添加只支持同一分类；请先切到对应分类再选图");
            return;
        }

        var category = categories[0];
        // ---TODO 资源整理：只跳过「配置表里已有行」的图；模板库登记过但没落表的图（如 cl_*）要能批量落表。
        var pending = _selection
            .Where(item => _itemTable.Find(item.Name) is null)
            .ToList();
        var skipped = _selection.Count - pending.Count;

        if (pending.Count == 0)
        {
            _notify($"选中的 {_selection.Count} 项都已在配置表中，无需重复添加");
            return;
        }

        if (ResolveWritableTargetTable(category) is not { } targetTable)
        {
            return;
        }

        var preferredTable = targetTable.File;
        var createdTable = targetTable.Created;
        var type = ResourceCategories.ObjectTypeOfKey(category);
        // ---TODO ID 管理：批量新增前只修复确有重复的目标表，循环内继续按最大 ID 递增。
        RepairTableIdsIfNeeded(preferredTable ?? _itemTable.Primary, type);
        var form = ReadFormValues(
            LayerOrder.Base(TemplateDefaults.LayerKey(type)),
            category == ResourceCategories.CollectibleKey);

        var reusedCount = 0;
        foreach (var item in pending)
        {
            var existing = _idCoordinator.FindTemplate(item.Name, item.Path);
            ObjectTemplate target;
            if (existing is not null)
            {
                // 复用已有模板：只补空字段，不覆盖动画/排序等既有配置，也不改它的模板 ID。
                target = existing;
                reusedCount++;
                target.ResName = string.IsNullOrWhiteSpace(target.ResName) ? item.Name : target.ResName;
                target.ImagePath = string.IsNullOrWhiteSpace(target.ImagePath) ? item.Name : target.ImagePath;
                if (string.IsNullOrWhiteSpace(target.TemplateName))
                {
                    target.TemplateName = item.Name;
                }

                if (string.IsNullOrWhiteSpace(target.TemplateId))
                {
                    target.TemplateId = _idCoordinator.Allocate(target.Type);
                }

                ApplyBatchFormDefaults(target, form);
            }
            else
            {
                target = new ObjectTemplate
                {
                    Type = type,
                    IsBuiltIn = false,
                    TemplateId = _idCoordinator.Allocate(type),
                    TemplateName = item.Name,
                    ResName = item.Name,
                    ImagePath = item.Name,
                    AnimName = form.AnimName,
                    Loop = form.Loop,
                    Speed = form.Speed,
                    SortOrder = form.SortOrder,
                    ShadowEnabled = form.ShadowEnabled,
                    IsCollectible = form.IsCollectible,
                    InteractType = TemplateDefaults.InteractType(type),
                    CollisionEnabled = TemplateDefaults.CollisionEnabled(type),
                };

                _templates.Add(target);
            }

            _itemTable.Upsert(target, preferredTable);
        }

        _tableDirty = true;
        ResetSelection();
        RefreshAfterTableChange();

        var tableText = createdTable ? $"已创建 {preferredTable?.Name} 并" : "已";
        var reuseText = reusedCount > 0 ? $"（其中 {reusedCount} 项复用已有模板）" : string.Empty;
        var skipText = skipped > 0 ? $"，跳过 {skipped} 项已在配置表" : string.Empty;
        _notify($"{tableText}批量添加 {pending.Count} 项到配置表{reuseText}{skipText}，记得点「保存修改」落盘");
    }

    /// <summary>批量新增复用模板时，只把表单值写进尚未设置的字段，避免把已有动画 / 排序配置刷掉。</summary>
    /// <param name="target">被复用的模板。</param>
    /// <param name="values">表单聚合值。</param>
    private static void ApplyBatchFormDefaults(ObjectTemplate target, ResourceFormValues values)
    {
        if (string.IsNullOrWhiteSpace(target.AnimName))
        {
            target.AnimName = values.AnimName;
        }

        if (!target.Loop)
        {
            target.Loop = values.Loop;
        }

        if (target.Speed <= 0)
        {
            target.Speed = values.Speed;
        }

        if (target.SortOrder == 0)
        {
            target.SortOrder = values.SortOrder;
        }

        target.IsCollectible = target.IsCollectible || values.IsCollectible;
    }

    /// <summary>点「编辑」：把已配置模板与配置表行回填到表单，进入修改模式。</summary>
    /// <param name="template">要编辑的模板。</param>
    private void SelectTemplateForEdit(ObjectTemplate template)
    {
        _editing = template;
        _selectedName = template.ImagePath;

        var path = _assets.Resolve(template.ImagePath) ?? template.ImagePath;
        _selectedPath = path;
        ShowPreview(path);

        FillFields(
            template.TemplateId,
            template.ImagePath,
            template.TemplateName,
            template.AnimName,
            template.Loop,
            template.Speed,
            template.SortOrder,
            template.ShadowEnabled,
            template.IsCollectible);

        var row = _itemTable?.FindByTemplate(template);
        _csvInfoText.Text = row is null
            ? "配置表里没有对应行，保存时会新增一行。"
            : row.DescribeBinding(_itemTable?.FileOf(row)?.Name);
        _csvInfoText.Foreground = row is null ? UiTheme.TextSecondary : UiTheme.AccentDark;

        SetAddButtonMode(editing: true);

        RefreshUnconfiguredList();
    }

    #endregion
}
