using HarmonyLib;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Biomes
{
    public class RealmsConfig
    {
        public List<string> NorthernRealms = new();
        public List<string> SouthernRealms = new();

        public List<string> AllRealms()
        {
            return NorthernRealms.Union(SouthernRealms).ToList();
        }
    }

    // Preserve block patch comments for debugging
    [JsonObject(MemberSerialization.OptIn)]
    public class BlockPatchWithComment
    {
        [JsonProperty]
        public AssetLocation[] blockCodes;

        [JsonProperty]
        public string comment;
    }

    public class BiomeConfigItem
    {
        public List<string> biorealm = new();
        public string bioriver = "both";
    }

    public class BiomeConfigv1
    {
        public List<string> EntitySpawnWhiteList = new();
        public Dictionary<string, List<string>> TreeBiomes = new();
        public Dictionary<string, List<string>> FruitTreeBiomes = new();
        public Dictionary<string, List<string>> BlockPatchBiomes = new();
    }

    public class BiomeConfigv2
    {
        public List<string> EntitySpawnWhiteList = new();
        public Dictionary<string, BiomeConfigItem> TreeBiomes = new();
        public Dictionary<string, BiomeConfigItem> FruitTreeBiomes = new();
        public Dictionary<string, BiomeConfigItem> BlockPatchBiomes = new();
    }

    public class BiomeUserConfig
    {
        public bool FlipNorthSouth = false;
        public List<string> EntitySpawnWhiteList = new();
        public bool Debug = false;
    }

    [ProtoContract]
    public class BiomeNameAndCoords
    {
        [ProtoMember(1)]
        public int chunkX;

        [ProtoMember(2)]
        public int chunkZ;

        [ProtoMember(3)]
        public string biome;
    }

    public class BiomesModSystem : ModSystem
    {
        public ICoreServerAPI sapi;

        public const string MapRealmPropertyName = "biorealm";
        public const string MapRiverPropertyName = "bioriver";
        public const string MapHemispherePropertyName = "hemisphere";

        public const string EntityRealmPropertyName = "biorealm";
        public const string EntityRiverPropertyName = "bioriver";
        public const string EntitySeasonPropertyName = "bioseason";

        public const string MapChunkRiverArrayPropertyName = "bioriverarray";
        public const string MapChunkRiverBoolPropertyName = "bioriverbool";

        public BiomeUserConfig UserConfig;
        public RealmsConfig RealmsConfig;
        public BiomeConfigv2 BiomeConfig;

        public bool TagOnChunkGen = true;

        public FruitTreeWorldGenConds[] originalFruitTrees = null;

        public bool IsRiversModInstalled = false;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            HarmonyPatches.Init(this);

            sapi = api;

            RealmsConfig = JsonConvert.DeserializeObject<RealmsConfig>(sapi.Assets.Get($"{Mod.Info.ModID}:config/realms.json").ToText());

            UserConfig = sapi.LoadModConfig<BiomeUserConfig>($"{Mod.Info.ModID}.json");
            UserConfig ??= new BiomeUserConfig();
            UserConfig.EntitySpawnWhiteList ??= new List<string>();
            sapi.StoreModConfig(UserConfig, $"{Mod.Info.ModID}.json");

            if (UserConfig.FlipNorthSouth)
            {
                var tmp = RealmsConfig.NorthernRealms;
                RealmsConfig.NorthernRealms = RealmsConfig.SouthernRealms;
                RealmsConfig.SouthernRealms = tmp;
            }

            BiomeConfig = new BiomeConfigv2();

            foreach (var item in UserConfig.EntitySpawnWhiteList)
                BiomeConfig.EntitySpawnWhiteList.Add(item);

            // Version 1 config file format
            foreach (var biomeAsset in sapi.Assets.GetMany("config/biomes.json"))
            {
                var tmp = JsonConvert.DeserializeObject<BiomeConfigv1>(biomeAsset.ToText());

                foreach (var item in tmp.EntitySpawnWhiteList)
                    BiomeConfig.EntitySpawnWhiteList.Add(item);
                foreach (var item in tmp.TreeBiomes)
                    BiomeConfig.TreeBiomes[item.Key] = new BiomeConfigItem { biorealm = item.Value, bioriver = "both" };
                foreach (var item in tmp.FruitTreeBiomes)
                    BiomeConfig.FruitTreeBiomes[item.Key] = new BiomeConfigItem { biorealm = item.Value, bioriver = "both" };
                foreach (var item in tmp.BlockPatchBiomes)
                    BiomeConfig.BlockPatchBiomes[item.Key] = new BiomeConfigItem { biorealm = item.Value, bioriver = "both" };
            }

            // Version 2 config file format
            foreach (var biomeAsset in sapi.Assets.GetMany("config/biomes2.json"))
            {
                var tmp = JsonConvert.DeserializeObject<BiomeConfigv2>(biomeAsset.ToText());

                foreach (var item in tmp.EntitySpawnWhiteList)
                    BiomeConfig.EntitySpawnWhiteList.Add(item);
                foreach (var item in tmp.TreeBiomes)
                    BiomeConfig.TreeBiomes[item.Key] = item.Value;
                foreach (var item in tmp.FruitTreeBiomes)
                    BiomeConfig.FruitTreeBiomes[item.Key] = item.Value;
                foreach (var item in tmp.BlockPatchBiomes)
                    BiomeConfig.BlockPatchBiomes[item.Key] = item.Value;
            }

            if (UserConfig.Debug)
            {
                var treeGenProps = api.Assets.Get("worldgen/treengenproperties.json").ToObject<TreeGenProperties>();

                List<BlockPatchWithComment> bpc = new();
                var blockpatchesfiles = api.Assets.GetMany<BlockPatchWithComment[]>(sapi.World.Logger, "worldgen/blockpatches/");
                foreach (var patches in blockpatchesfiles.Values)
                    bpc.AddRange(patches);

                var fruitTreeBlocks = api.World.Blocks.Where(x => x is BlockFruitTreeBranch).ToList();

                foreach (var realm in RealmsConfig.AllRealms())
                {
                    api.Logger.Debug($"BioRealm: {realm}");

                    string output = "Trees: ";
                    List<string> strs = new();
                    foreach (var item in BiomeConfig.TreeBiomes)
                        foreach (var treeGenConfig in treeGenProps.TreeGens)
                            if (WildcardUtil.Match(item.Key, treeGenConfig.Generator.GetName()))
                                if (item.Value.biorealm.Contains(realm))
                                    strs.Add(treeGenConfig.Generator.GetName());
                    api.Logger.Debug(output + string.Join(',', strs.Distinct().Order()));

                    strs.Clear();
                    output = "Shrubs: ";
                    foreach (var item in BiomeConfig.TreeBiomes)
                        foreach (var treeGenConfig in treeGenProps.ShrubGens)
                            if (WildcardUtil.Match(item.Key, treeGenConfig.Generator.GetName()))
                                if (item.Value.biorealm.Contains(realm))
                                    strs.Add(treeGenConfig.Generator.GetName());
                    api.Logger.Debug(output + string.Join(',', strs.Distinct().Order()));

                    strs.Clear();
                    output = "FruitTrees: ";
                    var fruitTreeList = new List<string>();
                    foreach (var item in BiomeConfig.FruitTreeBiomes)
                        foreach (var fruitTreeBlock in fruitTreeBlocks)
                            foreach (var fruitTreeWorldGenConds in fruitTreeBlock.Attributes["worldgen"].AsObject<FruitTreeWorldGenConds[]>())
                                if (WildcardUtil.Match(item.Key, fruitTreeWorldGenConds.Type))
                                    if (item.Value.biorealm.Contains(realm))
                                        strs.Add(fruitTreeWorldGenConds.Type);
                    api.Logger.Debug(output + string.Join(',', strs.Distinct().Order()));

                    strs.Clear();
                    output = "BlockPatches: ";
                    foreach (var item in BiomeConfig.BlockPatchBiomes)
                        foreach (var blockPatch in bpc)
                            if (blockPatch.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x)))
                                if (item.Value.biorealm.Contains(realm))
                                    strs.Add(blockPatch.comment);
                    api.Logger.Debug(output + string.Join(',', strs.Distinct())); // dont order these

                    strs.Clear();
                    output = "Entities: ";
                    foreach (var type in api.World.EntityTypes)
                        if (type.Attributes != null && type.Attributes.KeyExists(EntityRealmPropertyName))
                            if (type.Attributes[EntityRealmPropertyName].AsArray<string>().Contains(realm))
                                strs.Add(type.Code.ToString());
                    api.Logger.Debug(output + string.Join(',', strs.Distinct().Order()));
                }
            }

            BiomeConfig.EntitySpawnWhiteList = BiomeConfig.EntitySpawnWhiteList.Distinct().ToList();

            sapi.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Vegetation, "standard");
            sapi.Event.MapChunkGeneration(OnMapChunkGeneration, "standard");

            sapi.ChatCommands.Create("biome")
                .WithDescription("Biomes main command")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.gamemode)
                .BeginSubCommand("show")
                    .HandleWith(onShowBiomeCommand)
                .EndSubCommand()
                .BeginSubCommand("tree")
                    .HandleWith(onTreesCommand)
                .EndSubCommand()
                .BeginSubCommand("fruit")
                    .HandleWith(onFruitTreesCommand)
                .EndSubCommand()
                .BeginSubCommand("blockpatch")
                    .HandleWith(onBlockPatchCommand)
                .EndSubCommand()
                .BeginSubCommand("hemisphere")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("hemisphere", Enum.GetNames(typeof(EnumHemisphere))))
                    .HandleWith(onSetHemisphereCommand)
                .EndSubCommand()
                .BeginSubCommand("add")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", RealmsConfig.NorthernRealms.Union(RealmsConfig.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
                    .HandleWith(onAddRealmCommand)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", RealmsConfig.NorthernRealms.Union(RealmsConfig.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
                    .HandleWith(onRemoveRealmCommand)
                .EndSubCommand();
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            IsRiversModInstalled = api.ModLoader.GetModSystem("RiversMod") != null;
        }

        public override void Dispose()
        {
            HarmonyPatches.Shutdown();
            base.Dispose();
        }

        public String NorthOrSouth(EnumHemisphere hemisphere, int realm)
        {
            // modconfig.flipworld exchanges the lists, so we always choose the same here no matter what
            return hemisphere == EnumHemisphere.North ? RealmsConfig.NorthernRealms[realm] : RealmsConfig.SouthernRealms[realm];
        }

        public void OnChunkColumnGeneration(IChunkColumnGenerateRequest request)
        {
            if (!IsRiversModInstalled)
                return;

            foreach (var chunk in request.Chunks)
            {
                var arrayX = chunk.GetModdata("flowVectorsX");
                var arrayZ = chunk.GetModdata("flowVectorsZ");

                if (arrayX != null && arrayZ != null)
                {
                    var flowVectorX = SerializerUtil.Deserialize<float[]>(arrayX);
                    var flowVectorZ = SerializerUtil.Deserialize<float[]>(arrayZ);

                    bool chunkHasRiver = false;
                    var blockHasRiver = new bool[sapi.WorldManager.ChunkSize * sapi.WorldManager.ChunkSize];
                    for (int x = 0; x < sapi.WorldManager.ChunkSize; x++)
                    {
                        for (int z = 0; z < sapi.WorldManager.ChunkSize; z++)
                        {
                            float xMag = flowVectorX[z * sapi.WorldManager.ChunkSize + x];
                            float zMag = flowVectorZ[z * sapi.WorldManager.ChunkSize + x];
                            bool isRiver = float.Abs(xMag) > float.Epsilon || float.Abs(zMag) > float.Epsilon;
                            blockHasRiver[z * sapi.WorldManager.ChunkSize + x] = isRiver;
                            if (isRiver)
                                chunkHasRiver = true;
                        }
                    }

                    setModProperty(chunk.MapChunk, MapChunkRiverArrayPropertyName, ref blockHasRiver);
                    setModProperty(chunk.MapChunk, MapChunkRiverBoolPropertyName, ref chunkHasRiver);
                }
            }
        }

        public void OnMapChunkGeneration(IMapChunk mapChunk, int chunkX, int chunkZ)
        {
            if (!TagOnChunkGen)
                return;

            EnumHemisphere hemisphere;
            int currentRealm;
            var realmNames = new List<string>();
            CalculateValues(chunkX - 1, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(chunkX + 1, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(chunkX, chunkZ - 1, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(chunkX, chunkZ + 1, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(chunkX, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            realmNames = realmNames.Distinct().ToList();

            setModProperty(mapChunk, MapHemispherePropertyName, ref hemisphere);
            setModProperty(mapChunk, MapRealmPropertyName, ref realmNames);
        }

        public bool IsWhiteListed(string name)
        {
            return BiomeConfig.EntitySpawnWhiteList.Any(x => WildcardUtil.Match(x, name));
        }

        public bool CheckSeason(BlockPos pos, string[] seasons)
        {
            if (pos is null || seasons is null || !seasons.Any() || seasons.Contains("all"))
                return true;

            switch (sapi.World.Calendar.GetSeason(pos))
            {
                case EnumSeason.Spring:
                    return seasons.Contains("spring");
                case EnumSeason.Summer:
                    return seasons.Contains("summer");
                case EnumSeason.Fall:
                    return seasons.Contains("fall");
                case EnumSeason.Winter:
                    return seasons.Contains("winter");
                default:
                    return true;
            }
        }

        public bool CheckRiver(IMapChunk mapChunk, string biomeRiver, BlockPos blockPos = null)
        {
            // NOTE: Right now, river implies fresh-water only.
            if (string.IsNullOrEmpty(biomeRiver) || biomeRiver == "both" || !IsRiversModInstalled)
                return true;

            // GenVegetationAndPatches.genPatches/genTrees/genShrubs only provides us with chunkX and chunkY, not the actual blockpos
            // In that case, use MapChunkRiverArrayPropertyName
            if (blockPos == null)
            {
                bool isRiver = false;
                if (getModProperty(mapChunk, MapChunkRiverBoolPropertyName, ref isRiver) == EnumCommandStatus.Error)
                    return true;
                return isRiver;
            }
            else
            {
                bool[] boolArray = null;
                if (getModProperty(mapChunk, MapChunkRiverArrayPropertyName, ref boolArray) == EnumCommandStatus.Error)
                    return true;

                return boolArray[(blockPos.Z % sapi.WorldManager.ChunkSize) * sapi.WorldManager.ChunkSize + (blockPos.X % sapi.WorldManager.ChunkSize)];
            }
        }

        public bool AllowBlockPatchSpawn(IMapChunk mapChunk, BlockPatch blockPatch, Dictionary<string, BiomeConfigItem> biomeConfig, BlockPos blockPos = null)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(mapChunk, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            foreach (var item in biomeConfig)
            {
                if (blockPatch.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x)))
                {
                    return item.Value.biorealm.Intersect(chunkRealms).Any() && CheckRiver(mapChunk, item.Value.bioriver, blockPos);
                }
            }

            return false;
        }

        public bool AllowFruitTreeSpawn(IMapChunk mapChunk, FruitTreeWorldGenConds fruitTreeWorldGenConds, Dictionary<string, BiomeConfigItem> biomeConfig, BlockPos blockPos)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(mapChunk, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            foreach (var item in biomeConfig)
            {
                if (WildcardUtil.Match(item.Key, fruitTreeWorldGenConds.Type))
                {
                    return item.Value.biorealm.Intersect(chunkRealms).Any() && CheckRiver(mapChunk, item.Value.bioriver, blockPos);
                }
            }

            return false;
        }

        public bool AllowTreeShrubSpawn(IMapChunk mapChunk, TreeVariant treeVariant, Dictionary<string, BiomeConfigItem> biomeConfig)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(mapChunk, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            foreach (var item in biomeConfig)
            {
                if (WildcardUtil.Match(item.Key, treeVariant.Generator.GetName()))
                {
                    return item.Value.biorealm.Intersect(chunkRealms).Any() && CheckRiver(mapChunk, item.Value.bioriver); // treeVariant.Habitat
                }
            }

            return false;
        }

        public bool AllowEntitySpawn(IMapChunk mapChunk, EntityProperties type, BlockPos blockPos = null)
        {
            if (IsWhiteListed(type.Code.Path))
                return true;

            // Test map chunk attributes
            var chunkRealms = new List<string>();
            if (getModProperty(mapChunk, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            // Only blessed animals get in.
            if (type.Attributes == null || !type.Attributes.KeyExists(EntityRealmPropertyName))
                return false;
            var entityNativeRealms = type.Attributes[EntityRealmPropertyName].AsArray<string>();

            bool result = entityNativeRealms.Intersect(chunkRealms).Any();

            if (IsRiversModInstalled && type.Attributes.KeyExists(EntityRiverPropertyName))
                result = result && CheckRiver(mapChunk, type.Attributes[EntityRiverPropertyName].AsString(), blockPos);

            if (type.Attributes.KeyExists(EntitySeasonPropertyName))
                result = result && CheckSeason(blockPos, type.Attributes[EntityRiverPropertyName].AsArray<string>());

            return result;
        }

        public void CalculateValues(int chunkX, int chunkZ, out EnumHemisphere hemisphere, out int currentRealm)
        {
            BlockPos blockPos = new BlockPos(chunkX * sapi.WorldManager.ChunkSize, 0, chunkZ * sapi.WorldManager.ChunkSize, 0);
            hemisphere = sapi.World.Calendar.GetHemisphere(blockPos);

            int realmCount;
            if (hemisphere == EnumHemisphere.North)
                realmCount = RealmsConfig.NorthernRealms.Count;
            else
                realmCount = RealmsConfig.SouthernRealms.Count;

            int worldWidthInChunks = sapi.WorldManager.MapSizeX / sapi.WorldManager.ChunkSize;
            float realmWidthInChunks = worldWidthInChunks / (float)realmCount;
            currentRealm = 0;
            if (realmWidthInChunks != 0)
                currentRealm = (int)(chunkX / realmWidthInChunks);
            if (currentRealm >= realmCount)
                currentRealm = realmCount - 1;
            if (currentRealm < 0)
                currentRealm = 0;
        }

        public TextCommandResult onTreesCommand(TextCommandCallingArgs args)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(args.Caller, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get($"chunknotgenwithbiomes") };

            var trees = new List<string>();
            foreach (var item in BiomeConfig.TreeBiomes)
                if (item.Value.biorealm.Intersect(chunkRealms).Any())
                    trees.Add(item.Key);

            return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = trees.Distinct().Join(delimiter: "\r\n") };
        }

        public TextCommandResult onFruitTreesCommand(TextCommandCallingArgs args)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(args.Caller, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };

            var trees = new List<string>();
            foreach (var item in BiomeConfig.FruitTreeBiomes)
                if (item.Value.biorealm.Intersect(chunkRealms).Any())
                    trees.Add(item.Key);

            return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = trees.Distinct().Join(delimiter: "\r\n") };
        }

        public TextCommandResult onBlockPatchCommand(TextCommandCallingArgs args)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(args.Caller, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };

            var trees = new List<string>();
            foreach (var item in BiomeConfig.BlockPatchBiomes)
                if (item.Value.biorealm.Intersect(chunkRealms).Any())
                    trees.Add(item.Key);

            return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = trees.Distinct().Join(delimiter: "\r\n") };
        }

        public TextCommandResult onShowBiomeCommand(TextCommandCallingArgs args)
        {
            var chunkHemisphere = EnumHemisphere.North;
            if (getModProperty(args.Caller, MapHemispherePropertyName, ref chunkHemisphere) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };
            var hemisphereStr = Enum.GetName(typeof(EnumHemisphere), chunkHemisphere).ToLower();

            var chunkRealms = new List<string>();
            if (getModProperty(args.Caller, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };
            var realmsStr = chunkRealms?.Join(delimiter: ",");

            var riversModNotInstalled = Lang.Get("chunknotgenwithrivers");
            string chunkRiver = riversModNotInstalled;
            string blockRiver = riversModNotInstalled;
            if (IsRiversModInstalled)
            {
                bool cr = false;
                if (getModProperty(args.Caller, MapChunkRiverBoolPropertyName, ref cr) != EnumCommandStatus.Error)
                {
                    chunkRiver = cr ? "true" : "false";
                }
                else
                {
                    chunkRiver = "not set";
                }

                bool[] arr = null;
                if (getModProperty(args.Caller, MapChunkRiverArrayPropertyName, ref arr) != EnumCommandStatus.Error)
                {
                    BlockPos blockPos = args.Caller.Entity.Pos.AsBlockPos;
                    blockRiver = arr[(blockPos.Z % sapi.WorldManager.ChunkSize) * sapi.WorldManager.ChunkSize + (blockPos.X % sapi.WorldManager.ChunkSize)] ? "true" : "false";
                }
                else
                {
                    blockRiver = "not set";
                }
            }

            return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = $"Hemisphere: {hemisphereStr}\nRealms: {realmsStr}\nChunk has river: {chunkRiver}\nBlock has river: {blockRiver}" };
        }

        public TextCommandResult onSetHemisphereCommand(TextCommandCallingArgs args)
        {
            if (Enum.TryParse(args.Parsers[0].GetValue() as string, out EnumHemisphere hemisphere))
                return new TextCommandResult { Status = setModPropertyForCallerChunk(args.Caller, MapHemispherePropertyName, hemisphere) };

            return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = "Bad hemisphere argument" };
        }

        public TextCommandResult onAddRealmCommand(TextCommandCallingArgs args)
        {
            var currentRealms = new List<string>();
            getModProperty(args.Caller, MapRealmPropertyName, ref currentRealms);
            if (currentRealms == null)
                currentRealms = new();

            var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
            currentRealms.Add(value);

            return new TextCommandResult { Status = setModPropertyForCallerChunk(args.Caller, MapRealmPropertyName, currentRealms.Distinct()), StatusMessage = currentRealms?.Join(delimiter: ",") };
        }

        private TextCommandResult onRemoveRealmCommand(TextCommandCallingArgs args)
        {
            var currentRealms = new List<string>();
            getModProperty(args.Caller, MapRealmPropertyName, ref currentRealms);
            if (currentRealms == null)
                currentRealms = new();

            var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
            currentRealms.Remove(value);

            return new TextCommandResult { Status = setModPropertyForCallerChunk(args.Caller, MapRealmPropertyName, currentRealms.Distinct()), StatusMessage = currentRealms?.Join(delimiter: ",") };
        }

        public EnumCommandStatus setModPropertyForCallerChunk(Caller caller, string name, object value)
        {
            var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
            return setModProperty(chunk, name, ref value);
        }

        public EnumCommandStatus setModProperty<T>(IMapChunk chunk, string name, ref T value)
        {
            if (chunk == null)
                return EnumCommandStatus.Error;

            chunk.SetModdata(name, value);
            chunk.MarkDirty();
            return EnumCommandStatus.Success;
        }

        public EnumCommandStatus getModProperty<T>(Caller caller, string name, ref T value)
        {
            var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
            return getModProperty(chunk, name, ref value);
        }

        public EnumCommandStatus getModProperty<T>(IMapChunk chunk, string name, ref T value)
        {
            if (chunk == null)
                return EnumCommandStatus.Error;

            value = chunk.GetModdata<T>(name);
            return value == null ? EnumCommandStatus.Error : EnumCommandStatus.Success;
        }
    }
}
