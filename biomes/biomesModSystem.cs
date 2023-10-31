using HarmonyLib;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Biomes
{
    public class BiomeUserConfig
    {
        public bool FlipNorthSouth = false;
    }

    public class RealmsConfig
    {
        public List<string> NorthernRealms = new List<string>();
        public List<string> SouthernRealms = new List<string>();
    }

    public class BiomeConfig
    {
        public List<string> EntitySpawnWhiteList = new List<string>();
        public Dictionary<string, List<string>> TreeBiomes = new Dictionary<string, List<string>>();
        public Dictionary<string, List<string>> BlockPatchBiomes = new Dictionary<string, List<string>>();
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

        public BiomeUserConfig UserConfig;
        public RealmsConfig RealmsConfig;
        public BiomeConfig ModConfig;

        //public List<Regex> EntitySpawnWhiteListRx = new List<Regex>();
        //public Dictionary<Regex, List<string>> TreeBiomesRx = new Dictionary<Regex, List<string>>();
        //public Dictionary<Regex, List<string>> BlockPatchBiomesRx = new Dictionary<Regex, List<string>>();

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
                foreach (var item in tmp.BlockPatchBiomes)
                    ModConfig.BlockPatchBiomes[item.Key] = item.Value;
            }

            ModConfig.EntitySpawnWhiteList = ModConfig.EntitySpawnWhiteList.Distinct().ToList();

            UserConfig = sapi.LoadModConfig<BiomeUserConfig>($"{Mod.Info.ModID}.json");
            if (UserConfig == null)
            {
                UserConfig = new BiomeUserConfig();
                UserConfig.FlipNorthSouth = false;
            }
            sapi.StoreModConfig(UserConfig, $"{Mod.Info.ModID}.json");

            if (UserConfig.FlipNorthSouth)
            {
                var tmp = RealmsConfig.NorthernRealms;
                RealmsConfig.NorthernRealms = RealmsConfig.SouthernRealms;
                RealmsConfig.SouthernRealms = tmp;
            }

            sapi.Event.MapChunkGeneration(OnMapChunkGeneration, "standard");

            sapi.ChatCommands.Create("biome")
                .WithDescription("Biome main command")
                .RequiresPrivilege(Privilege.gamemode)
                .BeginSubCommand("show")
                    .HandleWith(onGetBiomeCommand)
                .EndSubCommand()
                .BeginSubCommand("trees")
                    .HandleWith(onTreesCommand)
                .EndSubCommand()
                .BeginSubCommand("debug")
                    .HandleWith(onDebugCommand)
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
            getModProperty(mapChunk, RealmPropertyName, ref chunkRealms);
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
            getModProperty(args.Caller, RealmPropertyName, ref chunkRealms);

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
            var treeStr = trees.Distinct().Join(delimiter: "\r\n");

            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer != null)
                serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, treeStr, EnumChatType.Notification);

            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        public TextCommandResult onDebugCommand(TextCommandCallingArgs args)
        {
            int worldWidthInChunks = sapi.WorldManager.MapSizeX / sapi.WorldManager.ChunkSize;
            for (int chunkX = 0; chunkX < worldWidthInChunks; chunkX++)
            {
                CalculateValues(chunkX, 0, out EnumHemisphere hemisphere, out int currentRealm);
                string realmName = NorthOrSouth(hemisphere, currentRealm);
                sapi.Logger.Notification($"{chunkX} => {realmName}");
            }
            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        public TextCommandResult onGetBiomeCommand(TextCommandCallingArgs args)
        {
            var chunkHemisphere = EnumHemisphere.North;
            getModProperty(args.Caller, HemispherePropertyName, ref chunkHemisphere);
            var hemisphereStr = Enum.GetName(typeof(EnumHemisphere), chunkHemisphere);

            var chunkRealms = new List<string>();
            getModProperty(args.Caller, RealmPropertyName, ref chunkRealms);
            var realmsStr = chunkRealms?.Join(delimiter: ",");

            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer != null)
                serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, $"Hemisphere: {hemisphereStr} Realms: {realmsStr}", EnumChatType.Notification);

            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        public TextCommandResult onSetHemisphereCommand(TextCommandCallingArgs args)
        {
            if (Enum.TryParse(args.Parsers[0].GetValue() as string, out EnumHemisphere hemisphere))
                return new TextCommandResult { Status = setModPropertyForCallerChunk(args.Caller, HemispherePropertyName, hemisphere) };

            return new TextCommandResult { Status = EnumCommandStatus.Error };
        }

        public TextCommandResult onAddRealmCommand(TextCommandCallingArgs args)
        {
            var currentRealms = new List<string>();
            EnumCommandStatus result = getModProperty(args.Caller, RealmPropertyName, ref currentRealms);
            if (result == EnumCommandStatus.Success)
            {
                var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
                currentRealms.Add(value);
                result = setModPropertyForCallerChunk(args.Caller, RealmPropertyName, currentRealms.Distinct());
            }

            return new TextCommandResult { Status = result };
        }

        private TextCommandResult onRemoveRealmCommand(TextCommandCallingArgs args)
        {
            var currentRealms = new List<string>();
            EnumCommandStatus result = getModProperty(args.Caller, RealmPropertyName, ref currentRealms);
            if (result == EnumCommandStatus.Success)
            {
                var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
                currentRealms.Remove(value);
                result = setModPropertyForCallerChunk(args.Caller, RealmPropertyName, currentRealms.Distinct());
            }

            return new TextCommandResult { Status = result };
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
            return EnumCommandStatus.Success;
        }
    }
}
