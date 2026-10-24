using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UObject = UnityEngine.Object;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UISelectable = Il2CppScheduleOne.UISelectable;
using CreatorUI = Il2CppScheduleOne.CharacterCreator.CharacterCreatorInterface;

namespace MeowcowMirror.Layout
{
    /// <summary>
    /// Rebuilds the character creator's pages from a LayoutDefinition: moves vanilla rows between
    /// containers, re-registers their gamepad selectables, and assigns a new _windows array.
    /// </summary>
    public static class CreatorLayoutBuilder
    {
        private static CreatorUI _ui;
        private static readonly Dictionary<string, CreatorUI.Window> _vanillaWindows = new Dictionary<string, CreatorUI.Window>();
        private static readonly Dictionary<string, Transform> _rows = new Dictionary<string, Transform>();
        private static readonly Dictionary<string, UISelectable[]> _rowSelectables = new Dictionary<string, UISelectable[]>();
        private static readonly Dictionary<string, RectTransform> _customContainers = new Dictionary<string, RectTransform>();
        private static readonly Dictionary<string, UIPanel> _customPanels = new Dictionary<string, UIPanel>();
        private static RectTransform _template;
        private static Transform _parking;

        // Measured from the vanilla Body tab so rebuilt pages match the game's spacing.
        private static float _padTop = 10f;
        private static float _spacing = 10f;

        private sealed class BuiltTab
        {
            public TabDefinition Tab;
            public RectTransform Container;
            public UIPanel Panel;
            public readonly List<string> Placed = new List<string>();
        }

        public static CreatorUI FindInterface() => UObject.FindObjectOfType<CreatorUI>(true);

        public static bool Apply(LayoutDefinition layout)
        {
            var ui = FindInterface();
            if (ui == null) { MirrorMod.Log.Warning("CharacterCreatorInterface not found"); return false; }
            if (_ui == null || _ui != ui) Capture(ui);

            List<TabDefinition> tabs = Normalize(layout);
            HideAllContainers();

            var built = new List<BuiltTab>();
            var placed = new HashSet<string>();

            foreach (var tab in tabs)
            {
                var (container, panel) = GetContainer(tab.Id);
                var entry = new BuiltTab { Tab = tab, Container = container, Panel = panel };

                foreach (var optionId in tab.Options)
                {
                    if (!_rows.TryGetValue(optionId, out var row)) { MirrorMod.Log.Warning($"Unknown option '{optionId}' in tab '{tab.Id}'"); continue; }
                    if (!placed.Add(optionId)) { MirrorMod.Log.Warning($"Option '{optionId}' listed twice; keeping first"); continue; }
                    row.SetParent(container, false);
                    row.SetAsLastSibling();
                    entry.Placed.Add(optionId);
                }

                built.Add(entry);
            }

            // Anything the layout doesn't mention is parked out of sight.
            foreach (var kv in _rows)
                if (!placed.Contains(kv.Key)) kv.Value.SetParent(_parking, false);

            foreach (var b in built)
                if (NeedsLayout(b)) EnsureLayoutGroup(b.Container);

            RebuildPanels(built);

            var windows = built
                .Select(b => new CreatorUI.Window { Name = b.Tab.Name, Container = b.Container, Panel = b.Panel })
                .ToArray();

            ui._windows = new Il2CppReferenceArray<CreatorUI.Window>(windows);
            ui.OpenWindow(0);

            MirrorMod.Log.Msg($"Layout applied: {windows.Length} tabs, {placed.Count} options, {_rows.Count - placed.Count} hidden");
            return true;
        }

        /// <summary>Records the vanilla windows, rows and selectables once per interface instance (rows must still be in place).</summary>
        private static void Capture(CreatorUI ui)
        {
            _ui = ui;
            _vanillaWindows.Clear();
            _rows.Clear();
            _rowSelectables.Clear();
            _customContainers.Clear();
            _customPanels.Clear();

            var windows = ui._windows;
            for (int i = 0; i < windows.Length; i++)
            {
                var w = windows[i];
                _vanillaWindows[w.Name.ToLowerInvariant()] = w;

                bool hasPanel = w.Panel != null;
                bool onContainer = hasPanel && w.Panel.gameObject == w.Container.gameObject;
                MirrorMod.Log.Msg($"  Tab '{w.Name}': panel={hasPanel}, onContainer={onContainer}");
            }

            foreach (var opt in VanillaOptions.All)
            {
                if (!_vanillaWindows.TryGetValue(opt.TabId, out var w)) { MirrorMod.Log.Warning($"Vanilla tab '{opt.TabId}' not found"); continue; }
                var row = w.Container.Find(opt.RowName);
                if (row == null) { MirrorMod.Log.Warning($"Row '{opt.RowName}' not found in '{opt.TabId}'"); continue; }
                _rows[opt.Id] = row;
                _rowSelectables[opt.Id] = row.GetComponentsInChildren<UISelectable>(true).ToArray();
            }

            _template = windows[0].Container;
            MeasureVanillaSpacing(_template);

            var parking = new GameObject("MirrorParking");
            parking.SetActive(false);
            parking.transform.SetParent(ui.transform, false);
            _parking = parking.transform;

            int selectableCount = _rowSelectables.Values.Sum(a => a.Length);
            MirrorMod.Log.Msg($"Captured {_vanillaWindows.Count} vanilla tabs, {_rows.Count} rows, {selectableCount} selectables");
        }

        /// <summary>Copies the layout, pins Finalize last, and makes sure Done is always reachable.</summary>
        private static List<TabDefinition> Normalize(LayoutDefinition layout)
        {
            var tabs = layout.Tabs
                .Where(t => t.Id != VanillaOptions.FinalizeTab)
                .Select(t => t.Copy())
                .ToList();

            var finalize = layout.Tabs.FirstOrDefault(t => t.Id == VanillaOptions.FinalizeTab)?.Copy()
                           ?? VanillaOptions.CreateFinalizeTab();
            tabs.Add(finalize);

            if (!tabs.Any(t => t.Options.Contains(VanillaOptions.Done)))
                finalize.Options.Add(VanillaOptions.Done);

            return tabs;
        }

        private static void HideAllContainers()
        {
            foreach (var w in _vanillaWindows.Values) w.Container.gameObject.SetActive(false);
            foreach (var c in _customContainers.Values) if (c != null) c.gameObject.SetActive(false);
        }

        /// <summary>Vanilla tabs reuse their own container and panel; new tabs get an empty clone of the first vanilla one.</summary>
        private static (RectTransform container, UIPanel panel) GetContainer(string tabId)
        {
            if (_vanillaWindows.TryGetValue(tabId, out var w)) return (w.Container, w.Panel);

            if (_customContainers.TryGetValue(tabId, out var existing) && existing != null)
            {
                _customPanels.TryGetValue(tabId, out var existingPanel);
                return (existing, existingPanel);
            }

            // Template is inactive here (HideAllContainers ran first), so cloned rows never run Awake.
            var go = UObject.Instantiate(_template.gameObject, _template.parent);
            go.name = "Mirror_" + tabId;
            var rt = go.GetComponent<RectTransform>();
            for (int i = rt.childCount - 1; i >= 0; i--)
                UObject.DestroyImmediate(rt.GetChild(i).gameObject);
            go.SetActive(false);
            _customContainers[tabId] = rt;

            // The clone carries a UIPanel only if vanilla keeps it on the container itself.
            var panel = go.GetComponent<UIPanel>();
            if (panel != null)
            {
                panel.ClearAllSelectables();          // drop references to the destroyed row clones
                panel.SetParentScreen(_ui._screen);
                _customPanels[tabId] = panel;
            }
            else
            {
                MirrorMod.Log.Warning($"Custom tab '{tabId}' has no UIPanel; gamepad won't reach it");
            }

            return (rt, panel);
        }

        /// <summary>Moves each row's selectables into the panel of the tab it now lives in.</summary>
        private static void RebuildPanels(List<BuiltTab> built)
        {
            var rowSelectables = _rowSelectables.Values.SelectMany(a => a).ToList();

            var managed = _vanillaWindows.Values.Select(w => w.Panel)
                .Concat(_customPanels.Values)
                .Where(p => p != null)
                .Distinct()
                .ToList();

            // Edit the list directly: RemoveSelectable deselects a panel when it empties.
            foreach (var panel in managed)
            {
                foreach (var s in rowSelectables) panel.selectables.Remove(s);
                panel.DeselectSelectable();
            }

            foreach (var b in built)
            {
                if (b.Panel == null) continue;
                foreach (var id in b.Placed)
                    foreach (var s in _rowSelectables[id])
                        b.Panel.AddSelectable(s);
            }
        }

        /// <summary>Reads top padding and row spacing from the first two rows of a vanilla container.</summary>
        private static void MeasureVanillaSpacing(RectTransform container)
        {
            if (container.childCount < 2) { MirrorMod.Log.Warning("Can't measure spacing; using defaults"); return; }

            var first = container.GetChild(0).GetComponent<RectTransform>();
            var second = container.GetChild(1).GetComponent<RectTransform>();

            float Top(RectTransform r) => r.localPosition.y + r.rect.yMax;
            float Bottom(RectTransform r) => r.localPosition.y + r.rect.yMin;

            _padTop = Mathf.Max(0f, container.rect.yMax - Top(first));
            _spacing = Mathf.Max(0f, Bottom(first) - Top(second));

            MirrorMod.Log.Msg($"Measured vanilla spacing: padTop={_padTop:0.#}, spacing={_spacing:0.#}");
        }

        /// <summary>Vanilla Finalize keeps its hand-placed layout; every other container gets a layout group.</summary>
        private static bool NeedsLayout(BuiltTab b)
        {
            if (b.Tab.Id != VanillaOptions.FinalizeTab) return true;
            return b.Placed.Any(id => id != VanillaOptions.Done && id != "vanilla.finalizeText");
        }

        private static void EnsureLayoutGroup(RectTransform container)
        {
            var group = container.GetComponent<VerticalLayoutGroup>();
            if (group == null) group = container.gameObject.AddComponent<VerticalLayoutGroup>();

            group.childAlignment = TextAnchor.UpperCenter;
            group.padding = new RectOffset(0, 0, Mathf.RoundToInt(_padTop), 0);
            group.spacing = _spacing;
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
        }
    }
}