using HarmonyLib;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Biomes
{
    public class RealmsConfig
    {
        public List<string> NorthernRealms = new List<string>();
        public List<string> SouthernRealms = new List<string>();
    }

    public class BiomeConfig
    {
        public List<string> EntitySpawnWhiteList = new List<string>();
        public Dictionary<string, List<string>> TreeBiomes = new Dictionary<string, List<string>>();
        public Dictionary<string, List<string>> FruitTreeBiomes = new Dictionary<string, List<string>>();
        public Dictionary<string, List<string>> BlockPatchBiomes = new Dictionary<string, List<string>>();
    }

    public class BiomeUserConfig
    {
        public bool FlipNorthSouth = false;
        public List<string> EntitySpawnWhiteList = new List<string>();
    }

    [ProtoContract]
    public class BiomeNameAndCoords
    {
        [ProtoMember(1)]
        public int chunkX;

        [ProtoMember(2)]
        public int chunkZ;

        [ProtoMember(3)]
        public string biome;
    }

    public class BiomesModSystem : ModSystem
    {
        public ICoreServerAPI sapi;

        public const string RealmPropertyName = "biorealm";
        public const string HemispherePropertyName = "hemisphere";
        public const string ChunkNotGeneratedWithBiomesModInstalled = "Chunk was not generated with Biomes mod installed.";

        public BiomeUserConfig UserConfig;
        public RealmsConfig RealmsConfig;
        public BiomeConfig ModConfig;

        //public NormalizedSimplexNoise noise = new NormalizedSimplexNoise();

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            HarmonyPatches.Init(this);

            sapi = api;

            RealmsConfig = JsonConvert.DeserializeObject<RealmsConfig>(sapi.Assets.Get($"{Mod.Info.ModID}:config/realms.json").ToText());
            ModConfig = new BiomeConfig();

            foreach (var biomeAsset in sapi.Assets.GetMany("config/biomes.json"))
            {
                var tmp = JsonConvert.DeserializeObject<BiomeConfig>(biomeAsset.ToText());

                foreach (var item in tmp.EntitySpawnWhiteList)
                    ModConfig.EntitySpawnWhiteList.Add(item);
                foreach (var item in tmp.TreeBiomes)
                    ModConfig.TreeBiomes[item.Key] = item.Value;
                foreach (var item in tmp.FruitTreeBiomes)
                    ModConfig.FruitTreeBiomes[item.Key] = item.Value;
                foreach (var item in tmp.BlockPatchBiomes)
                    ModConfig.BlockPatchBiomes[item.Key] = item.Value;
            }

            ModConfig.EntitySpawnWhiteList = ModConfig.EntitySpawnWhiteList.Distinct().ToList();

            UserConfig = sapi.LoadModConfig<BiomeUserConfig>($"{Mod.Info.ModID}.json");
            UserConfig ??= new BiomeUserConfig();
            UserConfig.EntitySpawnWhiteList ??= new List<string>();
            sapi.StoreModConfig(UserConfig, $"{Mod.Info.ModID}.json");

            if (UserConfig.FlipNorthSouth)
            {
                var tmp = RealmsConfig.NorthernRealms;
                RealmsConfig.NorthernRealms = RealmsConfig.SouthernRealms;
                RealmsConfig.SouthernRealms = tmp;
            }
            foreach (var item in UserConfig.EntitySpawnWhiteList)
                ModConfig.EntitySpawnWhiteList.Add(item);

            sapi.Event.MapChunkGeneration(OnMapChunkGeneration, "standard");

            sapi.ChatCommands.Create("biome")
                .WithDescription("Biomes main command")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.gamemode)
                .BeginSubCommand("show")
                    .HandleWith(onGetBiomeCommand)
                .EndSubCommand()
                .BeginSubCommand("tree")
                    .HandleWith(onTreesCommand)
                .EndSubCommand()
                .BeginSubCommand("fruit")
                    .HandleWith(onFruitTreesCommand)
                .EndSubCommand()
                .BeginSubCommand("blockpatch")
                    .HandleWith(onBlockPatchCommand)
                .EndSubCommand()
                .BeginSubCommand("hemisphere")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("hemisphere", Enum.GetNames(typeof(EnumHemisphere))))
                    .HandleWith(onSetHemisphereCommand)
                .EndSubCommand()
                .BeginSubCommand("add")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", RealmsConfig.NorthernRealms.Union(RealmsConfig.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
                    .HandleWith(onAddRealmCommand)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", RealmsConfig.NorthernRealms.Union(RealmsConfig.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
                    .HandleWith(onRemoveRealmCommand)
                .EndSubCommand();
        }

        public override void Dispose()
        {
            HarmonyPatches.Shutdown();
            base.Dispose();
        }

        public String NorthOrSouth(EnumHemisphere hemisphere, int realm)
        {
            // modconfig.flipworld exchanges the lists, so we always do choose the same here no matter what
            return hemisphere == EnumHemisphere.North ? RealmsConfig.NorthernRealms[realm] : RealmsConfig.SouthernRealms[realm];
        }

        public void OnMapChunkGeneration(IMapChunk mapChunk, int chunkX, int chunkZ)
        {
            EnumHemisphere hemisphere;
            int currentRealm;
            var realmNames = new List<string>();
            CalculateValues(chunkX - 1, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(chunkX + 1, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(chunkX, chunkZ - 1, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(chunkX, chunkZ + 1, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(chunkX, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            realmNames = realmNames.Distinct().ToList();

            setModProperty(mapChunk, HemispherePropertyName, ref hemisphere);
            setModProperty(mapChunk, RealmPropertyName, ref realmNames);
        }

        public bool IsWhiteListed(string name)
        {
            return ModConfig.EntitySpawnWhiteList.Any(x => WildcardUtil.Match(x, name));
        }

        public bool AllowEntitySpawn(IMapChunk mapChunk, EntityProperties type)
        {
            if (IsWhiteListed(type.Code.Path))
                return true;

            // Only blessed animals get in.
            if (type.Attributes == null || !type.Attributes.KeyExists(RealmPropertyName))
                return false;

            // Test map chunk attributes
            var chunkRealms = new List<string>();
            if (getModProperty(mapChunk, RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            var entityNativeRealms = type.Attributes[RealmPropertyName].AsArray<string>();
            return entityNativeRealms.Intersect(chunkRealms).Any();
        }

        public void CalculateValues(int chunkX, int chunkZ, out EnumHemisphere hemisphere, out int currentRealm)
        {
            BlockPos blockPos = new BlockPos(chunkX * sapi.WorldManager.ChunkSize, 0, chunkZ * sapi.WorldManager.ChunkSize);
            hemisphere = sapi.World.Calendar.GetHemisphere(blockPos);

            int realmCount;
            if (hemisphere == EnumHemisphere.North)
                realmCount = RealmsConfig.NorthernRealms.Count;
            else
                realmCount = RealmsConfig.SouthernRealms.Count;

            int worldWidthInChunks = sapi.WorldManager.MapSizeX / sapi.WorldManager.ChunkSize;
            float realmWidthInChunks = worldWidthInChunks / (float)realmCount;
            currentRealm = 0;
            if (realmWidthInChunks != 0)
                currentRealm = (int)(chunkX / realmWidthInChunks);
            if (currentRealm >= realmCount)
                currentRealm = realmCount - 1;
            if (currentRealm < 0)
                currentRealm = 0;
        }

        public TextCommandResult onTreesCommand(TextCommandCallingArgs args)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(args.Caller, RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = ChunkNotGeneratedWithBiomesModInstalled };

            var trees = new List<string>();
            foreach (var realm in chunkRealms)
            {
                foreach (var item in ModConfig.TreeBiomes)
                {
                    if (item.Value.Intersect(chunkRealms).Any())
                    {
                        trees.Add(item.Key);
                    }
                }
            }

            return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = trees.Distinct().Join(delimiter: "\r\n") };
        }

        public TextCommandResult onFruitTreesCommand(TextCommandCallingArgs args)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(args.Caller, RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = ChunkNotGeneratedWithBiomesModInstalled };

            var trees = new List<string>();
            foreach (var realm in chunkRealms)
            {
                foreach (var item in ModConfig.FruitTreeBiomes)
                {
                    if (item.Value.Intersect(chunkRealms).Any())
                    {
                        trees.Add(item.Key);
                    }
                }
            }

            return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = trees.Distinct().Join(delimiter: "\r\n") };
        }

        public TextCommandResult onBlockPatchCommand(TextCommandCallingArgs args)
        {
            var chunkRealms = new List<string>();
            if (getModProperty(args.Caller, RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = ChunkNotGeneratedWithBiomesModInstalled };

            var trees = new List<string>();
            foreach (var realm in chunkRealms)
            {
                foreach (var item in ModConfig.BlockPatchBiomes)
                {
                    if (item.Value.Intersect(chunkRealms).Any())
                    {
                        trees.Add(item.Key);
                    }
                }
            }

            return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = trees.Distinct().Join(delimiter: "\r\n") };
        }

        public TextCommandResult onGetBiomeCommand(TextCommandCallingArgs args)
        {
            var chunkHemisphere = EnumHemisphere.North;
            if (getModProperty(args.Caller, HemispherePropertyName, ref chunkHemisphere) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = ChunkNotGeneratedWithBiomesModInstalled };
            var hemisphereStr = Enum.GetName(typeof(EnumHemisphere), chunkHemisphere);

            var chunkRealms = new List<string>();
            if (getModProperty(args.Caller, RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = ChunkNotGeneratedWithBiomesModInstalled };
            var realmsStr = chunkRealms?.Join(delimiter: ",");

            return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = $"Hemisphere: {hemisphereStr} Realms: {realmsStr}" };
        }

        public TextCommandResult onSetHemisphereCommand(TextCommandCallingArgs args)
        {
            if (Enum.TryParse(args.Parsers[0].GetValue() as string, out EnumHemisphere hemisphere))
                return new TextCommandResult { Status = setModPropertyForCallerChunk(args.Caller, HemispherePropertyName, hemisphere) };

            return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = "Bad hemisphere argument" };
        }

        public TextCommandResult onAddRealmCommand(TextCommandCallingArgs args)
        {
            var currentRealms = new List<string>();
            getModProperty(args.Caller, RealmPropertyName, ref currentRealms);
            if (currentRealms == null)
                currentRealms = new();

            var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
            currentRealms.Add(value);

            return new TextCommandResult { Status = setModPropertyForCallerChunk(args.Caller, RealmPropertyName, currentRealms.Distinct()), StatusMessage = currentRealms?.Join(delimiter: ",") };
        }

        private TextCommandResult onRemoveRealmCommand(TextCommandCallingArgs args)
        {
            var currentRealms = new List<string>();
            getModProperty(args.Caller, RealmPropertyName, ref currentRealms);
            if (currentRealms == null)
                currentRealms = new();

            var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
            currentRealms.Remove(value);

            return new TextCommandResult { Status = setModPropertyForCallerChunk(args.Caller, RealmPropertyName, currentRealms.Distinct()), StatusMessage = currentRealms?.Join(delimiter: ",") };
        }

        public EnumCommandStatus setModPropertyForCallerChunk(Caller caller, string name, object value)
        {
            var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
            return setModProperty(chunk, name, ref value);
        }

        public EnumCommandStatus setModProperty<T>(IMapChunk chunk, string name, ref T value)
        {
            if (chunk == null)
                return EnumCommandStatus.Error;

            chunk.SetModdata(name, value);
            chunk.MarkDirty();
            return EnumCommandStatus.Success;
        }

        public EnumCommandStatus getModProperty<T>(Caller caller, string name, ref T value)
        {
            var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
            return getModProperty(chunk, name, ref value);
        }

        public EnumCommandStatus getModProperty<T>(IMapChunk chunk, string name, ref T value)
        {
            if (chunk == null)
                return EnumCommandStatus.Error;

            value = chunk.GetModdata<T>(name);
            return value == null ? EnumCommandStatus.Error : EnumCommandStatus.Success;
        }
    }
}
