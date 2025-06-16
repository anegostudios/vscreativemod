using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods.WorldEdit
{
    public partial class WorldEdit
    {
        public ICoreServerAPI sapi;

        public WorldEditClientHandler clientHandler;
        IServerNetworkChannel serverChannel;

        Dictionary<string, WorldEditWorkspace> workspaces = new();

        public static string ExportFolderPath;

        public static bool ReplaceMetaBlocks { get; set; }

        public override void StartPre(ICoreAPI api)
        {
            ToolRegistry.RegisterDefaultTools();
        }

        public override void Start(ICoreAPI api)
        {
            api.Network
                .RegisterChannel("worldedit")
                .RegisterMessageType(typeof(RequestWorkSpacePacket))
                .RegisterMessageType(typeof(WorldEditWorkspace))
                .RegisterMessageType(typeof(ChangePlayerModePacket))
                .RegisterMessageType(typeof(CopyToClipboardPacket))
                .RegisterMessageType(typeof(SchematicJsonPacket))
                .RegisterMessageType(typeof(WorldInteractPacket))
                .RegisterMessageType(typeof(PreviewBlocksPacket))
            ;
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            clientHandler = new WorldEditClientHandler(capi);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            this.sapi = sapi;

            ExportFolderPath = sapi.GetOrCreateDataPath("WorldEdit");

            sapi.Permissions.RegisterPrivilege("worldedit", "Ability to use world edit tools");

            RegisterCommands();

            sapi.Event.PlayerNowPlaying += Event_PlayerNowPlaying;

            sapi.Event.PlayerSwitchGameMode += OnSwitchedGameMode;

            sapi.Event.BreakBlock += OnBreakBlock;
            sapi.Event.DidPlaceBlock += OnDidBuildBlock;
            sapi.Event.SaveGameLoaded += OnLoad;
            sapi.Event.GameWorldSave += OnSave;

            serverChannel =
                sapi.Network.GetChannel("worldedit")
                .SetMessageHandler<RequestWorkSpacePacket>(OnRequestWorkSpaceMessage)
                .SetMessageHandler<ChangePlayerModePacket>(OnChangePlayerModeMessage)
                .SetMessageHandler<SchematicJsonPacket>(OnReceivedSchematic)
                .SetMessageHandler<WorldInteractPacket>(OnWorldInteract)
            ;

            var cmdapi = sapi.ChatCommands;
            var parsers = sapi.ChatCommands.Parsers;
            cmdapi
                .GetOrCreate("land")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .BeginSub("claim")
                    .BeginSub("download")
                        .WithDesc("Download a claim of yours to your computer")
                        .WithArgs(parsers.Int("claim id"))
                        .HandleWith(downloadClaim)
                    .EndSub()
                    .BeginSub("export")
                        .RequiresPrivilege("exportclaims")
                        .WithDesc("Export a claim of yours to a file on the game server")
                        .WithArgs(parsers.Int("claim id"))
                        .HandleWith(exportClaim)
                    .EndSub()
                .EndSub()
            ;
        }

        private TextCommandResult downloadClaim(TextCommandCallingArgs args)
        {
            var plr = args.Caller.Player as IServerPlayer;
            var ownclaims = sapi.WorldManager.SaveGame.LandClaims.Where(claim => claim.OwnedByPlayerUid == plr.PlayerUID).ToArray();
            int claimid = (int)args[0];
            if (claimid < 0 || claimid >= ownclaims.Length)
            {
                return TextCommandResult.Error(Lang.Get("Incorrect claimid, you only have {0} claims", ownclaims.Length));
            }
            else
            {
                var claim = ownclaims[claimid];
                var world = sapi.World;
                BlockSchematic blockdata = new BlockSchematic();
                BlockPos minPos = null;

                foreach (var area in claim.Areas)
                {
                    blockdata.AddArea(world, area.Start.ToBlockPos(), area.End.ToBlockPos());

                    if (minPos == null) minPos = area.Start.ToBlockPos();

                    minPos.X = Math.Min(area.Start.X, minPos.X);
                    minPos.Y = Math.Min(area.Start.Y, minPos.Y);
                    minPos.Z = Math.Min(area.Start.Z, minPos.Z);
                }

                blockdata.Pack(world, minPos);

                serverChannel.SendPacket(new SchematicJsonPacket() {
                    Filename = "claim-"+GamePaths.ReplaceInvalidChars(claim.Description),
                    JsonCode = blockdata.ToJson() }, plr);

                return TextCommandResult.Success(Lang.Get("Ok, claim sent"));
            }
        }

        private TextCommandResult exportClaim(TextCommandCallingArgs args)
        {
            var plr = args.Caller.Player as IServerPlayer;
            var ownclaims = sapi.WorldManager.SaveGame.LandClaims.Where(claim => claim.OwnedByPlayerUid == plr.PlayerUID).ToArray();
            int claimid = (int)args[0];
            if (claimid < 0 || claimid >= ownclaims.Length)
            {
                return TextCommandResult.Error(Lang.Get("Incorrect claimid, you only have {0} claims", ownclaims.Length));
            }
            else
            {
                var claim = ownclaims[claimid];
                var world = sapi.World;
                BlockSchematic blockdata = new BlockSchematic();
                BlockPos minPos = null;

                foreach (var area in claim.Areas)
                {
                    blockdata.AddArea(world, area.Start.ToBlockPos(), area.End.ToBlockPos());

                    if (minPos == null) minPos = area.Start.ToBlockPos();

                    minPos.X = Math.Min(area.Start.X, minPos.X);
                    minPos.Y = Math.Min(area.Start.Y, minPos.Y);
                    minPos.Z = Math.Min(area.Start.Z, minPos.Z);
                }

                blockdata.Pack(world, minPos);
                string filename = "claim-" + claimid + "-" + GamePaths.ReplaceInvalidChars(plr.PlayerName) + ".json";
                blockdata.Save(Path.Combine(ExportFolderPath, filename));

                return TextCommandResult.Success(Lang.Get("Ok, claim saved to file " + filename));
            }
        }

        private void OnWorldInteract(IServerPlayer fromPlayer, WorldInteractPacket packet)
        {
            BlockSelection blockSel = new BlockSelection()
            {
                SelectionBoxIndex = packet.SelectionBoxIndex,
                DidOffset = packet.DidOffset,
                Face = BlockFacing.ALLFACES[packet.Face],
                Position = packet.Position,
                HitPosition = packet.HitPosition
            };

            if (packet.Mode == 1)
            {
                OnDidBuildBlock(fromPlayer, -1, blockSel, fromPlayer.InventoryManager.ActiveHotbarSlot.Itemstack);
            } else
            {
                var handle = EnumHandling.PassThrough;
                float sdf = 1f;
                OnBreakBlock(fromPlayer, blockSel, ref sdf, ref handle);
            }
        }

        private void OnReceivedSchematic(IServerPlayer fromPlayer, SchematicJsonPacket networkMessage)
        {
            WorldEditWorkspace workspace = GetOrCreateWorkSpace(fromPlayer);
            if (workspace.ToolsEnabled && workspace.ToolInstance is ImportTool)
            {
                ImportTool impTool = workspace.ToolInstance as ImportTool;

                string error = null;
                BlockSchematic schematic = BlockSchematic.LoadFromString(networkMessage.JsonCode, ref error);

                if (error == null)
                {
                    impTool.SetBlockDatas(this, schematic);
                    var pos = fromPlayer.CurrentBlockSelection?.Position?.UpCopy() ?? fromPlayer.Entity.Pos.AsBlockPos;
                    var origin = schematic.GetStartPos(pos, impTool.Origin);
                    workspace.CreatePreview(schematic, origin);
                    workspace.PreviewPos = origin;
                    fromPlayer.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("Ok, schematic loaded into clipboard."), EnumChatType.CommandSuccess);
                }
                else
                {
                    fromPlayer.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("Error loading schematic: {0}", error), EnumChatType.CommandError);
                }
            }
        }

        private void OnChangePlayerModeMessage(IPlayer fromPlayer, ChangePlayerModePacket plrmode)
        {
            IServerPlayer plr = fromPlayer as IServerPlayer;

            bool freeMoveAllowed =  plr.HasPrivilege(Privilege.freemove);
            bool pickRangeAllowed = plr.HasPrivilege(Privilege.pickingrange);

            if (plrmode.axisLock != null)
            {
                fromPlayer.WorldData.FreeMovePlaneLock = (EnumFreeMovAxisLock)plrmode.axisLock;
            }
            if (plrmode.pickingRange != null && pickRangeAllowed)
            {
                fromPlayer.WorldData.PickingRange = (float)plrmode.pickingRange;
            }
            if (plrmode.fly != null)
            {
                fromPlayer.WorldData.FreeMove = (bool)plrmode.fly && freeMoveAllowed;
            }
            if (plrmode.noclip != null)
            {
                fromPlayer.WorldData.NoClip = (bool)plrmode.noclip && freeMoveAllowed;
            }
        }

        private void OnRequestWorkSpaceMessage(IPlayer fromPlayer, RequestWorkSpacePacket networkMessage)
        {
            SendPlayerWorkSpace(fromPlayer.PlayerUID);
        }

        public WorldEditWorkspace GetWorkSpace(string playerUid)
        {
            workspaces.TryGetValue(playerUid, out WorldEditWorkspace space);
            return space;
        }

        public void SendPlayerWorkSpace(string playerUID)
        {
            serverChannel.SendPacket(workspaces[playerUID], (IServerPlayer)sapi.World.PlayerByUid(playerUID));
        }

        public void RegisterTool(string toolname, Type tool)
        {
            ToolRegistry.RegisterToolType(toolname, tool);
        }

        private void OnSwitchedGameMode(IServerPlayer player)
        {
            if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) return;

            WorldEditWorkspace workspace = GetOrCreateWorkSpace(player);
            workspace.ToolsEnabled = false;
            workspace.StartMarker = null;
            workspace.EndMarker = null;
            workspace.ResendBlockHighlights();
        }

        private void Event_PlayerNowPlaying(IServerPlayer player)
        {
            WorldEditWorkspace workspace = GetOrCreateWorkSpace(player);

            // Initialize all tools once to build up the workspace for the client gui tool options
            foreach (var val in ToolRegistry.ToolTypes)
            {
                ToolRegistry.InstanceFromType(val.Key, workspace, workspace.revertableBlockAccess);
            }

            if (workspace.ToolsEnabled && workspace.ToolInstance != null)
            {
                workspace.ToolInstance?.Load(sapi);
                workspace.ResendBlockHighlights();
                SendPlayerWorkSpace(player.PlayerUID);
            }
            else
            {
                if (player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    SendPlayerWorkSpace(player.PlayerUID);
                }
            }
        }

        private void RevertableBlockAccess_BeforeCommit(IBulkBlockAccessor ba, WorldEditWorkspace workspace)
        {
            if (workspace.WorldEditConstraint == EnumWorldEditConstraint.Selection && workspace.StartMarker != null && workspace.EndMarker != null)
            {
                constrainEditsToSelection(ba, workspace);
            }
        }

        private void constrainEditsToSelection(IBulkBlockAccessor ba, WorldEditWorkspace workspace)
        {
            var selection = new Cuboidi(workspace.StartMarker, workspace.EndMarker);

            var stagedBlockPositions = ba.StagedBlocks.Keys.ToList();
            foreach (var pos in stagedBlockPositions)
            {
                if (!selection.Contains(pos))
                {
                    ba.StagedBlocks.Remove(pos);
                }
            }

            var stagedDecorPositions = ba.StagedBlocks.Where(b => b.Value.Decors != null).ToArray();
            foreach (var pos in stagedDecorPositions)
            {
                if (!selection.Contains(new Vec3i(pos.Key.X, pos.Key.Y, pos.Key.Z)))
                {
                    ba.StagedBlocks[pos.Key].Decors = null;
                }
            }
        }

        private void OnSave()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);

                writer.Write(workspaces.Count);
                foreach (WorldEditWorkspace workspace in workspaces.Values)
                {
                    workspace.ToBytes(writer);
                }

                sapi.WorldManager.SaveGame.StoreData("worldeditworkspaces", ms.ToArray());
            }
        }

        private void OnLoad()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData("worldeditworkspaces");
            if (data == null || data.Length == 0) return;

            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    int count = reader.ReadInt32();

                    while (count-- > 0)
                    {
                        IBlockAccessorRevertable revertableBlockAccess = sapi.World.GetBlockAccessorRevertable(true, true);
                        WorldEditWorkspace workspace = new WorldEditWorkspace(sapi.World, revertableBlockAccess);
                        revertableBlockAccess.BeforeCommit += (ba) => RevertableBlockAccess_BeforeCommit(ba, workspace);

                        workspace.FromBytes(reader);
                        workspace.Init(sapi);
                        if (workspace.PlayerUID == null)
                        {
                            continue;
                        }

                        workspaces[workspace.PlayerUID] = workspace;
                    }
                }
            }
            catch (Exception)
            {
                sapi.Server.LogEvent("Exception thrown when trying to load worldedit workspaces. Will ignore.");
            }
        }

        public static bool CanUseWorldEdit(IServerPlayer player, bool showError = false)
        {
            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                if (showError)
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, "Only available in creative mode.", EnumChatType.CommandError);
                }
                return false;
            }

            if (!player.HasPrivilege("worldedit"))
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "No privilege to use", EnumChatType.CommandError);
                return false;
            }

            return true;
        }

        WorldEditWorkspace GetOrCreateWorkSpace(IPlayer player)
        {
            string playeruid = player.PlayerUID;

            if (workspaces.ContainsKey(playeruid))
            {
                return workspaces[playeruid];
            }
            else
            {
                IBlockAccessorRevertable revertableBlockAccess = sapi.World.GetBlockAccessorRevertable(true, true);
                var workspace = workspaces[playeruid] = new WorldEditWorkspace(sapi.World, revertableBlockAccess);
                revertableBlockAccess.BeforeCommit += (ba) => RevertableBlockAccess_BeforeCommit(ba, workspace);
                workspace.Init(sapi);
                workspace.PlayerUID = playeruid;
                return workspace;
            }
        }

        private TextCommandResult GenMarkedMultiblockCode(IServerPlayer player)
        {
            BlockPos centerPos = player.CurrentBlockSelection.Position;
            API.Datastructures.OrderedDictionary<int, int> blocks = new ();
            List<Vec4i> offsets = new List<Vec4i>();

            MultiblockStructure ms = new MultiblockStructure();
            var workspace = workspaces[player.PlayerUID];

            sapi.World.BlockAccessor.WalkBlocks(workspace.StartMarker, workspace.EndMarker, (block, x, y, z) =>
            {
                if (block.Id == 0) return;

                int blockNum = ms.GetOrCreateBlockNumber(block);
                BlockOffsetAndNumber offset = new BlockOffsetAndNumber(x - centerPos.X, y - centerPos.Y, z - centerPos.Z, blockNum);
                ms.Offsets.Add(offset);
            }, true);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("multiblockStructure: {");
            sb.AppendLine("\tblockNumbers: {");
            foreach (var val in ms.BlockNumbers)
            {
                sb.AppendLine(string.Format("\t\t\"{0}\": {1},", val.Key.ToShortString(), val.Value));
            }
            sb.AppendLine("\t},");
            sb.AppendLine("\toffsets: [");

            foreach (var val in ms.Offsets)
            {
                sb.AppendLine(string.Format("\t\t{{ x: {0}, y: {1}, z: {2}, w: {3} }},", val.X, val.Y, val.Z, val.W));
            }

            sb.AppendLine("\t]");
            sb.AppendLine("}");

            sapi.World.Logger.Notification("Multiblockstructure centered around {0}:\n{1}", centerPos, sb.ToString());
            return TextCommandResult.Success("Json code written to server-main.log");
        }

        private int RebuildRainMap()
        {
            int mapChunksRebuilt = 0;

            Dictionary<long, IMapChunk> mapchunks = sapi.WorldManager.AllLoadedMapchunks;
            int ymax = sapi.WorldManager.MapSizeY / sapi.WorldManager.ChunkSize;

            IServerChunk[] column = new IServerChunk[ymax];
            int chunksize = sapi.WorldManager.ChunkSize;

            foreach (var val in mapchunks)
            {
                int cx = (int)(val.Key % (sapi.WorldManager.MapSizeX / chunksize));
                int cz = (int)(val.Key / (sapi.WorldManager.MapSizeX / chunksize));
                mapChunksRebuilt++;


                for (int cy = 0; cy < ymax; cy++)
                {
                    column[cy] = sapi.WorldManager.GetChunk(cx, cy, cz);
                    column[cy]?.Unpack();
                }

                for (int dx = 0; dx < chunksize; dx++)
                {
                    for (int dz = 0; dz < chunksize; dz++)
                    {
                        for (int dy = sapi.WorldManager.MapSizeY - 1; dy >= 0; dy--)
                        {
                            IServerChunk chunk = column[dy / chunksize];
                            if (chunk == null) continue;

                            int index = ((dy % chunksize) * chunksize + dz) * chunksize + dx;
                            Block block = sapi.World.Blocks[chunk.Data.GetBlockId(index, BlockLayersAccess.FluidOrSolid)];

                            if (!block.RainPermeable || dy == 0)
                            {
                                val.Value.RainHeightMap[dz * chunksize + dx] = (ushort)dy;
                                break;
                            }
                        }
                    }
                }

                sapi.WorldManager.ResendMapChunk(cx, cz, true);
                val.Value.MarkDirty();
            }

            return mapChunksRebuilt;
        }

        private void EnsureInsideMap(BlockPos pos)
        {
            pos.X = GameMath.Clamp(pos.X, 0, sapi.WorldManager.MapSizeX - 1);
            pos.Y = GameMath.Clamp(pos.Y, 0, sapi.WorldManager.MapSizeY - 1);
            pos.Z = GameMath.Clamp(pos.Z, 0, sapi.WorldManager.MapSizeZ - 1);
        }

        public void SelectionMode(bool on, IServerPlayer player)
        {
            if (player == null) return;

            player.WorldData.AreaSelectionMode = on;
            player.BroadcastPlayerData();
        }

        private TextCommandResult HandleHistoryChange(TextCommandCallingArgs args, bool redo)
        {
            var workspace = GetWorkSpace(args.Caller.Player.PlayerUID);
            if (redo && workspace.revertableBlockAccess.CurrentHistoryState == 0)
            {
                return TextCommandResult.Error("Can't redo. Already on newest history state.");
            }
            if (!redo && workspace.revertableBlockAccess.CurrentHistoryState == workspace.revertableBlockAccess.AvailableHistoryStates)
            {
                return TextCommandResult.Error("Can't undo. Already on oldest available history state.");
            }

            int prevHstate = workspace.revertableBlockAccess.CurrentHistoryState;

            int steps = (int)args[0];

            workspace.revertableBlockAccess.ChangeHistoryState(steps * (redo ? -1 : 1));
            workspace.ResendBlockHighlights();

            int quantityChanged = Math.Abs(prevHstate - workspace.revertableBlockAccess.CurrentHistoryState);

            return TextCommandResult.Success(string.Format("Performed {0} {1} times.", redo ? "redo" : "undo", quantityChanged));
        }



        public void OnInteractStart(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            if (!CanUseWorldEdit(byPlayer)) return;

            var workspace = GetOrCreateWorkSpace(byPlayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnInteractStart(this, blockSel?.Clone());
        }

        public void OnAttackStart(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            if (!CanUseWorldEdit(byPlayer)) return;

            var workspace = GetOrCreateWorkSpace(byPlayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnAttackStart(this, blockSel?.Clone());
        }


        private void OnDidBuildBlock(IServerPlayer byPlayer, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (!CanUseWorldEdit(byPlayer)) return;

            var workspace = GetOrCreateWorkSpace(byPlayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnBuild(this, oldBlockId, blockSel.Clone(), withItemStack);
        }

        private void OnBreakBlock(IServerPlayer byBplayer, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (!CanUseWorldEdit(byBplayer)) return;

            var workspace = GetOrCreateWorkSpace(byBplayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnBreak(this, blockSel, ref handling);
        }

        public static void Good(IServerPlayer player, string message, params object[] args)
        {
            player.SendMessage(0, string.Format(message, args), EnumChatType.CommandSuccess);
        }

        public static void Bad(IServerPlayer player, string message, params object[] args)
        {
            player.SendMessage(0, string.Format(message, args), EnumChatType.CommandError);
        }
    }
}
