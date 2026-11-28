/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NamingGlossary.cs
 * author:    云毅
 * created:   2026
 * descrip:   物件中文命名词表：按图片名逐词翻译，编号名走人工确认表
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MapEditorTool.Services;

#region 建议结果

/// <summary>命名建议的可信度。</summary>
public enum NamingConfidence
{
    /// <summary>词表逐词命中，或人工确认表直取。</summary>
    High,

    /// <summary>词表缺词，建议名里保留了英文原词。</summary>
    Low,
}

/// <summary>一条命名建议。</summary>
/// <param name="Text">建议的中文名。</param>
/// <param name="Reason">依据（命中词表 / 人工确认 / 缺词）。</param>
/// <param name="Confidence">可信度。</param>
public readonly record struct NamingSuggestion(string Text, string Reason, NamingConfidence Confidence);

#endregion

#region 词表

/// <summary>
/// 中文命名词表。数据放在 <c>output/Config/NamingGlossary.json</c>，新增美术只需补词条，
/// 不重新编译：<c>tokens</c> 负责逐词翻译，<c>exact</c> 负责 <c>cl_15</c> 这类无语义编号名，
/// <c>tokensByDomain</c> 负责同名不同义的覆盖（角色表的 bread 是「面包师」，建筑表才是「面包店」）。
/// </summary>
public sealed class NamingGlossary
{
    #region 常量

    private const string FileName = "NamingGlossary.json";

    #endregion

    #region 字段

    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _domains = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>分域覆盖词：同一英文词在不同美术域里叫法不同（角色表的 bread 是面包师，不是面包店）。</summary>
    private readonly Dictionary<string, Dictionary<string, string>> _domainTokens =
        new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 构造

    private NamingGlossary()
    {
    }

    #endregion

    #region 加载

    /// <summary>从配置目录加载词表；文件缺失时返回空词表（建议一律低可信，不会写坏数据）。</summary>
    /// <param name="configDirectory">配置目录（<c>output/Config</c>）。</param>
    /// <returns>词表实例；永不返回 null。</returns>
    public static NamingGlossary Load(string? configDirectory)
    {
        var glossary = new NamingGlossary();
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return glossary;
        }

        var path = Path.Combine(configDirectory, FileName);
        if (!File.Exists(path))
        {
            return glossary;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            glossary.ReadMap(root, "tokens", glossary._tokens);
            glossary.ReadMap(root, "exact", glossary._exact);
            glossary.ReadMap(root, "domains", glossary._domains);
            glossary.ReadDomainTokens(root, "tokensByDomain");
        }
        catch (Exception)
        {
            // 词表是「建议」用的可选资源，读坏了就当没有，不影响编辑器其它功能
        }

        return glossary;
    }

    private void ReadMap(JsonElement root, string property, Dictionary<string, string> target)
    {
        if (!root.TryGetProperty(property, out var map) || map.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        FillMap(map, target);
    }

    /// <summary>读分域覆盖词：<c>{ "char": { "bread": "面包师" } }</c>。</summary>
    private void ReadDomainTokens(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var map) || map.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var domain in map.EnumerateObject())
        {
            if (domain.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            FillMap(domain.Value, target);
            if (target.Count > 0)
            {
                _domainTokens[domain.Name] = target;
            }
        }
    }

    private static void FillMap(JsonElement map, Dictionary<string, string> target)
    {
        foreach (var item in map.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.Value.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[item.Name] = value!;
            }
        }
    }

    #endregion

    #region 建议

    /// <summary>词表里是否有任何条目（没有就不要给用户「一键应用」的幻觉）。</summary>
    public bool IsEmpty => _tokens.Count == 0 && _exact.Count == 0 && _domainTokens.Count == 0;

    /// <summary>
    /// 按图片名生成中文名：<c>building_bread_1</c> → 「面包店 1」。
    /// 先剥末尾的实例坐标（含负坐标），再按 <c>域_主体_序号</c> 逐词翻译。
    /// </summary>
    /// <param name="icon">图片名（可带或不带 .png）。</param>
    /// <returns>建议；无法给出主体时 Text 为空。</returns>
    public NamingSuggestion Suggest(string? icon)
    {
        var stem = Normalize(icon);
        if (stem.Length == 0)
        {
            return new NamingSuggestion(string.Empty, "无图片名", NamingConfidence.Low);
        }

        if (_exact.TryGetValue(stem, out var exact))
        {
            return new NamingSuggestion(exact, "人工确认", NamingConfidence.High);
        }

        var baseName = StripInstanceSuffix(stem);
        var parts = baseName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new NamingSuggestion(string.Empty, "无图片名", NamingConfidence.Low);
        }

        var domain = _domains.TryGetValue(parts[0], out var domainText) ? domainText : string.Empty;
        var overrides = _domainTokens.TryGetValue(parts[0], out var scoped) ? scoped : null;
        var words = new List<string>();
        var misses = new List<string>();
        foreach (var part in parts.Skip(1))
        {
            if (IsNumber(part))
            {
                continue;
            }

            if (overrides is not null && overrides.TryGetValue(part, out var scopedWord))
            {
                words.Add(scopedWord);
            }
            else if (_tokens.TryGetValue(part, out var word))
            {
                words.Add(word);
            }
            else
            {
                misses.Add(part);
            }
        }

        var body = string.Concat(words);
        if (body.Length == 0 && misses.Count == 0)
        {
            body = domain;
        }

        var index = TrailingNumber(baseName);
        var text = string.IsNullOrEmpty(index) ? body : $"{body} {index}";
        if (misses.Count > 0)
        {
            return new NamingSuggestion(text.Trim(), "词表缺词：" + string.Join("、", misses), NamingConfidence.Low);
        }

        return new NamingSuggestion(text.Trim(), "词表命中", NamingConfidence.High);
    }

    #endregion

    #region 文本处理

    private static string Normalize(string? icon)
    {
        var text = (icon ?? string.Empty).Trim();
        if (text.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^4];
        }

        return text.ToLowerInvariant();
    }

    /// <summary>
    /// 剥掉末尾的实例坐标，如 <c>_746_2602</c>、<c>_-60_88</c>。
    /// 只有「最后两段都是数字」才剥，避免把 <c>prop_lamp_2</c> 的版本号当坐标吃掉。
    /// </summary>
    private static string StripInstanceSuffix(string stem)
    {
        var last = stem.LastIndexOf('_');
        if (last <= 0)
        {
            return stem;
        }

        var second = stem.LastIndexOf('_', last - 1);
        if (second <= 0)
        {
            return stem;
        }

        if (!IsNumber(stem[(last + 1)..]) || !IsNumber(stem[(second + 1)..last]))
        {
            return stem;
        }

        return stem[..second];
    }

    /// <summary>图片名末尾的序号（表内区分同主体的多版本美术）。</summary>
    private static string TrailingNumber(string stem)
    {
        var cut = stem.LastIndexOf('_');
        if (cut < 0)
        {
            return string.Empty;
        }

        var piece = stem[(cut + 1)..];
        return IsNumber(piece) ? piece : string.Empty;
    }

    private static bool IsNumber(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        var start = text[0] == '-' ? 1 : 0;
        if (start >= text.Length)
        {
            return false;
        }

        for (var index = start; index < text.Length; index++)
        {
            if (!char.IsDigit(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    #endregion
}

#endregion
