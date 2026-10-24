using System;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Avatar;
using Il2CppScheduleOne.Avatar.Player;
using Il2CppScheduleOne.Avatar.Tools;
using Il2CppScheduleOne.Core.Avatar;

namespace MeowcowMirror
{
    /// <summary>Shared helpers for reading and editing player appearance.</summary>
    public static class AppearanceService
    {
        /// <summary>Clones the local player's appearance, applies the edit, and syncs it.</summary>
        public static bool TryEditLocal(Action<PlayerAppearance> edit)
        {
            var player = Player.Local;
            if (player == null || player.CurrentAppearance == null)
            {
                MirrorMod.Log.Warning("No local player or appearance yet");
                return false;
            }

            PlayerAppearance app = player.CurrentAppearance.Clone();
            edit(app);
            player.SetAppearance_Server(app);
            return true;
        }

        /// <summary>Looks up an avatar object by exact ID and serializes it with default colors.</summary>
        public static bool TryGetSerialized(string id, out SerializedAvatarObject result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(id)) return false;

            if (!AvatarObjectLibrary.TryGetAvatarObjectById(id.Trim(), out AvatarObject obj))
                return false;

            result = obj.Serialize();
            return true;
        }
    }
}