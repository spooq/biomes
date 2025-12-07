using Biomes.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Biomes.Caches;

public class EntityCache(BiomesModSystem mod, ICoreAPI vsapi)
{
    private readonly Dictionary<AssetLocation, BiomeData> _entityCache = new(new Fnv1aAssetLocationComparer());

    public readonly HashSet<AssetLocation> Whitelist = new(new Fnv1aAssetLocationComparer());

    // Explicitly not initialized unless we have a nohit found
    private HashSet<AssetLocation>? _alreadyRecordedNoHit = new(new Fnv1aAssetLocationComparer());

    public void GenWhitelist(List<string> whitelistSpecified)
    {
        foreach (var entity in vsapi.World.EntityTypes)
        {
            var code = entity.Code.ToString();

            if (whitelistSpecified.Any(x => WildcardUtil.Match(x, code)))
                Whitelist.Add(code);
        }
    }

    // Build entity info caches
    // This happens after configs are loaded.
    // Unlike world gen stuff which is lazy generated because
    // you may not even be doing world gen, the server is
    // *always* spawning entities.
    public void BuildCaches(BiomesConfig config)
    {
        var fullWhitelist = config.Whitelist.ToList();
        fullWhitelist.AddRange(config.User.EntitySpawnWhiteList);
        GenWhitelist(fullWhitelist);

        foreach (var entity in vsapi.World.EntityTypes)
        {
            var biomeData = new BiomeData(0);
            // only ever null if we have no attributes
            var validRealms = entity.Attributes?[ModPropName.Entity.Realm];
            if (validRealms is { Exists: true })
                foreach (var realm in validRealms.AsArray<string>())
                {
                    if (!config.ValidRealmIndexes.TryGetValue(realm, out var index))
                    {
                        mod.Mod.Logger.Error(
                            $"Didn't load invalid realm \"{realm}\" off of \"{entity.Code}\", please report this to biomes!");
                        continue;
                    }

                    biomeData.SetRealm(index, true);
                }
            else
                continue;


            // Now we know Attributes isn't null, so we can just assert
            var validSeasons = entity.Attributes![ModPropName.Entity.Season];
            if (validSeasons.Exists)
            {
                var seasons = validSeasons.AsArray<string>([]).ToList();

                foreach (var seasonStr in seasons) biomeData.SetSeason(seasonStr, true);
            }
            else
            {
                // no season data set = valid for all seasons
                biomeData.SetAllSeasons(true);
            }

            var riverMode = entity.Attributes![ModPropName.Entity.River];
            if (riverMode.Exists)
            {
                var riverModeStr = riverMode.ToString();
                var riverEnum = BioRiverExtensions.FromString(riverModeStr);
                biomeData.SetFromBioRiver(riverEnum);
            }
            else
            {
                // No data = either one
                biomeData.SetRiver(true);
                biomeData.SetNoRiver(true);
            }

            _entityCache[entity.Code] = biomeData;
        }
    }

    public bool IsSpawnValid(EntityProperties type, BlockPos blockPos = null)
    {
        var code = type.Code!;
        if (Whitelist.Contains(code)) return true;

        if (!_entityCache.TryGetValue(code, out var entityData))
        {
            _alreadyRecordedNoHit ??= [];
            if (mod.Config.User.SpawnMode.ShouldWarn() &&
                !_alreadyRecordedNoHit.Contains(code))
            {
                vsapi.Logger.Warning(
                    $"Entity \"{type.Code}\" has no cache data, likely has no compat data for Biomes. Report this to biomes!");
                _alreadyRecordedNoHit.Add(code);
            }

            return mod.Config.User.SpawnMode.ShouldAllowByDefaut();
        }

        var chunkData = mod.Cache.ChunkData.GetBiomeData(blockPos);

        if (!chunkData.CheckRealmAndRiverAgainst(entityData)) return false;

        // If all season, we can short out here and not actually do a season check
        if (entityData.IsAllSeason()) return true;

        var currentSeason = Util.FastInlinedGetSeason(vsapi, blockPos);
        return entityData.GetSeason(currentSeason);
    }
}