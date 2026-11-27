/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FogEditPanel.Drawing.cs
 * author:    云毅
 * created:   2026
 * descrip:   迷雾绘制控制：开始/停止绘制、同步画布设置与提交回调
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class FogEditPanel : UserControl
{
    #region 绘制控制

    private void ToggleDrawing()
    {
        _drawing = !_drawing;
        _drawButton.Content = _drawing ? StopDrawingText : StartDrawingText;
        // 主行动按钮常驻实心强调色：绘制中用深一档的青，停止后仍在视觉焦点上
        _drawButton.Background = _drawing ? UiTheme.AccentDark : UiTheme.Accent;
        _drawButton.BorderBrush = _drawing ? UiTheme.AccentDark : UiTheme.Accent;
        _drawButton.Foreground = UiTheme.White;
        _drawButton.FontWeight = FontWeight.SemiBold;
        _drawButton.CornerRadius = new CornerRadius(DrawButtonCornerRadius);
        _drawButton.Height = DrawButtonHeight;
        SyncCanvasSettings();
        DrawingChanged?.Invoke(this, EventArgs.Empty);
        _notify(_drawing ? "迷雾绘制中：左键涂抹 / 右键擦除" : "已停止迷雾绘制");
    }

    private void SyncCanvasSettings()
    {
        _canvas.FogDrawing = _drawing;
    }

    private void OnFogEdited()
    {
        RefreshRegionList();
        _sceneCanvas?.Refresh();
        FogChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
