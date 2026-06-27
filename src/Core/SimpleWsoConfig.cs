using BepInEx.Configuration;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SimpleWSO.Core
{
    /// <summary>
    /// All tunables for the mod. Bound from BepInEx config so users (and ConfigManager)
    /// can rebind keys without recompiling.
    /// </summary>
    public static class SimpleWsoConfig
    {
        public static ConfigEntry<KeyCode> ToggleGunnerKey;
        public static ConfigEntry<KeyCode> ShareTargetsKey;
        public static ConfigEntry<KeyCode> CycleCameraPositionKey;

        public static ConfigEntry<bool> ReplaceSharedTargets;
        public static ConfigEntry<bool> VerboseLogging;
        private static ConfigFile _configFile;
        private static readonly Dictionary<string, ConfigEntry<string>> CameraOffsetEntries =
            new Dictionary<string, ConfigEntry<string>>();
        /// <summary>
        /// Per-airframe default gunner camera position 1 (aircraft-local meters from origin).
        /// </summary>
        private static readonly Dictionary<string, Vector3> BaselineCameraOffsets =
            new Dictionary<string, Vector3>
            {
                { "CI-22_Cricket", new Vector3(0f, 0.15f, -0.9f) },
                { "T_A-30_Compass", new Vector3(0f, 0.25f, -1f) },
                { "UH-90_Ibis", new Vector3(0.93f, 0f, 0f) },
                { "SAH-46_Chicane", new Vector3(0f, -0.4f, 1.5f) },
                { "VL-49_Tarantula", new Vector3(1.34f, 0f, 0f) },
                { "EW-25_Medusa", new Vector3(0.85f, 0f, 0f) },
                { "SFB-81_Darkreach", new Vector3(1f, 0f, 0f) },
                { "Alkyon_AB-4", new Vector3(0.8f, 0f, 0f) },
            };

        /// <summary>
        /// Default gunner camera positions 2+ (aircraft-local meters from origin).
        /// </summary>
        private static readonly Dictionary<string, string[]> ExtraCameraPositionDefaults =
            new Dictionary<string, string[]>
            {
                { "CI-22_Cricket", new[] { "0,-1.5,1.9" } },
                { "T_A-30_Compass", new[] { "0,-1.3,2.9" } },
                { "UH-90_Ibis", new[] { "-0.4,0,-1.2", "1.4,0,-1.2" } },
                { "SAH-46_Chicane", new[] { "0,-1.9,3.2" } },
                { "VL-49_Tarantula", new[] { "-2,-1.9,-12" } },
            };

        public static void Bind(ConfigFile cfg)
        {
            _configFile = cfg;

            ToggleGunnerKey = cfg.Bind("Keys", "ToggleGunnerKey", KeyCode.H,
                "Spectate/follow an aircraft, then press to possess a gunner seat. Press again to leave.");
            ShareTargetsKey = cfg.Bind("Keys", "ShareTargetsKey", KeyCode.U,
                "Share current targets with the other seat. Gunner sends to pilot; pilot sends to gunner.");
            CycleCameraPositionKey = cfg.Bind("Keys", "CycleCameraPositionKey", KeyCode.K,
                "While in a gunner seat, cycle between configured camera positions for the current aircraft.");

            ReplaceSharedTargets = cfg.Bind("Behaviour", "ReplaceSharedTargets", false,
                "Replace the other seat's targets when sharing (ShareTargetsKey). When false, shared targets are merged.");
            VerboseLogging = cfg.Bind("Behaviour", "VerboseLogging", false,
                "Extra debug logging for view state, fire diagnostics, and gunner input.");

            foreach (string aircraftKey in BaselineCameraOffsets.Keys)
            {
                GetCameraOffsetEntry(aircraftKey);
                EnsureExtraCameraPositionEntries(aircraftKey);
            }
        }

        public static string CameraOffsetKey(Aircraft aircraft)
        {
            string raw = null;
            if (aircraft != null && aircraft.definition != null)
                raw = aircraft.definition.unitName;
            if (string.IsNullOrEmpty(raw) && aircraft != null)
                raw = aircraft.unitName;
            if (string.IsNullOrEmpty(raw) && aircraft != null)
                raw = aircraft.name;
            if (string.IsNullOrEmpty(raw))
                raw = "UnknownAircraft";

            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                raw = raw.Replace(invalid, '_');
            return raw.Replace(' ', '_');
        }

        public static Vector3 GetCameraOffset(Aircraft aircraft)
            => GetCameraOffset(aircraft, 0);

        public static Vector3 GetCameraOffset(Aircraft aircraft, int positionIndex)
        {
            string key = CameraOffsetKey(aircraft);
            if (positionIndex > 0 &&
                TryParseVector(GetExtraCameraPositionEntry(key, positionIndex + 1).Value, out Vector3 positionOffset))
            {
                return positionOffset;
            }

            return GetPrimaryCameraOffset(key);
        }

        public static int GetCameraPositionCount(Aircraft aircraft)
        {
            string key = CameraOffsetKey(aircraft);
            if (!ExtraCameraPositionDefaults.TryGetValue(key, out var defaults))
                return 1;

            int count = 1;
            for (int i = 0; i < defaults.Length; i++)
            {
                if (TryParseVector(GetExtraCameraPositionEntry(key, i + 2).Value, out _))
                    count = i + 2;
            }

            return count;
        }

        private static Vector3 GetPrimaryCameraOffset(string key)
            => ParseVector(GetCameraOffsetEntry(key).Value);

        private static Vector3 GetBaselineCameraOffset(string key)
        {
            return BaselineCameraOffsets.TryGetValue(key, out var tuned)
                ? tuned
                : Vector3.zero;
        }

        private static ConfigEntry<string> GetCameraOffsetEntry(string key)
        {
            if (!CameraOffsetEntries.TryGetValue(key, out var entry))
            {
                string defaultValue = FormatVector(GetBaselineCameraOffset(key));
                entry = _configFile.Bind("CameraOffsets", key, defaultValue,
                    "Gunner camera position 1 (aircraft-local meters from origin: X right, Y up, Z forward).");
                CameraOffsetEntries[key] = entry;
            }
            return entry;
        }

        private static void EnsureExtraCameraPositionEntries(string key)
        {
            if (!ExtraCameraPositionDefaults.TryGetValue(key, out var defaults))
                return;

            for (int i = 0; i < defaults.Length; i++)
                GetExtraCameraPositionEntry(key, i + 2);
        }

        private static ConfigEntry<string> GetExtraCameraPositionEntry(string key, int positionNumber)
        {
            string entryKey = $"{key}.Position{positionNumber}";
            if (!CameraOffsetEntries.TryGetValue(entryKey, out var entry))
            {
                string defaultValue = GetExtraCameraPositionDefault(key, positionNumber);
                entry = _configFile.Bind("CameraOffsets", entryKey, defaultValue,
                    $"Gunner camera position {positionNumber} (aircraft-local meters from origin: X right, Y up, Z forward).");
                CameraOffsetEntries[entryKey] = entry;
            }
            return entry;
        }

        private static string GetExtraCameraPositionDefault(string key, int positionNumber)
        {
            int index = positionNumber - 2;
            return ExtraCameraPositionDefaults.TryGetValue(key, out var defaults) &&
                   index >= 0 &&
                   index < defaults.Length
                ? defaults[index]
                : "";
        }

        private static string FormatVector(Vector3 vector)
            => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", vector.x, vector.y, vector.z);

        private static Vector3 ParseVector(string value)
        {
            return TryParseVector(value, out Vector3 parsed) ? parsed : Vector3.zero;
        }

        private static bool TryParseVector(string value, out Vector3 vector)
        {
            vector = Vector3.zero;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] parts = value.Split(',');
            if (parts.Length != 3) return false;

            vector = new Vector3(
                ParseFloat(parts[0]),
                ParseFloat(parts[1]),
                ParseFloat(parts[2]));
            return true;
        }

        private static float ParseFloat(string value)
        {
            return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0f;
        }
    }
}
