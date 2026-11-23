/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementPanel.Binding.cs
 * author:    云毅
 * created:   2026
 * descrip:   元件面板字段绑定与回填：控件事件、字段写回、选中项刷新。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool.Controls;

public sealed partial class ItemElementPanel : UserControl
{
    #region 字段绑定

    /// <summary>注册全部字段的「回填 + 写回」，字段增删只改本处，避免两侧手写清单漂移。</summary>
    private void BindFields()
    {
        BindCombo(_typeBox, ObjectTypeNames.EditableChoices,
            template => ObjectTypeNames.Of(template.Type),
            (template, value) => template.Type = ObjectTypeNames.ToType(value),
            () =>
            {
                if (_selected is not null)
                {
                    _tabType = _selected.Type;
                }

                RefreshList();
                LoadForm();
            });

        BindText(_nameBox, template => template.TemplateName, (template, value) => template.TemplateName = value);
        BindText(_resNameBox, template => template.ResName, (template, value) => template.ResName = value);
        BindText(_animBox, template => template.AnimName, (template, value) => template.AnimName = value);
        BindBool(_loopBox, template => template.Loop, (template, value) => template.Loop = value);
        BindNumber(_speedBox, template => template.Speed, (template, value) => template.Speed = value);
        BindBool(_interactableBox, template => template.Interactable, (template, value) => template.Interactable = value);
        BindCombo(_interactTypeBox, TemplateInteractTypes.DisplayChoices,
            template => TemplateInteractTypes.ToDisplay(template.InteractType),
            (template, value) => template.InteractType = TemplateInteractTypes.ToCode(value));
        BindText(_bubbleTextBox, template => template.DialogBubble, (template, value) => template.DialogBubble = value);
        BindInt(_bubbleIdBox, template => template.BubbleId, (template, value) => template.BubbleId = value);
        BindInt(_sortOrderBox, template => template.SortOrder, (template, value) => template.SortOrder = value);
        BindCombo(_facingBox, FacingNames.DisplayChoices,
            template => FacingNames.ToDisplay(template.FacingDirection),
            (template, value) => template.FacingDirection = FacingNames.ToCode(value));
        BindBool(_collisionBox, template => template.CollisionEnabled, (template, value) => template.CollisionEnabled = value);
        BindBool(_shadowBox, template => template.ShadowEnabled, (template, value) => template.ShadowEnabled = value);
        // 层级档直接显示当前文件 / 元素真正落在哪一层（表里没填显示层时取归属层），不再写「跟随类型」
        BindCombo(_displayLayerBox, TemplateLayers.DisplayChoices,
            template => TemplateLayers.ResolveDisplay(template.DisplayLayer, template.LayerTypeKey),
            (template, value) => template.DisplayLayer = TemplateLayers.ToKeyOrFollow(value, template.LayerTypeKey));
        BindCombo(_trafficBox, TemplateTrafficCategories.DisplayChoices,
            template => TemplateTrafficCategories.ToDisplay(template.TrafficCategory),
            (template, value) => template.TrafficCategory = TemplateTrafficCategories.ToCode(value));
        BindBool(_collectibleBox, template => template.IsCollectible, (template, value) => template.IsCollectible = value);
        BindNumber(_respawnBox, template => template.RespawnTime, (template, value) => template.RespawnTime = value);
        BindBool(_ySortBox, template => template.YSortEnabled, (template, value) => template.YSortEnabled = value);
        BindNumber(_ySortOffsetBox, template => template.YSortOffset, (template, value) => template.YSortOffset = value);
        BindBool(_movableBox, template => template.IsMovable, (template, value) => template.IsMovable = value);
        BindText(_moveAnimBox, template => template.MoveAnimName, (template, value) => template.MoveAnimName = value);
        BindNumber(_loopRestartBox, template => template.LoopRestartDelay, (template, value) => template.LoopRestartDelay = value);
        BindBool(_hideOffMapBox, template => template.HideWhenOffMap, (template, value) => template.HideWhenOffMap = value);
        BindBool(_teleportBox, template => template.IsTeleport, (template, value) => template.IsTeleport = value);
        BindText(_teleportSceneBox, template => template.TeleportTargetScene, (template, value) => template.TeleportTargetScene = value);
        BindNumber(_teleportXBox, template => template.TeleportTargetX, (template, value) => template.TeleportTargetX = value);
        BindNumber(_teleportYBox, template => template.TeleportTargetY, (template, value) => template.TeleportTargetY = value);
        BindInt(_widthBox, template => template.DefaultWidth, (template, value) => template.DefaultWidth = Math.Max(1, value));
        BindInt(_heightBox, template => template.DefaultHeight, (template, value) => template.DefaultHeight = Math.Max(1, value));
        BindCombo(_layerKeyBox, new[] { TemplateLayers.FollowType }.Concat(TemplateLayers.DisplayChoices).ToList(),
            template => TemplateLayers.ToDisplay(template.LayerTypeKey),
            (template, value) => template.LayerTypeKey = TemplateLayers.ToKey(value));

        BindBool(_nineSliceBox,
            template => template.NineSlice.Enabled,
            (template, value) => template.NineSlice.Enabled = value,
            after: () =>
            {
                UpdateNineSliceEditorState();
                RefreshNineSlicePreview();
                NineSliceChanged?.Invoke(this, EventArgs.Empty);
            });
        BindInt(_nineLeftBox,
            template => template.NineSlice.Left,
            (template, value) => template.NineSlice.Left = Math.Max(0, value),
            after: () =>
            {
                RefreshNineSlicePreview();
                NineSliceChanged?.Invoke(this, EventArgs.Empty);
            });
        BindInt(_nineRightBox,
            template => template.NineSlice.Right,
            (template, value) => template.NineSlice.Right = Math.Max(0, value),
            after: () =>
            {
                RefreshNineSlicePreview();
                NineSliceChanged?.Invoke(this, EventArgs.Empty);
            });
        BindInt(_nineTopBox,
            template => template.NineSlice.Top,
            (template, value) => template.NineSlice.Top = Math.Max(0, value),
            after: () =>
            {
                RefreshNineSlicePreview();
                NineSliceChanged?.Invoke(this, EventArgs.Empty);
            });
        BindInt(_nineBottomBox,
            template => template.NineSlice.Bottom,
            (template, value) => template.NineSlice.Bottom = Math.Max(0, value),
            after: () =>
            {
                RefreshNineSlicePreview();
                NineSliceChanged?.Invoke(this, EventArgs.Empty);
            });
        BindCombo(_nineCenterBox, NineSliceModes.DisplayChoices,
            template => NineSliceModes.ToDisplay(template.NineSlice.CenterMode),
            (template, value) => template.NineSlice.CenterMode = NineSliceModes.ToCode(value),
            () => NineSliceChanged?.Invoke(this, EventArgs.Empty));

        _loaders.Add(template => UpdateNineSliceEditorState());
        _loaders.Add(UpdateNineSlicePreview);
    }

    /// <summary>九宫格预览里拖动绿线后，把共享设置回写到四个数字框和开关。</summary>
    private void OnNineSlicePreviewChanged()
    {
        if (_loading || _selected is null)
        {
            return;
        }

        var settings = _selected.NineSlice;
        _loading = true;
        _nineSliceBox.IsChecked = settings.Enabled;
        _nineLeftBox.Text = Math.Max(0, settings.Left).ToString(CultureInfo.InvariantCulture);
        _nineRightBox.Text = Math.Max(0, settings.Right).ToString(CultureInfo.InvariantCulture);
        _nineTopBox.Text = Math.Max(0, settings.Top).ToString(CultureInfo.InvariantCulture);
        _nineBottomBox.Text = Math.Max(0, settings.Bottom).ToString(CultureInfo.InvariantCulture);
        _loading = false;

        UpdateNineSliceEditorState();
        NineSliceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>把当前模板的九宫格设置与素材位图灌进预览；切换模板或改 Icon 后调用。</summary>
    private void UpdateNineSlicePreview(ObjectTemplate? template)
    {
        if (_nineSlicePreview is null)
        {
            return;
        }

        _nineSlicePreview.Settings = template?.NineSlice;
        _nineSlicePreview.Image = template is null ? null : LoadTemplateImage(template);
        _nineSlicePreview.InvalidateVisual();
    }

    /// <summary>数字框 / 开关改动后只刷新预览的参考线，不重复解码位图。</summary>
    private void RefreshNineSlicePreview()
    {
        if (_nineSlicePreview is null)
        {
            return;
        }

        _nineSlicePreview.Settings = _selected?.NineSlice;
        _nineSlicePreview.InvalidateVisual();
    }

    /// <summary>解析模板对应的素材位图，解析顺序与左侧列表缩略图一致（ImagePath → ResName）。</summary>
    private Bitmap? LoadTemplateImage(ObjectTemplate template)
    {
        var imageName = !string.IsNullOrWhiteSpace(template.ImagePath)
            ? Path.GetFileNameWithoutExtension(template.ImagePath)
            : template.ResName;
        var imagePath = _assets.FindImage(imageName) ?? _assets.FindImage(template.ResName);
        return imagePath is null ? null : _assets.Load(imagePath);
    }

    /// <summary>预览、四条边距与中心填充只在勾选「需要九宫格」时显示；未勾选时整块隐藏，不占表单高度。</summary>
    private void UpdateNineSliceEditorState()
    {
        if (_nineSliceFields is null || _nineSlicePreview is null)
        {
            return;
        }

        var enabled = _selected is not null && _nineSliceBox.IsChecked == true;
        _nineSlicePreview.IsVisible = enabled;
        _nineSliceFields.IsVisible = enabled;
        _nineSliceFields.IsEnabled = enabled;
        _nineSliceFields.Opacity = 1.0;
    }

    /// <summary>
    /// 绑定文本字段。Avalonia 的 TextChanged 可能在回填之后才异步派发，
    /// 因此只在输入框获得焦点时（真实用户输入）写回，另在失焦与回车时兜底提交，
    /// 并在写回前与模型现值比较以避免重复刷新列表。
    /// </summary>
    private void BindText(TextBox box, Func<ObjectTemplate, string> read, Action<ObjectTemplate, string> write, Action? after = null)
    {
        _loaders.Add(template =>
        {
            _loading = true;
            box.Text = read(template);
            _loading = false;
        });

        void Commit()
        {
            if (_loading || _selected is null)
            {
                return;
            }

            var text = box.Text ?? string.Empty;
            if (text == read(_selected))
            {
                return;
            }

            write(_selected, text);
            after?.Invoke();
            RefreshList();
        }

        box.TextChanged += (_, _) =>
        {
            if (box.IsFocused)
            {
                Commit();
            }
        };

        box.LostFocus += (_, _) => Commit();
        _committers.Add(Commit);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };
    }

    private void BindInt(TextBox box, Func<ObjectTemplate, int> read, Action<ObjectTemplate, int> write, Action? after = null)
    {
        _loaders.Add(template =>
        {
            _loading = true;
            box.Text = read(template).ToString(CultureInfo.InvariantCulture);
            _loading = false;
        });

        void Commit()
        {
            if (_loading || _selected is null)
            {
                return;
            }

            if (!int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                || value == read(_selected))
            {
                return;
            }

            write(_selected, value);
            after?.Invoke();
            RefreshList();
        }

        box.TextChanged += (_, _) =>
        {
            if (box.IsFocused)
            {
                Commit();
            }
        };

        box.LostFocus += (_, _) => Commit();
        _committers.Add(Commit);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };
    }

    private void BindNumber(TextBox box, Func<ObjectTemplate, double> read, Action<ObjectTemplate, double> write)
    {
        _loaders.Add(template =>
        {
            _loading = true;
            box.Text = TableTextFormat.Number(read(template));
            _loading = false;
        });

        void Commit()
        {
            if (_loading || _selected is null)
            {
                return;
            }

            if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || value.Equals(read(_selected)))
            {
                return;
            }

            write(_selected, value);
        }

        box.TextChanged += (_, _) =>
        {
            if (box.IsFocused)
            {
                Commit();
            }
        };

        box.LostFocus += (_, _) => Commit();
        _committers.Add(Commit);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };
    }

    private void BindBool(CheckBox box, Func<ObjectTemplate, bool> read, Action<ObjectTemplate, bool> write, Action? after = null)
    {
        _loaders.Add(template =>
        {
            _loading = true;
            box.IsChecked = read(template);
            _loading = false;
        });

        box.IsCheckedChanged += (_, _) =>
        {
            if (_loading || _selected is null)
            {
                return;
            }

            var value = box.IsChecked == true;
            if (value == read(_selected))
            {
                return;
            }

            write(_selected, value);
            after?.Invoke();
        };
    }

    private void BindCombo(ComboBox box, IReadOnlyList<string> items,
        Func<ObjectTemplate, string> read, Action<ObjectTemplate, string> write, Action? after = null)
    {
        _loaders.Add(template =>
        {
            _loading = true;
            box.SelectedIndex = IndexOf(items, read(template));
            _loading = false;
        });

        box.SelectionChanged += (_, _) =>
        {
            if (_loading || _selected is null || box.SelectedIndex < 0 || box.SelectedIndex >= items.Count)
            {
                return;
            }

            var display = items[box.SelectedIndex];
            if (display == read(_selected))
            {
                return;
            }

            write(_selected, display);
            after?.Invoke();
        };
    }

    private static int IndexOf(IReadOnlyList<string> items, string value)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] == value)
            {
                return index;
            }
        }

        return 0;
    }

    #endregion
    #region 回填

    private void LoadForm()
    {
        var template = _selected;
        var enabled = template is not null;

        foreach (var control in CollectInputs())
        {
            control.IsEnabled = enabled;
        }

        if (template is null)
        {
            _emptyHint.IsVisible = true;
            _groupsHost.IsVisible = false;
            _idBox.Text = string.Empty;
            _iconBox.Text = string.Empty;
            _nameBox.Text = string.Empty;
            _resNameBox.Text = string.Empty;
            UpdateNineSlicePreview(null);
            UpdateNineSliceEditorState();
            return;
        }

        _emptyHint.IsVisible = false;
        _groupsHost.IsVisible = true;

        _loading = true;
        _idBox.Text = template.TemplateId;
        _iconBox.Text = template.ImagePath;
        foreach (var loader in _loaders)
        {
            loader(template);
        }

        _loading = false;
        UpdateNineSliceEditorState();
        _statusLabel.Text = "修改实时生效，点「保存」写盘并同步场景实例";
    }

    private IEnumerable<Control> CollectInputs() => new Control[]
    {
        _typeBox, _nameBox, _resNameBox, _widthBox, _heightBox, _layerKeyBox,
        _animBox, _loopBox, _speedBox,
        _interactableBox, _interactTypeBox, _bubbleTextBox, _bubbleIdBox,
        _sortOrderBox, _facingBox, _collisionBox, _shadowBox, _displayLayerBox,
        _trafficBox,
        _collectibleBox, _respawnBox,
        _ySortBox, _ySortOffsetBox,
        _movableBox, _moveAnimBox, _loopRestartBox, _hideOffMapBox,
        _teleportBox, _teleportSceneBox, _teleportXBox, _teleportYBox,
        _nineSliceBox, _nineLeftBox, _nineRightBox, _nineTopBox, _nineBottomBox, _nineCenterBox,
    };

    #endregion
}
