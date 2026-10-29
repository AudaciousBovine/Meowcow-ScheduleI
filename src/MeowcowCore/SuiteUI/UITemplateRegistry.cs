using System;
using System.Collections.Generic;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.UI;
using ActionBindingReference = Il2CppScheduleOne.CustomUI.ActionBindingReference;
using EmbeddedInputPromptUI = Il2CppScheduleOne.UI.Input.EmbeddedInputPromptUI;
using GameInput = Il2CppScheduleOne.GameInput;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// The suite's library of ready-made UI pieces, kept for the whole run so every scene builds from the same
/// ones (design §7). Each piece is made once from vanilla's parts, wherever they exist (the main menu), and
/// kept hidden in a holder that survives scene changes. Windows clone pieces from here instead of searching
/// the current scene, so a piece looks and behaves the same in the main menu and in-game.
/// </summary>
/// <remarks>
/// [CHANGED] New (Settings rework slice 2a). First piece: the Back button, with its own live key prompt.
/// The holder is inactive, so nothing on a piece runs until a window clones it.
/// [PERFORMANCE] Pieces are made once per run; each use is one clone.
/// </remarks>
public static class UITemplateRegistry
{
    /// <summary>
/// The window Back button: the save slots' dark button, a live Back prompt (vanilla's gamepad prompt, or its
/// key cap on keyboard; see <see cref="InputPrompts"/>), and "Back".
/// </summary>
    public const string BackButton = "button.back";

    private const string BackText = "Back";
    private const float BackSpacing = 8f;     // between the prompt and the text
    private const float BackPadding = 10f;    // inside the button, either side

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Registry");
    private static readonly Dictionary<string, GameObject> Pieces = new(StringComparer.Ordinal);
    private static GameObject? _holder;

    /// <summary>
    /// Makes any pieces not made yet, from what the current scene has. Called at each main menu load, so a
    /// piece that couldn't be made (its vanilla source missing) is tried again next time.
    /// </summary>
    internal static void CaptureMissing()
    {
        if (!Pieces.ContainsKey(BackButton))
        {
            try
            {
                GameObject? back = MakeBackButton();
                if (back != null)
                {
                    Pieces[BackButton] = back;
                    Log.Debug("Made the Back button");
                }
            }
            catch (Exception e)
            {
                Log.Error("Couldn't make the Back button; windows will have none (Escape still works)", e);
            }
        }
    }

    /// <summary>A copy of a piece under <paramref name="parent"/>, or null if the piece doesn't exist.</summary>
    public static GameObject? Create(string key, Transform parent, string name)
    {
        if (!Pieces.TryGetValue(key, out GameObject? piece) || piece == null) return null;
        GameObject copy = UObject.Instantiate(piece, parent);
        copy.name = name;
        return copy;
    }

    /// <summary>The hidden holder, made on first use and kept across scene changes.</summary>
    private static Transform Holder()
    {
        if (_holder == null)
        {
            _holder = new GameObject("MeowcowTemplates");
            UObject.DontDestroyOnLoad(_holder);
            _holder.SetActive(false); // nothing on a piece wakes up while it's stored
        }
        return _holder.transform;
    }

    // ---------- Pieces ----------

    /// <summary>
    /// The Back button: a copy of the save slots' Import button (look only), holding a copy of a bumper
    /// prompt pointed at the game's exit action, then the word "Back".
    /// </summary>
    /// <remarks>
    /// The prompt is vanilla's self-contained <c>EmbeddedInputPromptUI</c>: it asks the game's prompt
    /// manager for the action's binding on the current device (empty binding id), and redraws when the
    /// device changes. So it shows Esc, B or Circle, and follows rebinding, by itself.
    /// [SOURCE] Vanilla <c>EmbeddedInputPromptUI</c>, <c>InputPromptsManager.GetBindingDataFromActionReference</c>,
    /// <c>GameInput.PrimaryExitAction</c> (what Escape and B trigger).
    /// </remarks>
    private static GameObject? MakeBackButton()
    {
        Button? style = UIPathIndex.FindImportButton();
        Transform? promptSource = UIPathIndex.FindSettingsScreen()?.transform.Find(UIPathIndex.WindowLeftBumper);
        GameInput? input = Il2CppScheduleOne.DevUtilities.Singleton<GameInput>.Instance;
        if (style == null || promptSource == null || input == null || input.PrimaryExitAction == null)
        {
            Log.Debug("Back button sources not here yet; will try again at the next menu");
            return null;
        }

        // The button: look only (its import script and any setting script removed).
        GameObject button = UObject.Instantiate(style.gameObject, Holder());
        button.name = "BackButton";
        UIBuilder.StripToLook(button);

        TMP_Text? label = null;
        var texts = button.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0)
        {
            label = texts[0];
            label.text = BackText;
            label.enableWordWrapping = false;

            // [CHANGED] The same size as vanilla's own prompt text ("Back" in the corner), which matches its
            // key caps; the Import button's text is smaller.
            float? promptSize = VanillaPromptTextSize();
            if (promptSize.HasValue) label.fontSize = promptSize.Value;
        }

        // The prompt: vanilla's embedded prompt, minus the script that hides it on some platforms.
        GameObject prompt = UObject.Instantiate(promptSource.gameObject, button.transform);
        prompt.name = "Prompt";
        foreach (MonoBehaviour behaviour in prompt.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.GetIl2CppType().Name == "PlatformConditionalActive")
                UObject.DestroyImmediate(behaviour);
        }

        EmbeddedInputPromptUI? embedded = prompt.GetComponent<EmbeddedInputPromptUI>();
        if (embedded == null)
        {
            Log.Warning("The bumper prompt has no prompt script; Back button made without a prompt");
            UObject.DestroyImmediate(prompt);
        }
        else
        {
            embedded._runOnlyWithGamepad = true; // [CHANGED] gamepad only, as vanilla uses it; keys get the key cap
            embedded._actionBindingReference = new ActionBindingReference
            {
                ActionReference = input.PrimaryExitAction,
                BindingId = string.Empty, // empty: the current device's binding
            };
        }

        // Prompt, then text, centred side by side.
        HorizontalLayoutGroup row = button.GetComponent<HorizontalLayoutGroup>() ?? button.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = BackSpacing;
        row.padding = new RectOffset { left = (int)BackPadding, right = (int)BackPadding };
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        if (embedded != null) prompt.transform.SetAsFirstSibling();

        // [CHANGED] The keyboard half: an empty slot after the gamepad prompt, filled with vanilla's key cap
        // when a window opens (InputPrompts). Without a gamepad prompt there's no action to show.
        GameObject? slot = embedded != null ? InputPrompts.AddKeyCapSlot(prompt.transform) : null;

        // Anything else in the button (a selection frame, a shadow) keeps its own place, outside the row.
        for (int i = 0; i < button.transform.childCount; i++)
        {
            Transform child = button.transform.GetChild(i);
            if (child == prompt.transform || (slot != null && child == slot.transform)
                || (label != null && child == label.transform)) continue;
            LayoutElement other = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            other.ignoreLayout = true;
        }
        if (label != null)
        {
            LayoutElement element = label.gameObject.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = label.preferredWidth;
            label.rectTransform.sizeDelta = new Vector2(label.preferredWidth, label.rectTransform.sizeDelta.y);
        }

        return button;
    }

    /// <summary>
    /// The font size of vanilla's prompt text, from the main menu's corner prompt (its "Back"). Null if the
    /// corner prompt isn't showing.
    /// </summary>
    private static float? VanillaPromptTextSize()
    {
        GameObject? corner = GameObject.Find(UIPathIndex.MenuCornerPrompt);
        if (corner == null) return null;
        foreach (TMP_Text text in corner.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == UIPathIndex.PromptLabel) return text.fontSize;
        }
        return null;
    }
}