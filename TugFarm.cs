using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("TugFarm", "RustFlash", "1.0.1")]
    [Description("Ermöglicht Spielern mit dem Tugboat zu farmen, indem Kisten gespawned werden, wenn es sich nicht bewegt.")]
    public class TugFarm : RustPlugin
    {
        private ConfigData configData;
        private Dictionary<ulong, Vector3> lastPositions = new Dictionary<ulong, Vector3>();
        private Dictionary<ulong, float> stationarySince = new Dictionary<ulong, float>();
        private const float MaxPlacementDistance = 5f; // Maximale Entfernung für die Platzierung des Netzes

        private class ConfigData
        {
            public int SpawnIntervalInSeconds { get; set; }
            public int MaxCrates { get; set; }
            public float MovementThreshold { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new ConfigData
            {
                SpawnIntervalInSeconds = 300, // Zeit in Sekunden, wie lange das Tugboat stillstehen muss, bevor Kisten gespawnt werden
                MaxCrates = 5, // Maximale Anzahl von Kisten, die in der Nähe des Tugboats gespawnt werden können
                MovementThreshold = 2f // Die minimale Bewegung in Metern, um als "bewegt" zu gelten
            }, true);
        }

        private void Init()
        {
            configData = Config.ReadObject<ConfigData>();
        }

        private void OnServerInitialized()
        {
            timer.Every(10f, () =>
            {
                foreach (var boat in BaseNetworkable.serverEntities.Select(x => x.GetComponent<BaseEntity>()).OfType<BaseBoat>())
                {
                    if (boat.ShortPrefabName.Equals("tugboat", System.StringComparison.OrdinalIgnoreCase))
                    {
                        CheckCrate(boat);
                    }
                }
            });
        }

        private void CheckCrate(BaseBoat boat)
        {
            ulong id = boat.net.ID != null ? boat.net.ID.Value : 0; // Konvertiere NetworkableId in ulong
            Vector3 currentPosition = boat.transform.position;
            if (!lastPositions.TryGetValue(id, out var lastPosition))
            {
                lastPositions[id] = currentPosition;
                stationarySince[id] = Time.realtimeSinceStartup;
                return;
            }

            if (Vector3.Distance(lastPosition, currentPosition) > configData.MovementThreshold)
            {
                stationarySince[id] = Time.realtimeSinceStartup;
                lastPositions[id] = currentPosition;
                return;
            }

            if (Time.realtimeSinceStartup - stationarySince[id] >= configData.SpawnIntervalInSeconds)
            {
                if (CountCratesNearby(boat.transform.position) < configData.MaxCrates)
                {
                    SpawnCrateNearBoat(boat);
                }
                stationarySince[id] = Time.realtimeSinceStartup;
            }
        }

        private int CountCratesNearby(Vector3 position)
        {
            int count = 0;
            foreach (var crate in UnityEngine.Object.FindObjectsOfType<LootContainer>())
            {
                if (Vector3.Distance(crate.transform.position, position) <= 10f)
                {
                    count++;
                }
            }
            return count;
        }

        private void SpawnCrateNearBoat(BaseBoat boat)
        {
            var position = boat.transform.position + boat.transform.forward * -2f + boat.transform.right * UnityEngine.Random.Range(-2f, 2f) + Vector3.up * 2f;
            var cratePrefab = UnityEngine.Random.Range(0, 2) == 0 ? "assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab" : "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab";
            var crate = GameManager.server.CreateEntity(cratePrefab, position, Quaternion.identity);
            if (crate)
            {
                crate.Spawn();
            }
        }

        private void OnPlayerUse(BasePlayer player)
        {
            if (player.IsBuildingBlocked()) return; // Überprüfen Sie, ob der Spieler in der Lage ist zu bauen

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, MaxPlacementDistance)) // MaxPlacementDistance ist die maximale Entfernung, in der das Netz platziert werden kann
            {
                BaseBoat boat = hit.transform.GetComponent<BaseBoat>();
                if (boat != null && boat.ShortPrefabName.Equals("tugboat", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Berechnen Sie die Platzierungspunkte am Tugboat
                    Vector3 placementPoint = hit.point; // Verwenden Sie den getroffenen Punkt als Platzierungspunkt

                    // Berechnen Sie die Rotation des Netzes basierend auf der Ausrichtung des Bootes
                    Quaternion rotation = Quaternion.LookRotation(placementPoint - boat.transform.position);

                    // Platzieren Sie das Netz
                    var nettingPrefab = "wall.frame.netting"; // Shortname des Netzes
                    var netting = GameManager.server.CreateEntity(nettingPrefab, placementPoint, rotation);
                    if (netting)
                    {
                        netting.Spawn();
                    }
                }
            }
        }
    }
}
