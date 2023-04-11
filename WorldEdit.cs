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

namespace Vintagestory.ServerMods.WorldEdit
{

    public partial class WorldEdit : ModSystem
    {
        public ICoreServerAPI sapi;

        public WorldEditClientHandler clientHandler;
        IServerNetworkChannel serverChannel;

        Dictionary<string, WorldEditWorkspace> workspaces = new Dictionary<string, WorldEditWorkspace>();


        // Unpretty, but too lazy to type it over and over again ;-)
        IServerPlayer fromPlayer;
        WorldEditWorkspace workspace; 
        string exportFolderPath;


        public bool ReplaceMetaBlocks
        {
            get
            {
                object val;
                sapi.ObjectCache.TryGetValue("donotResolveImports", out val);

                if (val is bool)
                {
                    return !(bool)val;
                }

                return true;
            }
        }


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
            ;
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            clientHandler = new WorldEditClientHandler(capi);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            this.sapi = sapi;

            exportFolderPath = sapi.GetOrCreateDataPath("WorldEdit");

            sapi.Permissions.RegisterPrivilege("worldedit", "Ability to use world edit tools");

            registerCommands();

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
                    this.fromPlayer = fromPlayer;
                    impTool.SetBlockDatas(this, new BlockSchematic[] { schematic });
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
            WorldEditWorkspace space;
            workspaces.TryGetValue(playerUid, out space);
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
            fromPlayer = player;
            workspace.ResendBlockHighlights(this);
        }


        private void Event_PlayerNowPlaying(IServerPlayer player)
        {
            fromPlayer = player as IServerPlayer;

            WorldEditWorkspace workspace = GetOrCreateWorkSpace(player);

            IBlockAccessorRevertable revertableBlockAccess = sapi.World.GetBlockAccessorRevertable(true, true);

            // Initialize all tools once to build up the workspace for the client gui tool options
            foreach (var val in ToolRegistry.ToolTypes)
            {
                ToolRegistry.InstanceFromType(val.Key, workspace, revertableBlockAccess);
            }

            if (workspace.ToolsEnabled)
            {
                workspace.ToolInstance.Load(sapi);
                workspace.ResendBlockHighlights(this);
                SendPlayerWorkSpace(player.PlayerUID);
            }
            else
            {
                workspace.HighlightSelectedArea();
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
                        workspace.FromBytes(reader);
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


        public bool CanUseWorldEdit(IPlayer player, bool showError = false)
        {
            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                if (showError) Bad("Only available in creative mode.");
                return false;
            }

            if (!((IServerPlayer)player).HasPrivilege("worldedit"))
            {
                if (showError) Bad("No privilege to use");
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
                workspaces[playeruid] = new WorldEditWorkspace(sapi.World, revertableBlockAccess);
                workspaces[playeruid].PlayerUID = playeruid;
                return workspaces[playeruid];
            }
        }



        private TextCommandResult GenMarkedMultiblockCode(IServerPlayer player)
        {
            BlockPos centerPos = player.CurrentBlockSelection.Position;
            OrderedDictionary<int, int> blocks = new OrderedDictionary<int, int>();
            List<Vec4i> offsets = new List<Vec4i>();

            MultiblockStructure ms = new MultiblockStructure();

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
            return TextCommandResult.Success("Json code written to server-main.txt");
        }

        public TextCommandResult SetStartPos(Vec3d pos)
        {
            workspace.StartMarkerExact = pos.Clone();
            updateSelection();
            return TextCommandResult.Success("Start position " + workspace.StartMarkerExact.AsBlockPos + " marked");
        }

        public TextCommandResult SetEndPos(Vec3d pos)
        {
            workspace.EndMarkerExact = pos.Clone();
            updateSelection();
            return TextCommandResult.Success("End position " + workspace.EndMarkerExact.AsBlockPos + " marked");
        }

        void updateSelection()
        {
            if (workspace.StartMarkerExact != null && workspace.EndMarkerExact != null)
            {
                workspace.StartMarker = new BlockPos(
                    (int)Math.Min(workspace.StartMarkerExact.X, workspace.EndMarkerExact.X),
                    (int)Math.Min(workspace.StartMarkerExact.Y, workspace.EndMarkerExact.Y),
                    (int)Math.Min(workspace.StartMarkerExact.Z, workspace.EndMarkerExact.Z)
                );

                workspace.EndMarker = new BlockPos(
                    (int)Math.Ceiling(Math.Max(workspace.StartMarkerExact.X, workspace.EndMarkerExact.X)),
                    (int)Math.Ceiling(Math.Max(workspace.StartMarkerExact.Y, workspace.EndMarkerExact.Y)),
                    (int)Math.Ceiling(Math.Max(workspace.StartMarkerExact.Z, workspace.EndMarkerExact.Z))
                );

                EnsureInsideMap(workspace.StartMarker);
                EnsureInsideMap(workspace.EndMarker);
            }
            
            workspace.HighlightSelectedArea();
            workspace.revertableBlockAccess.StoreHistoryState(HistoryState.Empty);
            SendPlayerWorkSpace(workspace.PlayerUID);
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


        public void SelectionMode(bool on)
        {
            if (fromPlayer == null) return;

            fromPlayer.WorldData.AreaSelectionMode = on;
            fromPlayer.BroadcastPlayerData();
        }

        private TextCommandResult HandleHistoryChange(TextCommandCallingArgs args, bool redo)
        {
            if (redo && workspace.revertableBlockAccess.CurrentyHistoryState == 0)
            {
                TextCommandResult.Error("Can't redo. Already on newest history state.");
            }
            if (!redo && workspace.revertableBlockAccess.CurrentyHistoryState == workspace.revertableBlockAccess.AvailableHistoryStates)
            {
                TextCommandResult.Error("Can't undo. Already on oldest available history state.");
            }

            int prevHstate = workspace.revertableBlockAccess.CurrentyHistoryState;

            int steps = (int)args[0];

            workspace.revertableBlockAccess.ChangeHistoryState(steps * (redo ? -1 : 1));

            int quantityChanged = Math.Abs(prevHstate - workspace.revertableBlockAccess.CurrentyHistoryState);

            return TextCommandResult.Success(string.Format("Performed {0} {1} times.", redo ? "redo" : "undo", quantityChanged));
        }


        private void BlockLineup(BlockPos pos, CmdArgs args)
        {
            IList<Block> blocks = sapi.World.Blocks;

            bool all = args.PopWord() == "all";

            List<Block> existingBlocks = new List<Block>();
            for (int i = 0; i < blocks.Count; i++)
            {
                Block block = blocks[i];

                if (block == null || block.Code == null) continue;

                if (all) existingBlocks.Add(block);
                else if (block.CreativeInventoryTabs != null && block.CreativeInventoryTabs.Length > 0)
                {
                    existingBlocks.Add(block);
                }
            }

            int width = (int)Math.Sqrt(existingBlocks.Count);

            FillArea(null, pos.AddCopy(0, 0, 0), pos.AddCopy(width + 1, 10, width + 1));

            for (int i = 0; i < existingBlocks.Count; i++)
            {
                workspace.revertableBlockAccess.SetBlock(existingBlocks[i].BlockId, pos.AddCopy(i / width, 0, i % width));
            }

            workspace.revertableBlockAccess.Commit();
        }

        public void OnInteractStart(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            this.fromPlayer = byPlayer;
            if (!CanUseWorldEdit(byPlayer)) return;

            workspace = GetOrCreateWorkSpace(byPlayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnInteractStart(this, blockSel?.Clone());
        }

        public void OnAttackStart(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            this.fromPlayer = byPlayer;
            if (!CanUseWorldEdit(byPlayer)) return;

            workspace = GetOrCreateWorkSpace(byPlayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnAttackStart(this, blockSel?.Clone());
        }


        private void OnDidBuildBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            this.fromPlayer = byPlayer;
            if (!CanUseWorldEdit(byPlayer)) return;

            workspace = GetOrCreateWorkSpace(byPlayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnBuild(this, oldblockId, blockSel.Clone(), withItemStack);
        }

        private void OnBreakBlock(IServerPlayer byBplayer, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            this.fromPlayer = byBplayer;
            if (!CanUseWorldEdit(byBplayer)) return;

            workspace = GetOrCreateWorkSpace(byBplayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnBreak(this, blockSel, ref handling);
        }

        private BlockSchematic CopyArea(BlockPos start, BlockPos end)
        {
            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            BlockSchematic blockdata = new BlockSchematic();
            blockdata.AddArea(sapi.World, start, end);
            blockdata.Pack(sapi.World, startPos);
            return blockdata;
        }


        private void PasteBlockData(BlockSchematic blockData, BlockPos startPos, EnumOrigin origin)
        {
            BlockPos originPos = blockData.GetStartPos(startPos, origin);

            EnumAxis? axis = null;
            if (workspace.ImportFlipped) axis = EnumAxis.Y;

            BlockSchematic rotated = blockData.ClonePacked();
            rotated.TransformWhilePacked(sapi.World, origin, workspace.ImportAngle, axis);

            rotated.Init(workspace.revertableBlockAccess);
            rotated.Place(workspace.revertableBlockAccess, sapi.World, originPos, EnumReplaceMode.ReplaceAll, ReplaceMetaBlocks);
            workspace.revertableBlockAccess.Commit();

            blockData.PlaceEntitiesAndBlockEntities(workspace.revertableBlockAccess, sapi.World, originPos, blockData.BlockCodes, blockData.ItemCodes);
            rotated.PlaceDecors(workspace.revertableBlockAccess, originPos);

            workspace.revertableBlockAccess.CommitBlockEntityData();
        }

        private TextCommandResult ImportArea(string filename, BlockPos startPos, EnumOrigin origin)
        {
            string infilepath = Path.Combine(exportFolderPath, filename);
            BlockSchematic blockData;

            string error = "";

            blockData = BlockSchematic.LoadFromFile(infilepath, ref error);
            if (blockData == null)
            {
                return TextCommandResult.Error(error);
            }

            PasteBlockData(blockData, startPos, origin);
            return TextCommandResult.Success(filename + " imported.");
        }


        private TextCommandResult ExportArea(string filename, BlockPos start, BlockPos end, IServerPlayer sendToPlayer = null)
        {
            BlockSchematic blockdata = CopyArea(start, end);
            int exported = blockdata.BlockIds.Count;

            string outfilepath = Path.Combine(exportFolderPath, filename);

            if (sendToPlayer != null)
            {
                serverChannel.SendPacket<SchematicJsonPacket>(new SchematicJsonPacket() { Filename = filename, JsonCode = blockdata.ToJson() }, sendToPlayer);
                return TextCommandResult.Success(exported + " blocks schematic sent to client.");
            }
            else
            {
                string error = blockdata.Save(outfilepath);
                if (error != null)
                {
                    return TextCommandResult.Success("Failed exporting: " + error);
                }
                else
                {
                    return TextCommandResult.Success(exported + " blocks exported.");
                }
            }


        }



        public bool MayPlace(Block block, int quantityBlocks)
        {
            if (workspace.serverOverloadProtection)
            {
                if (quantityBlocks > 100 && block.LightHsv[2] > 5)
                {
                    Bad("Operation rejected. Server overload protection is on. Might kill the server to place that many light sources.");
                    return false;
                }

                if (quantityBlocks > 200 * 200 * 200)
                {
                    Bad("Operation rejected. Server overload protection is on. Might kill the server to (potentially) place that many blocks.");
                    return false;
                }

                ItemStack stack = new ItemStack(block);
                if (quantityBlocks > 1000 && block.GetBlockMaterial(sapi.World.BlockAccessor, null, stack) == EnumBlockMaterial.Plant)
                {
                    Bad("Operation rejected. Server overload protection is on. Might kill the server when placing that many plants (might cause massive neighbour block updates if one plant is broken).");
                    return false;
                }
            }

            return true;
        }

        public void Good(string message, params object[] args)
        {
            fromPlayer.SendMessage(0, string.Format(message, args), EnumChatType.CommandSuccess);
        }

        public void Bad(string message, params object[] args)
        {
            fromPlayer.SendMessage(0, string.Format(message, args), EnumChatType.CommandError);
        }

    }
}
