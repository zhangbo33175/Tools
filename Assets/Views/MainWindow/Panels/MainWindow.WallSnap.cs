/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.WallSnap.cs
 * author:    云毅
 * created:   2026
 * descrip:   主界面斜边吸附接线：开关墙体贴菱形共边、把选中墙重新贴边
 ***************************************************************/

using Avalonia.Controls;

namespace MapEditorTool;

public partial class MainWindow
{
    /// <summary>接线：斜边吸附开关 + 「贴斜边」按钮。</summary>
    private void WireWallSnap()
    {
        EdgeSnapToggle.IsCheckedChanged += (_, _) =>
        {
            var enabled = EdgeSnapToggle.IsChecked == true;
            Canvas.EdgeSnapEnabled = enabled;
            if (!enabled)
            {
                SetMessage("斜边吸附已关闭：回到格心落位");
                return;
            }

            SetMessage(Canvas.CanEdgeSnap
                ? "斜边吸附已开启：墙脚对齐菱形共边，朝向自动取 90° 档位"
                : "斜边吸附已开启，但当前是矩形 / 六边形网格：切到 Isometric 才生效");
        };

        BtnSnapEdge.Click += (_, _) =>
        {
            if (!Canvas.CanEdgeSnap)
            {
                SetMessageError("斜边吸附只对 Isometric（菱形）网格有效，先在「摆放模式」里切形状");
                return;
            }

            if (Canvas.Selected is null)
            {
                SetMessageError("先选中一面墙 / 隔断，再点「贴斜边」");
                return;
            }

            if (!Canvas.ResnapSelectedToEdge())
            {
                SetMessage("这面墙已经贴在斜边上，无需再吸附");
                return;
            }

            SyncProperties();
            SetMessage($"已贴边：{Canvas.Selected.Name} · 朝向 {Canvas.Selected.Rotation:0}°");
        };
    }
}
