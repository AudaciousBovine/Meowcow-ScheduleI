using System;
using System.Collections.Generic;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GameInput = Il2CppScheduleOne.GameInput;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// A page's description panel (design §9): on the right of the page, showing the name and description of
/// the row that's selected or hovered, so nothing covers a setting. Invisible unless a row with a
/// description is selected. Rows are wired to it by <see cref="WindowPage"/>.
/// </summary>
/// <remarks>
/// [CHANGED] New (Settings rework slice 3).
/// [CHANGED] Two sources, one per device: with a mouse, the hovered row (<see cref="RowHover"/>); with a
/// gamepad, the selected control (vanilla's UISelectable OnSelected/OnDeselected). A hover wins while there
/// is one. Selection is ignored with a mouse, because the page auto-selects its first row when it opens and
/// that read as a stuck panel. Switching device drops the other device's source (see <see cref="OnDeviceChanged"/>).
/// [CHANGED] Sources are keyed by the exact control (or row) that raised them, so moving between two controls
/// in one row can't hide the panel whichever order vanilla sends Select and Deselect in.
/// [PERFORMANCE] Built once per page that has a description. Event-driven only: nothing runs per frame. Text
/// is rewritten only when the shown source changes (two TMP rebuilds), not on every event.
/// </remarks>
internal sealed class DescriptionPanel
{
    private const float Width = 320f;
    private const float Inset = 10f;
    private const float Padding = 16f;
    private const float Spacing = 8f;
    private const float BodySizeRatio = 0.85f;
    private static readonly Color Shade = new(0f, 0f, 0f, 0.25f); // like the unselected subtabs
    private static readonly Color BodyColor = new(1f, 1f, 1f, 0.85f);

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Window");

    /// <summary>Every panel built, for the device-changed hook. Dead ones are pruned as they're found.</summary>
    private static readonly List<DescriptionPanel> Live = new();
    private static Il2CppSystem.Object? _listener; // kept referenced (IL2CPP rule)

    /// <summary>What a row's description shows: its title and text, made once per row.</summary>
    internal sealed class Entry
    {
        public Entry(string title, string text)
        {
            Title = title;
            Text = text;
        }

        public string Title { get; }
        public string Text { get; }
    }

    private readonly GameObject _root;
    private readonly TMP_Text _title;
    private readonly TMP_Text _body;

    // [CHANGED] Replaces the single owner. Compared by reference: each owner is the same captured wrapper
    // every time its event fires, so no native call is needed to compare.
    private object? _hoverOwner;
    private Entry? _hover;
    private object? _selectionOwner;
    private Entry? _selection;
    private Entry? _shown; // what the text currently holds, to skip rewriting it

    /// <summary>The page's described controls, to find the selected one when a gamepad takes over.</summary>
    private readonly List<(object Owner, GameObject Control, Entry Entry)> _selectables = new();

    private DescriptionPanel(GameObject root, TMP_Text title, TMP_Text body)
    {
        _root = root;
        _title = title;
        _body = body;
    }

    /// <summary>
    /// Builds the panel on the right of <paramref name="page"/>, hidden, with text in the font of
    /// <paramref name="style"/> (a row label). Its background takes the page's own shape (rounded corners).
    /// </summary>
    internal static DescriptionPanel Create(Transform page, TMP_Text style)
    {
        var root = new GameObject("DescriptionPanel");
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.SetParent(page, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(Width, -Inset * 2f);
        rect.anchoredPosition = new Vector2(-Inset, 0f);

        Image background = root.AddComponent<Image>();
        Image? pageImage = page.GetComponent<Image>();
        if (pageImage != null && pageImage.sprite != null)
        {
            background.sprite = pageImage.sprite;
            background.type = pageImage.type;
            background.pixelsPerUnitMultiplier = pageImage.pixelsPerUnitMultiplier;
        }
        background.color = Shade;
        background.raycastTarget = false;

        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset { left = (int)Padding, right = (int)Padding, top = (int)Padding, bottom = (int)Padding };
        layout.spacing = Spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text title = AddText(root.transform, "Title", style, style.fontSize, Color.white);
        title.fontStyle = FontStyles.Bold;
        TMP_Text body = AddText(root.transform, "Description", style, style.fontSize * BodySizeRatio, BodyColor);

        root.SetActive(false);
        var panel = new DescriptionPanel(root, title, body);
        Live.RemoveAll(p => p._root == null); // [CHANGED] windows rebuilt since: their panels are gone
        Live.Add(panel);
        ListenOnce();
        return panel;
    }

    // ---------- Sources ----------

    /// <summary>The mouse entered a described row. [CHANGED] New.</summary>
    internal void Hover(object row, Entry entry)
    {
        _hoverOwner = row;
        _hover = entry;
        Refresh();
    }

    /// <summary>The mouse left a row; ignored if another row has taken over. [CHANGED] New.</summary>
    internal void Unhover(object row)
    {
        if (!ReferenceEquals(_hoverOwner, row)) return;
        _hoverOwner = null;
        _hover = null;
        Refresh();
    }

    /// <summary>
    /// Remembers a described control, so the right one shows when a gamepad takes over mid-page.
    /// [CHANGED] New.
    /// </summary>
    /// <param name="owner">The object later passed to Select/Deselect for this control.</param>
    internal void Track(object owner, GameObject control, Entry entry) => _selectables.Add((owner, control, entry));

    /// <summary>A described control was selected. Acted on only with a gamepad. [CHANGED] Was Show.</summary>
    internal void Select(object control, Entry entry)
    {
        if (!GameInput.GetCurrentInputDeviceIsGamepad()) return;
        _selectionOwner = control;
        _selection = entry;
        Refresh();
    }

    /// <summary>A control was deselected; ignored if another has taken over. [CHANGED] Was Hide (keyed by row).</summary>
    internal void Deselect(object control)
    {
        if (!ReferenceEquals(_selectionOwner, control)) return;
        _selectionOwner = null;
        _selection = null;
        Refresh();
    }

    /// <summary>Hides the panel and forgets both sources (the page was left).</summary>
    internal void Clear()
    {
        _hoverOwner = null;
        _hover = null;
        _selectionOwner = null;
        _selection = null;
        Refresh();
    }

    /// <summary>Shows the hovered row's description, else the selected control's, else nothing.</summary>
    private void Refresh()
    {
        if (_root == null) return; // its window is gone
        Entry? wanted = _hover ?? _selection;
        if (wanted == null)
        {
            _shown = null;
            if (_root.activeSelf) _root.SetActive(false);
            return;
        }

        if (!ReferenceEquals(wanted, _shown))
        {
            _title.text = wanted.Title;
            _body.text = wanted.Text;
            _shown = wanted;
        }
        if (!_root.activeSelf) _root.SetActive(true);
    }

    // ---------- Device changes ----------

    /// <summary>One subscription to vanilla's device-changed event, for the whole run.</summary>
    /// <remarks>[SOURCE] Same pattern as <see cref="InputPrompts"/>.</remarks>
    private static void ListenOnce()
    {
        if (_listener != null) return;
        var handler = UIBuilder.ToIl2Cpp<Il2CppSystem.Action<GameInput.InputDeviceType>>(
            new Action<GameInput.InputDeviceType>(OnDeviceChanged));
        _listener = handler;
        GameInput.OnInputDeviceChanged = Il2CppSystem.Delegate.Combine(GameInput.OnInputDeviceChanged, handler)
            .TryCast<Il2CppSystem.Action<GameInput.InputDeviceType>>();
    }

    /// <summary>
    /// To a mouse: the selection is dropped (hover takes over). To a gamepad: the hover is dropped, and the
    /// control already selected shows (the page may have selected it while the mouse was in use).
    /// </summary>
    /// <remarks>
    /// Called from native code: exceptions must never escape.
    /// [PERFORMANCE] Only on a device change: a pass over the live panels, and over a panel's controls only
    /// if it's showing.
    /// </remarks>
    private static void OnDeviceChanged(GameInput.InputDeviceType device)
    {
        try
        {
            bool gamepad = device != GameInput.InputDeviceType.KeyboardMouse;
            GameObject? selected = gamepad ? EventSystem.current?.currentSelectedGameObject : null;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                DescriptionPanel panel = Live[i];
                if (panel._root == null)
                {
                    Live.RemoveAt(i); // its window is gone
                    continue;
                }
                panel.SwitchDevice(gamepad, selected);
            }
        }
        catch (Exception e)
        {
            Log.Error("Couldn't switch description panels", e);
        }
    }

    private void SwitchDevice(bool gamepad, GameObject? selected)
    {
        if (gamepad)
        {
            _hoverOwner = null;
            _hover = null;
            if (selected != null && _root.transform.parent != null && _root.transform.parent.gameObject.activeInHierarchy)
            {
                foreach ((object owner, GameObject control, Entry entry) in _selectables)
                {
                    if (control != null && control.Pointer == selected.Pointer)
                    {
                        _selectionOwner = owner;
                        _selection = entry;
                        break;
                    }
                }
            }
        }
        else
        {
            _selectionOwner = null;
            _selection = null;
        }
        Refresh();
    }

    private static TMP_Text AddText(Transform parent, string name, TMP_Text style, float size, Color color)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>().SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSharedMaterial = style.fontSharedMaterial;
        text.fontSize = size;
        text.color = color;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
        return text;
    }
}