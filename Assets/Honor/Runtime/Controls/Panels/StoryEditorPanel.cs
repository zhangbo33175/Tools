/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryEditorPanel.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情编辑页：节点列表 + 节点详情 + 演出与自检，剧情随地图同名落盘
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 「剧情编辑」页：左边是节点树，中间写这一节点说什么，右边配演出与分支并做自检。
/// <para>一份地图对应一份 <c>.story.json</c>，节点之间靠 <see cref="StoryNode.NextId"/> 串起来，
/// 选项节点额外带分支；改动即时写回内存文档，「保存剧情」才会落盘。</para>
/// </summary>
public sealed partial class StoryEditorPanel : UserControl
{
    #region 字段

    private readonly Action<string> _notify;
    private StoryDocument _doc = new();
    private MapConfigData? _config;
    private AssetLibrary? _assets;
    private ItemConfigSet? _itemTable;
    private IReadOnlyList<StorySpeaker> _speakers = Array.Empty<StorySpeaker>();
    private string _mapPath = string.Empty;
    private string _savedSnapshot = string.Empty;
    private bool _dirty;
    private bool _touched;
    private bool _sync;
    private StoryNode? _node;

    private TextBlock _headText = null!;
    private TextBlock _mapText = null!;
    private TextBlock _nodeCountText = null!;
    private TextBlock _footerText = null!;
    private TextBlock _statText = null!;
    private TextBlock _issuesText = null!;
    private TextBlock _optionEmpty = null!;
    private ListBox _list = null!;
    private TextBox _titleBox = null!;
    private TextBox _idText = null!;
    private TextBox _speakerBox = null!;
    private TextBox _textBox = null!;
    private TextBox _noteBox = null!;
    private TextBox _waitBox = null!;
    private ComboBox _kindCombo = null!;
    private ComboBox _speakerPick = null!;
    private ComboBox _nextCombo = null!;
    private ComboBox _portraitCombo = null!;
    private ComboBox _effectCombo = null!;
    private CheckBox _showPortraitCheck = null!;
    private StackPanel _choicesHost = null!;

    #endregion

    #region 构造

    /// <summary>构造剧情编辑页。</summary>
    /// <param name="notify">回写状态栏文案的回调。</param>
    public StoryEditorPanel(Action<string> notify)
    {
        _notify = notify;
        Content = BuildLayout();
    }

    #endregion

    #region 挂载

    /// <summary>切换地图后重新挂载：读同目录剧情文件，刷新节点列表与详情。</summary>
    /// <param name="config">当前地图数据，用于取角色当说话人。</param>
    /// <param name="assets">资源库，预览立绘用。</param>
    /// <param name="mapPath">当前地图 JSON 路径；为空时只显示空白剧情。</param>
    public void Attach(MapConfigData? config, AssetLibrary? assets, string mapPath)
    {
        _config = config;
        _assets = assets;
        _mapPath = mapPath ?? string.Empty;
        RebuildSpeakers();
        _doc = StoryService.Load(_mapPath);
        _savedSnapshot = StoryService.Serialize(_doc);
        _dirty = false;
        _touched = false;
        _node = _doc.Start;

        _sync = true;
        _titleBox.Text = _doc.Title;
        _sync = false;

        RebuildNodeList(_node?.Id);
        RefreshInspector();
        RefreshFooter();
        SetMessage(string.IsNullOrWhiteSpace(_mapPath)
            ? "还没打开地图：剧情文件要跟着地图存，先载入一张地图"
            : StoryService.Exists(_mapPath)
                ? $"已载入剧情：{System.IO.Path.GetFileName(StoryService.PathFor(_mapPath))}"
                : "这张地图还没有剧情文件，已给出空白开头，保存后会生成 .story.json");
    }

    /// <summary>切页时同步上下文：同一张地图只换数据引用，换了地图才重新读剧情文件。</summary>
    /// <param name="config">当前地图数据。</param>
    /// <param name="assets">资源库。</param>
    /// <param name="mapPath">当前地图路径。</param>
    public void SyncMap(MapConfigData? config, AssetLibrary? assets, string mapPath)
    {
        var path = mapPath ?? string.Empty;
        if (!string.Equals(_mapPath, path, StringComparison.Ordinal))
        {
            Attach(config, assets, path);
            return;
        }

        _config = config;
        _assets = assets;
        RebuildSpeakers();
    }

    /// <summary>注入物件配置表：说话人下拉靠它把物件解析成独有的数字 ID。</summary>
    /// <param name="table">物件配置表，可为空。</param>
    public void SetItemTable(ItemConfigSet? table)
    {
        if (ReferenceEquals(_itemTable, table))
        {
            return;
        }

        _itemTable = table;
        RebuildSpeakers();
        FillSpeakerPick(_node);
    }

    /// <summary>按当前地图与配置表重建可绑定角色索引。</summary>
    private void RebuildSpeakers()
    {
        _speakers = StorySpeakerBook.Build(_config, _itemTable, _assets?.Roots);
    }

    /// <summary>地图保存后顺带落盘剧情；没有改动时什么都不做。</summary>
    /// <returns>写入的文件路径；未写入返回空串。</returns>
    public string SaveIfDirty()
    {
        if (!_dirty || !_touched || string.IsNullOrWhiteSpace(_mapPath))
        {
            return string.Empty;
        }

        return SaveStory(silent: true);
    }


    #endregion

    #region 列表

    /// <summary>重建节点列表，并尽量把选中项落回指定节点。</summary>
    /// <param name="selectId">要选中的节点 ID，可空。</param>
    private void RebuildNodeList(string? selectId = null)
    {
        var target = selectId ?? _node?.Id;
        _sync = true;
        _list.ItemsSource = _doc.Nodes.ToList();
        _list.SelectedItem = _doc.Nodes.FirstOrDefault(node => node.Id == target) ?? _doc.Nodes.FirstOrDefault();
        _node = _list.SelectedItem as StoryNode;
        _sync = false;
        RefreshStats();
    }

    /// <summary>列表选中项变化：切节点并回填右侧表单。</summary>
    private void OnNodeSelected()
    {
        if (_sync)
        {
            return;
        }

        _node = _list.SelectedItem as StoryNode;
        RefreshInspector();
    }

    #endregion

    #region 节点操作

    /// <summary>在当前节点后插入一个新对白节点，并把主线自动接上。</summary>
    private void AddNode()
    {
        var id = StoryService.NextId(_doc);
        var node = new StoryNode
        {
            Id = id,
            Kind = StoryNodeKinds.Line,
            Text = string.Empty,
        };

        var index = _node is null ? _doc.Nodes.Count : _doc.Nodes.IndexOf(_node) + 1;
        index = Math.Clamp(index, 0, _doc.Nodes.Count);

        // 新节点接管当前节点的后续，前一个节点改为指向新节点，保证主线不断
        if (_node is not null)
        {
            node.NextId = _node.NextId;
            if (_node.Kind is StoryNodeKinds.Line or StoryNodeKinds.Narration or StoryNodeKinds.Choice)
            {
                _node.NextId = id;
            }
        }

        _doc.Nodes.Insert(index, node);
        _dirty = true;
        _node = node;
        RebuildNodeList(id);
        RefreshInspector();
        RefreshFooter();
        SetMessage($"已新增节点 {id}，可直接写对白");
    }

    /// <summary>删除当前节点，并把前一节点接到它的后续，避免主线断开。</summary>
    private void DeleteNode()
    {
        if (_node is null)
        {
            SetMessage("先选中一个节点再删除");
            return;
        }

        var index = _doc.Nodes.IndexOf(_node);
        var removed = _node;
        var previous = index > 0 ? _doc.Nodes[index - 1] : null;

        if (previous is not null && previous.NextId == removed.Id)
        {
            previous.NextId = removed.NextId;
        }

        // 其他节点指向它的分支一并改指向它的后续，避免出现悬空跳转
        foreach (var node in _doc.Nodes.Where(n => n != removed))
        {
            if (node.NextId == removed.Id)
            {
                node.NextId = removed.NextId;
            }

            foreach (var choice in node.Choices.Where(c => c.TargetId == removed.Id))
            {
                choice.TargetId = removed.NextId;
            }
        }

        _doc.Nodes.Remove(removed);
        if (_doc.StartNodeId == removed.Id)
        {
            _doc.StartNodeId = _doc.Nodes.Count > 0 ? _doc.Nodes[0].Id : string.Empty;
        }

        _dirty = true;
        _node = _doc.Nodes.Count > 0 ? _doc.Nodes[Math.Clamp(index, 0, _doc.Nodes.Count - 1)] : null;
        RebuildNodeList(_node?.Id);
        RefreshInspector();
        RefreshFooter();
        SetMessage($"已删除节点 {removed.Id}");
    }

    /// <summary>在列表里上移或下移一格，只改展示顺序，不改跳转关系。</summary>
    /// <param name="delta">-1 上移，1 下移。</param>
    private void MoveNode(int delta)
    {
        if (_node is null)
        {
            return;
        }

        var index = _doc.Nodes.IndexOf(_node);
        var target = index + delta;
        if (target < 0 || target >= _doc.Nodes.Count)
        {
            return;
        }

        _doc.Nodes.RemoveAt(index);
        _doc.Nodes.Insert(target, _node);
        _dirty = true;
        RebuildNodeList(_node.Id);
        RefreshFooter();
    }

    /// <summary>把当前节点设为剧情起点。</summary>
    private void MarkAsStart()
    {
        if (_node is null)
        {
            return;
        }

        _doc.StartNodeId = _node.Id;
        _dirty = true;
        RebuildNodeList(_node.Id);
        RefreshFooter();
        SetMessage($"已把 {_node.Id} 设为剧情起点");
    }

    #endregion

    #region 保存与预览

    /// <summary>「保存剧情」按钮：写到地图同目录的 <c>.story.json</c>，覆盖前留 .bak。</summary>
    private void SaveStoryFromButton()
    {
        if (SaveStory(silent: false).Length == 0)
        {
            return;
        }

        RefreshFooter();
    }

    /// <summary>落盘并提示。</summary>
    /// <param name="silent">true 时由调用方决定提示文案。</param>
    /// <returns>写入路径；无地图时返回空串。</returns>
    private string SaveStory(bool silent)
    {
        if (string.IsNullOrWhiteSpace(_mapPath))
        {
            SetMessage("没有打开地图：剧情要跟着地图同名保存，先载入一张地图");
            return string.Empty;
        }

        try
        {
            var path = StoryService.Save(_doc, _mapPath);
            _savedSnapshot = StoryService.Serialize(_doc);
            _dirty = false;
            if (!silent)
            {
                SetMessage($"剧情已保存：{path}");
            }

            return path;
        }
        catch (Exception ex)
        {
            SetMessage($"剧情保存失败：{ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>「预览播放」按钮：用当前文档开剧情预览窗，未保存的改动也会一起播。</summary>
    private void OpenPreview()
    {
        if (_doc.Nodes.Count == 0)
        {
            SetMessage("剧情还没有节点，先新增一个节点");
            return;
        }

        var window = new StoryPreviewWindow(_doc, _config, _assets, _speakers);
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        SetMessage("已打开剧情预览：点击对话框或按空格推进，选项会停下等选择");
    }

    #endregion

    #region 提示

    /// <summary>写状态栏。</summary>
    /// <param name="message">提示文案。</param>
    private void SetMessage(string message) => _notify(message);

    #endregion
}