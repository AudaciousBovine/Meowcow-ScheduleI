using MelonLoader;
using UnityEngine;
using MeowcowMirror.Layout;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Avatar;
using Il2CppScheduleOne.Avatar.Tools;
using Creator = Il2CppScheduleOne.CharacterCreator.CharacterCreator;

[assembly: MelonInfo(typeof(MeowcowMirror.MirrorMod), "MeowcowMirror", "0.0.1", "Evelyna")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MeowcowMirror
{
    public class MirrorMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        private MelonPreferences_Entry<string> _testId;
        private MelonPreferences_Entry<KeyCode> _openKey, _vanillaLayoutKey, _testLayoutKey;
        private MelonPreferences_Entry<KeyCode> _dumpKey, _addKey, _removeKey, _inspectKey, _layoutKey;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;

            var cat = MelonPreferences.CreateCategory("MeowcowMirror");
            _testId           = cat.CreateEntry("TestObjectId", "", "Avatar object ID to add/remove (exact ID as shown by F7)");
            _openKey          = cat.CreateEntry("OpenCreatorKey",    KeyCode.F4,  "Open the character creator (testing)");
            _vanillaLayoutKey = cat.CreateEntry("VanillaLayoutKey",  KeyCode.F5,  "Rebuild the creator with the vanilla layout");
            _testLayoutKey    = cat.CreateEntry("TestLayoutKey",     KeyCode.F6,  "Rebuild the creator with the test layout");
            _dumpKey          = cat.CreateEntry("DumpKey",           KeyCode.F7,  "Log every AvatarObjectLibrary key");
            _addKey           = cat.CreateEntry("AddKey",            KeyCode.F8,  "Add TestObjectId to local appearance");
            _removeKey        = cat.CreateEntry("RemoveKey",         KeyCode.F9,  "Remove TestObjectId from local appearance");
            _inspectKey       = cat.CreateEntry("InspectKey",        KeyCode.F10, "Log extra avatar objects for every player");
            _layoutKey        = cat.CreateEntry("LayoutDumpKey",     KeyCode.F11, "Log the character creator's windows");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(_openKey.Value))          OpenCreator();
            if (Input.GetKeyDown(_vanillaLayoutKey.Value)) CreatorLayoutBuilder.Apply(VanillaOptions.CreateVanillaLayout());
            if (Input.GetKeyDown(_testLayoutKey.Value))    CreatorLayoutBuilder.Apply(VanillaOptions.CreateTestLayout());
            if (Input.GetKeyDown(_dumpKey.Value))          DumpLibrary();
            if (Input.GetKeyDown(_addKey.Value))           Add();
            if (Input.GetKeyDown(_removeKey.Value))        Remove();
            if (Input.GetKeyDown(_inspectKey.Value))       InspectAll();
            if (Input.GetKeyDown(_layoutKey.Value))        DumpCreatorLayout();
        }

        private void OpenCreator()
        {
            var creator = Object.FindObjectOfType<Creator>(true);
            if (creator == null) { Log.Warning("CharacterCreator not found"); return; }
            var container = creator._container.gameObject;

            if (creator.IsOpen)
            {
                if (container.activeSelf) { Log.Msg("Creator already open"); return; }

                // Open but hidden: a reopen raced vanilla's close timer. Recover.
                Log.Warning("Creator stuck open but hidden; recovering");
                creator.Close();
                MirrorFrame.RestoreCamera();
                return;
            }

            if (container.activeSelf) { Log.Msg("Creator still closing; try again in a moment"); return; }
            creator.Open();
            SeedFromPlayer(creator);
            CreatorLayoutBuilder.Apply(VanillaOptions.CreateVanillaLayout());
            Log.Msg("Creator opened");
        }

        /// <summary>Vanilla opens on a default preset; edit sessions start from the player's current look.</summary>
        private static void SeedFromPlayer(Creator creator)
        {
            var current = Player.Local?.CurrentAppearance;
            var state = creator._currentState;
            if (current == null || state == null) { Log.Warning("Couldn't seed creator from player"); return; }

            state.Appearance = current.Clone();
            creator.ApplyNakedAppearance();
            CreatorLayoutBuilder.FindInterface()?.SetInputsToAppearance(state);
        }

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

            var windows = ui._windows;
            Log.Msg($"Creator windows: {windows.Length}");
            for (int i = 0; i < windows.Length; i++)
            {
                var w = windows[i];
                Log.Msg($"[{i}] {w.Name} ({w.Container.name})");
                for (int c = 0; c < w.Container.childCount; c++)
                {
                    var child = w.Container.GetChild(c);
                    Log.Msg($"    {child.name}");
                    for (int g = 0; g < child.childCount; g++)
                        if (child.name.StartsWith("MirrorColumn")) Log.Msg($"        {child.GetChild(g).name}");
                }
            }
        }
    }
}