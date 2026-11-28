/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiTheme.Canvas.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布色：地图画布、预览画布与地图网格。
 ***************************************************************/

using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public static partial class UiTheme
{
    #region 地图画布色

    /// <summary>主地图画布的语义色：底色、图层、编辑提示、碰撞/阴影占位、路径与红绿灯。</summary>
    public static class MapView
    {
        /// <summary>画布外底色（视口空白处）：近墨的深青，与预览舞台同族，负责把地图之外安静下去。</summary>
        public static readonly IBrush Backdrop = Rgb(0x10, 0x3C, 0x4C);

        /// <summary>
        /// 地图有效区域底色：比视口底色略亮一档的墨青面，靠 1px 半透明外框分层。
        /// 不再沿用主强调亮青——整屏四分之三的面积都在这里，必须让位给美术。
        /// </summary>
        public static readonly IBrush Surface = Rgb(0x0D, 0x8C, 0xB6);

        /// <summary>地图图层叠加色：没导底图时的「地图层」填充上缘，往下渐深一档，避免整块纯色发平。</summary>
        public static readonly Color Layer = Color.FromRgb(0x14, 0x96, 0xCB);

        /// <summary>地图图层渐变下缘色。</summary>
        public static readonly Color LayerDeep = Color.FromRgb(0x0F, 0x80, 0xAB);

        /// <summary>框选矩形填充。</summary>
        public static readonly IBrush Marquee = Argb(0x38, 0xFF, 0xFF, 0xFF);

        /// <summary>白色编辑提示线。</summary>
        public static readonly IBrush Accent = Brushes.White;

        /// <summary>控制点/标记黄色。</summary>
        public static readonly IBrush Handle = Rgb(0xFF, 0xD2, 0x4A);

        /// <summary>画布内文字。</summary>
        public static readonly IBrush Label = Rgb(0xF2, 0xFB, 0xFE);

        /// <summary>地图外框（白色半透明，1 设备像素）。</summary>
        public static readonly IBrush Border = Argb(0x66, 0xFF, 0xFF, 0xFF);

        /// <summary>选中格填充。</summary>
        public static readonly IBrush SelectedCellFill = Argb(0x66, 0x4C, 0xD9, 0x64);

        /// <summary>选中格描边。</summary>
        public static readonly IBrush SelectedCellStroke = Argb(0xFF, 0x2E, 0xC4, 0x4A);

        /// <summary>碰撞区填充。</summary>
        public static readonly IBrush CollisionFill = Argb(0x33, 0xE5, 0x53, 0x3D);

        /// <summary>碰撞区描边。</summary>
        public static readonly IBrush CollisionStroke = Rgb(0xE5, 0x53, 0x3D);

        /// <summary>阴影区填充。</summary>
        public static readonly IBrush ShadowFill = Argb(0x33, 0x7A, 0x5C, 0xD6);

        /// <summary>阴影区描边。</summary>
        public static readonly IBrush ShadowStroke = Rgb(0x7A, 0x5C, 0xD6);

        /// <summary>占用格填充：游戏内落位格同款半透明绿，草地 / 石板底色上都能读出这一格被占了。</summary>
        public static readonly IBrush OccupancyFill = Argb(0x28, 0xFF, 0xD2, 0x4A);

        /// <summary>占用格描边：高饱和亮绿，与游戏内落位格一致；矩形 / 菱形 / 六边形共用一套。</summary>
        public static readonly IBrush OccupancyStroke = Argb(0xE6, 0xFF, 0xC8, 0x1E);

        /// <summary>占用格内部十字线：比外框淡一档，看得出格子四分，又不抢外框。</summary>
        public static readonly IBrush OccupancyCross = Argb(0xD0, 0x9C, 0xFF, 0xB4);

        /// <summary>未选中图元的淡占格描边：同色系压淡，避免一屏几百个占格抢视线。</summary>
        public static readonly IBrush OccupancyIdleStroke = Argb(0x55, 0xFF, 0xD2, 0x4A);

        /// <summary>占格顶层描边：画在所有贴图之上，贴图把整格盖住时仍能看见占格边界。</summary>
        public static readonly IBrush OccupancyOutline = Argb(0xCC, 0x3C, 0xFF, 0x7A);

        /// <summary>整图包围盒绿线。</summary>
        public static readonly IBrush Boundary = Rgb(0x00, 0xFF, 0x00);

        /// <summary>未旋转图元的占格足迹描边。</summary>
        public static readonly IBrush Footprint = Argb(0x40, 0xFF, 0xFF, 0xFF);

        /// <summary>房间触发区淡填充：薄薄一层青蓝，标出「玩家走到这里算进屋」。</summary>
        public static readonly IBrush RoomFill = Argb(0x1C, 0x4F, 0xC3, 0xF7);

        /// <summary>房间触发区虚线描边。</summary>
        public static readonly IBrush RoomStroke = Argb(0xB4, 0x8F, 0xDC, 0xFF);

        /// <summary>当前「进屋预览」的房间描边（更亮，和普通房间区分）。</summary>
        public static readonly IBrush RoomPreviewStroke = Argb(0xFF, 0xFF, 0xD2, 0x4A);

        /// <summary>层高标尺（画在地图左侧的竖向虚线 + 两端刻度）：勾选「楼层参考」时才画。</summary>
        public static readonly IBrush LevelGuide = Argb(0xCC, 0xB4, 0xE2, 0xF5);

        /// <summary>楼层牌子底：半透明深青，压在美术上也读得清，又不糊住下面的像素。</summary>
        public static readonly IBrush LevelTagFill = Argb(0xD8, 0x0B, 0x2A, 0x36);

        /// <summary>未登记配置表的资源标记描边（低饱和砖红，与碰撞红区分），画布上给缺失资源画框。</summary>
        public static readonly IBrush ResourceMissingStroke = Rgb(0xC2, 0x4A, 0x3A);

        /// <summary>未登记资源标记的淡填充：只压一层薄色，不盖住美术本来的样子。</summary>
        public static readonly IBrush ResourceMissingFill = Argb(0x1E, 0xC2, 0x4A, 0x3A);

        /// <summary>未登记资源标签底板。</summary>
        public static readonly IBrush ResourceMissingPlate = Argb(0xE6, 0x7A, 0x25, 0x1C);

        /// <summary>未登记资源标签文字。</summary>
        public static readonly IBrush ResourceMissingLabel = Rgb(0xFF, 0xEE, 0xE8);

        /// <summary>路径轨迹主色。</summary>
        public static readonly IBrush Path = Rgb(0xBF, 0xF0, 0x6B);

        /// <summary>路径节点填充。</summary>
        public static readonly IBrush PathPoint = Rgb(0x62, 0xD9, 0x8B);

        /// <summary>被选中的路径点填充：与编辑器主色一致，和未选中的绿点区分开。</summary>
        public static readonly IBrush PathPointSelected = Rgb(0x0D, 0x8C, 0xB6);

        /// <summary>被选中的路径点描边（白圈，压在深色底图上也看得见）。</summary>
        public static readonly IBrush PathPointSelectedRing = Rgb(0xF7, 0xFD, 0xFF);

        /// <summary>预览路径节点填充（略透明，避免遮住底图）。</summary>
        public static readonly IBrush PathPointFill = Argb(0xEE, 0x62, 0xD9, 0x8B);

        /// <summary>路径轨迹阴影。</summary>
        public static readonly IBrush PathShadow = Argb(0xB8, 0x10, 0x25, 0x18);

        /// <summary>路径节点描边。</summary>
        public static readonly IBrush PathPointBorder = Rgb(0x0F, 0x6B, 0x42);

        /// <summary>路径起终点标记。</summary>
        public static readonly IBrush PathMarker = Rgb(0xFF, 0xD2, 0x4A);

        /// <summary>路径起终点标记描边。</summary>
        public static readonly IBrush PathMarkerBorder = Rgb(0x3A, 0x2B, 0x00);

        /// <summary>人行道描边。</summary>
        public static readonly IBrush Sidewalk = Argb(0xC8, 0x6F, 0xD9, 0xE8);

        /// <summary>道路中心线。</summary>
        public static readonly IBrush Road = Argb(0xD8, 0xDC, 0xE6, 0xF2);

        /// <summary>斑马线填充。蓝底地图上 0xCC 会泛蓝，用到 0xE6 保证条纹在深色道路上也够白。</summary>
        public static readonly IBrush Crosswalk = Argb(0xE6, 0xFB, 0xFB, 0xFB);

        /// <summary>斑马线边线。</summary>
        public static readonly IBrush CrosswalkEdge = Argb(0xEE, 0xFF, 0xD2, 0x4A);

        /// <summary>红灯。</summary>
        public static readonly IBrush TrafficRed = Rgb(0xE0, 0x3A, 0x3A);

        /// <summary>黄灯。</summary>
        public static readonly IBrush TrafficYellow = Rgb(0xE8, 0xC5, 0x45);

        /// <summary>绿灯。</summary>
        public static readonly IBrush TrafficGreen = Rgb(0x4C, 0xC0, 0x69);

        /// <summary>交通停止线。</summary>
        public static readonly IBrush StopLine = Argb(0xFF, 0xFF, 0x5A, 0x5A);

        /// <summary>红绿灯灯体。</summary>
        public static readonly IBrush TrafficBody = Rgb(0x1B, 0x1B, 0x1B);

        /// <summary>灯体到斑马线的关联引导线。</summary>
        public static readonly IBrush TrafficGuide = Argb(0x88, 0xFF, 0xFF, 0xFF);

        /// <summary>战争迷雾编辑面板的中性底。</summary>
        public static readonly IBrush FogNeutral = Rgb(0xCF, 0xD8, 0xDC);

        /// <summary>迷雾覆盖层基色；实际绘制按区域浓度叠加透明度。</summary>
        public static readonly Color FogOverlay = Color.FromRgb(0x07, 0x0C, 0x14);

        /// <summary>迷雾笔刷范围填充。</summary>
        public static readonly IBrush FogBrushFill = Argb(0x1F, 0xFF, 0xD2, 0x4A);

        /// <summary>迷雾笔刷范围描边。</summary>
        public static readonly IBrush FogBrushStroke = Argb(0xCC, 0xFF, 0xD2, 0x4A);
    }

    #endregion

    #region 预览画布色

    /// <summary>动画/整图预览共用的深色舞台色。</summary>
    public static class PreviewView
    {
        /// <summary>预览窗深色舞台底。</summary>
        public static readonly IBrush Stage = Rgb(0x0F, 0x3A, 0x4A);

        /// <summary>动画预览画布背景。</summary>
        public static readonly IBrush CanvasBackdrop = Rgb(0x10, 0x4A, 0x5E);

        /// <summary>动画预览网格。</summary>
        public static readonly IBrush Grid = Argb(0x18, 0xBF, 0xE8, 0xF0);

        /// <summary>动画预览路径轨迹。</summary>
        public static readonly IBrush Path = Rgb(0xBF, 0xF0, 0x6B);

        /// <summary>动画预览路径节点。</summary>
        public static readonly IBrush PathPoint = Rgb(0x62, 0xD9, 0x8B);

        /// <summary>脚底阴影。</summary>
        public static readonly IBrush Shadow = Argb(0x55, 0x00, 0x00, 0x00);

        /// <summary>脚底落点填充。</summary>
        public static readonly IBrush FootprintFill = Argb(0x33, 0x62, 0xD9, 0x8B);

        /// <summary>脚底落点描边。</summary>
        public static readonly IBrush FootprintStroke = Argb(0x99, 0x9C, 0xE8, 0xB0);

        /// <summary>预览画布文字。</summary>
        public static readonly IBrush Label = Argb(0xE6, 0xFF, 0xFF, 0xFF);

        /// <summary>预览画布次要文字。</summary>
        public static readonly IBrush Muted = Argb(0xB0, 0xDD, 0xEE, 0xF0);

        /// <summary>预览画布空态提示。</summary>
        public static readonly IBrush Empty = Argb(0xCC, 0xFF, 0xD2, 0x4A);

        /// <summary>预览画布外框。</summary>
        public static readonly IBrush FrameBorder = Rgb(0xA9, 0xC0, 0xCB);
    }

    #endregion

    #region 地图网格色

    /// <summary>地图画布统一网格主题。</summary>
    public static class MapGrid
    {
        /// <summary>摆放网格弱线。</summary>
        public static readonly IBrush MinorLine = Argb(0x50, 0x38, 0xFF, 0x86);

        /// <summary>摆放网格强线（每若干格一条）。</summary>
        public static readonly IBrush MajorLine = Argb(0x9A, 0x16, 0xD9, 0x66);

        /// <summary>菱形网格细线：与正交网格同一支绿，透明度再压一档，密排铺开时不糊住底图。</summary>
        public static readonly IBrush DiamondMinorLine = Argb(0x3A, 0x38, 0xFF, 0x86);

        /// <summary>菱形网格主网格线：每 2 格一条，用强线的深绿，把密排的 45° 细线读成「大菱形套小菱形」。</summary>
        public static readonly IBrush DiamondMajorLine = Argb(0x7A, 0x16, 0xD9, 0x66);

        /// <summary>路径编辑网格线。</summary>
        public static readonly IBrush PathLine = Argb(0x9A, 0x3D, 0x9B, 0xFF);

        /// <summary>地图外框线。</summary>
        public static readonly IBrush MapFrameLine = Argb(0xC0, 0x8A, 0x93, 0xA8);
    }

    #endregion

    #region 交通仿真灯头色

    /// <summary>
    /// 交通仿真叠层的灯头与等待标记配色。灯箱、灭灯暗档与相位亮色都放这里，
    /// 画布类只负责几何，不再自带一套色值。
    /// </summary>
    public static class Sim
    {
        /// <summary>红灯亮档。</summary>
        public static readonly IBrush PhaseRed = Argb(0xFF, 0xE5, 0x39, 0x35);

        /// <summary>黄灯亮档。</summary>
        public static readonly IBrush PhaseYellow = Argb(0xFF, 0xFF, 0xB3, 0x00);

        /// <summary>绿灯亮档。</summary>
        public static readonly IBrush PhaseGreen = Argb(0xFF, 0x2E, 0x7D, 0x32);

        /// <summary>红灯灭档：保留同色暗调，避免深色灯箱上出现"看不见的黑点"。</summary>
        public static readonly IBrush PhaseRedDim = Argb(0xFF, 0x4A, 0x1E, 0x20);

        /// <summary>黄灯灭档。</summary>
        public static readonly IBrush PhaseYellowDim = Argb(0xFF, 0x4C, 0x38, 0x12);

        /// <summary>绿灯灭档。</summary>
        public static readonly IBrush PhaseGreenDim = Argb(0xFF, 0x1C, 0x38, 0x22);

        /// <summary>灯位外圈。</summary>
        public static readonly IBrush PhaseRing = Argb(0xE6, 0x0B, 0x0F, 0x13);

        /// <summary>灯箱外壳。</summary>
        public static readonly IBrush PhaseHousing = Argb(0xF0, 0x16, 0x1C, 0x22);

        /// <summary>灯箱描边。</summary>
        public static readonly IBrush PhaseHousingEdge = Argb(0xFF, 0x55, 0x62, 0x6E);

        /// <summary>灯杆。</summary>
        public static readonly IBrush PhasePole = Argb(0xD0, 0x3A, 0x44, 0x4E);

        /// <summary>等待中车辆的高亮标记。</summary>
        public static readonly IBrush Waiting = Argb(0xFF, 0xFF, 0xD5, 0x4F);

        /// <summary>等待标记的底衬。</summary>
        public static readonly IBrush WaitingBack = Argb(0xE6, 0x33, 0x2A, 0x00);

        /// <summary>仿真停线。</summary>
        public static readonly IBrush StopLine = Argb(0x8C, 0xFF, 0xFF, 0xFF);

        /// <summary>亮灯位置的光晕（白色低透明度，叠在灯头上）。</summary>
        public static readonly IBrush LampGlow = Argb(0x55, 0xFF, 0xFF, 0xFF);
    }

    #endregion

}