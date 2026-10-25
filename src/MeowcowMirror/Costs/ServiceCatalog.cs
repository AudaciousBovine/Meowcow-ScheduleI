using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Il2CppScheduleOne.Avatar;
using MeowcowMirror.Contexts;
using PlayerAppearance = Il2CppScheduleOne.Avatar.Player.PlayerAppearance;

namespace MeowcowMirror.Costs
{
    /// <summary>
    /// A chargeable change: one or more options that are compared, priced and reverted together
    /// (e.g. the four eyebrow sliders are one "Eyebrow shaping" service, so one receipt line).
    /// </summary>
    internal sealed class Service
    {
        public string Id;
        public string[] Options;                                   // option IDs that belong to this service
        public Func<PlayerAppearance, string> Key;                 // compares start vs current
        public Func<PlayerAppearance, string> PriceValue;          // value used for per-value prices (null = service price only)
        public Func<PlayerAppearance, string> Describe;            // receipt line text
        public Action<PlayerAppearance, PlayerAppearance> Revert;  // (target, start)

        public bool AppearsIn(string contextId)
        {
            foreach (var option in Options)
                if (OptionContexts.IsIn(option, contextId)) return true;
            return false;
        }
    }

    /// <summary>The vanilla services. Gender, skin colour and clothing are Create-only, so never charged.</summary>
    internal static class ServiceCatalog
    {
        public static readonly List<Service> All = new List<Service>
        {
            new Service
            {
                Id = "vanilla.hair", Options = new[] { "vanilla.hair" },
                Key = a => a.HairStyleId ?? "",
                PriceValue = a => a.HairStyleId ?? "",
                Describe = a => "Hair style: " + ObjectName(a.HairStyleId),
                Revert = (t, s) => t.HairStyleId = s.HairStyleId,
            },
            new Service
            {
                Id = "vanilla.hairColor", Options = new[] { "vanilla.hairColor" },
                Key = a => Hex(a.HairColor),
                Describe = a => "Hair colour",
                Revert = (t, s) => t.HairColor = s.HairColor,
            },
            new Service
            {
                Id = "vanilla.facialHair", Options = new[] { "vanilla.facialHair" },
                Key = a => a.FacialHairId ?? "",
                PriceValue = a => a.FacialHairId ?? "",
                Describe = a => "Facial hair: " + ObjectName(a.FacialHairId),
                Revert = (t, s) => t.FacialHairId = s.FacialHairId,
            },
            new Service
            {
                Id = "vanilla.eyebrows",
                Options = new[] { "vanilla.eyebrowScale", "vanilla.eyebrowThickness", "vanilla.eyebrowHeight", "vanilla.eyebrowAngle" },
                Key = a => $"{F(a.EyebrowScale)}|{F(a.EyebrowThickness)}|{F(a.EyebrowHeight)}|{F(a.EyebrowAngle)}",
                Describe = a => "Eyebrow shaping",
                Revert = (t, s) =>
                {
                    t.EyebrowScale = s.EyebrowScale;
                    t.EyebrowThickness = s.EyebrowThickness;
                    t.EyebrowHeight = s.EyebrowHeight;
                    t.EyebrowAngle = s.EyebrowAngle;
                },
            },
            new Service
            {
                Id = "vanilla.weight", Options = new[] { "vanilla.weight" },
                Key = a => F(a.Weight),
                Describe = a => "Weight",
                Revert = (t, s) => t.Weight = s.Weight,
            },
            new Service
            {
                Id = "vanilla.facialDetails", Options = new[] { "vanilla.facialDetails", "vanilla.facialDetailIntensity" },
                Key = a => $"{a.FacialDetailId}|{F(a.FacialDetailIntensity)}",
                PriceValue = a => a.FacialDetailId ?? "",
                Describe = a => "Facial details: " + ObjectName(a.FacialDetailId),
                Revert = (t, s) =>
                {
                    t.FacialDetailId = s.FacialDetailId;
                    t.FacialDetailIntensity = s.FacialDetailIntensity;
                },
            },
            new Service
            {
                Id = "vanilla.eyeballColor", Options = new[] { "vanilla.eyeballColor" },
                Key = a => Hex(a.EyeballColor),
                Describe = a => "Eye colour",
                Revert = (t, s) => t.EyeballColor = s.EyeballColor,
            },
            new Service
            {
                Id = "vanilla.face", Options = new[] { "vanilla.face" },
                Key = a => a.FaceId ?? "",
                PriceValue = a => a.FaceId ?? "",
                Describe = a => "Expression: " + ObjectName(a.FaceId),
                Revert = (t, s) => t.FaceId = s.FaceId,
            },
            new Service
            {
                Id = "vanilla.eyes", Options = new[] { "vanilla.pupilDilation", "vanilla.upperEyelid", "vanilla.lowerEyelid" },
                Key = a => $"{F(a.PupilDilation)}|{F(a.UpperEyelidPosition)}|{F(a.LowerEyelidPosition)}",
                Describe = a => "Eyes",
                Revert = (t, s) =>
                {
                    t.PupilDilation = s.PupilDilation;
                    t.UpperEyelidPosition = s.UpperEyelidPosition;
                    t.LowerEyelidPosition = s.LowerEyelidPosition;
                },
            },
        };

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        private static string ObjectName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "None";
            var obj = AvatarObjectLibrary.GetAvatarObjectById(id);
            return obj != null ? obj.Name : id;
        }
    }
}
