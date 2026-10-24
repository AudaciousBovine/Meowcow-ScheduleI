using System;
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
    /// Rebuilds the character creator's pages from a LayoutDefinition: moves vanilla rows into two
    /// columns per tab, re-registers their gamepad selectables, and assigns a new _windows array.
    /// </summary>
    public static class CreatorLayoutBuilder
    {
        private static CreatorUI _ui;
        private static readonly Dictionary<string, CreatorUI.Window> _vanillaWindows = new Dictionary<string, CreatorUI.Window>();
        private static readonly Dictionary<string, Transform> _rows = new Dictionary<string, Transform>();
        private static readonly Dictionary<string, UISelectable[]> _rowSelectables = new Dictionary<string, UISelectable[]>();
        private static readonly Dictionary<string, RectTransform> _customContainers = new Dictionary<string, RectTransform>();
        private static readonly Dictionary<string, UIPanel> _customPanels = new Dictionary<string, UIPanel>();
        private static readonly Dictionary<IntPtr, (RectTransform left, RectTransform right)> _columns =
            new Dictionary<IntPtr, (RectTransform, RectTransform)>();
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

            // The footer replaces the Finalize tab.
            var tabs = layout.Tabs
                .Where(t => t.Id != VanillaOptions.FinalizeTab)
                .Select(t => t.Copy())
                .ToList();
            if (tabs.Count == 0) { MirrorMod.Log.Warning("Layout has no tabs"); return false; }

            // Resize the panel first: rows are positioned relative to their column, so sizes must be final.
            MirrorFrame.EnsureBuilt(ui, _template.parent.GetComponent<RectTransform>());

            HideAllContainers();

            var built = new List<BuiltTab>();
            var placed = new HashSet<string>();

            foreach (var tab in tabs)
            {
                var (container, panel) = GetContainer(tab.Id);
                var entry = new BuiltTab { Tab = tab, Container = container, Panel = panel };
                var rows = new List<Transform>();

                foreach (var optionId in tab.Options)
                {
                    if (!_rows.TryGetValue(optionId, out var row)) { MirrorMod.Log.Warning($"Unknown option '{optionId}' in tab '{tab.Id}'"); continue; }
                    if (!placed.Add(optionId)) { MirrorMod.Log.Warning($"Option '{optionId}' listed twice; keeping first"); continue; }
                    rows.Add(row);
                    entry.Placed.Add(optionId);
                }

                PlaceInColumns(container, rows);
                built.Add(entry);
            }

            // Anything the layout doesn't mention is parked out of sight.
            foreach (var kv in _rows)
                if (!placed.Contains(kv.Key)) kv.Value.SetParent(_parking, false);

            RebuildPanels(built);

            var windows = built
                .Select(b => new CreatorUI.Window { Name = b.Tab.Name, Container = b.Container, Panel = b.Panel })
                .ToArray();
            ui._windows = new Il2CppReferenceArray<CreatorUI.Window>(windows);

            MirrorFrame.SetTabs(built.Select(b => b.Tab.Name).ToList(), layout.CommitMode);
            MirrorFrame.Select(0);

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
            _columns.Clear();

            var windows = ui._windows;
            for (int i = 0; i < windows.Length; i++)
                _vanillaWindows[windows[i].Name.ToLowerInvariant()] = windows[i];

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

            MirrorMod.Log.Msg($"Captured {_vanillaWindows.Count} vanilla tabs and {_rows.Count} rows");
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

            // Vanilla keeps panels on rows, not containers, so share the first vanilla tab's panel.
            var panel = _vanillaWindows.Values.First().Panel;
            _customPanels[tabId] = panel;
            return (rt, panel);
        }

        // ---------- Columns ----------

        /// <summary>
        /// Splits rows into left and right columns, keeping their order: the left column fills until it
        /// holds about half the total height, then the rest go right.
        /// </summary>
        private static void PlaceInColumns(RectTransform container, List<Transform> rows)
        {
            var (left, right) = GetColumns(container);
            var heights = rows.Select(RowHeight).ToList();
            float half = heights.Sum() / 2f;

            var leftRows = new List<Transform>();
            var rightRows = new List<Transform>();
            float used = 0f;
            bool toRight = false;

            for (int i = 0; i < rows.Count; i++)
            {
                if (!toRight && leftRows.Count > 0 && used + heights[i] / 2f > half) toRight = true;
                (toRight ? rightRows : leftRows).Add(rows[i]);
                used += heights[i];
            }

            foreach (var row in leftRows) { row.SetParent(left, false); row.SetAsLastSibling(); }
            foreach (var row in rightRows) { row.SetParent(right, false); row.SetAsLastSibling(); }

            StackRows(left, leftRows);
            StackRows(right, rightRows);
        }

        private static (RectTransform left, RectTransform right) GetColumns(RectTransform container)
        {
            if (_columns.TryGetValue(container.Pointer, out var cols) && cols.left != null && cols.right != null)
                return cols;

            cols = (CreateColumn(container, "MirrorColumnLeft", 0f, 0.5f),
                    CreateColumn(container, "MirrorColumnRight", 0.5f, 1f));
            _columns[container.Pointer] = cols;
            return cols;
        }

        private static RectTransform CreateColumn(RectTransform container, string name, float minX, float maxX)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(container, false);
            rt.anchorMin = new Vector2(minX, 0f);
            rt.anchorMax = new Vector2(maxX, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static float RowHeight(Transform row)
        {
            var (top, bottom) = ContentBounds(row.parent, row);
            return top >= bottom ? top - bottom : 0f;
        }

        // ---------- Measuring and stacking ----------

        /// <summary>Reads top padding and row spacing from the vanilla Body tab, using what the rows actually draw.</summary>
        private static void MeasureVanillaSpacing(RectTransform container)
        {
            if (container.childCount < 2) { MirrorMod.Log.Warning("Can't measure spacing; using defaults"); return; }

            var (top0, bottom0) = ContentBounds(container, container.GetChild(0));
            var (top1, _) = ContentBounds(container, container.GetChild(1));

            _padTop = Mathf.Max(0f, container.rect.yMax - top0);
            _spacing = Mathf.Max(0f, bottom0 - top1);

            MirrorMod.Log.Msg($"Measured vanilla spacing: padTop={_padTop:0.#}, spacing={_spacing:0.#}");
        }

        /// <summary>Stacks rows top to bottom by moving them, never resizing them.</summary>
        private static void StackRows(RectTransform column, List<Transform> rows)
        {
            float cursor = column.rect.yMax - _padTop;

            foreach (var row in rows)
            {
                var (top, bottom) = ContentBounds(column, row);
                if (top < bottom) continue; // nothing visible to measure

                float delta = cursor - top;
                var pos = row.localPosition;
                pos.y += delta;
                row.localPosition = pos;

                cursor = bottom + delta - _spacing;
            }
        }

        /// <summary>Top and bottom of everything a row actually draws, in the given space.</summary>
        private static (float top, float bottom) ContentBounds(Transform space, Transform row)
        {
            float top = float.MinValue, bottom = float.MaxValue;

            foreach (var rt in row.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.GetComponent<Graphic>() == null || !IsShownWithin(rt, row)) continue;

                var r = rt.rect;
                float yTop = space.InverseTransformPoint(rt.TransformPoint(new Vector3(r.center.x, r.yMax, 0f))).y;
                float yBottom = space.InverseTransformPoint(rt.TransformPoint(new Vector3(r.center.x, r.yMin, 0f))).y;

                top = Mathf.Max(top, yTop);
                bottom = Mathf.Min(bottom, yBottom);
            }

            return (top, bottom);
        }

        /// <summary>True if this object and every parent up to the row is switched on.</summary>
        private static bool IsShownWithin(Transform t, Transform row)
        {
            for (var current = t; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == row) return true;
            }
            return true;
        }

        // ---------- Gamepad ----------

        /// <summary>Moves each row's selectables into the panel of the tab it now lives in.</summary>
        private static void RebuildPanels(List<BuiltTab> built)
        {
            var rowSelectables = _rowSelectables.Values.SelectMany(a => a).ToList();

            var managed = _vanillaWindows.Values.Select(w => w.Panel)
                .Concat(_customPanels.Values)
                .Where(p => p != null)
                .GroupBy(p => p.Pointer)
                .Select(g => g.First())
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
    }
}