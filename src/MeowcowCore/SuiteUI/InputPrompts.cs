using System;
using System.Collections.Generic;
using Il2CppScheduleOne.DevUtilities;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.UI;
using EmbeddedInputPromptUI = Il2CppScheduleOne.UI.Input.EmbeddedInputPromptUI;
using GameInput = Il2CppScheduleOne.GameInput;
using InputPromptsManager = Il2CppScheduleOne.UI.Input.InputPromptsManager;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// Key and button prompts for the suite's UI, for any action. A prompt is two vanilla pieces side by side:
/// vanilla's embedded prompt (gamepad only, as vanilla uses it: B, Circle...) and a slot that holds
/// vanilla's own rounded key cap on keyboard (the same caps as vanilla's corner prompts). Nothing of
/// vanilla's is changed: each piece does what vanilla made it for, and this only chooses which is visible.
/// </summary>
/// <remarks>
/// [CHANGED] New (generalised from the Back button's prompt, which replaced a patch on vanilla's prompt).
/// <para>Making: <see cref="AddKeyCapSlot"/> puts a slot beside an embedded prompt (registry pieces do this
/// once). Using: a window calls <see cref="Shown"/> and <see cref="Hidden"/>, and every prompt inside it
/// follows the device. Each slot shows the key of the embedded prompt beside it, so one rule covers every
/// action.</para>
/// The key is read from the live binding each time a window opens, and a cap is rebuilt only if it changed,
/// so rebinding is followed. One listener on vanilla's device-changed event serves every open window.
/// [SOURCE] Vanilla <c>EmbeddedInputPromptUI</c>, <c>InputPromptsManager.GetPromptImage</c> (the key-cap
/// prefabs behind its prompt panels), <c>GameInput.OnInputDeviceChanged</c>.
/// [PERFORMANCE] A cap is made only when a window opens with a changed key; the listener runs only on a
/// device change, over the open windows' slots.
/// </remarks>
public static class InputPrompts
{
    private const string SlotName = "KeyCap";
    private const string CapPrefix = "Cap:"; // a cap's name carries its key, to spot a change

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Prompts");
    private static readonly List<Transform> OpenSlots = new();
    private static Il2CppSystem.Object? _listener; // kept referenced (IL2CPP rule)

    /// <summary>
    /// Adds an empty key-cap slot right after <paramref name="embeddedPrompt"/>, for its keyboard half.
    /// Hidden until a window shows it; a hidden slot takes no room in a layout row.
    /// </summary>
    public static GameObject AddKeyCapSlot(Transform embeddedPrompt)
    {
        var slot = new GameObject(SlotName);
        slot.AddComponent<RectTransform>();
        slot.AddComponent<LayoutElement>();
        slot.transform.SetParent(embeddedPrompt.parent, false);
        slot.transform.SetSiblingIndex(embeddedPrompt.GetSiblingIndex() + 1);
        slot.SetActive(false);
        return slot;
    }

    /// <summary>A window opened: its prompts' caps are refreshed, shown on keyboard, and follow the device.</summary>
    public static void Shown(Transform? window)
    {
        if (window == null) return;
        try
        {
            ListenOnce();
            bool keyboard = GameInput.GetCurrentInputDeviceIsKeyboardMouse();
            foreach (Transform slot in SlotsIn(window))
            {
                if (!OpenSlots.Contains(slot)) OpenSlots.Add(slot);
                if (keyboard) Refresh(slot);
                slot.gameObject.SetActive(keyboard);
            }
        }
        catch (Exception e)
        {
            Log.Error("Couldn't show key prompts", e);
        }
    }

    /// <summary>A window closed: its prompts stop following the device.</summary>
    public static void Hidden(Transform? window)
    {
        if (window == null) return;
        foreach (Transform slot in SlotsIn(window)) OpenSlots.Remove(slot);
    }

    // ---------- Internals ----------

    private static List<Transform> SlotsIn(Transform window)
    {
        var slots = new List<Transform>();
        foreach (Transform t in window.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == SlotName) slots.Add(t);
        }
        return slots;
    }

    /// <summary>Makes the cap for the slot's action's current key, unless it's already the right one.</summary>
    private static void Refresh(Transform slot)
    {
        string? key = KeyFor(slot);
        if (key == null) return;

        Transform? existing = slot.childCount > 0 ? slot.GetChild(0) : null;
        if (existing != null && existing.name == CapPrefix + key) return; // unchanged

        InputPromptsManager? prompts = Singleton<InputPromptsManager>.Instance;
        RectTransform? parent = slot.TryCast<RectTransform>();
        if (prompts == null || parent == null) return;

        if (existing != null) UObject.DestroyImmediate(existing.gameObject);
        var cap = prompts.GetPromptImage(key, parent);
        if (cap == null) return;
        cap.gameObject.name = CapPrefix + key;

        // The slot takes the cap's size, so a layout row makes room for it (Esc is a wide key).
        RectTransform capRect = cap.GetComponent<RectTransform>();
        capRect.anchorMin = new Vector2(0.5f, 0.5f);
        capRect.anchorMax = new Vector2(0.5f, 0.5f);
        capRect.anchoredPosition = Vector2.zero;
        Vector2 size = capRect.rect.size;
        parent.sizeDelta = size;
        LayoutElement? element = slot.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.minWidth = size.x;
            element.preferredWidth = size.x;
        }
    }

    /// <summary>
    /// The keyboard (or mouse) control of the action shown by the embedded prompt just before the slot, as
    /// vanilla's prompt prefabs name it: "escape" from "&lt;Keyboard&gt;/escape". Null if there's none.
    /// </summary>
    private static string? KeyFor(Transform slot)
    {
        int index = slot.GetSiblingIndex();
        if (index == 0 || slot.parent == null) return null;
        EmbeddedInputPromptUI? prompt = slot.parent.GetChild(index - 1).GetComponent<EmbeddedInputPromptUI>();
        var action = prompt?._actionBindingReference?.ActionReference?.action;
        if (action == null) return null;

        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            string path = bindings[i].effectivePath;
            if (string.IsNullOrEmpty(path) || !(path.Contains("Keyboard") || path.Contains("Mouse"))) continue;
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }
        return null;
    }

    /// <summary>One subscription to vanilla's device-changed event, for the whole run.</summary>
    private static void ListenOnce()
    {
        if (_listener != null) return;
        var handler = UIBuilder.ToIl2Cpp<Il2CppSystem.Action<GameInput.InputDeviceType>>(
            new Action<GameInput.InputDeviceType>(OnDeviceChanged));
        _listener = handler;
        GameInput.OnInputDeviceChanged = Il2CppSystem.Delegate.Combine(GameInput.OnInputDeviceChanged, handler)
            .TryCast<Il2CppSystem.Action<GameInput.InputDeviceType>>();
    }

    /// <summary>Called from native code: exceptions must never escape.</summary>
    private static void OnDeviceChanged(GameInput.InputDeviceType device)
    {
        try
        {
            bool keyboard = device == GameInput.InputDeviceType.KeyboardMouse;
            for (int i = OpenSlots.Count - 1; i >= 0; i--)
            {
                Transform slot = OpenSlots[i];
                if (slot == null)
                {
                    OpenSlots.RemoveAt(i); // its window is gone
                    continue;
                }
                if (keyboard) Refresh(slot);
                slot.gameObject.SetActive(keyboard);
            }
        }
        catch (Exception e)
        {
            Log.Error("Couldn't switch key prompts", e);
        }
    }
}