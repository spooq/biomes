using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Biomes.util;
using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Biomes;

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
    [JsonProperty] public AssetLocation[] blockCodes;

    [JsonProperty] public string comment;
}

public class BiomeConfigItem
{
    public List<string> biorealm = new();
    public string bioriver = "both";
}

public class BiomeConfigv1
{
    public readonly Dictionary<string, List<string>> BlockPatchBiomes = new();
    public readonly List<string> EntitySpawnWhiteList = new();
    public readonly Dictionary<string, List<string>> FruitTreeBiomes = new();
    public readonly Dictionary<string, List<string>> TreeBiomes = new();
}

public class BiomeConfigv2
{
    public Dictionary<string, BiomeConfigItem> BlockPatchBiomes = new();
    public List<string> EntitySpawnWhiteList = new();
    public Dictionary<string, BiomeConfigItem> FruitTreeBiomes = new();
    public Dictionary<string, BiomeConfigItem> TreeBiomes = new();
}

public class BiomeUserConfig
{
    public bool Debug = false;
    public List<string> EntitySpawnWhiteList = new();
    public bool FlipNorthSouth = false;
}

[ProtoContract]
public class BiomeNameAndCoords
{
    [ProtoMember(3)] public string biome;
    [ProtoMember(1)] public int chunkX;

    [ProtoMember(2)] public int chunkZ;
}

public class BiomesModSystem : ModSystem
{
    public const string MapRealmPropertyName = "biorealm";
    public const string MapRiverPropertyName = "bioriver";
    public const string MapHemispherePropertyName = "hemisphere";

    public const string EntityRealmPropertyName = "biorealm";
    public const string EntityRiverPropertyName = "bioriver";
    public const string EntitySeasonPropertyName = "bioseason";

    public const string MapChunkRiverArrayPropertyName = "bioriverarray";
    public const string MapChunkRiverBoolPropertyName = "bioriverbool";
    public BiomeConfigv2 BiomeConfig;
    public RealmCache Cache;
    private Commands _commands;

    public bool IsRiversModInstalled;

    public FruitTreeWorldGenConds[] originalFruitTrees = null;
    public RealmsConfig RealmsConfig;

    public ICoreServerAPI sapi;

    public bool TagOnChunkGen = true;

    public BiomeUserConfig UserConfig { get; private set; }

    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Server;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        sapi = api;
        Cache = new RealmCache();

        HarmonyPatches.Init(this);

        // Realms config
        RealmsConfig =
            JsonConvert.DeserializeObject<RealmsConfig>(sapi.Assets.Get("biomes:config/realms.json")
                .ToText())!;

        // BiomeConfig v2 is a superset of v1
        BiomeConfig = new BiomeConfigv2();

        // Version 1 config file format
        foreach (var biomeAsset in sapi.Assets.GetMany("config/biomes.json"))
        {
            var tmp = JsonConvert.DeserializeObject<BiomeConfigv1>(biomeAsset.ToText())!;

            foreach (var item in tmp.EntitySpawnWhiteList)
                BiomeConfig.EntitySpawnWhiteList.Add(item);
            foreach (var item in tmp.TreeBiomes)
                BiomeConfig.TreeBiomes[item.Key] = new BiomeConfigItem { biorealm = item.Value, bioriver = "both" };
            foreach (var item in tmp.FruitTreeBiomes)
                BiomeConfig.FruitTreeBiomes[item.Key] = new BiomeConfigItem
                    { biorealm = item.Value, bioriver = "both" };
            foreach (var item in tmp.BlockPatchBiomes)
                BiomeConfig.BlockPatchBiomes[item.Key] = new BiomeConfigItem
                    { biorealm = item.Value, bioriver = "both" };
        }

        // Version 2 config file format
        foreach (var biomeAsset in sapi.Assets.GetMany("config/biomes2.json"))
        {
            var tmp = JsonConvert.DeserializeObject<BiomeConfigv2>(biomeAsset.ToText())!;

            foreach (var item in tmp.EntitySpawnWhiteList)
                BiomeConfig.EntitySpawnWhiteList.Add(item);
            foreach (var item in tmp.TreeBiomes)
                BiomeConfig.TreeBiomes[item.Key] = item.Value;
            foreach (var item in tmp.FruitTreeBiomes)
                BiomeConfig.FruitTreeBiomes[item.Key] = item.Value;
            foreach (var item in tmp.BlockPatchBiomes)
                BiomeConfig.BlockPatchBiomes[item.Key] = item.Value;
        }

        // User config
        UserConfig = sapi.LoadModConfig<BiomeUserConfig>("biomes.json");
        UserConfig ??= new BiomeUserConfig();
        sapi.StoreModConfig(UserConfig, "biomes.json");

        if (UserConfig.FlipNorthSouth)
        {
            (RealmsConfig.NorthernRealms, RealmsConfig.SouthernRealms) = (RealmsConfig.SouthernRealms, RealmsConfig.NorthernRealms);
        }

        foreach (var item in UserConfig.EntitySpawnWhiteList)
            BiomeConfig.EntitySpawnWhiteList.Add(item);

        if (UserConfig.Debug)
        {
            var treeGenProps = api.Assets.Get("worldgen/treengenproperties.json").ToObject<TreeGenProperties>();

            List<BlockPatchWithComment> bpc = new();
            var blockpatchesfiles =
                api.Assets.GetMany<BlockPatchWithComment[]>(sapi.World.Logger, "worldgen/blockpatches/");
            foreach (var patches in blockpatchesfiles.Values)
                bpc.AddRange(patches);

            var fruitTreeBlocks = api.World.Blocks.Where(x => x is BlockFruitTreeBranch).ToList();

            foreach (var realm in RealmsConfig.AllRealms())
            {
                api.Logger.Debug($"BioRealm: {realm}");

                var output = "Trees: ";
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
                foreach (var item in BiomeConfig.FruitTreeBiomes)
                foreach (var fruitTreeBlock in fruitTreeBlocks)
                foreach (var fruitTreeWorldGenConds in fruitTreeBlock.Attributes["worldgen"]
                             .AsObject<FruitTreeWorldGenConds[]>())
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

        _commands = new Commands(this, sapi);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);

        IsRiversModInstalled = api.ModLoader.GetModSystem("RiversMod") != null ||
                               api.ModLoader.GetModSystem("RiverGenMod") != null;
    }

    public override void Dispose()
    {
        HarmonyPatches.Shutdown();
        base.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string NorthOrSouth(EnumHemisphere hemisphere, int realm)
    {
        // modconfig.flipworld exchanges the lists, so we always choose the same here no matter what
        return hemisphere == EnumHemisphere.North
            ? RealmsConfig.NorthernRealms[realm]
            : RealmsConfig.SouthernRealms[realm];
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

                var chunkHasRiver = false;
                var blockHasRiver = new bool[sapi.WorldManager.ChunkSize * sapi.WorldManager.ChunkSize];
                for (var x = 0; x < sapi.WorldManager.ChunkSize; x++)
                for (var z = 0; z < sapi.WorldManager.ChunkSize; z++)
                {
                    var xMag = flowVectorX[z * sapi.WorldManager.ChunkSize + x];
                    var zMag = flowVectorZ[z * sapi.WorldManager.ChunkSize + x];
                    var isRiver = float.Abs(xMag) > float.Epsilon || float.Abs(zMag) > float.Epsilon;
                    blockHasRiver[z * sapi.WorldManager.ChunkSize + x] = isRiver;
                    if (isRiver)
                        chunkHasRiver = true;
                }

                ModProperty.Set(chunk.MapChunk, MapChunkRiverArrayPropertyName, ref blockHasRiver);
                ModProperty.Set(chunk.MapChunk, MapChunkRiverBoolPropertyName, ref chunkHasRiver);
            }
        }
    }

    public void OnMapChunkGeneration(IMapChunk mapChunk, int chunkX, int chunkZ)
    {
        if (!TagOnChunkGen)
            return;

        var blockPos = new BlockPos(chunkX * sapi.WorldManager.ChunkSize, 0,
            chunkZ * sapi.WorldManager.ChunkSize, 0);
        var hemisphere = sapi.World.Calendar.GetHemisphere(blockPos);
        int currentRealm;
        var realmNames = new List<string>(4);
        CalculateValues(chunkX - 1, chunkZ, hemisphere, out currentRealm);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        CalculateValues(chunkX + 1, chunkZ, hemisphere, out currentRealm);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        CalculateValues(chunkX, chunkZ - 1, hemisphere, out currentRealm);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        CalculateValues(chunkX, chunkZ + 1, hemisphere, out currentRealm);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        CalculateValues(chunkX, chunkZ, hemisphere, out currentRealm);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        realmNames = realmNames.Distinct().ToList();

        realmNames.Capacity = realmNames.Count;
        ModProperty.Set(mapChunk, MapHemispherePropertyName, ref hemisphere);
        ModProperty.Set(mapChunk, MapRealmPropertyName, ref realmNames);
    }

    public bool IsWhiteListed(string name)
    {
        return BiomeConfig.EntitySpawnWhiteList.Any(x => WildcardUtil.Match(x, name));
    }

    public bool CheckSeason(BlockPos? pos, string[]? seasons)
    {
        if (pos is null || seasons is null || seasons.Length == 0 || seasons.Contains("all"))
            return true;

        return sapi.World.Calendar.GetSeason(pos) switch
        {
            EnumSeason.Spring => seasons.Contains("spring"),
            EnumSeason.Summer => seasons.Contains("summer"),
            EnumSeason.Fall => seasons.Contains("fall"),
            EnumSeason.Winter => seasons.Contains("winter"),
            _ => true
        };
    }

    public bool CheckRiver(IMapChunk mapChunk, string? biomeRiver, BlockPos? blockPos = null)
    {
        // NOTE: Right now, river implies fresh-water only.
        if (string.IsNullOrEmpty(biomeRiver) || biomeRiver == "both" || !IsRiversModInstalled)
            return true;

        // GenVegetationAndPatches.genPatches/genTrees/genShrubs only provides us with chunkX and chunkY, not the actual blockpos
        // In that case, use MapChunkRiverArrayPropertyName
        if (blockPos == null)
        {
            var isRiver = false;
            if (ModProperty.Get(mapChunk, MapChunkRiverBoolPropertyName, ref isRiver) == EnumCommandStatus.Error)
                return true;
            return isRiver;
        }

        bool[] boolArray = null;
        if (ModProperty.Get(mapChunk, MapChunkRiverArrayPropertyName, ref boolArray) == EnumCommandStatus.Error)
            return true;

        return boolArray[
            blockPos.Z % sapi.WorldManager.ChunkSize * sapi.WorldManager.ChunkSize +
            blockPos.X % sapi.WorldManager.ChunkSize];
    }

    public static List<string>? GetChunkRealms(IMapChunk mapChunk)
    {
        var realms = new List<string>();
        if (ModProperty.Get(mapChunk, MapRealmPropertyName, ref realms) == EnumCommandStatus.Error)
            return null;
        return realms;
    }

    public bool AllowEntitySpawn(IMapChunk mapChunk, EntityProperties type, BlockPos blockPos = null)
    {
        if (IsWhiteListed(type.Code))
            return true;

        // Test map chunk attributes
        var chunkRealms = new List<string>();
        if (ModProperty.Get(mapChunk, MapRealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
            return true;

        // Only blessed animals get in.
        if (type.Attributes == null || !type.Attributes.KeyExists(EntityRealmPropertyName))
        {
            if (UserConfig.Debug)
                sapi.Logger.Debug($"Entity {type.Code} is not blessed");
            return false;
        }

        var entityNativeRealms = type.Attributes[EntityRealmPropertyName].AsArray<string>();
        var result = entityNativeRealms.Intersect(chunkRealms).Any();

        if (IsRiversModInstalled && type.Attributes.KeyExists(EntityRiverPropertyName))
            result = result && CheckRiver(mapChunk, type.Attributes[EntityRiverPropertyName].AsString(), blockPos);

        if (type.Attributes.KeyExists(EntitySeasonPropertyName))
            result = result && CheckSeason(blockPos, type.Attributes[EntityRiverPropertyName].AsArray<string>());

        return result;
    }

    private void CalculateValues(int chunkX, int chunkZ, EnumHemisphere hemisphere, out int currentRealm)
    {
        var realmCount = hemisphere == EnumHemisphere.North
            ? RealmsConfig.NorthernRealms.Count
            : RealmsConfig.SouthernRealms.Count;

        var worldWidthInChunks = sapi.WorldManager.MapSizeX / sapi.WorldManager.ChunkSize;
        var realmWidthInChunks = worldWidthInChunks / (float)realmCount;
        currentRealm = 0;
        if (realmWidthInChunks != 0)
            currentRealm = (int)(chunkX / realmWidthInChunks);
        if (currentRealm >= realmCount)
            currentRealm = realmCount - 1;
        if (currentRealm < 0)
            currentRealm = 0;
    }

}