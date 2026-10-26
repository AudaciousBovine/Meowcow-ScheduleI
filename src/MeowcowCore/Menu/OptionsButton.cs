using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UObject = UnityEngine.Object;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UISelectable = Il2CppScheduleOne.UISelectable;

namespace MeowcowCore.Menu
{
    /// <summary>Adds "Meowcow Options" to the main menu list, right after Settings.</summary>
    internal static class OptionsButton
    {
        private const string Label = "Meowcow Options";
        private const float LabelMargin = 10f;

        private static UnityAction _onClick; // kept referenced so the converted delegate isn't collected

        internal static void Add(RectTransform panel)
        {
            var settings = panel.Find(MenuPaths.SettingsButton);
            if (settings == null) { CoreMod.Log.Warning("Settings button not found; Meowcow Options not added"); return; }

            var go = UObject.Instantiate(settings.gameObject, panel);
            go.name = MenuPaths.OptionsButtonName;
            go.transform.SetSiblingIndex(settings.GetSiblingIndex() + 1);

            // The clone copies Settings' scene wiring (SettingsScreen.Open), which RemoveAllListeners
            // can't clear; a fresh event drops it.
            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            _onClick ??= DelegateSupport.ConvertDelegate<UnityAction>(new Action(OptionsScreen.Open));
            button.onClick.AddListener(_onClick);

            var labelT = go.transform.Find(MenuPaths.ButtonText);
            var label = labelT != null ? labelT.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.text = Label;
                label.enableWordWrapping = false;
            }

            var highlighted = settings.Find(MenuPaths.ButtonTextHighlighted);
            float slide = highlighted != null ? Mathf.Abs(highlighted.TryCast<RectTransform>().anchoredPosition.x) : 15f;

            GrowPanel(panel, settings, label, slide);
            RegisterForGamepad(panel, settings, go);
        }

        /// <summary>One button taller (top edge stays put), and wide enough for the new label (left edge stays put).</summary>
        private static void GrowPanel(RectTransform panel, Transform settings, TMP_Text label, float slide)
        {
            var group = panel.GetComponent<VerticalLayoutGroup>();
            float spacing = group != null ? group.spacing : 0f;
            float step = settings.TryCast<RectTransform>().rect.height + spacing;

            var size = panel.sizeDelta;
            var pos = panel.anchoredPosition;
            var pivot = panel.pivot;

            size.y += step;
            pos.y -= step * (1f - pivot.y);

            if (label != null)
            {
                float needed = label.preferredWidth + slide + LabelMargin;
                if (needed > size.x)
                {
                    float extra = needed - size.x;
                    size.x = needed;
                    pos.x += extra * pivot.x;

                    // If the layout group doesn't size the buttons, widen them ourselves.
                    if (group != null && !group.childControlWidth)
                        for (int i = 0; i < panel.childCount; i++)
                        {
                            var child = panel.GetChild(i).TryCast<RectTransform>();
                            if (child != null) child.sizeDelta = new Vector2(needed, child.sizeDelta.y);
                        }
                }
            }

            panel.sizeDelta = size;
            panel.anchoredPosition = pos;
        }

        /// <summary>Adds the new button to the list's gamepad panel, right after Settings.</summary>
        private static void RegisterForGamepad(RectTransform panel, Transform settings, GameObject go)
        {
            try
            {
                var uiPanel = panel.GetComponent<UIPanel>();
                var added = go.GetComponent<UISelectable>();
                var original = settings.GetComponent<UISelectable>();
                if (uiPanel == null || added == null)
                {
                    CoreMod.Log.Warning("List has no gamepad panel; Meowcow Options may not be reachable by gamepad");
                    return;
                }

                var list = uiPanel.selectables;
                list.Remove(added); // in case it registered itself on Awake
                int index = original != null ? list.IndexOf(original) : -1;
                if (index >= 0) list.Insert(index + 1, added);
                else uiPanel.AddSelectable(added);
            }
            catch (Exception e)
            {
                CoreMod.Log.Warning($"Couldn't register Meowcow Options for gamepad: {e.Message}");
            }
        }
    }
}
