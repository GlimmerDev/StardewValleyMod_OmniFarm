using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Minecarts;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using xTile;
using SObject = StardewValley.Object;

namespace OmniFarm
{
    public class OmniFarm : Mod
    {
        /*********
        ** Fields
        *********/
        private OmniFarmConfig Config;
        private MinecartDestinationData FarmDestination;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<OmniFarmConfig>();
            FarmDestination = CreateFarmDestination();

            helper.Events.Content.AssetRequested += OnAssetRequested;
            if (Config.addFarmMinecartDest)
                helper.Events.Content.AssetReady += OnAssetReady;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
        }

        /*********
        ** Private methods
        *********/
        /// <inheritdoc cref="IContentEvents.AssetRequested" />
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo(@"Maps\Farm_Combat"))
                e.LoadFromModFile<xTile.Map>(@"assets\Farm_OmniFarm.tmx", AssetLoadPriority.Exclusive);
            if (e.Name.IsEquivalentTo(@"Maps\FarmCave") && Config.useOptionalCave)
                e.LoadFromModFile<xTile.Map>(@"assets\FarmCave_OmniFarm.tmx", AssetLoadPriority.Exclusive);
            if (e.Name.IsEquivalentTo(@"Data\Minecarts") && Config.addFarmMinecartDest)
                e.Edit(EditMinecartsData);
        }

        /// <inheritdoc cref="IContentEvents.AssetReady"/>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnAssetReady(object sender, AssetReadyEventArgs e)
        {
            if (Game1.whichFarm != Farm.combat_layout)
                return;

            // Ensure Minecart data is always reloaded, so that the Farm location can be dynamically added
            // depending on the current location.
            if (e.Name.IsEquivalentTo(@"Data\Minecarts") && Config.addFarmMinecartDest)
                this.Helper.GameContent.InvalidateCache(@"Data\Minecarts");
        }

        /// <summary>Raised after the game begins a new day (including when the player loads a save).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnDayStarted(object sender, EventArgs e)
        {
            
            if (Game1.whichFarm == Farm.combat_layout)
            {
                ChangeWarpPoints();

                foreach (GameLocation GL in Game1.locations)
                {
                    if (GL is Farm ourFarm)
                    {
                        foreach (Vector2 tile in Config.stumpLocations)
                        {
                            ClearResourceClump(ourFarm.resourceClumps, tile);
                            ourFarm.addResourceClumpAndRemoveUnderlyingTerrain(ResourceClump.stumpIndex, 2, 2, tile);
                        }

                        foreach (Vector2 tile in Config.hollowLogLocations)
                        {
                            ClearResourceClump(ourFarm.resourceClumps, tile);
                            ourFarm.addResourceClumpAndRemoveUnderlyingTerrain(ResourceClump.hollowLogIndex, 2, 2, tile);
                        }

                        foreach (Vector2 tile in Config.meteoriteLocations)
                        {
                            ClearResourceClump(ourFarm.resourceClumps, tile);
                            ourFarm.addResourceClumpAndRemoveUnderlyingTerrain(ResourceClump.meteoriteIndex, 2, 2, tile);
                        }

                        foreach (Vector2 tile in Config.boulderLocations)
                        {
                            ClearResourceClump(ourFarm.resourceClumps, tile);
                            ourFarm.addResourceClumpAndRemoveUnderlyingTerrain(ResourceClump.boulderIndex, 2, 2, tile);
                        }

                        foreach (Vector2 tile in Config.largeRockLocations)
                        {
                            ClearResourceClump(ourFarm.resourceClumps, tile);
                            ourFarm.addResourceClumpAndRemoveUnderlyingTerrain(ResourceClump.mineRock1Index, 2, 2, tile);
                        }

                        //grass
                        if (Game1.IsWinter == false)
                            foreach (Vector2 tile in Config.getGrassLocations())
                            {
                                if (ourFarm.terrainFeatures.TryGetValue(tile, out TerrainFeature check))
                                {
                                    if (check is Grass grass)
                                        grass.numberOfWeeds.Value = Config.GrassGrowth_1forsparse_4forFull;
                                }
                                else
                                    ourFarm.terrainFeatures.Add(tile, new Grass(Grass.springGrass, Config.GrassGrowth_1forsparse_4forFull));
                            }

                        //mine
                        Random randomGen = new Random();
                        foreach (Vector2 tile in Config.getMineLocations())
                        {
                            if (ourFarm.isObjectAt((int)tile.X, (int)tile.Y))
                                continue;

                            //calculate ore spawn
                            if (Game1.player.hasSkullKey)
                            {
                                //5% chance of spawn ore
                                if (randomGen.NextDouble() < Config.oreChance)
                                {
                                    addRandomOre(ref ourFarm, ref randomGen, 4, tile);
                                    continue;
                                }
                            }
                            else
                            {
                                //check mine level
                                if (Game1.player.deepestMineLevel > 80) //gold level
                                {
                                    if (randomGen.NextDouble() < Config.oreChance)
                                    {
                                        addRandomOre(ref ourFarm, ref randomGen, 3, tile);
                                        continue;
                                    }
                                }
                                else if (Game1.player.deepestMineLevel > 40) //iron level
                                {
                                    if (randomGen.NextDouble() < Config.oreChance)
                                    {
                                        addRandomOre(ref ourFarm, ref randomGen, 2, tile);
                                        continue;
                                    }
                                }
                                else
                                {
                                    if (randomGen.NextDouble() < Config.oreChance)
                                    {
                                        addRandomOre(ref ourFarm, ref randomGen, 1, tile);
                                        continue;
                                    }
                                }
                            }

                            //if ore doesn't spawn then calculate gem spawn
                            //1% to spawn gem
                            if (randomGen.NextDouble() < Config.gemChance)
                            {
                                //0.1% chance of getting mystic stone
                                if (Game1.player.hasSkullKey)
                                    if (randomGen.Next(0, 100) < 1)
                                    {
                                        ourFarm.setObject(tile, createOre("mysticStone", tile));
                                        continue;
                                    }
                                    else
                                    if (randomGen.Next(0, 500) < 1)
                                    {
                                        ourFarm.setObject(tile, createOre("mysticStone", tile));
                                        continue;
                                    }

                                switch (randomGen.Next(0, 100) % 8)
                                {
                                    case 0: ourFarm.setObject(tile, createOre("gemStone", tile)); break;
                                    case 1: ourFarm.setObject(tile, createOre("diamond", tile)); break;
                                    case 2: ourFarm.setObject(tile, createOre("ruby", tile)); break;
                                    case 3: ourFarm.setObject(tile, createOre("jade", tile)); break;
                                    case 4: ourFarm.setObject(tile, createOre("amethyst", tile)); break;
                                    case 5: ourFarm.setObject(tile, createOre("topaz", tile)); break;
                                    case 6: ourFarm.setObject(tile, createOre("emerald", tile)); break;
                                    case 7: ourFarm.setObject(tile, createOre("aquamarine", tile)); break;
                                }
                                continue;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Constructs the Farm's minecart destination data.
        /// </summary>
        /// <returns></returns>
        private MinecartDestinationData CreateFarmDestination()
        {
            var farm_loc = new MinecartDestinationData();

            farm_loc.Id = "Farm";
            farm_loc.DisplayName = "Farm";
            farm_loc.Condition = null;
            farm_loc.Price = 0;
            farm_loc.BuyTicketMessage = null;
            farm_loc.TargetLocation = "Farm";
            farm_loc.TargetTile.X = 77;
            farm_loc.TargetTile.Y = 7;
            farm_loc.TargetDirection = null;
            farm_loc.CustomFields = null;

            return farm_loc;
        }

        /// <summary>
        /// Adds the Farm as a destination on for the Minecart network, if the current location isn't the Farm.
        /// </summary>
        /// <param name="asset"></param>
        private void EditMinecartsData(IAssetData asset)
        {
            // Don't edit the data if on the Farm (or if on a different farm type)
            if (Game1.whichFarm != Farm.combat_layout || Game1.player.currentLocation is Farm)
            {
                return;
            }

            var destinations = asset.AsDictionary<string, MinecartNetworkData>().Data["Default"].Destinations;
            destinations.Add(this.FarmDestination);
        }

        /// <summary>
        /// Updates the farm warp points to the proper coordinates.
        /// </summary>
        private void ChangeWarpPoints()
        {
            foreach (GameLocation GL in Game1.locations)
            {
                if (Config.WarpFromForest.X != -1)
                {
                    if (GL is Forest)
                    {
                        foreach (Warp w in GL.warps)
                        {
                            if (w.TargetName.ToLower().Contains("farm"))
                            {
                                w.TargetX = (int)Config.WarpFromForest.X;
                                w.TargetY = (int)Config.WarpFromForest.Y;
                            }
                        }
                    }
                }

                if (Config.WarpFromBackWood.X != -1)
                {
                    if (GL.Name.ToLower().Contains("backwood"))
                    {
                        foreach (Warp w in GL.warps)
                        {
                            if (w.TargetName.ToLower().Contains("farm"))
                            {
                                w.TargetX = (int)Config.WarpFromBackWood.X;
                                w.TargetY = (int)Config.WarpFromBackWood.Y;
                            }
                        }
                    }
                }

                if (Config.WarpFromBusStop.X != -1)
                {     
                    if (GL.Name.ToLower().Contains("busstop"))
                    {
                        foreach (Warp w in GL.warps)
                        {
                            if (w.TargetName.ToLower().Contains("farm"))
                            {
                                w.TargetX = (int)Config.WarpFromBusStop.X;
                                w.TargetY = (int)Config.WarpFromBusStop.Y;
                            }
                        }
                    }
                }
            }
        }

        private void addRandomOre(ref Farm input, ref Random randomGen, int highestOreLevel, Vector2 tileLocation)
        {
            switch (randomGen.Next(0, 100) % highestOreLevel)
            {
                case 0: input.setObject(tileLocation, createOre("copperStone", tileLocation)); break;
                case 1: input.setObject(tileLocation, createOre("ironStone", tileLocation)); break;
                case 2: input.setObject(tileLocation, createOre("goldStone", tileLocation)); break;
                case 3: input.setObject(tileLocation, createOre("iridiumStone", tileLocation)); break;
            }
        }

        private ItemMetadata getObjectMetadata(int id)
        {
            return ItemRegistry.GetMetadata(id.ToString());
        }

        private SObject getObject(int id)
        {
            return (SObject)getObjectMetadata(id).CreateItemOrErrorItem();
        }

        private SObject createOre(string oreName, Vector2 tileLocation)
        {
            switch (oreName)
            {
                case "mysticStone": return getObject(46);
                case "gemStone": return getObject((Game1.random.Next(7) + 1) * 2);
                case "diamond": return getObject(2);
                case "ruby": return getObject(4);
                case "jade": return getObject(6);
                case "amethyst": return getObject(8);
                case "topaz": return getObject(10);
                case "emerald": return getObject(12);
                case "aquamarine": return getObject(14);
                case "iridiumStone": return getObject(765);
                case "goldStone": return getObject(764);
                case "ironStone": return getObject(290);
                case "copperStone": return getObject(751);
                default: return null;
            }
        }

        private void ClearResourceClump(IList<ResourceClump> input, Vector2 tile)
        {
            for (int i = 0; i < input.Count; i++)
            {
                ResourceClump RC = input[i];
                if (RC.Tile == tile)
                {
                    input.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
