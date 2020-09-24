using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Tree Planter", "Bazz3l", "1.1.3")]
    [Description("Buy and plant trees in building authed areas using in-game currency.")]
    public class TreePlanter : RustPlugin
    {
        [PluginReference] Plugin ServerRewards, Economics;

        #region Fields
        
        private const string PermUse = "treeplanter.use";
        
        private ConfigData _config;
        
        #endregion

        #region Config
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData {
                UseServerRewards = false,
                UseEconomics = false,
                UseCurrency = true,
                OwnerOnly = false,
                CurrencyItemID = -932201673,
                Items = new List<TreeConfig> {
                    new TreeConfig("oak", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_c.prefab"
                    }),
                    new TreeConfig("birch", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_small_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_medium_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_large_temp.prefab"
                    }),
                    new TreeConfig("douglas", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_b_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_c_snow.prefab"
                    }),
                    new TreeConfig("swamp", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_c.prefab"
                    }),
                    new TreeConfig("palm", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_small_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_med_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_tall_a_entity.prefab"
                    }),
                    new TreeConfig("pine", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_b snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_c_snow.prefab"
                    })
                }
            };
        }

        private class ConfigData
        {
            public bool UseServerRewards;
            public bool UseEconomics;
            public bool UseCurrency;
            public bool OwnerOnly;
            public int CurrencyItemID;
            public List<TreeConfig> Items;

            public TreeConfig FindItemByName(string name) => Items.Find(x => x.Name == name);
        }

        private class TreeConfig
        {
            public string Name;
            public int Cost;
            public int Amount;
            public List<string> Prefabs;

            public TreeConfig(string name, List<string> prefabs)
            {
                Name = name;
                Prefabs = prefabs;
                Cost = 10;
                Amount = 1;
            }
        }
        
        #endregion

        #region Oxide
        
        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                {"Prefix", "<color=#DC143C>Tree Planter</color>:"},
                {"NoPermission", "No permission"},
                {"NotEnoughSpace", "You do not have enough inventory space."},
                {"Balance", "You do not have enough for that."},
                {"Planted", "You planted a tree."},
                {"Authed", "You must have build privlage."},
                {"Planter", "Can not be placed in a planter."},
                {"Given", "You received {0} ({1})."},
                {"Cost", "\n{0}, cost {1}."},
                {"Error", "Something went wrong."},
                {"Invalid", "Invalid type."},
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermUse, this);
        }

        private void Init()
        {
            _config = Config.ReadObject<ConfigData>();
        }

        private object OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            BaseEntity entity = info?.HitEntity;
            if (entity == null || !IsTree(entity.ShortPrefabName))
            {
                return null;
            }

            if (_config.OwnerOnly && !IsOwner(player.userID, entity.OwnerID))
            {
                info.damageTypes.ScaleAll(0.0f);

                return false;
            }

            return null;
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            if (!HasPermission(player))
            {
                return;
            }

            GrowableEntity plant = go.GetComponent<GrowableEntity>();
            if (plant == null)
            {
                return;
            }

            Item item = player.GetActiveItem();
            if (item == null)
            {
                return;
            }

            TreeConfig tree = _config.FindItemByName(item.name);
            if (tree == null)
            {
                return;
            }

            NextTick(() => {
                if (plant.GetParentEntity() is PlanterBox)
                {
                    RefundItem(player, item.name);
                    RemoveEntity(plant);

                    player.ChatMessage(Lang("Planter", player.UserIDString));
                    return;
                }

                if (!player.IsBuildingAuthed())
                {
                    RefundItem(player, item.name);
                    RemoveEntity(plant);

                    player.ChatMessage(Lang("Authed", player.UserIDString));
                    return;
                }

                PlantTree(player, plant, tree.Prefabs.GetRandom());
            });
        }
        
        #endregion

        #region Core
        
        private void PlantTree(BasePlayer player, GrowableEntity plant, string prefabName)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefabName, plant.transform.position, Quaternion.identity);
            if (entity == null)
            {
                return;
            }

            entity.OwnerID = player.userID;
            entity.Spawn();

            RemoveEntity(plant);

            player.ChatMessage(Lang("Planted", player.UserIDString));
        }

        private bool CheckBalance(BasePlayer player, int cost)
        {
            if (_config.UseServerRewards && ServerRewards)
            {
                return ServerRewards.Call<int>("CheckPoints", player.userID) >= cost;
            }
            
            if (_config.UseEconomics && Economics)
            {
                return Economics.Call<double>("Balance", player.userID) >= (double) cost;
            }
            
            if (_config.UseCurrency)
            {
                return player.inventory.GetAmount(_config.CurrencyItemID) >= cost;
            }

            return false;
        }

        private void BalanceTake(BasePlayer player, int cost)
        {
            if (_config.UseServerRewards && ServerRewards)
            {
                ServerRewards.Call<object>("TakePoints", player.userID, cost, null);
            }
            
            if (_config.UseEconomics && Economics)
            {
                Economics.Call<object>("Withdraw", player.userID, (double) cost);
            }
            
            if (_config.UseCurrency)
            {
                player.inventory.Take(new List<Item>(), _config.CurrencyItemID, cost);
            }
        }

        private static Item CreateItem(string treeType, int treeAmount = 1)
        {
            Item item = ItemManager.CreateByName("clone.hemp", treeAmount);
            item.name = treeType;
            item.info.stackable = 1;
            
            return item;
        }

        private void RefundItem(BasePlayer player, string treeType)
        {
            Item refundItem = CreateItem(treeType);
            if (refundItem == null)
            {
                player.ChatMessage(Lang("Error", player.UserIDString));
                return;
            }

            player.GiveItem(refundItem);
        }

        private static void RemoveEntity(GrowableEntity plant)
        {
            if (plant != null && !plant.IsDestroyed)
            {
                plant.Kill();
            }
        }

        private static bool IsOwner(ulong userID, ulong ownerID)
        {
            return userID == ownerID;
        }

        private static bool IsTree(string prefab)
        {
            if (prefab.Contains("oak_") 
            || prefab.Contains("birch_") 
            || prefab.Contains("douglas_") 
            || prefab.Contains("swamp_") 
            || prefab.Contains("palm_") 
            || prefab.Contains("pine_"))
            {
                return true;
            }

            return false;
        }

        private static bool NotEnoughSpace(BasePlayer player)
        {
            return player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull();
        }

        private void ListTypes(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(Lang("Prefix", player.UserIDString));

            foreach (TreeConfig tc in _config.Items)
            {
                sb.Append(Lang("Cost", player.UserIDString, tc.Name, tc.Cost));
            }

            player.ChatMessage(sb.ToString());
        }

        #endregion

        #region Commands
        
        [ChatCommand("tree")]
        private void BuyCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length != 1)
            {
                ListTypes(player);
                return;
            }

            TreeConfig tree = _config.FindItemByName(string.Join(" ", args));
            if (tree == null)
            {
                player.ChatMessage(Lang("Invalid", player.UserIDString));
                return;
            }
            
            if (NotEnoughSpace(player))
            {
                player.ChatMessage(Lang("NotEnoughSpace", player.UserIDString));
                return;
            }

            if (!CheckBalance(player, tree.Cost))
            {
                player.ChatMessage(Lang("Balance", player.UserIDString));
                return;
            }

            Item item = CreateItem(tree.Name, tree.Amount);
            if (item == null)
            {
                player.ChatMessage(Lang("Error", player.UserIDString));
                return;
            }

            BalanceTake(player, tree.Cost);

            player.GiveItem(item);

            player.ChatMessage(Lang("Given", player.UserIDString, tree.Amount, tree.Name));
        }
        
        #endregion
        
        #region Helpers
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private bool HasPermission(BasePlayer player) => permission.UserHasPermission(player.UserIDString, PermUse);

        #endregion
    }
}