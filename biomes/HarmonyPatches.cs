using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Biomes
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        public static Harmony harmony;
        public static BiomesModSystem BiomesMod;

        public static void Init(BiomesModSystem mod)
        {
            BiomesMod = mod;
            harmony = new Harmony(BiomesMod.Mod.Info.ModID);
            harmony.PatchAll();
        }

        public static void Shutdown()
        {
            harmony.UnpatchAll(BiomesMod.Mod.Info.ModID);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
        public static bool TryPlaceBlockForWorldGenPrefix(ref BlockFruitTreeBranch __instance, out FruitTreeWorldGenConds[] __state, IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldgenRandom)
        {
            __state = __instance.WorldGenConds;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos); var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var newConds = new List<FruitTreeWorldGenConds>();
            foreach (var cond in __state)
            {
                foreach (var item in BiomesMod.ModConfig.FruitTreeBiomes)
                {
                    if (WildcardUtil.Match(item.Key, cond.Type) && item.Value.Intersect(chunkRealms).Any())
                    {
                        newConds.Add(cond);
                        break;
                    }
                }
            }

            __instance.WorldGenConds = newConds.ToArray();
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
        public static void TryPlaceBlockForWorldGenPostfix(ref BlockFruitTreeBranch __instance, FruitTreeWorldGenConds[] __state, IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldgenRandom)
        {
            __instance.WorldGenConds = __state;
        }

        [HarmonyPrefix]
        [HarmonyPriority(400)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesUnderTreePrefix(ref ForestFloorSystem __instance, out List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var blockPatchesField = Traverse.Create(__instance).Field("underTreePatches");
            var blockPatchesValue = blockPatchesField.GetValue() as List<BlockPatch>;
            __state = blockPatchesValue;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var blockPatches = new List<BlockPatch>();
            foreach (var bp in __state)
            {
                foreach (var item in BiomesMod.ModConfig.BlockPatchBiomes)
                {
                    if (bp.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x)) && item.Value.Intersect(chunkRealms).Any())
                    {
                        blockPatches.Add(bp);
                        break;
                    }
                }
            }

            blockPatchesField.SetValue(blockPatches);
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(400)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesUnderTreePostfix(ref ForestFloorSystem __instance, List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            Traverse.Create(__instance).Field("underTreePatches").SetValue(__state);
        }

        [HarmonyPrefix]
        [HarmonyPriority(300)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesOnTreePrefix(ref ForestFloorSystem __instance, out List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var blockPatchesField = Traverse.Create(__instance).Field("onTreePatches");
            var blockPatchesValue = blockPatchesField.GetValue() as List<BlockPatch>;
            __state = blockPatchesValue;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var blockPatches = new List<BlockPatch>();
            foreach (var bp in __state)
            {
                foreach (var item in BiomesMod.ModConfig.BlockPatchBiomes)
                {
                    if (bp.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x)) && item.Value.Intersect(chunkRealms).Any())
                    {
                        blockPatches.Add(bp);
                        break;
                    }
                }
            }

            blockPatchesField.SetValue(blockPatches);
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(300)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesOnTreePostfix(ref ForestFloorSystem __instance, List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            Traverse.Create(__instance).Field("onTreePatches").SetValue(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
        public static bool genPatchesPrefix(ref GenVegetationAndPatches __instance, out BlockPatch[] __state, int chunkX, int chunkZ, bool postPass)
        {
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
            __state = bpc.PatchesNonTree;

            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var blockPatches = new List<BlockPatch>();
            foreach (var bp in __state)
            {
                foreach (var item in BiomesMod.ModConfig.BlockPatchBiomes)
                {
                    if (bp.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x)) && item.Value.Intersect(chunkRealms).Any())
                    {
                        blockPatches.Add(bp);
                        break;
                    }
                }
            }

            bpc.PatchesNonTree = blockPatches.ToArray();
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
        public static void genPatchesPostfix(ref GenVegetationAndPatches __instance, BlockPatch[] __state, int chunkX, int chunkZ, bool postPass)
        {
            var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
            bpc.PatchesNonTree = __state;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
        public static bool genShrubsPrefix(ref GenVegetationAndPatches __instance, out List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            __state = treeGenProps.ShrubGens.ToList();
            
            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.ShrubGens)
            {
                var name = gen.Generator.GetName();
                foreach (var item in BiomesMod.ModConfig.TreeBiomes)
                {
                    if (WildcardUtil.Match(item.Key, name) && item.Value.Intersect(chunkRealms).Any())
                    {
                        treeVariants.Add(gen);
                        break;
                    }
                }
            }

            treeGenProps.ShrubGens = treeVariants.ToArray();
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
        public static void genShrubsPostfix(ref GenVegetationAndPatches __instance, List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            treeGenProps.ShrubGens = __state.ToArray();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genTrees")]
        public static bool genTreesPrefix(ref GenVegetationAndPatches __instance, out List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            __state = treeGenProps.TreeGens.ToList();
            
            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.TreeGens)
            {
                var name = gen.Generator.GetName();
                foreach (var item in BiomesMod.ModConfig.TreeBiomes)
                {
                    if (WildcardUtil.Match(item.Key, name) && item.Value.Intersect(chunkRealms).Any())
                    {
                        treeVariants.Add(gen);
                        break;
                    }
                }
            }

            treeGenProps.TreeGens = treeVariants.ToArray();
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genTrees")]
        public static void genTreesPostfix(ref GenVegetationAndPatches __instance, List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            treeGenProps.TreeGens = __state.ToArray();
        }

        // World-gen spawn
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCreatures), "CanSpawnAtPosition")]
        public static bool CanSpawnAtPosition(GenCreatures __instance, ref bool __result, IBlockAccessor blockAccessor, EntityProperties type, BlockPos pos, BaseSpawnConditions sc)
        {
            __result = BiomesMod.AllowEntitySpawn(blockAccessor.GetMapChunkAtBlockPos(pos), type);
            return __result;
        }

        // Run-time spawn
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt")]
        public static bool CanSpawnAt(ServerSystemEntitySpawner __instance, ref Vec3d __result, EntityProperties type, Vec3i spawnPosition, RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
        {
            return chunkCol.Any() && BiomesMod.AllowEntitySpawn(chunkCol[0].MapChunk, type);
        }

        /*
        // Vanilla bugfix
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
        static bool genPatches(ref GenVegetationAndPatches __instance, int chunkX, int chunkZ, bool postPass)
        {
            var sapi = Traverse.Create(__instance).Field("sapi").GetValue() as ICoreServerAPI;
            var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
            var rnd = Traverse.Create(__instance).Field("rnd").GetValue() as LCGRandom;
            var regionSize = (int)Traverse.Create(__instance).Field("regionSize").GetValue();
            var heightmap = Traverse.Create(__instance).Field("heightmap").GetValue() as ushort[];
            var tmpPos = Traverse.Create(__instance).Field("tmpPos").GetValue() as BlockPos;
            var chunksize = (int)Traverse.Create(__instance).Field("chunksize").GetValue();
            var worldheight = (int)Traverse.Create(__instance).Field("worldheight").GetValue();
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;

            var forestUpLeft = (int)Traverse.Create(__instance).Field("forestUpLeft").GetValue();
            var forestUpRight = (int)Traverse.Create(__instance).Field("forestUpRight").GetValue();
            var forestBotLeft = (int)Traverse.Create(__instance).Field("forestBotLeft").GetValue();
            var forestBotRight = (int)Traverse.Create(__instance).Field("forestBotRight").GetValue();

            var shrubUpLeft = (int)Traverse.Create(__instance).Field("shrubUpLeft").GetValue();
            var shrubUpRight = (int)Traverse.Create(__instance).Field("shrubUpRight").GetValue();
            var shrubBotLeft = (int)Traverse.Create(__instance).Field("shrubBotLeft").GetValue();
            var shrubBotRight = (int)Traverse.Create(__instance).Field("shrubBotRight").GetValue();

            var climateUpLeft = (int)Traverse.Create(__instance).Field("climateUpLeft").GetValue();
            var climateUpRight = (int)Traverse.Create(__instance).Field("climateUpRight").GetValue();
            var climateBotLeft = (int)Traverse.Create(__instance).Field("climateBotLeft").GetValue();
            var climateBotRight = (int)Traverse.Create(__instance).Field("climateBotRight").GetValue();

            var forestMod = (float)Traverse.Create(__instance).Field("forestMod").GetValue();
            var shrubMod = (float)Traverse.Create(__instance).Field("shrubMod").GetValue();

            int dx, dz, x, z;
            Block liquidBlock;
            int mapsizeY = blockAccessor.MapSizeY;

            var mapregion = sapi?.WorldManager.GetMapRegion((chunkX * chunksize) / regionSize, (chunkZ * chunksize) / regionSize);

            for (int i = 0; i < bpc.PatchesNonTree.Length; i++)
            {
                BlockPatch blockPatch = bpc.PatchesNonTree[i];
                if (blockPatch.PostPass != postPass) continue;

                float chance = blockPatch.Chance * bpc.ChanceMultiplier.nextFloat();

                while (chance-- > rnd.NextFloat())
                {
                    dx = rnd.NextInt(chunksize);
                    dz = rnd.NextInt(chunksize);
                    x = dx + chunkX * chunksize;
                    z = dz + chunkZ * chunksize;

                    int y = heightmap[dz * chunksize + dx];
                    if (y <= 0 || y >= worldheight - 15) continue;

                    tmpPos.Set(x, y, z);

                    liquidBlock = blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid);

                    // Place according to forest value
                    float forestRel = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)dx / chunksize, (float)dz / chunksize) / 255f;
                    forestRel = GameMath.Clamp(forestRel + forestMod, 0, 1);

                    float shrubRel = GameMath.BiLerp(shrubUpLeft, shrubUpRight, shrubBotLeft, shrubBotRight, (float)dx / chunksize, (float)dz / chunksize) / 255f;
                    shrubRel = GameMath.Clamp(shrubRel + shrubMod, 0, 1);

                    int climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

                    bool res = false;
                    IsPatchSuitableAtCrab(ref bpc, ref res, blockPatch, liquidBlock, mapsizeY, climate, y, forestRel, shrubRel);
                    if (res)
                    {
                        if (__instance.SkipGenerationAt(tmpPos, EnumWorldGenPass.Vegetation)) continue;

                        if (blockPatch.MapCode != null && rnd.NextInt(255) > __instance.GetPatchDensity(blockPatch.MapCode, x, z, mapregion))
                        {
                            continue;
                        }

                        int firstBlockId = 0;
                        bool found = true;

                        if (blockPatch.BlocksByRockType != null)
                        {
                            found = false;
                            int dy = 1;
                            while (dy < 5 && y - dy > 0)
                            {
                                string lastCodePart = blockAccessor.GetBlock(x, y - dy, z).LastCodePart();
                                if (__instance.RockBlockIdsByType.TryGetValue(lastCodePart, out firstBlockId)) { found = true; break; }
                                dy++;
                            }
                        }

                        if (found)
                        {
                            blockPatch.Generate(blockAccessor, rnd, x, y, z, firstBlockId);
                        }
                    }
                }
            }
            return false;
        }
        */

        public static void CrabPlaceSeaweed(ref BlockSeaweed __instance, IBlockAccessor blockAccessor, BlockPos pos, int depth)
        {
            var random = Traverse.Create(__instance).Field("random").GetValue() as Random;
            var blocks = Traverse.Create(__instance).Field("blocks").GetValue() as Block[];

            int height = Math.Min(depth - 1, 1 + random.Next(3) + random.Next(3));

            if (blocks == null)
            {
                blocks = new Block[]
                {
                    blockAccessor.GetBlock(new AssetLocation("seaweed-section")),
                    blockAccessor.GetBlock(new AssetLocation("seaweed-top")),
                };
            }

            while (height-- > 0)
            {
                blockAccessor.SetBlock(height == 0 ? blocks[1].BlockId : blocks[0].BlockId, pos);
                pos.Up();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockSeaweed), "TryPlaceBlockForWorldGen")]
        public static bool TryPlaceBlockForWorldGen(ref BlockSeaweed __instance, ref bool __result, IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            BlockPos belowPos = pos.DownCopy();

            Block block = blockAccessor.GetBlock(belowPos, BlockLayersAccess.Fluid);
            if (block.LiquidCode != "saltwater") return __result = false;

            int depth = 1;
            while (depth < 10)
            {
                belowPos.Down();
                block = blockAccessor.GetBlock(belowPos);

                if (block.Fertility > 0)
                {
                    belowPos.Up();
                    CrabPlaceSeaweed(ref __instance, blockAccessor, belowPos, depth);
                    __result = true;
                    return false;
                }
                else
                {
                    if (!block.IsLiquid()) return __result = false;
                }

                depth++;
            }

            return __result = false;
        }

        // Vanilla bugfix
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockPatchConfig), "IsPatchSuitableAt")]
        public static bool IsPatchSuitableAtCrab(ref BlockPatchConfig __instance, ref bool __result, BlockPatch patch, Block onBlock, int mapSizeY, int climate, int y, float forestRel, float shrubRel)
        {
            if ((patch.Placement == EnumBlockPatchPlacement.NearWater || patch.Placement == EnumBlockPatchPlacement.UnderWater) && onBlock.LiquidCode != "water") return __result = false;
            if ((patch.Placement == EnumBlockPatchPlacement.NearSeaWater || patch.Placement == EnumBlockPatchPlacement.UnderSeaWater) && onBlock.LiquidCode != "saltwater") return __result = false;

            if (forestRel < patch.MinForest || forestRel > patch.MaxForest || shrubRel < patch.MinShrub || forestRel > patch.MaxShrub)
            {
                // faster path without needing to fetch rainfall and temperature etc
                return __result = false;
            }

            int rain = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, y);
            float rainRel = rain / 255f;
            if (rainRel < patch.MinRain || rainRel > patch.MaxRain)
            {
                // again faster path without needing to fetch temperature etc
                return __result = false;
            }

            int temp = TerraGenConfig.GetScaledAdjustedTemperature((climate >> 16) & 0xff, y - TerraGenConfig.seaLevel);
            if (temp < patch.MinTemp || temp > patch.MaxTemp)
            {
                // again faster path without needing to fetch sealevel and fertility
                return __result = false;
            }

            float sealevelDistRel = ((float)y - TerraGenConfig.seaLevel) / ((float)mapSizeY - TerraGenConfig.seaLevel);
            if (sealevelDistRel < patch.MinY || sealevelDistRel > patch.MaxY)
            {
                return __result = false;
            }

            // finally test fertility (the least common blockpatch criterion)
            float fertilityRel = TerraGenConfig.GetFertility(rain, temp, sealevelDistRel) / 255f;
            __result = fertilityRel >= patch.MinFertility && fertilityRel <= patch.MaxFertility;
            return false;
        }
    }
}
