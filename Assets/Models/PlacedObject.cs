/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlacedObject.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图实例模型：位姿、碰撞、阴影、路径与模板引用
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace MapEditorTool.Models;

/// <summary>
/// 已放置到地图上的物体实例。只保存实例自身的位姿与开关，模板属性通过 TemplateId 引用。
/// </summary>
public sealed class PlacedObject
{
    /// <summary>实例唯一 ID，8 位。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>引用模板 ID。</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>物体显示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>物体类型。</summary>
    public MapObjectType Type { get; set; } = MapObjectType.Prop;

    /// <summary>左上角世界坐标 X。</summary>
    public double X { get; set; }

    /// <summary>左上角世界坐标 Y。</summary>
    public double Y { get; set; }

    /// <summary>宽度，单位格子。</summary>
    public int Width { get; set; } = 1;

    /// <summary>高度，单位格子。</summary>
    public int Height { get; set; } = 1;

    /// <summary>PSD 原始像素宽，大于 0 时按像素还原。</summary>
    public double PixelWidth { get; set; }

    /// <summary>PSD 原始像素高，大于 0 时按像素还原。</summary>
    public double PixelHeight { get; set; }

    /// <summary>是否拉伸到配置尺寸。</summary>
    public bool Stretch { get; set; }

    /// <summary>图层类型键。</summary>
    public string LayerType { get; set; } = MapLayerKeys.Prop;

    /// <summary>显示层键。</summary>
    public string DisplayLayer { get; set; } = string.Empty;

    /// <summary>
    /// 手工指定的遮挡带（编辑器侧）：-1 = 跟随图层类型 / 名字自动判定，0 地表，1 立体，2 叠加。
    /// <para>改这个值等于把这张贴图抬到另一层；导出时只在非 -1 时写 <c>band</c> 字段，
    /// 排序仍然由 <see cref="SortOrder"/>（带号 × 1000 + 带内序位）表达。</para>
    /// </summary>
    public int BandOverride { get; set; } = -1;

    /// <summary>
    /// 当前 <see cref="BandOverride"/> 是否由导入/刷新策略自动写入（仅本次会话有效，不落盘）。
    /// <para>属性卡用它区分「自动提层」与「用户手改遮挡带」；JSON 只保存 <c>band</c>，
    /// 重启后无法还原来源，按手改显示即可。用户手动改带或恢复自动判定时必须清掉。</para>
    /// </summary>
    public bool BandOverrideAuto { get; set; }

    /// <summary>素材路径。</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>动画名。</summary>
    public string AnimName { get; set; } = MapToolDefaults.DefaultAnimName;

    /// <summary>动画来源，见 <see cref="AnimationKinds"/>；缺省为序列帧。</summary>
    public string AnimationKind { get; set; } = AnimationKinds.Sprite;

    /// <summary>Spine 资产名（对应 <c>SpineAnimations.json</c> 的 Name）。</summary>
    public string SpineAsset { get; set; } = string.Empty;

    /// <summary>Spine 骨架内的动画名；空串表示使用资产默认动画。</summary>
    public string SpineAnimation { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用动画。关闭时画布始终使用静态贴图；
    /// true 且 <see cref="AnimName"/> 能在动画目录里找到可用帧序列时，画布按帧播放。
    /// </summary>
    public bool AnimEnabled { get; set; } = true;

    /// <summary>循环播放。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>播放速度。</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>可交互。</summary>
    public bool Interactable { get; set; } = true;

    /// <summary>交互类型。</summary>
    public string InteractType { get; set; } = InteractTypes.None;

    /// <summary>气泡文本。</summary>
    public string DialogBubble { get; set; } = string.Empty;

    /// <summary>气泡配置 ID。</summary>
    public int BubbleId { get; set; }

    /// <summary>渲染层级排序。</summary>
    public int SortOrder { get; set; }

    /// <summary>启用 Y 轴排序。</summary>
    public bool YSortEnabled { get; set; } = true;

    /// <summary>Y 轴排序偏移。</summary>
    public double YSortOffset { get; set; }

    /// <summary>旋转角度，度。</summary>
    public double Rotation { get; set; }

    /// <summary>水平镜像。</summary>
    public bool FlipX { get; set; }

    /// <summary>缩放倍率。</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>启用碰撞。</summary>
    public bool CollisionEnabled { get; set; } = true;

    /// <summary>初始朝向。</summary>
    public string FacingDirection { get; set; } = Facings.Down;

    /// <summary>显示阴影。</summary>
    public bool ShadowEnabled { get; set; } = true;

    /// <summary>可收集。</summary>
    public bool IsCollectible { get; set; }

    /// <summary>收集物显示在普通物件上面（false = 在物件下面）。仅 <see cref="IsCollectible"/> 为 true 时参与排序。</summary>
    public bool CollectibleOverObjects { get; set; } = true;

    /// <summary>碰撞区域左偏移（相对包围盒左上角，像素）。</summary>
    public double CollisionX { get; set; }

    /// <summary>碰撞区域上偏移（相对包围盒左上角，像素）。</summary>
    public double CollisionY { get; set; }

    /// <summary>碰撞区域宽（像素），0 表示铺满包围盒。</summary>
    public double CollisionWidth { get; set; }

    /// <summary>碰撞区域高（像素），0 表示铺满包围盒。</summary>
    public double CollisionHeight { get; set; }

    /// <summary>阴影区域左偏移（相对包围盒左上角，像素）。</summary>
    public double ShadowX { get; set; }

    /// <summary>阴影区域上偏移（相对包围盒左上角，像素）。</summary>
    public double ShadowY { get; set; }

    /// <summary>阴影区域宽（像素），0 表示铺满包围盒。</summary>
    public double ShadowWidth { get; set; }

    /// <summary>阴影区域高（像素），0 表示铺满包围盒。</summary>
    public double ShadowHeight { get; set; }

    /// <summary>重生时间。</summary>
    public double RespawnTime { get; set; }

    /// <summary>可移动。</summary>
    public bool IsMovable { get; set; }

    /// <summary>移动动画名。</summary>
    public string MoveAnimName { get; set; } = "walk";

    /// <summary>行走模式。</summary>
    public string WalkMode { get; set; } = "loop";

    /// <summary>行走路径点，单位世界像素。</summary>
    public List<MapPathPointData> PathPoints { get; set; } = new();

    /// <summary>是否传送门。</summary>
    public bool IsTeleport { get; set; }

    /// <summary>传送目标场景。</summary>
    public string TeleportTargetScene { get; set; } = string.Empty;

    /// <summary>传送目标 X。</summary>
    public double TeleportTargetX { get; set; }

    /// <summary>传送目标 Y。</summary>
    public double TeleportTargetY { get; set; }

    /// <summary>是否载具（勾选后交通角色同步为载具）。</summary>
    public bool IsVehicle { get; set; }

    /// <summary>交通角色：pedestrian / vehicle / none。</summary>
    public string TrafficRole { get; set; } = TrafficRoles.Pedestrian;

    /// <summary>停止线 X（世界像素）。</summary>
    public double StopLineX { get; set; }

    /// <summary>停止线 Y（世界像素）。</summary>
    public double StopLineY { get; set; }

    /// <summary>绑定的红绿灯 ID 集合。</summary>
    public List<string> BoundLightIds { get; set; } = new();

    /// <summary>黄灯是否按红灯处理（仅载具消费）。</summary>
    public bool YellowTreatAsRed { get; set; } = true;

    /// <summary>格坐标 X，由世界坐标换算。</summary>
    public int CellX => (int)Math.Floor(X / MapToolDefaults.GridSize);

    /// <summary>格坐标 Y，由世界坐标换算。</summary>
    public int CellY => (int)Math.Floor(Y / MapToolDefaults.GridSize);

    /// <summary>世界空间包围盒（格宽高按像素换算后再乘缩放倍率）。</summary>
    public Avalonia.Rect Bounds
    {
        get
        {
            var width = (PixelWidth > 0 ? PixelWidth : Width * MapToolDefaults.GridSize) * Scale;
            var height = (PixelHeight > 0 ? PixelHeight : Height * MapToolDefaults.GridSize) * Scale;
            return new Avalonia.Rect(X, Y, width, height);
        }
    }

    /// <summary>序列化时使用的格宽。</summary>
    public int ExportWidth => PixelWidth > 0 ? (int)Math.Max(1, Math.Ceiling(PixelWidth / MapToolDefaults.GridSize)) : Width;

    /// <summary>序列化时使用的格高。</summary>
    public int ExportHeight => PixelHeight > 0 ? (int)Math.Max(1, Math.Ceiling(PixelHeight / MapToolDefaults.GridSize)) : Height;

    /// <summary>碰撞区域（世界像素）；宽或高为 0 时按偏移铺满包围盒剩余部分。</summary>
    public Avalonia.Rect CollisionBounds => ResolveArea(CollisionX, CollisionY, CollisionWidth, CollisionHeight);

    /// <summary>阴影区域（世界像素）；宽或高为 0 时按偏移铺满包围盒剩余部分。</summary>
    public Avalonia.Rect ShadowBounds => ResolveArea(ShadowX, ShadowY, ShadowWidth, ShadowHeight);

    /// <summary>把「相对包围盒偏移 + 尺寸」换算成世界像素矩形，尺寸缺省时铺满包围盒。</summary>
    /// <param name="offsetX">相对包围盒左偏移。</param>
    /// <param name="offsetY">相对包围盒上偏移。</param>
    /// <param name="width">宽，0 表示铺满。</param>
    /// <param name="height">高，0 表示铺满。</param>
    /// <returns>世界像素矩形。</returns>
    private Avalonia.Rect ResolveArea(double offsetX, double offsetY, double width, double height)
    {
        var bounds = Bounds;
        var w = width > 0 ? width : Math.Max(1, bounds.Width - offsetX);
        var h = height > 0 ? height : Math.Max(1, bounds.Height - offsetY);
        return new Avalonia.Rect(bounds.X + offsetX, bounds.Y + offsetY, w, h);
    }

    /// <summary>
    /// 世界坐标是否落在物体内：先按 <see cref="Rotation"/> 绕包围盒中心逆向旋转回局部坐标，再判轴对齐包围盒。
    /// </summary>
    /// <param name="world">世界坐标。</param>
    /// <returns>命中返回 true。</returns>
    public bool ContainsPoint(Avalonia.Point world) => TryGetLocalPoint(world, out _);

    /// <summary>
    /// 把世界坐标逆旋转回包围盒局部坐标（左上角为原点）。几何换算只在这里维护一份，
    /// 命中测试与透明像素采样共享同一口径。
    /// </summary>
    /// <param name="world">世界坐标。</param>
    /// <param name="local">局部坐标；未命中时也可能有值，调用方应以返回值判断。</param>
    /// <returns>落在包围盒内返回 true。</returns>
    public bool TryGetLocalPoint(Avalonia.Point world, out Avalonia.Point local)
    {
        var bounds = Bounds;
        local = default;

        if (Rotation == 0)
        {
            local = new Avalonia.Point(world.X - bounds.X, world.Y - bounds.Y);
            return bounds.Contains(world);
        }

        var center = bounds.Center;
        var rad = -Rotation * Math.PI / 180.0;
        var dx = world.X - center.X;
        var dy = world.Y - center.Y;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        // 逆旋转回局部坐标后仍按轴对齐包围盒判定，旋转不改包围盒口径
        var localWorld = new Avalonia.Point(
            center.X + (dx * cos - dy * sin),
            center.Y + (dx * sin + dy * cos));
        local = new Avalonia.Point(localWorld.X - bounds.X, localWorld.Y - bounds.Y);
        return bounds.Contains(localWorld);
    }

    /// <summary>
    /// 把世界坐标换算成包围盒内的归一化局部坐标，供贴图 alpha 采样使用。
    /// </summary>
    /// <param name="world">世界坐标。</param>
    /// <param name="normalizedX">局部 X，0 为左边、1 为右边。</param>
    /// <param name="normalizedY">局部 Y，0 为上边、1 为下边。</param>
    /// <returns>落在包围盒内且尺寸有效时返回 true。</returns>
    public bool TryGetNormalizedPoint(Avalonia.Point world, out double normalizedX, out double normalizedY)
    {
        normalizedX = 0;
        normalizedY = 0;

        if (!TryGetLocalPoint(world, out var local))
        {
            return false;
        }

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        normalizedX = local.X / bounds.Width;
        normalizedY = local.Y / bounds.Height;
        return true;
    }
}

