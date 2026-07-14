using System.Collections.Generic;
using MulticrewNuclearOption.Core;
using UnityEngine;

namespace MulticrewNuclearOption.Gunner
{
    /// <summary>
    /// Shows the pilot the vanilla turret crosshair for a remote gunner-controlled turret
    /// without switching CombatHUD away from the pilot's selected weapon.
    /// </summary>
    public class PilotGunnerTurretReticleOverlay
    {
        private const float OverlayScale = 0.6f;

        private readonly List<HUDTurretCrosshair> _crosshairs = new List<HUDTurretCrosshair>();
        private readonly List<GameObject> _instances = new List<GameObject>();

        private Aircraft _aircraft;
        private WeaponStation _station;

        public void Update()
        {
            TurretStation station = FindVisibleGunnerTurretStation();
            if (station == null)
            {
                Clear();
                return;
            }

            if (_aircraft != station.Aircraft || _station != station.Station || _crosshairs.Count != station.Turrets.Count)
                Rebuild(station);

            RefreshCrosshairs();
        }

        public void Clear()
        {
            foreach (GameObject instance in _instances)
            {
                if (instance != null)
                    Object.Destroy(instance);
            }

            _instances.Clear();
            _crosshairs.Clear();
            _aircraft = null;
            _station = null;
        }

        private static TurretStation FindVisibleGunnerTurretStation()
        {
            if (!GameManager.GetLocalAircraft(out Aircraft aircraft) || aircraft == null || !aircraft.LocalSim)
                return null;

            CombatHUD hud = CombatHUD.i;
            if (hud == null || hud.aircraft != aircraft)
                return null;

            foreach (TurretStation station in StationDiscovery.GetGunnerStations(aircraft))
            {
                if (station == null || !station.HasTurret)
                    continue;

                if (!MulticrewNet.IsRemoteGunnerStation(aircraft.NetId, station.Number))
                    continue;

                // If the pilot has this turret station selected, vanilla HUDTurretState is
                // already drawing the same crosshairs. Avoid a doubled-up reticle.
                if (hud.GetWeaponStation() == station.Station)
                    return null;

                return station;
            }

            return null;
        }

        private void Rebuild(TurretStation station)
        {
            Clear();

            GameObject crosshairPrefab = ResolveTurretCrosshairPrefab();
            Transform parent = FlightHud.i != null ? FlightHud.i.GetHUDCenter() : null;
            if (crosshairPrefab == null || parent == null)
                return;

            foreach (Turret turret in station.Turrets)
            {
                if (turret == null)
                    continue;

                GameObject instance = Object.Instantiate(crosshairPrefab, parent);
                instance.transform.localScale = Vector3.one * OverlayScale;
                HUDTurretCrosshair crosshair = instance.GetComponent<HUDTurretCrosshair>();
                if (crosshair == null)
                {
                    Object.Destroy(instance);
                    continue;
                }

                crosshair.Initialize(turret);
                _instances.Add(instance);
                _crosshairs.Add(crosshair);
            }

            _aircraft = station.Aircraft;
            _station = station.Station;
        }

        private static GameObject ResolveTurretCrosshairPrefab()
        {
            CombatHUD hud = CombatHUD.i;
            if (hud == null)
                return null;

            if (!Reflect.TryGetField<GameObject>(hud, "TurretUI", out GameObject turretUiPrefab) || turretUiPrefab == null)
                return null;

            HUDTurretState turretState = turretUiPrefab.GetComponent<HUDTurretState>();
            if (turretState == null)
                turretState = turretUiPrefab.GetComponentInChildren<HUDTurretState>(true);
            if (turretState == null)
                return null;

            return Reflect.TryGetField<GameObject>(turretState, "turretCrosshairPrefab", out GameObject prefab)
                ? prefab
                : null;
        }

        private void RefreshCrosshairs()
        {
            Camera camera = CameraStateManager.i != null ? CameraStateManager.i.mainCamera : Camera.main;
            if (camera == null)
                return;

            foreach (HUDTurretCrosshair crosshair in _crosshairs)
            {
                if (crosshair == null)
                    continue;

                crosshair.Refresh(camera, out _);
            }
        }
    }
}
