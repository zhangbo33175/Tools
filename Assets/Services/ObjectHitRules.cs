/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectHitRules.cs
 * author:    云毅
 * created:   2026
 * descrip:   统一的物件命中规则：选中看包围盒，路径加点额外看透明像素与遮挡带
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 枚举

/// <summary>物件命中模式。不同操作共用同一套几何判定，只在「是否要求可见像素」上分叉。</summary>
public enum ObjectHitKind
{
    /// <summary>普通选中 / 拖动：沿用包围盒口径，透明留白也能选中大物件。</summary>
    Selection,

    /// <summary>路径加点：包围盒内还必须落在真实不透明像素上，避免透明留白吃掉落点。</summary>
    PathPoint,
}

#endregion

#region 路径落点结果

/// <summary>路径加点的命中结论。只描述「被谁挡住」，不关心状态栏文案由谁展示。</summary>
/// <param name="BlockingObject">阻挡物件；为空表示落点有效。</param>
public readonly record struct PathPointHitDecision(PlacedObject? BlockingObject)
{
    /// <summary>落点是否可以作为路径点。</summary>
    public bool Accepted => BlockingObject is null;

    /// <summary>生成路径拒绝提示，供调用方直接交给 UI 状态栏。</summary>
    /// <returns>拒绝原因；落点有效时返回空字符串。</returns>
    public string BuildRejectionMessage()
    {
        if (BlockingObject is null)
        {
            return string.Empty;
        }

        return $"路径绘制中：点击位置被「{BlockingObject.Name}」占用，已忽略；请点地面 / 空白处加点，或先停止绘制再选其他物件。";
    }
}

#endregion

#region 命中规则

/// <summary>
/// 物件命中的统一入口。画布只负责按绘制顺序遍历，具体「什么算命中」全部收敛到这里，
/// 避免选中、路径加点、以后可能的摆放预览各自维护一套矩形 / 透明像素判断。
/// </summary>
public static class ObjectHitRules
{
    /// <summary>路径加点允许的最低 alpha。抗锯齿边缘的 1–7 视为透明，避免贴图毛边挡住落点。</summary>
    public const byte PathPointOpaqueAlphaThreshold = 8;

    /// <summary>
    /// 判断一个物件在指定世界坐标上是否命中。
    /// </summary>
    /// <param name="item">候选物件。</param>
    /// <param name="world">世界坐标。</param>
    /// <param name="kind">命中模式。</param>
    /// <param name="spriteProvider">贴图提供者；仅 <see cref="ObjectHitKind.PathPoint"/> 且包围盒命中后才会调用。</param>
    /// <returns>命中返回 true。</returns>
    public static bool Matches(
        PlacedObject item,
        Point world,
        ObjectHitKind kind,
        Func<PlacedObject, Bitmap?>? spriteProvider = null)
    {
        if (kind == ObjectHitKind.PathPoint && spriteProvider is not null)
        {
            // 路径模式一次换算同时得到「是否落在包围盒」与「局部归一化坐标」，不再重复做逆旋转。
            if (!item.TryGetNormalizedPoint(world, out var normalizedX, out var normalizedY))
            {
                return false;
            }

            return SpriteAlphaProbe.IsOpaque(
                spriteProvider(item),
                normalizedX,
                normalizedY,
                PathPointOpaqueAlphaThreshold);
        }

        return item.ContainsPoint(world);
    }

    /// <summary>
    /// 把路径命中结果翻译成落点结论：空白或地表带可加点，立体 / 叠加带必须拒绝。
    /// </summary>
    /// <param name="hit">路径模式命中的最上层可见物件；空白为 null。</param>
    /// <returns>路径落点结论。</returns>
    public static PathPointHitDecision EvaluatePathPoint(PlacedObject? hit)
    {
        if (hit is null || LayerBands.Of(hit) == LayerBands.Ground)
        {
            return new PathPointHitDecision(null);
        }

        return new PathPointHitDecision(hit);
    }
}

#endregion
