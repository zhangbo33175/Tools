/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Program.cs
 * author:    云毅
 * created:   2026
 * descrip:   程序入口与无界面子命令（PSD 校验 / 配置导出 / 地图打开）
 ***************************************************************/

using Avalonia;
using System;
using System.IO;
using MapEditorTool.Services;

namespace MapEditorTool;

/// <summary>
/// 程序入口。支持无界面子命令：
/// <c>--psd &lt;path&gt; [--out &lt;file&gt;]</c> 校验 PSD 解析；
/// <c>--export-config &lt;csv&gt;</c> 把元件配置导出为配置表；
/// <c>--import-config &lt;csv&gt;</c> 从配置表导入并写回工程 JSON。
/// </summary>
class Program
{
    /// <summary>启动编辑器，或在命令行下执行无界面子命令。</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--psd")
        {
            DumpPsd(args[1], args.Length >= 4 && args[2] == "--out" ? args[3] : null);
            return;
        }

        if (args.Length >= 2 && args[0] == "--export-config")
        {
            ExportConfig(args[1]);
            return;
        }

        if (args.Length >= 2 && args[0] == "--import-config")
        {
            ImportConfig(args[1]);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>导出元件配置表。</summary>
    /// <param name="target">目标 CSV 路径。</param>
    private static void ExportConfig(string target)
    {
        var store = new TemplateStore(DefaultConfigDirectory());
        var templates = store.LoadOrCreate(TemplateCatalog.CreateBuiltIn);
        if (store.TryExportCsv(target, templates, out var error))
        {
            Console.WriteLine($"EXPORT_OK count={templates.Count} path={target}");
        }
        else
        {
            Console.WriteLine($"EXPORT_ERROR {error}");
        }
    }

    /// <summary>导入元件配置表并写回工程 JSON。</summary>
    /// <param name="source">源 CSV 路径。</param>
    private static void ImportConfig(string source)
    {
        var store = new TemplateStore(DefaultConfigDirectory());
        var templates = store.ImportCsv(source, out var error);
        if (templates.Count == 0)
        {
            Console.WriteLine($"IMPORT_ERROR {error}");
            return;
        }

        if (!store.TrySaveJson(templates, out var saveError))
        {
            Console.WriteLine($"IMPORT_SAVE_ERROR {saveError}");
            return;
        }

        Console.WriteLine($"IMPORT_OK count={templates.Count} json={store.JsonPath}");
    }

    private static string DefaultConfigDirectory()
        => Path.Combine(AppContext.BaseDirectory, "output", "Config");

    /// <summary>解析 PSD 并输出图层清单。</summary>
    private static void DumpPsd(string path, string? outFile)
    {
        try
        {
            var document = PsdReader.Read(path);
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"size={document.Width}x{document.Height} depth={document.Depth} channels={document.Channels} mode={document.ColorMode}");
            foreach (var layer in document.Layers)
            {
                lines.AppendLine(
                    $"[{layer.Index}] depth={layer.Depth} group={layer.GroupPath} " +
                    $"name={layer.Name} rect={layer.Left},{layer.Top},{layer.Width}x{layer.Height} " +
                    $"visible={layer.Visible} opacity={layer.Opacity:F2} blend={layer.BlendMode} isGroup={layer.IsGroup}");
            }

            var text = lines.ToString();
            Console.WriteLine(text);
            if (outFile is not null)
            {
                System.IO.File.WriteAllText(outFile, text);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"PSD_ERROR {exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>Avalonia 配置，供预览器与设计器复用。</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
