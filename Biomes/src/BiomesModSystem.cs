using System.Runtime.CompilerServices;
using Biomes.Api;
using Biomes.Caches;
using Biomes.RealmGen;
using Biomes.Utils;
using ProtoBuf.Meta;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

[assembly: InternalsVisibleTo("BiomesTest")]

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
    private Commands _commands = null!;

    private bool _isRiversModInstalled;
    private IRealmGen _realmGen = null!;

    private ICoreServerAPI _vsapi = null!;

    internal Cache Cache;
    internal BiomesConfig Config = new();


    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Server;
    }

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);

        if (!RuntimeTypeModel.Default.IsDefined(typeof(BiomeData)))
            RuntimeTypeModel.Default.Add(typeof(BiomeData), false).SetSurrogate(typeof(BiomeDataSurrogate));


        Cache = new Cache(this, api);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);

        Config.LoadConfigs(this, api);
        _realmGen = IRealmGen.BuildGenerator(Config);

        _isRiversModInstalled = api.ModLoader.IsModEnabled("rivers") || api.ModLoader.IsModEnabled("rivergen");
    }


    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        Cache.Entities.BuildCaches(Config);
        ExternalRegistry.Initialize(this);
        ExternalData.Initialize(this, api);
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
        // if this doesn't hit, something is really wrong
        // every chunk in a column shares the same mapchunk
        var mapChunk = chunks[0].MapChunk;
        if (mapChunk == null) return;

        if (Config.User.AutoMigrateOldData)
        {
            var oldRealmData = mapChunk.GetModdata<List<string>>(ModPropName.Map.Realm);
            if (oldRealmData != null)
            {
                var biomeData = new BiomeData(0);
                foreach (var realm in oldRealmData) biomeData.SetRealm(Config.ValidRealmIndexes[realm], true);

                if (_isRiversModInstalled)
                {
                    var oldRiverBool = mapChunk.GetModdata<bool>(ModPropName.MapChunk.RiverBool);
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


                mapChunk.SetModdata(ModPropName.MapChunk.BiomeData, biomeData);

                mapChunk.RemoveModdata(ModPropName.Map.Realm);
                mapChunk.RemoveModdata(ModPropName.Map.Hemisphere);
                mapChunk.RemoveModdata(ModPropName.Map.River);

                mapChunk.RemoveModdata(ModPropName.MapChunk.RiverArray);
                mapChunk.RemoveModdata(ModPropName.MapChunk.RiverBool);
                mapChunk.MarkDirty();
            }
        }

        var chunkBiomeData = mapChunk.GetModdata<BiomeData>(ModPropName.MapChunk.BiomeData);
        Cache.ChunkData.CacheData(new FastVec2i(chunkPos.X, chunkPos.Y), chunkBiomeData);
    }

    private void OnChunkUnloaded(Vec2i chunkCoord, IWorldChunk[] chunks)
    {
        var fastvec = new FastVec2i(chunkCoord.X, chunkCoord.Y);

        Cache.ChunkData.Evict(fastvec);
    }

    private void OnChunkColumnGeneration(IChunkColumnGenerateRequest request)
    {
        // Full chunk column isn't needed because mapchunk is the same for all of them
        var mapChunk = request.Chunks[0].MapChunk;
        if (mapChunk == null) return;

        var biomeData = new BiomeData(0);

        var blockPos = new BlockPos(
            request.ChunkX * _vsapi.WorldManager.ChunkSize,
            0,
            request.ChunkZ * _vsapi.WorldManager.ChunkSize,
            0
        );
        var realms = _realmGen.GetRealmsForBlockPos(_vsapi, blockPos);

        foreach (var realm in realms) biomeData.SetRealm(Config.ValidRealmIndexes[realm], true);

        if (_isRiversModInstalled)
        {
            var flowVectors = mapChunk.GetModdata("flowVectors");
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

        mapChunk.SetModdata(ModPropName.MapChunk.BiomeData, biomeData);
        mapChunk.MarkDirty();
    }
}
