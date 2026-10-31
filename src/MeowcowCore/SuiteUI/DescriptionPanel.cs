using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// A page's description panel (design §9): on the right of the page, showing the name and description of
/// the row that's selected or hovered, so nothing covers a setting. Invisible unless a row with a
/// description is selected. Rows are wired to it by <see cref="WindowPage"/>.
/// </summary>
/// <remarks>
/// [CHANGED] New (Settings rework slice 3). Shown and hidden by rows' own selection events (vanilla's
/// UISelectable OnSelected/OnDeselected; with a mouse, hovering a row selects it), so nothing runs per frame.
/// [PERFORMANCE] Built once per page that has a description; two text writes per selection.
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

    private readonly GameObject _root;
    private readonly TMP_Text _title;
    private readonly TMP_Text _body;
    private Transform? _owner; // the row whose description is showing

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
        return new DescriptionPanel(root, title, body);
    }

    /// <summary>Shows a row's name and description.</summary>
    internal void Show(Transform owner, string title, string description)
    {
        _owner = owner;
        _title.text = title;
        _body.text = description;
        _root.SetActive(true);
    }

    /// <summary>Hides the panel, if it's showing this row's description (another row may have taken over).</summary>
    internal void Hide(Transform owner)
    {
        if (_owner == owner) Clear();
    }

    /// <summary>Hides the panel, whatever it shows (the page was left).</summary>
    internal void Clear()
    {
        _owner = null;
        if (_root != null) _root.SetActive(false);
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