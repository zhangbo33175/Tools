/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpineRenderer.cs
 * author:    云毅
 * created:   2026
 * descrip:   把 Spine 骨架的 region / mesh 附件绘制到 Avalonia 画布
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Media;
using Spine;

namespace MapEditorTool.Services;

#region 渲染器

/// <summary>
/// Spine 轻量渲染器。只处理预览需要的 region 与 mesh 附件；
/// 裁剪、蒙版、路径约束等高级附件不绘制，但不会影响骨骼变换与动画推进。
/// </summary>
public static class SpineRenderer
{
    /// <summary>把当前骨架姿态绘制到目标矩形。</summary>
    /// <param name="context">Avalonia 绘制上下文。</param>
    /// <param name="player">播放器。</param>
    /// <param name="asset">资产。</param>
    /// <param name="destination">目标矩形（世界或屏幕坐标）。</param>
    /// <param name="rotation">绕目标中心的旋转角（度）。</param>
    /// <param name="flipX">是否水平翻转。</param>
    public static void Draw(
        DrawingContext context,
        SpinePlayer player,
        SpineAnimationAsset asset,
        Rect destination,
        double rotation,
        bool flipX)
    {
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        var bounds = asset.SetupBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(destination.Width / bounds.Width, destination.Height / bounds.Height);
        var offsetX = destination.X + (destination.Width - bounds.Width * scale) / 2 - bounds.X * scale;
        var top = destination.Y + (destination.Height - bounds.Height * scale) / 2;
        var bottom = top + bounds.Bottom * scale;
        var centerX = destination.X + destination.Width / 2;
        var centerY = destination.Y + destination.Height / 2;

        var skeleton = player.Skeleton;
        var drawOrder = skeleton.DrawOrder;
        for (var index = 0; index < drawOrder.Count; index++)
        {
            var slot = drawOrder.Items[index];
            var attachment = slot.Attachment;
            if (attachment == null)
            {
                continue;
            }

            var alpha = Math.Clamp(slot.A, 0f, 1f);
            if (alpha <= 0.001f)
            {
                continue;
            }

            if (attachment is RegionAttachment regionAttachment)
            {
                DrawRegion(context, asset, slot, regionAttachment, offsetX, bottom, centerX, centerY, scale, rotation, flipX, alpha);
            }
            else if (attachment is MeshAttachment meshAttachment)
            {
                DrawMesh(context, asset, slot, meshAttachment, offsetX, bottom, centerX, centerY, scale, rotation, flipX, alpha);
            }
        }
    }

    /// <summary>绘制 region 附件：裁切位图 + 四边形裁剪。</summary>
    private static void DrawRegion(
        DrawingContext context,
        SpineAnimationAsset asset,
        Slot slot,
        RegionAttachment attachment,
        double offsetX,
        double bottom,
        double centerX,
        double centerY,
        double scale,
        double rotation,
        bool flipX,
        float alpha)
    {
        var region = attachment.Region as AtlasRegion;
        if (region == null)
        {
            return;
        }

        var page = asset.PageOf(region);
        if (page == null)
        {
            return;
        }

        var world = new float[8];
        attachment.ComputeWorldVertices(slot, world, 0, 2);
        var points = ProjectQuad(world, offsetX, bottom, centerX, centerY, scale, rotation, flipX);

        // 直接用整页贴图 + UV 映射：Atlas 的 90° 打包旋转已经体现在 UV 里，
        // 不再依赖 region 裁切图，避免旋转页被裁成错误尺寸。
        var pageWidth = page.Bitmap.PixelSize.Width;
        var pageHeight = page.Bitmap.PixelSize.Height;
        var source = new[]
        {
            new Point(
                attachment.UVs[RegionAttachment.BLX] * pageWidth,
                attachment.UVs[RegionAttachment.BLY] * pageHeight),
            new Point(
                attachment.UVs[RegionAttachment.ULX] * pageWidth,
                attachment.UVs[RegionAttachment.ULY] * pageHeight),
            new Point(
                attachment.UVs[RegionAttachment.URX] * pageWidth,
                attachment.UVs[RegionAttachment.URY] * pageHeight),
        };

        var matrix = CreateAffine(
            source[0],
            source[1],
            source[2],
            points[0],
            points[1],
            points[2]);

        var clip = CreateQuad(points);
        using (context.PushOpacity(alpha))
        using (context.PushGeometryClip(clip))
        using (context.PushTransform(matrix))
        {
            context.DrawImage(
                page.Bitmap,
                new Rect(0, 0, pageWidth, pageHeight),
                new Rect(0, 0, pageWidth, pageHeight));
        }
    }

    /// <summary>绘制 mesh 附件：逐三角形仿射映射 + 三角形裁剪。</summary>
    private static void DrawMesh(
        DrawingContext context,
        SpineAnimationAsset asset,
        Slot slot,
        MeshAttachment attachment,
        double offsetX,
        double bottom,
        double centerX,
        double centerY,
        double scale,
        double rotation,
        bool flipX,
        float alpha)
    {
        var region = attachment.Region as AtlasRegion;
        if (region == null)
        {
            return;
        }

        var page = asset.PageOf(region);
        if (page == null)
        {
            return;
        }

        var world = new float[attachment.WorldVerticesLength];
        attachment.ComputeWorldVertices(slot, world);

        var uvs = attachment.UVs;
        var triangles = attachment.Triangles;
        var pageWidth = page.Bitmap.PixelSize.Width;
        var pageHeight = page.Bitmap.PixelSize.Height;

        for (var index = 0; index + 2 < triangles.Length; index += 3)
        {
            var vertexA = triangles[index];
            var vertexB = triangles[index + 1];
            var vertexC = triangles[index + 2];
            if (vertexA < 0 || vertexB < 0 || vertexC < 0)
            {
                continue;
            }

            var pointA = ProjectPoint(world[vertexA * 2], world[vertexA * 2 + 1], offsetX, bottom, centerX, centerY, scale, rotation, flipX);
            var pointB = ProjectPoint(world[vertexB * 2], world[vertexB * 2 + 1], offsetX, bottom, centerX, centerY, scale, rotation, flipX);
            var pointC = ProjectPoint(world[vertexC * 2], world[vertexC * 2 + 1], offsetX, bottom, centerX, centerY, scale, rotation, flipX);

            var sourceA = new Point(uvs[vertexA * 2] * pageWidth, uvs[vertexA * 2 + 1] * pageHeight);
            var sourceB = new Point(uvs[vertexB * 2] * pageWidth, uvs[vertexB * 2 + 1] * pageHeight);
            var sourceC = new Point(uvs[vertexC * 2] * pageWidth, uvs[vertexC * 2 + 1] * pageHeight);

            var matrix = CreateAffine(sourceA, sourceB, sourceC, pointA, pointB, pointC);
            var clip = CreateTriangle(pointA, pointB, pointC);
            using (context.PushOpacity(alpha))
            using (context.PushGeometryClip(clip))
            using (context.PushTransform(matrix))
            {
                context.DrawImage(
                    page.Bitmap,
                    new Rect(0, 0, pageWidth, pageHeight),
                    new Rect(0, 0, pageWidth, pageHeight));
            }
        }
    }

    /// <summary>把 region 顶点投影成屏幕四边形。</summary>
    private static Point[] ProjectQuad(
        float[] world,
        double offsetX,
        double bottom,
        double centerX,
        double centerY,
        double scale,
        double rotation,
        bool flipX)
    {
        // 与 ComputeWorldVertices 的 BL/UL/UR/BR 顺序保持一致；
        // 顺序一旦错位，整块 region 会整体旋转 90°，表现为头或身体转向。
        return new[]
        {
            ProjectPoint(world[0], world[1], offsetX, bottom, centerX, centerY, scale, rotation, flipX),
            ProjectPoint(world[2], world[3], offsetX, bottom, centerX, centerY, scale, rotation, flipX),
            ProjectPoint(world[4], world[5], offsetX, bottom, centerX, centerY, scale, rotation, flipX),
            ProjectPoint(world[6], world[7], offsetX, bottom, centerX, centerY, scale, rotation, flipX),
        };
    }

    /// <summary>把 Spine 世界坐标投影到屏幕坐标，并按需要翻转、旋转。</summary>
    private static Point ProjectPoint(
        double x,
        double y,
        double offsetX,
        double bottom,
        double centerX,
        double centerY,
        double scale,
        double rotation,
        bool flipX)
    {
        var screenX = offsetX + x * scale;
        var screenY = bottom - y * scale;
        if (flipX)
        {
            screenX = 2 * centerX - screenX;
        }

        if (Math.Abs(rotation) > 0.0001)
        {
            var radians = rotation * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var dx = screenX - centerX;
            var dy = screenY - centerY;
            screenX = centerX + dx * cos - dy * sin;
            screenY = centerY + dx * sin + dy * cos;
        }

        return new Point(screenX, screenY);
    }

    /// <summary>
    /// 计算把 source 三点映射到 destination 三点的仿射矩阵。
    /// 顶点顺序必须一致（BL → UL → UR），否则纹理会错位。
    /// </summary>
    private static Matrix CreateAffine(
        Point sourceA,
        Point sourceB,
        Point sourceC,
        Point destA,
        Point destB,
        Point destC)
    {
        var ux = sourceB.X - sourceA.X;
        var uy = sourceB.Y - sourceA.Y;
        var vx = sourceC.X - sourceA.X;
        var vy = sourceC.Y - sourceA.Y;
        var determinant = ux * vy - vx * uy;
        if (Math.Abs(determinant) < 0.000001)
        {
            return Matrix.Identity;
        }

        var qx1 = destB.X - destA.X;
        var qy1 = destB.Y - destA.Y;
        var qx2 = destC.X - destA.X;
        var qy2 = destC.Y - destA.Y;

        var m11 = (qx1 * vy - qx2 * uy) / determinant;
        var m12 = (qy1 * vy - qy2 * uy) / determinant;
        var m21 = (qx2 * ux - qx1 * vx) / determinant;
        var m22 = (qy2 * ux - qy1 * vx) / determinant;
        var dx = destA.X - (m11 * sourceA.X + m21 * sourceA.Y);
        var dy = destA.Y - (m12 * sourceA.X + m22 * sourceA.Y);
        return new Matrix(m11, m12, m21, m22, dx, dy);
    }

    /// <summary>构造四边形裁剪路径。</summary>
    private static StreamGeometry CreateQuad(Point[] points)
    {
        var geometry = new StreamGeometry();
        using var writer = geometry.Open();
        writer.BeginFigure(points[0], true);
        writer.LineTo(points[1]);
        writer.LineTo(points[2]);
        writer.LineTo(points[3]);
        writer.EndFigure(true);
        return geometry;
    }

    /// <summary>构造三角形裁剪路径。</summary>
    private static StreamGeometry CreateTriangle(Point a, Point b, Point c)
    {
        var geometry = new StreamGeometry();
        using var writer = geometry.Open();
        writer.BeginFigure(a, true);
        writer.LineTo(b);
        writer.LineTo(c);
        writer.EndFigure(true);
        return geometry;
    }
}

#endregion
