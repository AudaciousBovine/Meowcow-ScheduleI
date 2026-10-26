using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Il2CppTMPro;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;
using UObject = UnityEngine.Object;

namespace MeowcowCore.Diagnostics
{
    /// <summary>
    /// Debug tool: writes every UI canvas in the loaded scenes to a text file. For each object it records
    /// the path, active state, components, RectTransform layout, any text, MenuScreen settings and what
    /// its button calls. Used to find vanilla UI that Core moves or extends.
    /// </summary>
    internal static class SceneDump
    {
        private const int MaxTextLength = 120;

        private static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "MeowcowCore", "Dumps");

        internal static void Run()
        {
            try
            {
                string scene = SceneManager.GetActiveScene().name;
                var sb = new StringBuilder();
                sb.AppendLine($"Scene dump: '{scene}' at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                int canvases = 0, objects = 0;
                foreach (var canvas in UObject.FindObjectsOfType<Canvas>(true))
                {
                    if (!IsRootCanvas(canvas)) continue;
                    canvases++;

                    sb.AppendLine();
                    sb.AppendLine($"=== Canvas '{canvas.name}' (mode {canvas.renderMode}, sort {canvas.sortingOrder}, scene '{canvas.gameObject.scene.name}') ===");
                    objects += Walk(canvas.transform, 0, sb);
                }

                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, $"SceneDump_{scene}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(path, sb.ToString());
                CoreMod.Log.Msg($"Scene dump: {canvases} canvases, {objects} objects -> {path}");
            }
            catch (Exception e)
            {
                CoreMod.Log.Error($"Scene dump failed: {e}");
            }
        }

        /// <summary>
        /// Nested canvases are dumped as part of their root, so only roots start a section.
        /// Walks up by hand: Il2CppInterop can't call the generic GetComponentInParent(bool) overload.
        /// </summary>
        private static bool IsRootCanvas(Canvas canvas)
        {
            for (var p = canvas.transform.parent; p != null; p = p.parent)
                if (p.GetComponent<Canvas>() != null) return false;
            return true;
        }

        /// <summary>Writes one object and everything under it. Returns the number of objects written.</summary>
        private static int Walk(Transform t, int depth, StringBuilder sb)
        {
            string indent = new string(' ', depth * 2);
            var go = t.gameObject;

            var types = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetIl2CppType().Name);
            sb.AppendLine($"{indent}{go.name} [{(go.activeSelf ? "on" : "OFF")}] ({string.Join(", ", types)})");

            // Extras are best-effort: one odd component shouldn't stop the dump.
            try { AppendDetails(go, t, indent + "  ", sb); }
            catch (Exception e) { sb.AppendLine($"{indent}  (details failed: {e.Message})"); }

            int count = 1;
            for (int i = 0; i < t.childCount; i++)
                count += Walk(t.GetChild(i), depth + 1, sb);
            return count;
        }

        private static void AppendDetails(GameObject go, Transform t, string indent, StringBuilder sb)
        {
            var rt = t.TryCast<RectTransform>();
            if (rt != null)
                sb.AppendLine($"{indent}rect: anchors {V(rt.anchorMin)}-{V(rt.anchorMax)} pivot {V(rt.pivot)} pos {V(rt.anchoredPosition)} size {V(rt.sizeDelta)} scale {V(rt.localScale)}");

            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null) sb.AppendLine($"{indent}text: \"{Clean(tmp.text)}\"");

            var legacy = go.GetComponent<Text>();
            if (legacy != null) sb.AppendLine($"{indent}text (legacy): \"{Clean(legacy.text)}\"");

            var screen = go.GetComponent<MenuScreen>();
            if (screen != null)
            {
                string previous = screen.PreviousScreen != null ? screen.PreviousScreen.name : "none";
                sb.AppendLine($"{indent}MenuScreen: OpenOnStart={screen.OpenOnStart} IsOpen={screen.IsOpen} Previous={previous} AttachLobby={screen.AttachLobbyToScreen}");
            }

            var button = go.GetComponent<Button>();
            if (button != null) AppendPersistentCalls(button.onClick, "onClick", indent, sb);
        }

        /// <summary>The calls wired up in the scene, e.g. a menu button opening another screen.</summary>
        private static void AppendPersistentCalls(UnityEventBase evt, string label, string indent, StringBuilder sb)
        {
            int n = evt.GetPersistentEventCount();
            for (int i = 0; i < n; i++)
            {
                var target = evt.GetPersistentTarget(i);
                string targetName = target != null ? $"{target.name} ({target.GetIl2CppType().Name})" : "null";
                sb.AppendLine($"{indent}{label}[{i}]: {targetName}.{evt.GetPersistentMethodName(i)}");
            }
        }

        private static string V(Vector2 v) =>
            $"({v.x.ToString("0.##", CultureInfo.InvariantCulture)}, {v.y.ToString("0.##", CultureInfo.InvariantCulture)})";

        private static string V(Vector3 v) =>
            $"({v.x.ToString("0.##", CultureInfo.InvariantCulture)}, {v.y.ToString("0.##", CultureInfo.InvariantCulture)}, {v.z.ToString("0.##", CultureInfo.InvariantCulture)})";

        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", "").Replace("\n", "\\n");
            return text.Length > MaxTextLength ? text.Substring(0, MaxTextLength) + "..." : text;
        }
    }
}
