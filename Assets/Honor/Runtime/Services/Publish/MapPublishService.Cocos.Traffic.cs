/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.Cocos.Traffic.cs
 * author:    云毅
 * created:   2026
 * descrip:   Cocos 路径与交通子表：路径点数组、红绿灯 / 横道 / 区域 / 格子
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
/// <summary>Cocos 路径与交通子表输出。</summary>
public static partial class MapPublishService
{
    #region Cocos 路径与交通子表

    /// <summary>
    /// 构建路径子表：每个带路径实例输出一条 <c>PathId → 路径点数组</c>，
    /// 路径点使用格坐标，运行时无需再按像素换算。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="characters">当前参与路径导出的实例（角色 / 载具等）。</param>
    /// <param name="gridSize">格尺寸（像素）。</param>
    /// <returns>路径表 Lua 文本。</returns>
    private static string BuildCocosPathsFile(string baseName, IReadOnlyList<PlacedObject> characters, int gridSize)
    {
        var sb = new StringBuilder();
        var tableName = SubTableName(baseName, PathsSuffix);
        AppendLuaHeader(sb, tableName, "路径数据（自动生成）", "所有带路径物件的路径点，按 PathId 索引",
            "路径点格式：{ gridX, gridY } 纯数组");
        sb.AppendLine($"Tables.{tableName} = {{");

        foreach (var item in characters)
        {
            if (item.PathPoints.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"    [\"path_{Escape(item.Id)}\"] = {{");
            foreach (var point in item.PathPoints)
            {
                // 与 Unity Lua 导出器一致：按编辑器格尺寸向下取整，而不是直接使用 PathPoint 自带的 GridX。
                var gridX = (int)Math.Floor(point.X / Math.Max(1, gridSize));
                var gridY = (int)Math.Floor(point.Y / Math.Max(1, gridSize));
                sb.AppendLine($"        {{{gridX}, {gridY}}},");
            }

            sb.AppendLine("    },");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>输出一个命名列表段，统一处理段头、序号/元素回调与结尾逗号。</summary>
    /// <typeparam name="T">列表元素类型。</typeparam>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="sectionName">Lua 字段名。</param>
    /// <param name="items">列表元素。</param>
    /// <param name="appendItem">元素输出回调，参数为缓冲、元素、从 0 开始的序号。</param>
    private static void AppendLuaListSection<T>(
        StringBuilder sb,
        string sectionName,
        IReadOnlyList<T> items,
        Action<StringBuilder, T, int> appendItem)
    {
        sb.AppendLine($"    {sectionName} = {{");
        for (var index = 0; index < items.Count; index++)
        {
            appendItem(sb, items[index], index);
        }

        sb.AppendLine("    },");
    }

    /// <summary>
    /// 构建交通子表：红绿灯、横道、人行道 / 道路格子与车辆绑定关系统一从地图配置导出。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="config">当前地图配置。</param>
    /// <returns>交通表 Lua 文本。</returns>
    private static string BuildCocosTrafficFile(string baseName, MapConfigData config)
    {
        var sb = new StringBuilder();
        var tableName = SubTableName(baseName, TrafficSuffix);
        AppendLuaHeader(sb, tableName, "交通规则数据（自动生成）", "红绿灯 / 斑马线 / 区域 / 人行道格 / 道路格");
        sb.AppendLine($"Tables.{tableName} = {{");

        AppendLuaListSection(sb, "Lights", config.TrafficLights, (target, light, index) =>
        {
            target.AppendLine($"        [{index + 1}] = {{");
            target.AppendLine($"            Uid                = \"{Escape(light.Id)}\",");
            target.AppendLine($"            TemplateId         = \"{Escape(light.TemplateId)}\",");
            target.AppendLine($"            Name               = \"{Escape(light.Name)}\",");
            target.AppendLine($"            GridX              = {light.GridX},");
            target.AppendLine($"            GridY              = {light.GridY},");
            target.AppendLine($"            PosX               = {Num(light.PosX)},");
            target.AppendLine($"            PosY               = {Num(light.PosY)},");
            target.AppendLine($"            Direction          = \"{Escape(light.Direction)}\",");
            target.AppendLine($"            Side               = \"{Escape(light.Side)}\",");
            target.AppendLine($"            IntersectionId     = \"{Escape(light.IntersectionId)}\",");
            target.AppendLine($"            RedDuration        = {Num(light.RedDuration)},");
            target.AppendLine($"            GreenDuration      = {Num(light.GreenDuration)},");
            target.AppendLine($"            YellowDuration     = {Num(light.YellowDuration)},");
            target.AppendLine($"            OffsetPhase        = {Num(light.OffsetPhase)},");
            target.AppendLine($"            BoundCrosswalkId   = \"{Escape(light.BoundCrosswalkId)}\",");
            target.AppendLine($"            InitialPhase       = \"{Escape(light.InitialPhase)}\",");
            target.AppendLine($"            Image              = \"{Escape(light.Image)}\",");
            target.AppendLine($"            Anim               = \"{Escape(light.Anim)}\",");
            target.AppendLine("        },");
        });

        AppendLuaListSection(sb, "Crosswalks", config.Crosswalks, (target, crosswalk, index) =>
        {
            target.AppendLine($"        [{index + 1}] = {{");
            target.AppendLine($"            Uid           = \"{Escape(crosswalk.Id)}\",");
            target.AppendLine($"            Name          = \"{Escape(crosswalk.Name)}\",");
            target.AppendLine($"            PointA        = {{ X = {Num(crosswalk.PointA.X)}, Y = {Num(crosswalk.PointA.Y)} }},");
            target.AppendLine($"            PointB        = {{ X = {Num(crosswalk.PointB.X)}, Y = {Num(crosswalk.PointB.Y)} }},");
            target.AppendLine($"            Width         = {Num(crosswalk.Width)},");
            target.AppendLine($"            Direction     = \"{Escape(crosswalk.Direction)}\",");
            target.AppendLine($"            BoundLightId  = \"{Escape(crosswalk.BoundLightId)}\",");
            target.AppendLine("        },");
        });

        AppendLuaListSection(sb, "Regions", config.Regions, (target, region, index) =>
        {
            target.AppendLine($"        [{index + 1}] = {{");
            target.AppendLine($"            Uid     = \"{Escape(region.Id)}\",");
            target.AppendLine($"            Type    = \"{Escape(region.Type)}\",");
            target.AppendLine($"            X       = {Num(region.X)},");
            target.AppendLine($"            Y       = {Num(region.Y)},");
            target.AppendLine($"            Width   = {Num(region.Width)},");
            target.AppendLine($"            Height  = {Num(region.Height)},");
            target.AppendLine("        },");
        });

        AppendLuaListSection(
            sb,
            "SidewalkCells",
            config.SidewalkCells,
            (target, cell, _) => target.AppendLine($"        {{{cell.X}, {cell.Y}}},"));

        AppendLuaListSection(
            sb,
            "RoadCells",
            config.RoadCells,
            (target, cell, _) => target.AppendLine($"        {{{cell.X}, {cell.Y}}},"));

        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion
}

