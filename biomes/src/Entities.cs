using Biomes.util;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Biomes;

public class Entities(BiomesModSystem mod, ICoreAPI vsapi)
{
    // bytemask with all 4 seasons set
    private const byte AllSeasons = 0b0000_1111;
    private readonly Dictionary<AssetLocation, HashSet<string>> _entityRealmCache = new();
    private readonly Dictionary<AssetLocation, ByteField> _entitySeasonCache = new();

    public readonly HashSet<AssetLocation> Whitelist = [];

    // Explicitly not initialized unless we have a nohit found
    private HashSet<AssetLocation>? _alreadyRecordedNoHit;

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
            // only ever null if we have no attributes
            var validRealms = entity.Attributes?[ModPropName.Entity.Realm];
            if (validRealms is { Exists: true })
            {
                var cacheRealms = new HashSet<string>();
                foreach (var realm in validRealms.AsArray<string>([]))
                {
                    if (!mod.Config.Realms.AllRealms().Contains(realm))
                    {
                        mod.Mod.Logger.Error(
                            $"Didn't load invalid realm \"{realm}\" off of \"{entity.Code}\", this is an error");
                        continue;
                    }

                    cacheRealms.Add(realm);
                }

                _entityRealmCache[entity.Code] = cacheRealms;
            }
            else
            {
                continue;
            }


            // Now we know Attributes isn't null, so we can just assert
            var validSeasons = entity.Attributes![ModPropName.Entity.Season];
            if (validSeasons.Exists)
            {
                var seasons = validSeasons.AsArray<string>([]);
                var seasonsBitfield = new ByteField(0);
                foreach (var season in seasons)
                {
                    var caseStrip = season.ToLowerInvariant();
                    switch (caseStrip)
                    {
                        case "spring":
                            seasonsBitfield.SetBit((int)EnumSeason.Spring, true);
                            break;
                        case "summer":
                            seasonsBitfield.SetBit((int)EnumSeason.Summer, true);
                            break;
                        case "fall":
                            seasonsBitfield.SetBit((int)EnumSeason.Fall, true);
                            break;
                        case "winter":
                            seasonsBitfield.SetBit((int)EnumSeason.Winter, true);
                            break;
                    }
                }

                _entitySeasonCache[entity.Code] = seasonsBitfield;
            }
            else
            {
                _entitySeasonCache[entity.Code] = new ByteField(AllSeasons);
            }
        }
    }

    public bool IsSpawnValid(IMapChunk? mapChunk, EntityProperties type, BlockPos blockPos = null)
    {
        if (mapChunk == null) return false;

        var code = type.Code!;
        if (Whitelist.Contains(code)) return true;

        var validRealms = _entityRealmCache.Get(code);
        if (validRealms == null)
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

        var chunkRealms = new List<string>();
        if (ModProperty.Get(mapChunk, ModPropName.Map.Realm, ref chunkRealms) == EnumCommandStatus.Error)
            return true;

        // Not a fan of this nonsense with holding a valid boolean and returning early and so on
        // Convolutes the control flow, so hopefully comments clarify it
        var valid = false;
        foreach (var realm in chunkRealms)
        {
            // If it's not a valid realm, continue through chunk realms
            if (!validRealms.Contains(realm)) continue;
            // reaches here if it is valid, setting valid
            valid = true;
            break;
        }

        // If we're not in a valid realm, we can just skip out early and avoid the season check
        if (!valid) return false;

        // now if we are in a valid realm, check for season data. If there's no season data, assume all seasons are valid
        // and return true
        if (!_entitySeasonCache.TryGetValue(code, out var validSeasons)) return true;

        // finally if we do have season data, get the current season and check if the entity's valid seasons are in the
        // cached season data
        var currentSeason = vsapi.World.Calendar.GetSeason(blockPos);
        return validSeasons.GetBit((int)currentSeason);
    }
}