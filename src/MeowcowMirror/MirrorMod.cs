using System.Linq;
using MelonLoader;
using UnityEngine;
using MeowcowMirror.Contexts;
using MeowcowMirror.Layout;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Avatar;
using Il2CppScheduleOne.Avatar.Tools;

[assembly: MelonInfo(typeof(MeowcowMirror.MirrorMod), "MeowcowMirror", "0.0.1", "Evelyna")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MeowcowMirror
{
    public class MirrorMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        private MelonPreferences_Entry<string> _testId;
        private MelonPreferences_Entry<KeyCode> _mirrorKey, _barberKey, _surgeonKey;
        private MelonPreferences_Entry<KeyCode> _dumpKey, _addKey, _removeKey, _inspectKey, _layoutKey;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;

            var cat = MelonPreferences.CreateCategory("MeowcowMirror");
            _testId     = cat.CreateEntry("TestObjectId", "", "Avatar object ID to add/remove (exact ID as shown by F7)");
            _mirrorKey  = cat.CreateEntry("OpenMirrorKey",  KeyCode.F4,  "Open the creator in the Mirror context (testing)");
            _barberKey  = cat.CreateEntry("OpenBarberKey",  KeyCode.F5,  "Open the creator in the Barber context (testing)");
            _surgeonKey = cat.CreateEntry("OpenSurgeonKey", KeyCode.F6,  "Open the creator in the Surgeon context (testing)");
            _dumpKey    = cat.CreateEntry("DumpKey",        KeyCode.F7,  "Log every AvatarObjectLibrary key");
            _addKey     = cat.CreateEntry("AddKey",         KeyCode.F8,  "Add TestObjectId to local appearance");
            _removeKey  = cat.CreateEntry("RemoveKey",      KeyCode.F9,  "Remove TestObjectId from local appearance");
            _inspectKey = cat.CreateEntry("InspectKey",     KeyCode.F10, "Log extra avatar objects for every player");
            _layoutKey  = cat.CreateEntry("LayoutDumpKey",  KeyCode.F11, "Log the character creator's windows");
        }

        /// <summary>Hook the creator as soon as a scene has it, so the intro is caught too.</summary>
        public override void OnSceneWasInitialized(int buildIndex, string sceneName) => CreatorHooks.HookIfPresent();

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(_mirrorKey.Value))  Mirror.Open(MirrorContexts.Mirror);
            if (Input.GetKeyDown(_barberKey.Value))  Mirror.Open(MirrorContexts.Barber);
            if (Input.GetKeyDown(_surgeonKey.Value)) Mirror.Open(MirrorContexts.Surgeon);
            if (Input.GetKeyDown(_dumpKey.Value))    DumpLibrary();
            if (Input.GetKeyDown(_addKey.Value))     Add();
            if (Input.GetKeyDown(_removeKey.Value))  Remove();
            if (Input.GetKeyDown(_inspectKey.Value)) InspectAll();
            if (Input.GetKeyDown(_layoutKey.Value))  DumpCreatorLayout();
        }

        // ---------- Test helpers ----------

        private void DumpLibrary()
        {
            var dict = AvatarObjectLibrary._avatarObjects;
            if (dict == null) { Log.Warning("_avatarObjects is null"); return; }
            Log.Msg($"AvatarObjectLibrary: {dict.Count} entries");
            foreach (var kv in dict) Log.Msg($"  {kv.Key}");
        }

        private void Add()
        {
            string id = _testId.Value;
            if (!AppearanceService.TryGetSerialized(id, out var obj))
            {
                Log.Warning($"'{id}' not in AvatarObjectLibrary");
                return;
            }
            if (AppearanceService.TryEditLocal(app => app.AddAvatarObject(obj)))
                Log.Msg($"Added '{id}'");
        }

        private void Remove()
        {
            string id = (_testId.Value ?? "").Trim();
            if (AppearanceService.TryEditLocal(app => app.RemoveAvatarObject(id)))
                Log.Msg($"Removed '{id}'");
        }

        private void InspectAll()
        {
            foreach (var p in Player.PlayerList)
            {
                var app = p?.CurrentAppearance;
                if (app == null) { Log.Msg($"{p?.PlayerName}: no appearance"); continue; }

                var extras = app.AdditionalAvatarObjects;
                int n = extras == null ? 0 : extras.Length;
                Log.Msg($"{p.PlayerName} (local={p.IsLocalPlayer}): {n} extra objects");
                for (int i = 0; i < n; i++) Log.Msg($"  [{i}] {extras[i].Id}");
            }
        }

        private void DumpCreatorLayout()
        {
            var ui = CreatorLayoutBuilder.FindInterface();
            if (ui == null) { Log.Warning("CharacterCreatorInterface not found"); return; }

            Log.Msg($"Active context: {Mirror.ActiveContext?.Id ?? "none"}");
            var windows = ui._windows;
            Log.Msg($"Creator windows: {windows.Length}");
            for (int i = 0; i < windows.Length; i++)
            {
                var w = windows[i];
                Log.Msg($"[{i}] {w.Name} ({w.Container.name})");
                DumpTree(w.Container, 1, 2);
            }
        }

        private void DumpTree(Transform t, int depth, int maxDepth)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                var types = string.Join(", ", c.GetComponents<Component>().Select(x => x.GetIl2CppType().Name));
                Log.Msg($"{new string(' ', depth * 4)}{c.name} active={c.gameObject.activeSelf} [{types}]");
                if (depth < maxDepth) DumpTree(c, depth + 1, maxDepth);
            }
        }
    }
}
