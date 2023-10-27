using Cairo;
using HarmonyLib;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using Vintagestory.ServerMods;

[assembly: ModInfo("biomes",
                    Authors = new string[] { "Crabb" },
                    Description = "Biomes",
                    Version = "1.0.0")]

namespace biomes
{
    [ProtoContract]
    public class SinglePlaceNameByMapRegionCoords
    {
        [ProtoMember(1)]
        public int mapRegionX;

        [ProtoMember(2)]
        public int mapRegionZ;

        [ProtoMember(3)]
        public string biome;
    }

    [ProtoContract]
    public class PlaceNameList
    {
        public PlaceNameList() { list = new List<SinglePlaceNameByMapRegionCoords>(); }

        [ProtoMember(1)]
        public List<SinglePlaceNameByMapRegionCoords> list;
    }

    [HarmonyPatch]
    public class BiomesModSystem : ModSystem
    {
        ICoreServerAPI sapi;
        public Harmony harmony;

        private const string realmProperty = "biorealm";
        private const string hemisphereProperty = "hemisphere";

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();

            sapi = api;

            sapi.Event.MapRegionGeneration(OnMapRegionGeneration, "standard");
            sapi.Event.PlayerJoin += OnPlayerJoin;

            sapi.ChatCommands.Create("biome")
                .WithDescription("Biome main command")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("set")
                    .WithArgs(sapi.ChatCommands.Parsers.Word("val"))
                    .HandleWith(onSetBiomeCommand)
                .EndSubCommand()
                .BeginSubCommand("get")
                    .HandleWith(onGetBiomeCommand)
                .EndSubCommand();
        }

        private void OnMapRegionGeneration(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams)
        {
            const int realmCount = 5;
            int widthInRegions = sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize;
            int realmWidthInRegions = widthInRegions / realmCount;
            int currentRealm = 2;
            if (realmWidthInRegions > 0)
                currentRealm = regionX / realmWidthInRegions;

            const int hemisphereCount = 2;
            int heightInRegions = sapi.WorldManager.MapSizeZ / sapi.WorldManager.RegionSize;
            int hemiHeightInRegions = heightInRegions / hemisphereCount;
            int hemi = 0;
            if (heightInRegions > 0)
                hemi = regionZ / hemiHeightInRegions;

            EnumHemisphere hemisphere = hemi == 0 ? EnumHemisphere.North : EnumHemisphere.South;
            //EnumHemisphere hemisphere = sapi.World.Calendar.GetHemisphere(new BlockPos(regionX * sapi.WorldManager.RegionSize, 0, regionZ * sapi.WorldManager.RegionSize));
            if (hemisphere == EnumHemisphere.North)
                mapRegion.SetModdata(hemisphereProperty, "north");
            else
                mapRegion.SetModdata(hemisphereProperty, "south");

            string realmName = "";
            switch (currentRealm)
            {
                case 1:
                    realmName = hemisphere == EnumHemisphere.North ? "nearctic" : "neotropical";
                    break;
                case 2:
                    realmName = hemisphere == EnumHemisphere.North ? "palearctic" : "afrotropical";
                    break;
                case 3:
                    realmName = hemisphere == EnumHemisphere.North ? "palearctic" : "indomalayan";
                    break;
                case 4:
                    realmName = hemisphere == EnumHemisphere.North ? "palearctic" : "austrolasian";
                    break;
                default:
                    break;
            }
            mapRegion.SetModdata(realmProperty, realmName);

        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt")]
        public static bool CanSpawnAt(ServerSystemEntitySpawner __instance, ref Vec3d __result, EntityProperties type, Vec3i spawnPosition, RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
        {
            IMapRegion mapRegion = chunkCol[0].MapChunk.MapRegion;
            string regionHemisphere = mapRegion.GetModdata<string>(hemisphereProperty);
            var regionRealm = mapRegion.GetModdata<string>(realmProperty);


            // Only blessed animals get in.
            if (type.Attributes == null
                || !type.Attributes.KeyExists(realmProperty)
                || !type.Attributes.KeyExists(hemisphereProperty))
            {
                __result = null;
                return false;
            }

            var animalHemisphere = type.Attributes[hemisphereProperty].AsArray<string>();
            if (!animalHemisphere.Contains(regionHemisphere))
            {
                __result = null;
                return false;
            }

            var animalRealms = type.Attributes[realmProperty].AsArray<string>();
            if (!animalRealms.Contains(regionRealm))
            {
                __result = null;
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCreatures), "CanSpawnAtPosition")]
        public static bool CanSpawnAtPosition(GenCreatures __instance, ref bool __result, IBlockAccessor blockAccessor, EntityProperties type, BlockPos pos, BaseSpawnConditions sc)
        {
            IMapRegion mapRegion = blockAccessor.GetMapRegion(pos.X / blockAccessor.RegionSize, pos.Z / blockAccessor.RegionSize);

            // Region attributes
            string regionHemisphere = mapRegion.GetModdata<string>(hemisphereProperty);
            var regionRealm = mapRegion.GetModdata<string>(realmProperty);

            // Only blessed animals get in.
            if (type.Attributes == null
                || !type.Attributes.KeyExists(realmProperty)
                || !type.Attributes.KeyExists(hemisphereProperty))
            {
                __result = false;
                return false;
            }

            var animalHemisphere = type.Attributes[hemisphereProperty].AsArray<string>();
            if (!animalHemisphere.Contains(regionHemisphere))
            {
                __result = false;
                return false;
            }

            var animalRealms = type.Attributes[realmProperty].AsArray<string>();
            if (!animalRealms.Contains(regionRealm))
            {
                __result = false;
                return false;
            }

            __result = true;
            return true;
        }

        private TextCommandResult onGetBiomeCommand(TextCommandCallingArgs args)
        {
            if (args.Caller == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error };

            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error };

            var coords = serverPlayer.Entity.Pos.AsBlockPos;
            var chunk = sapi.World.BlockAccessor.GetMapChunkAtBlockPos(coords);
            if (chunk == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error };
            var region = chunk.MapRegion;

            serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, $"{region.GetModdata<string>(realmProperty)}, {region.GetModdata<string>(hemisphereProperty)}", EnumChatType.Notification);

            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        private TextCommandResult onSetBiomeCommand(TextCommandCallingArgs args)
        {
            if (args.Caller == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error };

            var coords = args.Caller.Entity.Pos.AsBlockPos;
            var blockAccessor = sapi.World.BlockAccessor;
            var regionX = coords.X / blockAccessor.RegionSize;
            var regionZ = coords.Z / blockAccessor.RegionSize;

            string biome = args.Parsers[0].GetValue() as string;
            var chunk = sapi.World.BlockAccessor.GetMapChunkAtBlockPos(coords);
            var region = chunk.MapRegion;
            region.SetModdata(realmProperty, biome);

            //channelUpdateServer.SendPacket(new SinglePlaceNameByMapRegionCoords { mapRegionX = regionX, mapRegionZ = regionZ, biome = biome });

            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        private void onUpdateServer(IServerPlayer fromPlayer, SinglePlaceNameByMapRegionCoords inboundPacket)
        {
            var mapRegion = sapi.WorldManager.GetMapRegion(inboundPacket.mapRegionX, inboundPacket.mapRegionZ);
            if (mapRegion == null)
                return;

            mapRegion.SetModdata(realmProperty, inboundPacket.biome);
            mapRegion.DirtyForSaving = true;

            /*
            var outboundPacket = new PlaceNameList();
            outboundPacket.list.Add(new SinglePlaceNameByMapRegionCoords
            {
                mapRegionX = inboundPacket.mapRegionX,
                mapRegionZ = inboundPacket.mapRegionZ,
                biome = inboundPacket.biome
            });

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                var serverPlayer = player as IServerPlayer;
                if (serverPlayer == null)
                    continue;

                if (serverPlayer.ConnectionState != EnumClientState.Playing || fromPlayer.PlayerUID == serverPlayer.PlayerUID)
                    continue;

                channelUpdateClients.SendPacket(outboundPacket, serverPlayer);
            }
            */
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            /*
            var outboundPacket = new PlaceNameList();
            foreach (var pair in sapi.WorldManager.AllLoadedMapRegions)
            {
                string biome = pair.Value.GetModdata<string>(propertyName);
                if (String.IsNullOrEmpty(biome))
                    continue;
                Vec3i regioncoord = MapRegionPosFromIndex2D(pair.Key);
                outboundPacket.list.Add(new SinglePlaceNameByMapRegionCoords
                {
                    mapRegionX = regioncoord.X,
                    mapRegionZ = regioncoord.Z,
                    biome = biome
                });
            }
            channelUpdateClients.SendPacket(outboundPacket, byPlayer);
            */
        }

        public Vec3i MapRegionPosFromIndex2D(long index)
        {
            return new Vec3i(
                (int)(index % sapi.World.BlockAccessor.RegionMapSizeX),
                0,
                (int)(index / sapi.World.BlockAccessor.RegionMapSizeX)
            );
        }

    }
}
