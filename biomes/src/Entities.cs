using System.Collections.Generic;
using System.Linq;
using Biomes.util;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Biomes;

public class Entities(BiomesModSystem mod, ICoreAPI vsapi)
{
    private BiomesModSystem _mod = mod;
    private ICoreAPI _vsapi = vsapi;
    
    // bytemask with all 4 seasons set
    private const byte AllSeasons = 0b0000_1111;

    public readonly HashSet<AssetLocation> Whitelist = new();
    // 4th field is a bitfield representing valid season spawns
    // hierarchy here goes
    // Key is mob code
    // value dict is biome: seasons
    private readonly Dictionary<AssetLocation, HashSet<string>> _entityRealmCache = new();
    private readonly Dictionary<AssetLocation, ByteField> _entitySeasonCache = new();
    
    private HashSet<AssetLocation> _alreadyRecordedNoHit;

    public void GenWhitelist(List<string> whitelistSpecified)
    {
        foreach (var entity in _vsapi.World.EntityTypes)
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
    public void BuildCaches(BiomeUserConfig userConfig)
    {
        var whitelistConfig = JsonConvert.DeserializeObject<List<string>>(_vsapi.Assets.Get($"{_mod.Mod.Info.ModID}:config/whitelist.json").ToText());
        whitelistConfig?.AddRange(userConfig.EntitySpawnWhiteList);
        GenWhitelist(whitelistConfig!);

        foreach (var entity in _vsapi.World.EntityTypes)
        {
            // only ever null if we have no attributes
            var validRealms = entity.Attributes?[ModPropName.Entity.Realm];
            if (validRealms is { Exists: true })
            {
                var cacheRealms = new HashSet<string>();
                foreach (var realm in validRealms.AsArray<string>([]))
                {
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
                _entitySeasonCache[entity.Code] =  seasonsBitfield;
            }
            else
            {
                _entitySeasonCache[entity.Code] = new ByteField(AllSeasons);
            }
        }

    }

    public bool IsSpawnValid(IMapChunk mapChunk, EntityProperties type, BlockPos blockPos = null)
    {
        var code = type.Code!;
        if (Whitelist.Contains(code)) return true;

        var validRealms = _entityRealmCache.Get(code);
        if (validRealms == null)
        {
            if (_alreadyRecordedNoHit == null) _alreadyRecordedNoHit = new();
            if (!_alreadyRecordedNoHit.Contains(code))
            {
                _vsapi.Logger.Warning(
                    $"Entity \"{type.Code}\" has no cache data, likely has no compat data for Biomes. Report This!");
                _alreadyRecordedNoHit.Add(code);
            }

            return true;
        }

        var chunkRealms = new List<string>();
        if (ModProperty.Get(mapChunk, ModPropName.Map.Realm, ref chunkRealms) == EnumCommandStatus.Error)
            return true;

        var valid = false;
        foreach (var realm in chunkRealms)
        {
            if (!validRealms.Contains(realm)) continue;
            valid = true;
            break;
        }
        if (valid) return true;

        if (_entitySeasonCache.ContainsKey(code))
        {
            var currentSeason = _vsapi.World.Calendar.GetSeason(blockPos);
            var validSeasons = _entitySeasonCache[code];
            valid = validSeasons.GetBit((int)currentSeason);
        }
        return valid;
    }
}