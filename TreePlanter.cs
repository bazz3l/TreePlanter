using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Tree Planter", "Bazz3l", "1.0.5")]
    [Description("Buy and plant trees where you are looking.")]
    class TreePlanter : RustPlugin
    {
        [PluginReference]
        Plugin ServerRewards, Economics;

        #region Fields
        private readonly int BlockedLayers = LayerMask.GetMask("Construction", "World", "Deployable", "Default");
        private readonly int AllowedLayers = LayerMask.GetMask("Terrain");
        private const string Perm = "treeplanter.use";
        private bool IsReady;
        #endregion

        #region Config
        private PluginConfig config;
        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig {
                SpawnItems = new Dictionary<string, SpawnItem> {
                    // Swamp
                    {"swamp-a", new SpawnItem("assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_a.prefab")},
                    {"swamp-b", new SpawnItem("assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_b.prefab")},
                    {"swamp-c", new SpawnItem("assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_c.prefab")},

                    // Douglas
                    {"douglas-a", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_a_snow.prefab")},
                    {"douglas-b", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_b_snow.prefab")},
                    {"douglas-c", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_c_snow.prefab")},

                    // Pine
                    {"pine-a", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_a_snow.prefab")},
                    {"pine-b", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_b snow.prefab")},
                    {"pine-c", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_c_snow.prefab")},

                    // Birch
                    {"birch-small", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_small_temp.prefab")},
                    {"birch-med", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_medium_temp.prefab")},
                    {"birch-tall", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_large_temp.prefab")},

                    // Oak
                    {"oak-a", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_a.prefab")},
                    {"oak-b", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_b.prefab")},
                    {"oak-c", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_c.prefab")},

                    // Palm
                    {"palm-small", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_small_c_entity.prefab")},
                    {"palm-med", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_med_a_entity.prefab")},
                    {"palm-tall", new SpawnItem("assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_tall_a_entity.prefab")}
                },
                UseServerRewards = true,
                UseEconomics     = false
            };
        }

        private class PluginConfig
        {
            public Dictionary<string, SpawnItem> SpawnItems;
            public bool UseServerRewards;
            public bool UseEconomics;
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
                {"Type", "Type: {0}, Price: ({1})RP"},
                {"NoPermission", "<color=#DC143C>Tree Planter</color>: You do not have permission."},
            }, this);
        }

        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        private void OnServerInitialized()
        {
            if (config.UseEconomics && Economics == null || config.UseServerRewards && ServerRewards == null)
            {
                Interface.Oxide.LogDebug("ServerRewards/Economics is required.");
                return;
            }

            IsReady = true;
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
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10f, BlockedLayers))
            {
                return false;
            }

            bool cast = Physics.Raycast(player.eyes.HeadRay(), out hit, 10f, AllowedLayers);
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

            if (config.UseServerRewards)
                ServerRewards?.Call<object>("TakePoints", player.userID, treeCost, null);

            if (config.UseEconomics)
                Economics?.Call<object>("Withdraw", player.userID, (double) treeCost);
            
            player.ChatMessage(Lang("Planted", player.UserIDString));
        }
        #endregion

        #region Commands
        [ChatCommand("tree")]
        private void BuyCommand(BasePlayer player, string cmd, string[] args)
        {
            if (player == null || !IsReady) return;

            if (!permission.UserHasPermission(player.UserIDString, Perm))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args == null || args.Length < 1)
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

            SpawnItem spawnItem = config.SpawnItems[treeType];

            if (config.UseServerRewards && ServerRewards?.Call<int>("CheckPoints", player.userID) < spawnItem.Cost)
            {
                player.ChatMessage(Lang("NoPoints", player.UserIDString));
                return;
            }

            if (config.UseEconomics && Economics?.Call<double>("Balance", player.userID) < (double) spawnItem.Cost)
            {
                player.ChatMessage(Lang("NoPoints", player.UserIDString));
                return;
            }

            PlantTree(player, spawnPos, spawnItem.Prefab, spawnItem.Cost);
        }
        #endregion

        #region Helpers
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}
