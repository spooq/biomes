using System.Runtime.CompilerServices;
using Biomes.util;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Biomes;

public class Commands
{
    private readonly BiomesModSystem _biomesMod;
    private readonly ICoreServerAPI _sapi;


    public Commands(BiomesModSystem mod, ICoreServerAPI sapi)
    {
        _biomesMod = mod;
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
            .BeginSubCommand("hemisphere")
            .WithArgs(sapi.ChatCommands.Parsers.WordRange("hemisphere", Enum.GetNames(typeof(EnumHemisphere))))
            .HandleWith(OnSetHemisphereCommand)
            .EndSubCommand()
            .BeginSubCommand("add")
            .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm",
                _biomesMod.Config.Realms.AllRealms()
                    .Select(i => i.Replace(' ', '_'))
                    .ToArray()))
            .HandleWith(OnAddRealmCommand)
            .EndSubCommand()
            .BeginSubCommand("remove")
            .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm",
                _biomesMod.Config.Realms.AllRealms()
                    .Select(i => i.Replace(' ', '_'))
                    .ToArray()))
            .HandleWith(OnRemoveRealmCommand)
            .EndSubCommand();
    }

    private TextCommandResult OnTreesCommand(TextCommandCallingArgs args)
    {
        var chunkRealms = new List<string>();
        if (ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref chunkRealms) ==
            EnumCommandStatus.Error)
            return new TextCommandResult
                { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };

        var trees = new List<string>();
        foreach (var item in _biomesMod.Config.Trees)
            if (item.Value.biorealm.Intersect(chunkRealms).Any())
                trees.Add(item.Key);

        var msg = trees.Order().Distinct().Join(delimiter: "\r\n");
        _sapi.Logger.Debug($"Biomes Trees {string.Join(',', chunkRealms)}:\r\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnFruitTreesCommand(TextCommandCallingArgs args)
    {
        var chunkRealms = new List<string>();
        if (ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref chunkRealms) ==
            EnumCommandStatus.Error)
            return new TextCommandResult
                { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };

        var trees = new List<string>();
        foreach (var item in _biomesMod.Config.FruitTrees)
            if (item.Value.biorealm.Intersect(chunkRealms).Any())
                trees.Add(item.Key);

        var msg = trees.Order().Distinct().Join(delimiter: "\r\n");
        _sapi.Logger.Debug($"Biomes Fruit Trees {string.Join(',', chunkRealms)}:\r\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnBlockPatchCommand(TextCommandCallingArgs args)
    {
        var chunkRealms = new List<string>();
        if (ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref chunkRealms) ==
            EnumCommandStatus.Error)
            return new TextCommandResult
                { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };

        var bp = new List<string>();
        foreach (var item in _biomesMod.Config.BlockPatches)
            if (item.Value.biorealm.Intersect(chunkRealms).Any())
                bp.Add(item.Key);

        var msg = bp.Order().Distinct().Join(delimiter: "\r\n");
        _sapi.Logger.Debug($"Biomes Blockpatches {string.Join(',', chunkRealms)}:\r\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnEntityCommand(TextCommandCallingArgs args)
    {
        var chunkRealms = new List<string>();
        if (ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref chunkRealms) ==
            EnumCommandStatus.Error)
            return new TextCommandResult
                { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };

        var entityTypes = new List<string>();
        foreach (var entity in _sapi.World.EntityTypes)
            if (entity.Attributes != null && entity.Attributes.KeyExists(ModPropName.Entity.Realm))
                foreach (var realm in entity.Attributes[ModPropName.Entity.Realm])
                    if (chunkRealms.Contains(realm.ToString()))
                        entityTypes.Add(entity.Code);

        var msg = entityTypes.Order().Distinct().Join(delimiter: "\r\n");
        _sapi.Logger.Debug($"Biomes Entities {string.Join(',', chunkRealms)}:\r\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnUnconfiguredCommand(TextCommandCallingArgs args)
    {
        var chunkRealms = new List<string>();
        if (ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref chunkRealms) ==
            EnumCommandStatus.Error)
            return new TextCommandResult
                { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };

        var entityTypes = new List<string>();
        foreach (var entity in _sapi.World.EntityTypes)
            if ((entity.Attributes == null || !entity.Attributes.KeyExists(ModPropName.Entity.Realm)) &&
                !_biomesMod.Entities.Whitelist.Contains(entity.Code))
                entityTypes.Add(entity.Code);

        var msg = entityTypes.Order().Distinct().Join(delimiter: "\r\n");
        _sapi.Logger.Debug($"Biomes Unconfigured Entities:\r\n{msg}");
        return new TextCommandResult
            { Status = EnumCommandStatus.Success, StatusMessage = "Written to server debug log" };
    }

    private TextCommandResult OnWhitelistCommand(TextCommandCallingArgs args)
    {
        var chunkRealms = new List<string>();
        if (ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref chunkRealms) ==
            EnumCommandStatus.Error)
            return new TextCommandResult
                { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };

        var entityTypes = new List<string>();
        foreach (var entity in _sapi.World.EntityTypes)
            if (_biomesMod.Entities.Whitelist.Contains(entity.Code))
                entityTypes.Add(entity.Code);

        var msg = entityTypes.Order().Distinct().Join(delimiter: "\r\n");
        _sapi.Logger.Debug($"Biomes Whitelist:\r\n{msg}");
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = msg };
    }

    private TextCommandResult OnShowBiomeCommand(TextCommandCallingArgs args)
    {
        var chunkHemisphere = EnumHemisphere.North;
        if (ModProperty.Get(args.Caller, ModPropName.Map.Hemisphere,
                ref chunkHemisphere) ==
            EnumCommandStatus.Error)
            return new TextCommandResult
                { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };
        var hemisphereStr = Enum.GetName(typeof(EnumHemisphere), chunkHemisphere).ToLower();

        var chunkRealms = new List<string>();
        if (ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref chunkRealms) ==
            EnumCommandStatus.Error)
            return new TextCommandResult
                { Status = EnumCommandStatus.Error, StatusMessage = Lang.Get("chunknotgenwithbiomes") };
        var realmsStr = chunkRealms?.Join(delimiter: ",");

        var riversModNotInstalled = Lang.Get("chunknotgenwithrivers");
        var chunkRiver = riversModNotInstalled;
        var blockRiver = riversModNotInstalled;
        if (_biomesMod.IsRiversModInstalled)
        {
            var cr = false;
            if (ModProperty.Get(args.Caller, ModPropName.MapChunk.RiverBool, ref cr) !=
                EnumCommandStatus.Error)
                chunkRiver = cr ? "true" : "false";
            else
                chunkRiver = "not set";

            bool[] arr = null;
            if (ModProperty.Get(args.Caller, ModPropName.MapChunk.RiverArray, ref arr) !=
                EnumCommandStatus.Error)
            {
                var blockPos = args.Caller.Entity.Pos.AsBlockPos;
                blockRiver =
                    arr[
                        blockPos.Z % _sapi.WorldManager.ChunkSize * _sapi.WorldManager.ChunkSize +
                        blockPos.X % _sapi.WorldManager.ChunkSize]
                        ? "true"
                        : "false";
            }
            else
            {
                blockRiver = "not set";
            }
        }

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage =
                $"Hemisphere: {hemisphereStr}\nRealms: {realmsStr}\nChunk has river: {chunkRiver}\nBlock has river: {blockRiver}"
        };
    }

    private TextCommandResult OnSetHemisphereCommand(TextCommandCallingArgs args)
    {
        if (Enum.TryParse(args.Parsers[0].GetValue() as string, out EnumHemisphere hemisphere))
            return new TextCommandResult
            {
                Status = ModProperty.Set(args.Caller,
                    ModPropName.Map.Hemisphere, hemisphere)
            };

        return new TextCommandResult
            { Status = EnumCommandStatus.Error, StatusMessage = "Bad hemisphere argument" };
    }

    private TextCommandResult OnAddRealmCommand(TextCommandCallingArgs args)
    {
        var currentRealms = new List<string>();
        ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref currentRealms);
        if (Unsafe.IsNullRef(ref currentRealms))
            currentRealms = new List<string>();

        var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
        currentRealms.Add(value);

        return new TextCommandResult
        {
            Status = ModProperty.Set(args.Caller, ModPropName.Map.Realm,
                currentRealms.Distinct()),
            StatusMessage = currentRealms?.Join(delimiter: ",")
        };
    }

    private TextCommandResult OnRemoveRealmCommand(TextCommandCallingArgs args)
    {
        var currentRealms = new List<string>(0);
        ModProperty.Get(args.Caller, ModPropName.Map.Realm, ref currentRealms);
        if (Unsafe.IsNullRef(ref currentRealms))
            currentRealms = new List<string>();

        var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
        currentRealms.Remove(value);

        return new TextCommandResult
        {
            Status = ModProperty.Set(args.Caller, ModPropName.Map.Realm,
                currentRealms.Distinct()),
            StatusMessage = currentRealms?.Join(delimiter: ",")
        };
    }
}