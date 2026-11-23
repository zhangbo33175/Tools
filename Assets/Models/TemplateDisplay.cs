/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateDisplay.cs
 * author:    云毅
 * created:   2026
 * descrip:   元件编辑所需的显示名 ⇄ 编码互转目录
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace MapEditorTool.Models;

#region 元件枚举显示名

/// <summary>物体类型的中文名口径，列表、下拉与提示共用同一入口。</summary>
public static class ObjectTypeNames
{
    /// <summary>建筑。</summary>
    public const string Building = "建筑";

    /// <summary>人物。</summary>
    public const string Character = "人物";

    /// <summary>物件。</summary>
    public const string Prop = "物件";

    /// <summary>元件类型下拉的可选项（与 Unity 版 EditableTypeChoices 同序）。</summary>
    public static readonly IReadOnlyList<string> EditableChoices = new[] { Building, Character, Prop };

    /// <summary>类型转中文显示名。</summary>
    /// <param name="type">物体类型。</param>
    /// <returns>中文显示名。</param>
    public static string Of(MapObjectType type) => type switch
    {
        MapObjectType.Building => Building,
        MapObjectType.Character => Character,
        _ => Prop,
    };

    /// <summary>中文显示名转类型，未知值回退物件。</summary>
    /// <param name="displayName">中文显示名。</param>
    /// <returns>物体类型。</returns>
    public static MapObjectType ToType(string displayName)
    {
        if (displayName == Building)
        {
            return MapObjectType.Building;
        }

        if (displayName == Character)
        {
            return MapObjectType.Character;
        }

        return MapObjectType.Prop;
    }
}

/// <summary>
/// 模板交互类型目录。模板侧沿用 Unity 配置表的 code 口径（none/click/talk/enter/pickup），
/// 右侧场景实例仍使用 <see cref="InteractTypes"/> 的 code，落位时按 <see cref="ToInstanceCode"/> 归一。
/// </summary>
public static class TemplateInteractTypes
{
    /// <summary>不交互。</summary>
    public const string None = "none";

    /// <summary>点击。</summary>
    public const string Click = "click";

    /// <summary>对话。</summary>
    public const string Talk = "talk";

    /// <summary>进入。</summary>
    public const string Enter = "enter";

    /// <summary>收集。</summary>
    public const string Pickup = "pickup";

    /// <summary>code → 中文显示名。</summary>
    private static readonly Dictionary<string, string> DisplayMap = new()
    {
        [None] = "无",
        [Click] = "点击",
        [Talk] = "对话",
        [Enter] = "进入",
        [Pickup] = "收集",
    };

    /// <summary>下拉展示顺序（中文显示名）。</summary>
    public static readonly IReadOnlyList<string> DisplayChoices =
        new[] { "无", "点击", "对话", "进入", "收集" };

    /// <summary>code → 中文显示名，未知回退「无」。</summary>
    /// <param name="code">交互类型编码。</param>
    /// <returns>中文显示名。</param>
    public static string ToDisplay(string code) => DisplayMap.TryGetValue(code, out var name) ? name : DisplayMap[None];

    /// <summary>中文显示名 → code，未知回退 none。</summary>
    /// <param name="displayName">中文显示名。</param>
    /// <returns>交互类型编码。</returns>
    public static string ToCode(string displayName)
    {
        foreach (var pair in DisplayMap)
        {
            if (pair.Value == displayName)
            {
                return pair.Key;
            }
        }

        return DisplayMap.ContainsKey(displayName) ? displayName : None;
    }

    /// <summary>模板交互码归一为实例交互码：talk 落为场景实例的 dialog 口径。</summary>
    /// <param name="templateCode">模板交互码（也兼容历史数据里的实例码）。</param>
    /// <returns>场景实例交互码。</returns>
    public static string ToInstanceCode(string templateCode) => templateCode switch
    {
        Talk => InteractTypes.Dialog,
        InteractTypes.Dialog => InteractTypes.Dialog,
        None => InteractTypes.None,
        Click => InteractTypes.Click,
        Enter => InteractTypes.Enter,
        Pickup => InteractTypes.Pickup,
        _ => InteractTypes.None,
    };

    /// <summary>把实例码为主的旧数据归一为模板码：dialog → talk，view → click。</summary>
    /// <param name="code">任意交互码。</param>
    /// <returns>模板交互码。</returns>
    public static string ToTemplateCode(string code) => code switch
    {
        InteractTypes.Dialog => Talk,
        InteractTypes.View => Click,
        Talk or Click or Enter or Pickup or None => code,
        _ => None,
    };
}

/// <summary>模板交通类别目录，键与 Unity 版 TemplateTrafficCategory 一致。</summary>
public static class TemplateTrafficCategories
{
    /// <summary>不参与。</summary>
    public const string None = "none";

    /// <summary>路灯。</summary>
    public const string Streetlight = "streetlight";

    /// <summary>汽车 / 载具。</summary>
    public const string Vehicle = "vehicle";

    /// <summary>人物 / 行人。</summary>
    public const string Pedestrian = "pedestrian";

    /// <summary>马路。</summary>
    public const string Road = "road";

    /// <summary>斑马线。</summary>
    public const string Crosswalk = "crosswalk";

    /// <summary>code → 中文显示名。</summary>
    private static readonly Dictionary<string, string> DisplayMap = new()
    {
        [None] = "不参与",
        [Streetlight] = "路灯",
        [Vehicle] = "汽车",
        [Pedestrian] = "人物",
        [Road] = "马路",
        [Crosswalk] = "斑马线",
    };

    /// <summary>下拉展示顺序（中文显示名）。</summary>
    public static readonly IReadOnlyList<string> DisplayChoices =
        new[] { "不参与", "路灯", "汽车", "人物", "马路", "斑马线" };

    /// <summary>code → 中文显示名，未知回退「不参与」。</summary>
    /// <param name="code">类别键。</param>
    /// <returns>中文显示名。</param>
    public static string ToDisplay(string code) => DisplayMap.TryGetValue(code, out var name) ? name : DisplayMap[None];

    /// <summary>中文显示名 → code，未知回退 none。</summary>
    /// <param name="displayName">中文显示名。</param>
    /// <returns>类别键。</returns>
    public static string ToCode(string displayName)
    {
        foreach (var pair in DisplayMap)
        {
            if (pair.Value == displayName)
            {
                return pair.Key;
            }
        }

        return DisplayMap.ContainsKey(displayName) ? displayName : None;
    }
}

/// <summary>模板朝向显示名目录，code 口径与 <see cref="Facings"/> 一致。</summary>
public static class FacingNames
{
    /// <summary>code → 中文显示名。</summary>
    private static readonly Dictionary<string, string> DisplayMap = new()
    {
        [Facings.Up] = "上",
        [Facings.Down] = "下",
        [Facings.Left] = "左",
        [Facings.Right] = "右",
    };

    /// <summary>下拉展示顺序（中文显示名）。</summary>
    public static readonly IReadOnlyList<string> DisplayChoices = new[] { "上", "下", "左", "右" };

    /// <summary>code → 中文显示名，未知回退「下」。</summary>
    /// <param name="code">朝向编码。</param>
    /// <returns>中文显示名。</returns>
    public static string ToDisplay(string code) => DisplayMap.TryGetValue(code, out var name) ? name : DisplayMap[Facings.Down];

    /// <summary>中文显示名 → code，未知回退 down。</summary>
    /// <param name="displayName">中文显示名。</param>
    /// <returns>朝向编码。</returns>
    public static string ToCode(string displayName)
    {
        foreach (var pair in DisplayMap)
        {
            if (pair.Value == displayName)
            {
                return pair.Key;
            }
        }

        return DisplayMap.ContainsKey(displayName) ? displayName : Facings.Down;
    }
}

/// <summary>层级档显示名目录：只要八层显示名，不提供「跟随类型」伪选项。</summary>
public static class TemplateLayers
{
    /// <summary>跟随类型归属层。</summary>
    public const string FollowType = "跟随类型";

    /// <summary>八层显示名（顺序即下拉顺序）。</summary>
    public static readonly IReadOnlyList<string> DisplayChoices = new[]
    {
        MapLayerKeys.DisplayName(MapLayerKeys.Background),
        MapLayerKeys.DisplayName(MapLayerKeys.Greenery),
        MapLayerKeys.DisplayName(MapLayerKeys.Road),
        MapLayerKeys.DisplayName(MapLayerKeys.Building),
        MapLayerKeys.DisplayName(MapLayerKeys.Prop),
        MapLayerKeys.DisplayName(MapLayerKeys.Character),
        MapLayerKeys.DisplayName(MapLayerKeys.Foreground),
        MapLayerKeys.DisplayName(MapLayerKeys.Bubble),
    };

    /// <summary>八层键（与 <see cref="DisplayChoices"/> 同序）。</summary>
    public static readonly IReadOnlyList<string> Keys = new[]
    {
        MapLayerKeys.Background,
        MapLayerKeys.Greenery,
        MapLayerKeys.Road,
        MapLayerKeys.Building,
        MapLayerKeys.Prop,
        MapLayerKeys.Character,
        MapLayerKeys.Foreground,
        MapLayerKeys.Bubble,
    };

    /// <summary>显示层键 → 下拉显示名；空串表示跟随类型。</summary>
    /// <param name="layerKey">显示层键。</param>
    /// <returns>下拉显示名。</returns>
    public static string ToDisplay(string layerKey)
    {
        if (string.IsNullOrWhiteSpace(layerKey))
        {
            return FollowType;
        }

        return MapLayerKeys.DisplayName(layerKey);
    }

    /// <summary>下拉显示名 → 显示层键；跟随类型落空串。</summary>
    /// <param name="displayName">下拉显示名。</param>
    /// <returns>显示层键，空串表示跟随类型。</returns>
    public static string ToKey(string displayName)
    {
        if (displayName == FollowType)
        {
            return string.Empty;
        }

        foreach (var key in Keys)
        {
            if (MapLayerKeys.DisplayName(key) == displayName)
            {
                return key;
            }
        }

        return string.Empty;
    }

    /// <summary>当前文件 / 元素真正落在哪一层：手选过显示层用它，否则用归属层；都没有时回退「跟随类型」。</summary>
    /// <param name="displayLayer">显示层键（可能是空串）。</param>
    /// <param name="layerTypeKey">归属层键（可能是空串）。</param>
    /// <returns>层级档显示名，一定是具体一层（除非两者都空）。</returns>
    public static string ResolveDisplay(string? displayLayer, string? layerTypeKey)
    {
        var key = string.IsNullOrWhiteSpace(displayLayer) ? layerTypeKey : displayLayer;
        return string.IsNullOrWhiteSpace(key) ? FollowType : MapLayerKeys.DisplayName(key);
    }

    /// <summary>下拉选中的层写回显示层键：选的就是归属层时存空串（跟随类型），其余存具体层。</summary>
    /// <param name="displayName">下拉显示名。</param>
    /// <param name="layerTypeKey">归属层键。</param>
    /// <returns>要写入 DisplayLayer 的键。</returns>
    public static string ToKeyOrFollow(string displayName, string? layerTypeKey)
    {
        var key = ToKey(displayName);
        return string.Equals(key, layerTypeKey, StringComparison.OrdinalIgnoreCase) ? string.Empty : key;
    }

    /// <summary>层级档下拉的选项（只要具体八层，不再混「跟随类型」伪选项）。</summary>
    /// <returns>八层显示名。</returns>
    public static IReadOnlyList<string> ChoiceList => DisplayChoices;
}

#endregion
