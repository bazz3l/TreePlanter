using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Rust;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Tree Planter", "Bazz3l", "1.1.4")]
    [Description("Buy and plant trees in building authed areas using in-game currency.")]
    public class TreePlanter : RustPlugin
    {
        [PluginReference] Plugin ServerRewards, Economics;

        #region Fields
        
        private const string PermUse = "treeplanter.use";

        private PluginConfig _config;
        
        #endregion

        #region Config
        
        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                LoadDefaultConfig();

                PrintError("Config file contains an error and has been replaced with the default file.");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);
        
        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig {
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

        private class PluginConfig
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
            public int Cost = 10;
            public int Amount = 1;
            public readonly List<string> Prefabs;

            public TreeConfig(string name, List<string> prefabs)
            {
                Name = name;
                Prefabs = prefabs;
            }
        }
        
        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                {"Prefix", "<color=#DC143C>Tree Planter</color>:"},
                {"NoPermission", "Unknown command: tree"},
                {"Authed", "You must have build privilege."},
                {"Balance", "You do not have enough for that."},
                {"Planted", "You've successfully planted a tree."},
                {"Planter", "Sorry, must be planted in the ground ."},
                {"Received", "You've purchased <color=#ffc55c>{0}x</color> <color=#ffc55c>{1}</color>."},
                {"TreeItem", "\n<color=#ffc55c>{0}</color> cost <color=#ffc55c>${1}</color>."},
                {"NotFound", "Sorry, item not found."},
                {"Error", "Something went wrong."},
            }, this);
        }

        #endregion

        #region Oxide

        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }

        private object OnEntityAttack(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info?.InitiatorPlayer == null || !_config.OwnerOnly)
            {
                return null;
            }

            if (!IsTreeEntity(entity.ShortPrefabName))
            {
                return null;
            }

            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null)
            {
                return null;
            }
            
            if (entity.OwnerID != 0 && !IsOwner(entity.OwnerID, attacker.userID))
            {
                info.damageTypes  = new DamageTypeList();
                info.DoHitEffects = false;

                return false;
            }

            return null;
        }

        private void OnEntityBuilt(Planner plan, GameObject seed)
        {
            BasePlayer player = plan.GetOwnerPlayer();

            if (player == null || !permission.UserHasPermission(player.UserIDString, PermUse))
            {
                return;
            }

            GrowableEntity plant = seed.GetComponent<GrowableEntity>();
            if (plant == null)
            {
                return;
            }

            Item item = player.GetActiveItem();
            if (item == null)
            {
                return;
            }

            TreeConfig treeConfig = _config.FindItemByName(item.name);
            if (treeConfig == null)
            {
                return;
            }

            NextTick(() => {
                if (plant.GetParentEntity() is PlanterBox)
                {
                    RefundItem(player, item.name);
                    
                    KillSeed(plant);

                    player.ChatMessage(Lang("Planter", player.UserIDString));
                    
                    return;
                }
                
                if (!player.IsBuildingAuthed())
                {
                    RefundItem(player, item.name);

                    KillSeed(plant);

                    player.ChatMessage(Lang("Authed", player.UserIDString));
                    
                    return;
                }
                
                PlantTree(player, plant, treeConfig.Prefabs.GetRandom());

                KillSeed(plant);

                player.ChatMessage(Lang("Planted", player.UserIDString));
            });
        }

        #endregion

        #region Command
        
        [ChatCommand("tree")]
        private void BuyCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length != 1)
            {
                ListItems(player);
                return;
            }

            TreeConfig treeConfig = _config.FindItemByName(string.Join(" ", args));
            if (treeConfig == null)
            {
                player.ChatMessage(Lang("NotFound", player.UserIDString));
                return;
            }

            if (!Withdraw(player, treeConfig.Cost))
            {
                player.ChatMessage(Lang("Balance", player.UserIDString));
                return;
            }
            
            player.GiveItem(CreateItem(treeConfig.Name, treeConfig.Amount));

            player.ChatMessage(Lang("Received", player.UserIDString, treeConfig.Amount, treeConfig.Name));
        }
        
        #endregion

        #region Core

        private void ListItems(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();

            foreach (TreeConfig tc in _config.Items)
            {
                sb.Append(Lang("TreeItem", player.UserIDString, tc.Name, tc.Cost));
            }

            player.ChatMessage(Lang("Prefix", player.UserIDString) + sb);
        }

        private void PlantTree(BasePlayer player, GrowableEntity plant, string prefabName)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefabName, plant.ServerPosition, Quaternion.identity);
            if (entity == null)
            {
                return;
            }

            entity.OwnerID = player.userID;
            entity.Spawn();
        }
        
        private void KillSeed(GrowableEntity plant)
        {
            if (!IsValid(plant))
            {
                return;
            }
            
            plant.Kill();
        }

        private bool Withdraw(BasePlayer player, int treeCoat)
        {
            if (treeCoat == 0)
            {
                return true;
            }
            
            if (_config.UseServerRewards && ServerRewards != null)
            {
                return ServerRewards.Call<object>("TakePoints", player.userID, treeCoat) != null;
            }

            if (_config.UseEconomics && Economics != null)
            {
                return Economics.Call<bool>("Withdraw", player.userID, (double) treeCoat);
            }

            if (_config.UseCurrency && player.inventory.GetAmount(_config.CurrencyItemID) >= treeCoat)
            {
                player.inventory.Take(null, _config.CurrencyItemID, treeCoat);
                
                return true;
            }

            return false;
        }

        private Item CreateItem(string treeType, int treeAmount = 1)
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

        private bool IsOwner(ulong userID, ulong ownerID)
        {
            return userID == ownerID;
        }

        private bool IsTreeEntity(string prefab)
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
        
        #endregion

        #region Helpers
        
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private bool IsValid(BaseEntity entity)
        {
            return entity != null && !entity.IsDestroyed;
        }
        
        #endregion
    }
}