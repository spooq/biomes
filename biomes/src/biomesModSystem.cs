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


// HERE YE, HERE YE
// Biomes is a mod that a significant portion runs in a very hot loop,
// that being worldgen. The hot path code is deliberately written to use the
// fastest as is reasonable constructs.
// No super low level shenanigans are used, but attention was paid to things
// such as choice of hashing algorithm and avoiding heavy constructs like linq in favor of
// just imperative iteration.
// This is not a lack of best practice, it was a deliberate choice for speed’s sake.
// Please keep this in mind for any changes you wish to make. If you are unsure how to
// implement an idea that needs hot path code, make an issue or ask in the discord thread
// about it.
public class BiomesModSystem : ModSystem
{
    public BiomeConfigv2 BiomeConfig = null!;
    public RealmCache Cache = null!;
    public Entities Entities = null!;
    private Commands _commands;

    public bool IsRiversModInstalled = false;

    public RealmsConfig RealmsConfig = null!;

    public ICoreServerAPI _vsapi;

    public bool TagOnChunkGen = true;

    public BiomeUserConfig UserConfig { get; private set; }

    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Server;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        _vsapi = api;
        Cache = new RealmCache();
        Entities = new Entities(this, _vsapi);

        HarmonyPatches.Init(this);

        // Realms config
        RealmsConfig =
            JsonConvert.DeserializeObject<RealmsConfig>(_vsapi.Assets.Get($"{Mod.Info.ModID}:config/realms.json")
                .ToText())!;

        // BiomeConfig v2 is a superset of v1
        BiomeConfig = new BiomeConfigv2();

        // Version 1 config file format
        foreach (var biomeAsset in _vsapi.Assets.GetMany("config/biomes.json"))
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
        foreach (var biomeAsset in _vsapi.Assets.GetMany("config/biomes2.json"))
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
        UserConfig = _vsapi.LoadModConfig<BiomeUserConfig>("biomes.json");
        UserConfig ??= new BiomeUserConfig();
        _vsapi.StoreModConfig(UserConfig, "biomes.json");

        if (UserConfig.FlipNorthSouth)
        {
            (RealmsConfig.NorthernRealms, RealmsConfig.SouthernRealms) = (RealmsConfig.SouthernRealms, RealmsConfig.NorthernRealms);
        }

        foreach (var item in UserConfig.EntitySpawnWhiteList)
            BiomeConfig.EntitySpawnWhiteList.Add(item);
        
        Entities.BuildCaches(UserConfig);

        if (UserConfig.Debug)
        {
            var treeGenProps = api.Assets.Get("worldgen/treengenproperties.json").ToObject<TreeGenProperties>();

            List<BlockPatchWithComment> bpc = new();
            var blockpatchesfiles =
                api.Assets.GetMany<BlockPatchWithComment[]>(_vsapi.World.Logger, "worldgen/blockpatches/");
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
                    if (type.Attributes != null && type.Attributes.KeyExists(ModPropName.Entity.Realm))
                        if (type.Attributes[ModPropName.Entity.Realm].AsArray<string>().Contains(realm))
                            strs.Add(type.Code.ToString());
                api.Logger.Debug(output + string.Join(',', strs.Distinct().Order()));
            }
        }

        BiomeConfig.EntitySpawnWhiteList = BiomeConfig.EntitySpawnWhiteList.Distinct().ToList();

        _vsapi.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Vegetation, "standard");
        _vsapi.Event.MapChunkGeneration(OnMapChunkGeneration, "standard");

        _commands = new Commands(this, _vsapi);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);

        /*
        IsRiversModInstalled = api.ModLoader.GetModSystem("RiversMod") != null ||
                               api.ModLoader.GetModSystem("RiverGenMod") != null;
                               */
        
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
                var blockHasRiver = new bool[_vsapi.WorldManager.ChunkSize * _vsapi.WorldManager.ChunkSize];
                for (var x = 0; x < _vsapi.WorldManager.ChunkSize; x++)
                for (var z = 0; z < _vsapi.WorldManager.ChunkSize; z++)
                {
                    var xMag = flowVectorX[z * _vsapi.WorldManager.ChunkSize + x];
                    var zMag = flowVectorZ[z * _vsapi.WorldManager.ChunkSize + x];
                    var isRiver = float.Abs(xMag) > float.Epsilon || float.Abs(zMag) > float.Epsilon;
                    blockHasRiver[z * _vsapi.WorldManager.ChunkSize + x] = isRiver;
                    if (isRiver)
                        chunkHasRiver = true;
                }

                ModProperty.Set(chunk.MapChunk, ModPropName.MapChunk.RiverArray, ref blockHasRiver);
                ModProperty.Set(chunk.MapChunk, ModPropName.MapChunk.RiverBool, ref chunkHasRiver);
            }
        }
    }

    public void OnMapChunkGeneration(IMapChunk mapChunk, int chunkX, int chunkZ)
    {
        if (!TagOnChunkGen)
            return;

        var blockPos = new BlockPos(chunkX * _vsapi.WorldManager.ChunkSize, 0,
            chunkZ * _vsapi.WorldManager.ChunkSize, 0);
        var hemisphere = _vsapi.World.Calendar.GetHemisphere(blockPos);
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
        ModProperty.Set(mapChunk, ModPropName.Map.Hemisphere, ref hemisphere);
        ModProperty.Set(mapChunk, ModPropName.Map.Realm, ref realmNames);
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
            if (ModProperty.Get(mapChunk, ModPropName.MapChunk.RiverBool, ref isRiver) == EnumCommandStatus.Error)
                return true;
            return isRiver;
        }

        bool[] boolArray = null;
        if (ModProperty.Get(mapChunk, ModPropName.MapChunk.RiverArray, ref boolArray) == EnumCommandStatus.Error)
            return true;

        return boolArray[
            blockPos.Z % _vsapi.WorldManager.ChunkSize * _vsapi.WorldManager.ChunkSize +
            blockPos.X % _vsapi.WorldManager.ChunkSize];
    }

    private void CalculateValues(int chunkX, int chunkZ, EnumHemisphere hemisphere, out int currentRealm)
    {
        var realmCount = hemisphere == EnumHemisphere.North
            ? RealmsConfig.NorthernRealms.Count
            : RealmsConfig.SouthernRealms.Count;

        var worldWidthInChunks = _vsapi.WorldManager.MapSizeX / _vsapi.WorldManager.ChunkSize;
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