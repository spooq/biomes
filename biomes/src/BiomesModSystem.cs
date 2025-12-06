using Biomes.Caches;
using Biomes.RealmGen;
using Biomes.Utils;
using ProtoBuf.Meta;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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

    public Cache Cache;
    public BiomesConfig Config = new();

    public bool IsRiversModInstalled;
    public IRealmGen RealmGen = null!;


    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Server;
    }

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);

        RuntimeTypeModel.Default.Add(typeof(BiomeData), false).SetSurrogate(typeof(BiomeDataSurrogate));


        Cache = new Cache(this, api);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);

        Config.LoadConfigs(this, api);
        RealmGen = IRealmGen.BuildGenerator(Config);

        IsRiversModInstalled = api.ModLoader.IsModEnabled("rivers") || api.ModLoader.IsModEnabled("rivergen");
    }


    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        Cache.Entities.BuildCaches(Config);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        _vsapi = api;
        HarmonyPatches.Init(this);

        _vsapi.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Vegetation, "standard");
        _vsapi.Event.ChunkColumnLoaded += OnChunkLoaded;
        _vsapi.Event.ChunkColumnLoaded += OnChunkUnloaded;

        _commands = new Commands(this, _vsapi);
    }


    public override void Dispose()
    {
        HarmonyPatches.Shutdown();
        base.Dispose();
    }

    // Migrate old data
    private void OnChunkLoaded(Vec2i chunkPos, IWorldChunk[] chunks)
    {
        if (!Config.User.AutoMigrateOldData) return;
        foreach (var chunk in chunks)
        {
            var biomeData = new BiomeData(0);

            var oldRealmData = chunk.MapChunk.GetModdata<List<string>>(ModPropName.Map.Realm);
            if (oldRealmData == null) continue;

            foreach (var realm in oldRealmData) biomeData.SetRealm(Config.ValidRealmIndexes[realm], true);

            if (IsRiversModInstalled)
            {
                var oldRiverBool = chunk.MapChunk.GetModdata<bool>(ModPropName.MapChunk.RiverBool);
                if (oldRiverBool)
                    biomeData.SetRiver(true);
                else
                    biomeData.SetNoRiver(true);
            }
            else
            {
                biomeData.SetRiver(true);
                biomeData.SetNoRiver(true);
            }


            chunk.MapChunk.SetModdata(ModPropName.MapChunk.BiomeData, biomeData);

            chunk.MapChunk.RemoveModdata(ModPropName.Map.Realm);
            chunk.MapChunk.RemoveModdata(ModPropName.Map.Hemisphere);
            chunk.MapChunk.RemoveModdata(ModPropName.Map.River);

            chunk.MapChunk.RemoveModdata(ModPropName.MapChunk.RiverArray);
            chunk.MapChunk.RemoveModdata(ModPropName.MapChunk.RiverBool);
            chunk.MapChunk.MarkDirty();
        }
    }

    // Eventually will be used for cache eviction
    private void OnChunkUnloaded(Vec2i chunkCoord, IWorldChunk[] chunks)
    {
    }

    private void OnChunkColumnGeneration(IChunkColumnGenerateRequest request)
    {
        if (!TagOnChunkGen)
            return;


        foreach (var chunk in request.Chunks)
        {
            var biomeData = new BiomeData(0);

            var blockPos = new BlockPos(request.ChunkX * _vsapi.WorldManager.ChunkSize, 0,
                request.ChunkZ * _vsapi.WorldManager.ChunkSize, 0);
            var realms = RealmGen.GetRealmsForBlockPos(_vsapi, blockPos);

            foreach (var realm in realms) biomeData.SetRealm(Config.ValidRealmIndexes[realm], true);

            if (IsRiversModInstalled)
            {
                var flowVectors = chunk.GetModdata("flowVectors");
                if (flowVectors != null)
                    biomeData.SetRiver(true);
                else
                    biomeData.SetNoRiver(true);
            }
            else
            {
                // No rivers mod = always valid whether it s a river
                biomeData.SetRiver(true);
                biomeData.SetNoRiver(true);
            }

            chunk.MapChunk.SetModdata(ModPropName.MapChunk.BiomeData, biomeData);
            chunk.MapChunk.MarkDirty();
        }
    }
}