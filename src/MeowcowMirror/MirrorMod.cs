using MelonLoader;
using UnityEngine;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Avatar;

[assembly: MelonInfo(typeof(MeowcowMirror.MirrorMod), "MeowcowMirror", "0.0.1", "Evelyna")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MeowcowMirror
{
    public class MirrorMod : MelonMod
    {
        private MelonPreferences_Entry<string> _testId;
        private MelonPreferences_Entry<KeyCode> _dumpKey, _addKey, _removeKey, _inspectKey;

        internal static MelonLogger.Instance Log;
        
        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            // ...existing preference setup...

            var cat = MelonPreferences.CreateCategory("MeowcowMirror");
            _testId     = cat.CreateEntry("TestObjectId", "", "Avatar object ID to add/remove (lowercase)");
            _dumpKey    = cat.CreateEntry("DumpKey",    KeyCode.F7,  "Log every AvatarObjectLibrary key");
            _addKey     = cat.CreateEntry("AddKey",     KeyCode.F8,  "Add TestObjectId to local appearance");
            _removeKey  = cat.CreateEntry("RemoveKey",  KeyCode.F9,  "Remove TestObjectId from local appearance");
            _inspectKey = cat.CreateEntry("InspectKey", KeyCode.F10, "Log extra avatar objects for every player");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(_dumpKey.Value))    DumpLibrary();
            if (Input.GetKeyDown(_addKey.Value))     Add();
            if (Input.GetKeyDown(_removeKey.Value))  Remove();
            if (Input.GetKeyDown(_inspectKey.Value)) InspectAll();
        }

        private void DumpLibrary()
        {
            var dict = AvatarObjectLibrary._avatarObjects;
            if (dict == null) { LoggerInstance.Warning("_avatarObjects is null"); return; }
            LoggerInstance.Msg($"AvatarObjectLibrary: {dict.Count} entries");
            foreach (var kv in dict) LoggerInstance.Msg($"  {kv.Key}");
        }

        private void Add()
        {
            string id = _testId.Value;
            if (!AppearanceService.TryGetSerialized(id, out var obj))
            {
                LoggerInstance.Warning($"'{id}' not in AvatarObjectLibrary");
                return;
            }
            if (AppearanceService.TryEditLocal(app => app.AddAvatarObject(obj)))
                LoggerInstance.Msg($"Added '{id}'");
        }

        private void Remove()
        {
            string id = (_testId.Value ?? "").Trim().ToLowerInvariant();
            if (AppearanceService.TryEditLocal(app => app.RemoveAvatarObject(id)))
                LoggerInstance.Msg($"Removed '{id}'");
        }

        private void InspectAll()
        {
            foreach (var p in Player.PlayerList)
            {
                var app = p?.CurrentAppearance;
                if (app == null) { LoggerInstance.Msg($"{p?.PlayerName}: no appearance"); continue; }

                var extras = app.AdditionalAvatarObjects;
                int n = extras == null ? 0 : extras.Length;
                LoggerInstance.Msg($"{p.PlayerName} (local={p.IsLocalPlayer}): {n} extra objects");
                for (int i = 0; i < n; i++) LoggerInstance.Msg($"  [{i}] {extras[i].Id}");
            }
        }
    }
}
