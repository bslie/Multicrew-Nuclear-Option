using System.Collections.Generic;
using SimpleWSO.Core;
using UnityEngine;

namespace SimpleWSO.Gunner
{
    /// <summary>
    /// Drive turret aim and station targets only. Firing goes through GunnerWeaponFire.
    /// </summary>
    public static class TurretController
    {
        private static readonly HashSet<Turret> ManualTurrets = new HashSet<Turret>();
        private static readonly Dictionary<long, float> LastTurretVectorSent = new Dictionary<long, float>();

        internal static bool AllowTurretAimAuthority { get; private set; }

        /// <summary>Put every turret on the station into manual mode.</summary>
        public static void EngageManual(TurretStation ts)
        {
            if (ts == null || !ts.HasTurret) return;
            foreach (var t in ts.Turrets)
            {
                if (t == null) continue;
                PrepareManualTurret(ts, t);
                ManualTurrets.Add(t);
            }
        }

        /// <summary>Return the station to AI/auto control.</summary>
        public static void ReleaseManual(TurretStation ts)
        {
            if (ts == null) return;
            foreach (var t in ts.Turrets)
            {
                if (t == null) continue;
                ManualTurrets.Remove(t);
                t.SetManual(false);
            }
        }

        /// <summary>Drop turret target-lock on a gunner station so free-aim can drive rotation.</summary>
        public static void ReleaseTurretTargetLock(TurretStation ts)
        {
            if (ts == null || !ts.HasTurret) return;
            foreach (var t in ts.Turrets)
                ClearTurretTargetLock(t, ts.Number);
        }

        internal static void ClearTurretTargetLock(Turret turret, byte stationNumber)
        {
            if (turret == null) return;
            try
            {
                Reflect.SetField(turret, "target", null);
            }
            catch
            {
                // Fall back to vanilla clear when the field layout differs.
                turret.SetTarget(PersistentID.None, stationNumber);
            }
        }

        internal static void RunAimTurret(Turret turret, Vector3 aimDir)
        {
            try { Reflect.Call(turret, "AimTurret", new[] { typeof(Vector3) }, aimDir); }
            catch { /* private API; best effort */ }
        }

        internal static void PrepareManualTurret(TurretStation ts, Turret turret, bool clearTargetLock = true)
        {
            if (ts == null || turret == null) return;

            turret.SetManual(true);
            if (ts.Station != null)
            {
                try { Reflect.SetField(turret, "currentWeaponStation", ts.Station); }
                catch { /* field name/layout may differ between game builds */ }
            }
            if (clearTargetLock)
                ClearTurretTargetLock(turret, ts.Number);
        }

        /// <summary>Replicate gunner aim to other clients (owner LocalSim uses Cmd/Rpc).</summary>
        internal static void PublishTurretAim(Aircraft aircraft, byte stationNumber, Vector3 aimDir)
        {
            if (aircraft == null) return;

            long key = ((long)aircraft.NetId << 8) | stationNumber;
            float now = Time.timeSinceLevelLoad;
            if (LastTurretVectorSent.TryGetValue(key, out float last) && now - last <= 0.2f)
                return;

            AllowTurretAimAuthority = true;
            try { aircraft.SetTurretVector(stationNumber, aimDir); }
            finally { AllowTurretAimAuthority = false; }
            LastTurretVectorSent[key] = now;
        }

        /// <summary>
        /// Aim with an explicit target for station weapons. When <paramref name="driveTurretAim"/>
        /// is false and a target is set, turrets rely on native lock tracking only (local gunner).
        /// </summary>
        public static void Aim(TurretStation ts, Vector3 worldDir, Unit target, bool driveTurretAim = true)
        {
            if (ts == null || !ts.HasTurret || worldDir.sqrMagnitude < 1e-6f) return;

            worldDir.Normalize();

            foreach (var t in ts.Turrets)
            {
                if (t == null) continue;
                PrepareManualTurret(ts, t, clearTargetLock: target == null || driveTurretAim);
            }

            if (target != null && !driveTurretAim)
                return;

            ts.Station.SetTurretVector(worldDir);

            foreach (var t in ts.Turrets)
            {
                if (t == null) continue;
                t.SetVector(worldDir);
                RunAimTurret(t, worldDir);
            }

            if (ts.Aircraft != null && ts.Aircraft.LocalSim)
                PublishTurretAim(ts.Aircraft, ts.Number, worldDir);
        }

        public static void ApplyGunnerStationTargets(TurretStation ts)
            => ApplyStationTarget(ts, GunnerState.PrimaryTarget());

        public static void ApplyLocalStationTarget(TurretStation ts, Unit target)
        {
            ApplyWeaponTargets(ts, target);

            if (ts == null || !ts.HasTurret) return;

            PersistentID id = target != null ? target.persistentID : PersistentID.None;
            foreach (var turret in ts.Turrets)
                turret?.SetTarget(id, ts.Number);
        }

        public static void ApplyWeaponTargets(TurretStation ts, Unit target)
        {
            if (ts?.Station?.Weapons == null) return;
            foreach (var weapon in ts.Station.Weapons)
            {
                if (weapon != null)
                    weapon.SetTarget(target);
            }
        }

        internal static bool StationTurretTargetsMatch(TurretStation ts, uint desiredTargetId)
        {
            if (ts == null || !ts.HasTurret)
                return true;

            foreach (var turret in ts.Turrets)
            {
                if (turret == null)
                    continue;

                uint actualId = 0u;
                if (Reflect.TryGetField<Unit>(turret, "target", out Unit target) &&
                    target != null &&
                    !target.disabled)
                {
                    actualId = target.persistentID.Id;
                }

                if (actualId != desiredTargetId)
                    return false;
            }

            return true;
        }

        public static void ApplyStationTarget(TurretStation ts, Unit target)
        {
            ApplyWeaponTargets(ts, target);

            if (ts == null || !ts.HasTurret || ts.Aircraft == null) return;

            PersistentID id = target != null ? target.persistentID : PersistentID.None;
            for (byte i = 0; i < ts.Turrets.Count; i++)
                ts.Aircraft.SetStationTurretTarget(ts.Number, i, id);
        }

        public static void CleanupAll()
        {
            foreach (var turret in new List<Turret>(ManualTurrets))
            {
                if (turret == null) continue;
                try { turret.SetManual(false); } catch { }
            }
            ManualTurrets.Clear();
            LastTurretVectorSent.Clear();
            GunnerWeaponFire.CleanupAll();
        }
    }
}
