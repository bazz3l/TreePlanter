using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Tree Planter", "Bazz3l", "1.1.5")]
    [Description("Buy and plant trees in building authed areas using in-game currency.")]
    public class TreePlanter : RustPlugin
    {
        [PluginReference] Plugin ServerRewards, Economics, Clans;

        #region Fields

        private const string PermUse = "treeplanter.use";

        private PluginConfig _config;

        #endregion

        #region Config

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

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
                
                if (_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys)) return;
                
                PrintWarning("Loaded updated config.");
                    
                SaveConfig();
            }
            catch
            {
                PrintWarning("Loaded default config.");

                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private class PluginConfig
        {
            [JsonProperty("UseServerRewards (use server rewards as currency)")]
            public bool UseServerRewards;

            [JsonProperty("UseEconomics (use economics as currency)")]
            public bool UseEconomics;

            [JsonProperty("UseCurrency (use custom items as currency, by specifying the CurrencyItem)")]
            public bool UseCurrency;

            [JsonProperty("CurrencyItem (set an item id to use as currency, default is set to scrap)")]
            public int CurrencyItem;

            [JsonProperty("EnableOwner (enables owners to chop down trees)")]
            public bool EnableOwner;

            [JsonProperty("EnableClan (enables clan members to chop down trees)")]
            public bool EnableClan;

            [JsonProperty("AgriBlockTypes (specify which items should only be placed in a planter box)")]
            public Dictionary<string, bool> AgriBlockTypes;

            [JsonProperty("TreeItems (specify a list of tree items to buy)")]
            public Dictionary<string, TreeItem> TreeItems;

            public TreeItem FindTreeItemByName(string name)
            {
                TreeItem treeItem;
                return TreeItems.TryGetValue(name, out treeItem) ? treeItem : null;
            }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
            
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    UseServerRewards = false,
                    UseEconomics = false,
                    UseCurrency = true,
                    EnableOwner = false,
                    EnableClan = false,
                    CurrencyItem = -932201673,
                    AgriBlockTypes = new Dictionary<string, bool>
                    {
                        {"seed.black.berry", true},
                        {"seed.blue.berry", true},
                        {"seed.green.berry", true},
                        {"seed.yellow.berry", true},
                        {"seed.white.berry", true},
                        {"seed.red.berry", true},
                        {"seed.corn", true},
                        {"clone.corn", true},
                        {"seed.pumpkin", true},
                        {"clone.pumpkin", true},
                        {"seed.hemp", true},
                        {"clone.hemp", true}
                    },
                    TreeItems = new Dictionary<string, TreeItem>
                    {
                        {
                            "oak", new TreeItem("oak", new List<string>
                            {
                                "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_a.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_b.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_c.prefab"
                            })
                        },
                        {
                            "birch", new TreeItem("birch", new List<string>
                            {
                                "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_small_temp.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_medium_temp.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_large_temp.prefab"
                            })
                        },
                        {
                            "douglas", new TreeItem("douglas", new List<string>
                            {
                                "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_a_snow.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_b_snow.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_c_snow.prefab"
                            })
                        },
                        {
                            "swamp", new TreeItem("swamp", new List<string>
                            {
                                "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_a.prefab",
                                "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_b.prefab",
                                "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_c.prefab"
                            })
                        },
                        {
                            "palm", new TreeItem("palm", new List<string>
                            {
                                "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_small_c_entity.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_med_a_entity.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_tall_a_entity.prefab"
                            })
                        },
                        {
                            "pine", new TreeItem("pine", new List<string>
                            {
                                "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_a_snow.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_b snow.prefab",
                                "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_c_snow.prefab"
                            })
                        }
                    }
                };
            }
        }

        private class TreeItem
        {
            [JsonProperty("Name", Order = 0)]
            public string Name;
            
            [JsonProperty("Cost", Order = 1)]
            public int Cost = 10;
            
            [JsonProperty("Amount", Order = 2)]
            public int Amount = 1;
            
            [JsonProperty("Prefabs", Order = 3)]
            public readonly List<string> Prefabs;

            public TreeItem(string name, List<string> prefabs)
            {
                Name = name;
                Prefabs = prefabs;
            }

            public void GiveItem(BasePlayer player)
            {
                Item item = ItemManager.CreateByName("clone.hemp", Amount);
                item.name = Name;
                item.info.stackable = 1;

                player.GiveItem(item);
            }

            public static void RefundItem(BasePlayer player, Item item, GrowableGenes growableGenes)
            {
                Item refund = ItemManager.CreateByName(item.info.shortname, 1);
                refund.instanceData = new ProtoBuf.Item.InstanceData { };
                refund.instanceData.dataInt = GrowableGeneEncoding.EncodeGenesToInt(growableGenes);
                player.GiveItem(refund);
            }
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Prefix", "<color=#DC143C>Tree Planter</color>:"},
                {"NoPermission", "Unknown command: tree"},
                {"Authed", "You must have build privilege."},
                {"Balance", "You do not have enough for that."},
                {"Planter", "Sorry, must be planted in a planter."},
                {"Ground", "Sorry, must be planted in the ground."},
                {"Planted", "<color=#ffc55c>{0}</color> was successfully planted."},
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

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            if (!HasPermission(player, PermUse)) return;

            GrowableEntity entity = go.GetComponent<GrowableEntity>();
            if (entity == null || entity.GetParentEntity() is PlanterBox) return;

            Item item = player.GetActiveItem();
            if (item == null) return;

            NextTick(() =>
            {
                TreeItem treeItem = _config.FindTreeItemByName(item.name);

                if (treeItem != null)
                {
                    TryPlantTree(player, item, entity, treeItem);

                    return;
                }

                TryPlantSeed(player, item, entity);
            });
        }

        private object OnEntityAttack(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info?.Initiator == null || entity.OwnerID == 0) return null;

            if (!IsTreeEntity(entity.ShortPrefabName)) return null;

            BasePlayer player = info.Initiator as BasePlayer;
            if (player == null) return null;

            if (IsOwner(entity.OwnerID, player.userID)) return null;

            return false;
        }

        #endregion

        #region Core

        private void TryPlantTree(BasePlayer player, Item item, GrowableEntity entity, TreeItem treeItem)
        {
            if (player == null || item == null) return;

            if (entity.GetParentEntity() is PlanterBox)
            {
                treeItem.GiveItem(player);

                KillSeed(entity);

                player.ChatMessage(Lang("Ground", player.UserIDString));

                return;
            }

            if (!player.IsBuildingAuthed())
            {
                treeItem.GiveItem(player);

                KillSeed(entity);

                player.ChatMessage(Lang("Authed", player.UserIDString));

                return;
            }

            KillSeed(entity);

            PlantTree(player, entity, treeItem.Prefabs.GetRandom());

            player.ChatMessage(Lang("Planted", player.UserIDString, item.name.TitleCase()));
        }

        private void TryPlantSeed(BasePlayer player, Item item, GrowableEntity entity)
        {
            if (player == null || item == null || IsBlockedEntity(item.info.shortname)) return;

            TreeItem.RefundItem(player, item, entity.Genes);

            KillSeed(entity);

            player.ChatMessage(Lang("Planter", player.UserIDString));
        }

        private void ListItems(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();

            foreach (TreeItem treeItem in _config.TreeItems.Values)
            {
                sb.Append(Lang("TreeItem", player.UserIDString, treeItem.Name, treeItem.Cost));
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
            if (!IsValid(plant)) return;

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

            if (_config.UseCurrency && player.inventory.GetAmount(_config.CurrencyItem) >= treeCoat)
            {
                player.inventory.Take(null, _config.CurrencyItem, treeCoat);

                return true;
            }

            return false;
        }

        private bool IsOwner(ulong userID, ulong ownerID)
        {
            if (_config.EnableClan && InSameClan(userID, ownerID)) return true;

            return _config.EnableOwner && userID == ownerID;
        }

        private bool IsBlockedEntity(string shortname)
        {
            return _config.AgriBlockTypes.ContainsKey(shortname) && !_config.AgriBlockTypes[shortname];
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

        #region Command

        [ChatCommand("tree")]
        private void TreeCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PermUse))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));

                return;
            }

            if (args.Length != 1)
            {
                ListItems(player);

                return;
            }

            TreeItem treeItem = _config.FindTreeItemByName(string.Join(" ", args));

            if (treeItem == null)
            {
                player.ChatMessage(Lang("NotFound", player.UserIDString));

                return;
            }

            if (!Withdraw(player, treeItem.Cost))
            {
                player.ChatMessage(Lang("Balance", player.UserIDString));

                return;
            }

            treeItem.GiveItem(player);

            player.ChatMessage(Lang("Received", player.UserIDString, treeItem.Amount, treeItem.Name));
        }

        #endregion

        #region Helpers

        private bool HasPermission(BasePlayer player, string permName)
        {
            return player != null && permission.UserHasPermission(player.UserIDString, permName);
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }
        
        private bool IsValid(BaseEntity entity)
        {
            return entity != null && !entity.IsDestroyed;
        }

        private bool InSameClan(ulong userID, ulong targetID)
        {
            string playerClan = Clans?.Call<string>("GetClanOf", userID);

            if (string.IsNullOrEmpty(playerClan))
            {
                return false;
            }
            
            string targetClan = Clans?.Call<string>("GetClanOf", targetID);

            if (string.IsNullOrEmpty(targetClan))
            {
                return false;
            }

            return playerClan == targetClan;
        }
        
        #endregion
    }
}