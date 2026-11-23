/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PsdSliceBaker.Manifest.cs
 * author:    云毅
 * created:   2026
 * descrip:   切片清单：PSD 源文件戳、slices.json 读取与写回
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
public static partial class PsdSliceBaker
{
    #region PSD 源文件清单

    public static bool TryReadSourceStamp(
        string levelDirectory,
        out string sourcePath,
        out long length,
        out long ticks)
    {
        sourcePath = string.Empty;
        length = 0;
        ticks = 0;

        if (string.IsNullOrWhiteSpace(levelDirectory))
        {
            return false;
        }

        var manifest = TryReadAnyManifest(Path.Combine(levelDirectory, ManifestFileName));
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Source))
        {
            return false;
        }

        sourcePath = manifest.Source;
        length = manifest.Length;
        ticks = manifest.Ticks;
        return true;
    }

    /// <summary>读旧清单（不看版本、不看 PSD 是否变更），只用于清理残留。</summary>
    private static PsdSliceManifest? TryReadAnyManifest(string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PsdSliceManifest>(File.ReadAllText(manifestPath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static PsdSliceManifest? TryReadManifest(string manifestPath, string psdPath)
    {
        var manifest = TryReadAnyManifest(manifestPath);
        if (manifest is null || manifest.Slices.Count == 0 || manifest.Version != ManifestVersion)
        {
            return null;
        }

        var info = new FileInfo(psdPath);
        return string.Equals(manifest.Source, psdPath, StringComparison.OrdinalIgnoreCase)
            && manifest.Length == info.Length
            && manifest.Ticks == File.GetLastWriteTimeUtc(psdPath).Ticks
            ? manifest
            : null;
    }

    private static void WriteManifest(string manifestPath, string psdPath, List<PsdSliceRecord> records)
    {
        var info = new FileInfo(psdPath);
        var manifest = new PsdSliceManifest(psdPath, info.Length, File.GetLastWriteTimeUtc(psdPath).Ticks, records, ManifestVersion);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), new UTF8Encoding(false));
    }


    #endregion
}
