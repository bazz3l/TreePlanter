// Requires: ServerRewards

using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Tree Planter", "Bazz3l", "1.0.3")]
    [Description("Buy and plant trees where you are looking.")]
    class TreePlanter : RustPlugin
    {
        [PluginReference]
        Plugin ServerRewards;
        private readonly int BlockedLayers = LayerMask.GetMask("Construction", "World", "Deployable", "Default");
        private readonly int AllowedLayers = LayerMask.GetMask("Terrain");
        private const string Perm = "treeplanter.use";

        #region Config
        private PluginConfig config;
        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig {
                SpawnItems = new Dictionary<string, SpawnItem> {
                    {"birch", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_big_temp.prefab")},
                    {"oak", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_a.prefab")},
                    {"palm", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_med_a_entity.prefab")},
                }
            };
        }

        private class PluginConfig
        {
            public Dictionary<string, SpawnItem> SpawnItems;
        }
        #endregion

        #region Oxide
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                {"BuildBlocked", "<color=#DC143C>Tree Planter</color>: You are currently building Blocked."},
                {"NoTerrain", "<color=#DC143C>Tree Planter</color>: You are not looking at terrain."},
                {"NoPoints", "<color=#DC143C>Tree Planter</color>: You do not have enough points."},
                {"Planted", "<color=#DC143C>Tree Planter</color>: You planted a tree."},
                {"InvalidType", "<color=#DC143C>Tree Planter</color>: Invalid tree type."},
                {"ListTypes", "<color=#DC143C>Tree Planter</color>: /tree <type>\n{0}"},
                {"Type", "Type {0}, Price {1}"},
                {"NoPermission", "<color=#DC143C>Tree Planter</color>: You do not have permission."},
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private void Init()
        {
            permission.RegisterPermission(Perm, this);

            config = Config.ReadObject<PluginConfig>();
        }
        #endregion

        #region Core
        private bool IsValidSpawn(BasePlayer player, out Vector3 position)
        {
            position = Vector3.zero;

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 6f, BlockedLayers)) return false;
            bool cast = Physics.Raycast(player.eyes.HeadRay(), out hit, 6f, AllowedLayers);
            if (cast && hit.distance <= 5f)
            {
                position = hit.point;
                return true;
            }

            return false;
        }

        public void PlantTree(BasePlayer player, Vector3 spawnPos, string treeType, int treeCost)
        {
            BaseEntity entity = GameManager.server.CreateEntity(treeType, spawnPos) as BaseEntity;
            if (entity == null) return;
            entity.Spawn();

            ServerRewards?.Call<object>("TakePoints", player.userID, treeCost, null);
            
            player.ChatMessage(Lang("Planted", player.UserIDString));
        }

        private class SpawnItem
        {
            public int Cost = 10;
            public string Prefab = "";
            public SpawnItem(string prefab)
            {
                Prefab = prefab;
            }
        }
        #endregion

        #region Commands
        [ChatCommand("tree")]
        private void BuyCommand(BasePlayer player, string cmd, string[] args)
        {
            if (player == null || args == null) return;

            if (!permission.UserHasPermission(player.UserIDString, Perm))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                string items = string.Join("\n", config.SpawnItems.Select(x => Lang("Type", player.UserIDString, x.Key, x.Value.Cost)));
                player.ChatMessage(Lang("ListTypes", player.UserIDString, items));
                return;
            }

            if (player.IsBuildingBlocked())
            {
                player.ChatMessage(Lang("BuildBlocked", player.UserIDString));
                return;
            }

            Vector3 spawnPos;
            if (!IsValidSpawn(player, out spawnPos))
            {
                player.ChatMessage(Lang("NoTerrain", player.UserIDString));
                return;
            }
            
            string treeType = args[0];
            if (!config.SpawnItems.ContainsKey(treeType))
            {
                player.ChatMessage(Lang("InvalidType", player.UserIDString));
                return;
            }

            string treePrefab = config.SpawnItems[treeType].Prefab;
            int treeCost = config.SpawnItems[treeType].Cost;
            if (ServerRewards?.Call<int>("CheckPoints", player.userID) < treeCost)
            {
                player.ChatMessage(Lang("NoPoints", player.UserIDString));
                return;
            }

            PlantTree(player, spawnPos, treePrefab, treeCost);
        }
        #endregion

        #region Helpers
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}

