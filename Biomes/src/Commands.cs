using Biomes.Api;
using Biomes.Utils;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Biomes;

internal class Commands
{
    private readonly BiomesModSystem _mod;
    private readonly ICoreServerAPI _sapi;


    public Commands(BiomesModSystem mod, ICoreServerAPI sapi)
    {
        _mod = mod;
        _sapi = sapi;

        _sapi.ChatCommands.Create("biomes")
             .WithDescription("Biomes main command")
             .RequiresPlayer()
             .RequiresPrivilege(Privilege.gamemode)
             .BeginSubCommand("show")
             .HandleWith(OnShowBiomeCommand)
             .EndSubCommand()
             .BeginSubCommand("tree")
             .HandleWith(OnTreesCommand)
             .EndSubCommand()
             .BeginSubCommand("fruit")
             .HandleWith(OnFruitTreesCommand)
             .EndSubCommand()
             .BeginSubCommand("blockpatch")
             .HandleWith(OnBlockPatchCommand)
             .EndSubCommand()
             .BeginSubCommand("entity")
             .HandleWith(OnEntityCommand)
             .EndSubCommand()
             .BeginSubCommand("whitelist")
             .HandleWith(OnWhitelistCommand)
             .EndSubCommand()
             .BeginSubCommand("unconfigured")
             .HandleWith(OnUnconfiguredCommand)
             .EndSubCommand()
             .BeginSubCommand("add")
             .WithArgs(
                 sapi.ChatCommands.Parsers.WordRange(
                     "realm",
                     _mod.Config.ValidRealms.Select(i => i.Replace(' ', '_')).ToArray()
                 )
             )
             .HandleWith(OnAddRealmCommand)
             .EndSubCommand()
             .BeginSubCommand("remove")
             .WithArgs(
                 sapi.ChatCommands.Parsers.WordRange(
                     "realm",
                     _mod.Config.ValidRealms.Select(i => i.Replace(' ', '_')).ToArray()
                 )
             )
             .HandleWith(OnRemoveRealmCommand)
             .EndSubCommand();
    }

    private TextCommandResult OnTreesCommand(TextCommandCallingArgs args)
    {
        var chunk = args.Caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(args.Caller.Entity.Pos.AsBlockPos);
        var chunkData = chunk.GetModdata(ModPropName.MapChunk.BiomeData, new BiomeData(0));
        var chunkRealms = chunkData.RealmNames(_mod.Config);


        var trees = new List<string>();
        foreach (var item in _mod.Config.Trees)
            if (item.Value.biorealm.Intersect(chunkRealms).Any())
                trees.Add(item.Key);

        var msg = trees.Order().Distinct().Join(delimiter: "\n");
        _sapi.Logger.Debug($"Biomes Trees {string.Join(',', chunkRealms)}:\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnFruitTreesCommand(TextCommandCallingArgs args)
    {
        var chunk = args.Caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(args.Caller.Entity.Pos.AsBlockPos);
        var chunkData = chunk.GetModdata(ModPropName.MapChunk.BiomeData, new BiomeData(0));
        var chunkRealms = chunkData.RealmNames(_mod.Config);

        var trees = new List<string>();
        foreach (var item in _mod.Config.FruitTrees)
            if (item.Value.biorealm.Intersect(chunkRealms).Any())
                trees.Add(item.Key);

        var msg = trees.Order().Distinct().Join(delimiter: "\n");
        _sapi.Logger.Debug($"Biomes Fruit Trees {string.Join(',', chunkRealms)}:\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnBlockPatchCommand(TextCommandCallingArgs args)
    {
        var chunk = args.Caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(args.Caller.Entity.Pos.AsBlockPos);
        var chunkData = chunk.GetModdata(ModPropName.MapChunk.BiomeData, new BiomeData(0));
        var chunkRealms = chunkData.RealmNames(_mod.Config);

        var bp = new List<string>();
        foreach (var item in _mod.Config.BlockPatches)
            if (item.Value.biorealm.Intersect(chunkRealms).Any())
                bp.Add(item.Key);

        var msg = bp.Order().Distinct().Join(delimiter: "\n");
        _sapi.Logger.Debug($"Biomes Blockpatches {string.Join(',', chunkRealms)}:\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnEntityCommand(TextCommandCallingArgs args)
    {
        var chunk = args.Caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(args.Caller.Entity.Pos.AsBlockPos);
        var chunkData = chunk.GetModdata(ModPropName.MapChunk.BiomeData, new BiomeData(0));
        var chunkRealms = chunkData.RealmNames(_mod.Config);

        var entityTypes = new List<string>();
        foreach (var entity in _sapi.World.EntityTypes)
            if (entity.Attributes != null && entity.Attributes.KeyExists(ModPropName.Entity.Realm))
                foreach (var realm in entity.Attributes[ModPropName.Entity.Realm])
                    if (chunkRealms.Contains(realm.ToString()))
                        entityTypes.Add(entity.Code);

        var msg = entityTypes.Order().Distinct().Join(delimiter: "\n");
        _sapi.Logger.Debug($"Biomes Entities {string.Join(',', chunkRealms)}:\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnUnconfiguredCommand(TextCommandCallingArgs args)
    {
        var entityTypes = new List<string>();
        foreach (var entity in _sapi.World.EntityTypes)
            if ((entity.Attributes == null || !entity.Attributes.KeyExists(ModPropName.Entity.Realm))
                && !_mod.Cache.Entities.Whitelist.Contains(entity.Code))
                entityTypes.Add(entity.Code);

        var msg = entityTypes.Order().Distinct().Join(delimiter: "\n");
        _sapi.Logger.Debug($"Biomes Unconfigured Entities:\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnWhitelistCommand(TextCommandCallingArgs args)
    {
        var joinedWhitelist = _mod.Cache.Entities.Whitelist.Select(x => x.ToString()).Join(delimiter: "\n");

        _sapi.Logger.Debug($"Biomes Whitelist:\n{joinedWhitelist}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = joinedWhitelist };
    }

    private TextCommandResult OnShowBiomeCommand(TextCommandCallingArgs args)
    {
        var chunk = args.Caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(args.Caller.Entity.Pos.AsBlockPos);

        _mod.Mod.Logger.Error($"chunk: ${chunk}");
        var biomeData = chunk.GetModdata(ModPropName.MapChunk.BiomeData, new BiomeData(0));
        if (biomeData.IsNullData())
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes")
            };

        var realms = biomeData.RealmNames(_mod.Config);

        var realmsStr = realms.Join(delimiter: ",");

        var validRiver = biomeData.GetRiver();
        var validNoRiver = biomeData.GetNoRiver();
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = $"Realms: {realmsStr}\nValid For River Spawns: {validRiver
            }\nValid for No River Spawns: {validNoRiver}"
        };
    }

    private TextCommandResult OnAddRealmCommand(TextCommandCallingArgs args)
    {
        var chunk = args.Caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(args.Caller.Entity.Pos.AsBlockPos);
        var chunkData = chunk.GetModdata(ModPropName.MapChunk.BiomeData, new BiomeData(0));

        var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');

        chunkData.SetRealm(_mod.Config.ValidRealmIndexes[value], true);

        chunk.SetModdata(ModPropName.MapChunk.BiomeData, chunkData);

        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = "Added to realms list" };
    }

    private TextCommandResult OnRemoveRealmCommand(TextCommandCallingArgs args)
    {
        var chunk = args.Caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(args.Caller.Entity.Pos.AsBlockPos);
        var chunkData = chunk.GetModdata(ModPropName.MapChunk.BiomeData, new BiomeData(0));

        var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');

        chunkData.SetRealm(_mod.Config.ValidRealmIndexes[value], false);

        chunk.SetModdata(ModPropName.MapChunk.BiomeData, chunkData);

        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = "Removed from realms list" };
    }
}
