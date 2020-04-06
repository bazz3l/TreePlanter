using System.Collections.Generic;
using System.Text;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Tree Planter", "Bazz3l", "1.0.6")]
    [Description("Buy and plant trees where you are looking.")]
    class TreePlanter : RustPlugin
    {
        [PluginReference]
        Plugin ServerRewards, Economics;

        #region Fields
        readonly int BlockedLayers = LayerMask.GetMask("Construction", "World", "Deployable", "Default");
        readonly int AllowedLayers = LayerMask.GetMask("Terrain");
        const string Perm = "treeplanter.use";
        bool IsReady;
        #endregion

        #region Config
        PluginConfig config;
        PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                Items = new List<SpawnItem>
                {
                    // Swamp
                    new SpawnItem("swamp-a", "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_a.prefab"),
                    new SpawnItem("swamp-b", "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_b.prefab"),
                    new SpawnItem("swamp-c", "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_c.prefab"),

                    // Douglas
                    new SpawnItem("douglas-a", "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_a_snow.prefab"),
                    new SpawnItem("douglas-b", "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_b_snow.prefab"),
                    new SpawnItem("douglas-c", "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_c_snow.prefab"),

                    // Pine
                    new SpawnItem("pine-a", "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_a_snow.prefab"),
                    new SpawnItem("pine-b", "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_b snow.prefab"),
                    new SpawnItem("pine-c", "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_c_snow.prefab"),

                    // Birch
                    new SpawnItem("birch-small", "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_small_temp.prefab"),
                    new SpawnItem("birch-med", "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_medium_temp.prefab"),
                    new SpawnItem("birch-tall", "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_large_temp.prefab"),

                    // Oak
                    new SpawnItem("oak-a", "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_a.prefab"),
                    new SpawnItem("oak-b", "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_b.prefab"),
                    new SpawnItem("oak-c", "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_c.prefab"),

                    // Palm
                    new SpawnItem("palm-small", "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_small_c_entity.prefab"),
                    new SpawnItem("palm-med", "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_med_a_entity.prefab"),
                    new SpawnItem("palm-tall", "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_tall_a_entity.prefab")
                },
                UseServerRewards = true,
                UseEconomics     = false
            };
        }

        class PluginConfig
        {
            public List<SpawnItem> Items;
            public bool UseServerRewards;
            public bool UseEconomics;
            public SpawnItem FindByName(string Name) => Items.Find(x => x.Name == Name);
        }

        class SpawnItem
        {
            public string Name;
            public string Prefab;
            public int Cost = 10;
            public SpawnItem(string name, string prefab)
            {
                Name = name;
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
                {"ListTypes", "<color=#DC143C>Tree Planter</color>: /tree <type>{0}"},
                {"Type", "Type: {0}, Price: ({1})RP"},
                {"NoPermission", "<color=#DC143C>Tree Planter</color>: You do not have permission."},
            }, this);
        }

        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        void OnServerInitialized()
        {
            if (config.UseEconomics && Economics == null || config.UseServerRewards && ServerRewards == null)
            {
                Interface.Oxide.LogDebug("ServerRewards/Economics is required.");
                return;
            }

            IsReady = true;
        }

        void Init()
        {
            permission.RegisterPermission(Perm, this);

            config = Config.ReadObject<PluginConfig>();
        }
        #endregion

        #region Core
        bool IsValidSpawn(BasePlayer player, out Vector3 position)
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

        void PlantTree(BasePlayer player, Vector3 spawnPos, string treeType, int treeCost)
        {
            BaseEntity entity = GameManager.server.CreateEntity(treeType, spawnPos, Quaternion.identity);
            if (entity == null)
            {
                return;
            }

            entity.Spawn();

            if (config.UseServerRewards)
            {
                ServerRewards?.Call<object>("TakePoints", player.userID, treeCost, null);
            }

            if (config.UseEconomics)
            {
                Economics?.Call<object>("Withdraw", player.userID, (double) treeCost);
            }
            
            player.ChatMessage(Lang("Planted", player.UserIDString));
        }

        bool CheckBalance(BasePlayer player, int cost)
        {
            if (config.UseServerRewards && ServerRewards?.Call<int>("CheckPoints", player.userID) < cost)
            {
                return false;
            }

            if (config.UseEconomics && Economics?.Call<double>("Balance", player.userID) < (double) cost)
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Commands
        [ChatCommand("tree")]
        void BuyCommand(BasePlayer player, string cmd, string[] args)
        {
            if (player == null || !IsReady)
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, Perm))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length != 1)
            {
                StringBuilder sb = new StringBuilder();

                foreach (SpawnItem item in config.Items)
                {
                    sb.Append($"\n {item.Name}");
                }

                player.ChatMessage(Lang("ListTypes", player.UserIDString, sb.ToString()));
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
            
            SpawnItem spawnItem = config.FindByName(args[0]);

            if (spawnItem == null)
            {
                player.ChatMessage(Lang("InvalidType", player.UserIDString));
                return;
            }

            if (!CheckBalance(player, spawnItem.Cost))
            {
                player.ChatMessage(Lang("NoPoints", player.UserIDString));
                return;
            }

            PlantTree(player, spawnPos, spawnItem.Prefab, spawnItem.Cost);
        }
        #endregion

        #region Helpers
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}
