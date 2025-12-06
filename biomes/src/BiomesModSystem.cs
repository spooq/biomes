using Biomes.RealmGen;
using Biomes.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Biomes;

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
    public readonly bool TagOnChunkGen = true;
    private Commands _commands;

    private ICoreServerAPI _vsapi = null!;
    public RealmCache Cache = null!;
    public BiomesConfig Config = new();
    public Entities Entities = null!;

    public bool IsRiversModInstalled = false;
    public IRealmGen RealmGen = null!;


    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Server;
    }

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);

        Cache = new RealmCache();
        Entities = new Entities(this, api);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);

        Config.LoadConfigs(this, api);
        RealmGen = IRealmGen.BuildGenerator(Config);
    }


    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        Entities.BuildCaches(Config);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        _vsapi = api;
        HarmonyPatches.Init(this);

        _vsapi.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Vegetation, "standard");
        _vsapi.Event.MapChunkGeneration(OnMapChunkGeneration, "standard");

        _commands = new Commands(this, _vsapi);
    }


    public override void Dispose()
    {
        HarmonyPatches.Shutdown();
        base.Dispose();
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
        var realmNames = RealmGen.GetRealmsForBlockPos(_vsapi, blockPos);
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
}