using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.WorldEdit
{


    public partial class WorldEdit : ModSystem
    {
        public ICoreServerAPI sapi;

        WorldEditClientHandler clientHandler;
        IServerNetworkChannel serverChannel;

        Dictionary<string, WorldEditWorkspace> workspaces = new Dictionary<string, WorldEditWorkspace>();


        // Unpretty, but too lazy to type it over and over again ;-)
        IServerPlayer fromPlayer;
        int groupId;
        WorldEditWorkspace workspace;
        public bool serverOverloadProtection = true;
        string exportFolderPath;


        public bool ReplaceMetaBlocks
        {
            get
            {
                object val = null;
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

        public override void StartClientSide(ICoreClientAPI capi)
        {
            clientHandler = new WorldEditClientHandler(capi);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            
            exportFolderPath = sapi.GetOrCreateDataPath("WorldEdit");

            sapi.Permissions.RegisterPrivilege("worldedit", "Ability to use world edit tools");

            sapi.RegisterCommand("we", "World edit tools", "[ms|me|mc|mex|clear|mclear|mfill|imp|impr|blu|brs|brm|ers|range|tool|on|off|undo|redo|sovp|hp|sp|block|...]", CmdEditServer, "worldedit");

            sapi.Event.PlayerJoin += OnPlayerJoin;

            sapi.Event.PlayerSwitchGameMode += OnSwitchedGameMode;
            sapi.Event.DidBreakBlock += OnDidBreakBlock;
            sapi.Event.DidPlaceBlock += OnDidBuildBlock;
            sapi.Event.SaveGameLoaded += OnLoad;
            sapi.Event.GameWorldSave += OnSave;

            serverChannel =
                sapi.Network.RegisterChannel("worldedit")
                .RegisterMessageType(typeof(RequestWorkSpacePacket))
                .RegisterMessageType(typeof(WorldEditWorkspace))
                .RegisterMessageType(typeof(ChangePlayerModePacket))
                .RegisterMessageType(typeof(CopyToClipboardPacket))
                .RegisterMessageType(typeof(SchematicJsonPacket))
                .SetMessageHandler<RequestWorkSpacePacket>(OnRequestWorkSpaceMessage)
                .SetMessageHandler<ChangePlayerModePacket>(OnChangePlayerModeMessage)
                .SetMessageHandler<SchematicJsonPacket>(OnReceivedSchematic)
            ;
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
                } else
                {
                    fromPlayer.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("Error loading schematic: {0}", error), EnumChatType.CommandError);
                }
            }
        }

        private void OnChangePlayerModeMessage(IPlayer fromPlayer, ChangePlayerModePacket plrmode)
        {
            if (plrmode.axisLock != null)
            {
                fromPlayer.WorldData.FreeMovePlaneLock = (EnumFreeMovAxisLock)plrmode.axisLock;
            }
            if (plrmode.pickingRange != null)
            {
                fromPlayer.WorldData.PickingRange = (float)plrmode.pickingRange;
            }
            if (plrmode.fly != null)
            {
                fromPlayer.WorldData.FreeMove = (bool)plrmode.fly;
            }
            if (plrmode.noclip != null)
            {
                fromPlayer.WorldData.NoClip = (bool)plrmode.noclip;
            }
        }

        private void OnRequestWorkSpaceMessage(IPlayer fromPlayer, RequestWorkSpacePacket networkMessage)
        {
            SendPlayerWorkSpace(fromPlayer.PlayerUID);
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
            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {

                WorldEditWorkspace workspace = GetOrCreateWorkSpace(player);
                workspace.ToolsEnabled = false;
                workspace.StartMarker = null;
                workspace.EndMarker = null;
                fromPlayer = player;
                ResendBlockHighlights();
            }
        }

        private void OnPlayerJoin(IPlayer player)
        {
            fromPlayer = player as IServerPlayer;

            WorldEditWorkspace workspace = GetOrCreateWorkSpace(player);

            IBlockAccessorRevertable revertableBlockAccess = sapi.WorldManager.GetBlockAccessorRevertable(true, true);

            // Initialize all tools once to build up the workspace for the client gui tool options
            foreach (var val in ToolRegistry.ToolTypes)
            {
                ToolRegistry.InstanceFromType(val.Key, workspace, revertableBlockAccess);
            }
        

            if (workspace.ToolsEnabled)
            {
                if (workspace.ToolInstance != null)
                {
                    EnumHighlightBlocksMode mode = EnumHighlightBlocksMode.CenteredToSelectedBlock;
                    if (workspace.ToolOffsetMode == EnumToolOffsetMode.Attach) mode = EnumHighlightBlocksMode.AttachedToSelectedBlock;

                    sapi.World.HighlightBlocks(player, (int)EnumHighlightSlot.Brush, workspace.ToolInstance.GetBlockHighlights(this), workspace.ToolInstance.GetBlockHighlightColors(this), mode);
                }
                
            }
            else
            {
                HighlightSelectedArea(workspace, player);
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
                        IBlockAccessorRevertable revertableBlockAccess = sapi.WorldManager.GetBlockAccessorRevertable(true, true);
                        WorldEditWorkspace workspace = new WorldEditWorkspace(sapi.World, revertableBlockAccess);
                        workspace.FromBytes(reader);
                        if (workspace.PlayerUID == null)
                        {
                            continue;
                        }

                        workspaces[workspace.PlayerUID] = workspace;
                    }
                }
            } catch (Exception)
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
                IBlockAccessorRevertable revertableBlockAccess = sapi.WorldManager.GetBlockAccessorRevertable(true, true);
                workspaces[playeruid] = new WorldEditWorkspace(sapi.World, revertableBlockAccess);
                workspaces[playeruid].PlayerUID = playeruid;
                return workspaces[playeruid];
            }
        }





        private void CmdEditServer(IServerPlayer player, int groupId, CmdArgs args)
        {
            this.fromPlayer = player;
            this.groupId = groupId;

            if (!CanUseWorldEdit(player, true)) return;

            this.workspace = GetOrCreateWorkSpace(player);


            BlockPos centerPos = player.Entity.Pos.AsBlockPos;
            IPlayerInventoryManager plrInv = player.InventoryManager;
            IItemStack stack = plrInv.ActiveHotbarSlot.Itemstack;


            if (args.Length == 0)
            {
                Bad("No arguments supplied. Check the wiki for help, or did you mean to type .we?");
                return;
            }

            string cmd = args.PopWord();


            if ((cmd == "tr" || cmd == "tsx" || cmd == "tsy" || cmd == "tsz") && args.Length > 0)
            {
                double val = 0;
                if (double.TryParse(args[0], out val))
                {
                    if (val > 50 && serverOverloadProtection)
                    {
                        Bad("Operation rejected. Server overload protection is on. Might kill the server or client to use such a large brush (max is 50).");

                        SendPlayerWorkSpace(fromPlayer.PlayerUID);

                        return;
                    }
                }
            }

            switch (cmd)
            {
                case "impr":
                    int angle = 90;

                    if (args.Length > 0)
                    {
                        if (!int.TryParse(args[0], out angle))
                        {
                            Bad("Invalid Angle (not a number)");
                            break;
                        }
                    }
                    if (angle < 0) angle += 360;

                    if (angle != 0 && angle != 90 && angle != 180 && angle != 270)
                    {
                        Bad("Invalid Angle, allowed values are -270, -180, -90, 0, 90, 180 and 270");
                        break;
                    }

                    workspace.ImportAngle = angle;

                    Good("Ok, set rotation to " + angle + " degrees");

                    break;

                case "impflip":
                    workspace.ImportFlipped = !workspace.ImportFlipped;

                    Good("Ok, import data flip " + (workspace.ImportFlipped ? "on" : "off"));

                    break;


                case "mcopy":
                    if (workspace.StartMarker == null || workspace.EndMarker == null)
                    {
                        Bad("Please mark start and end position");
                        break;
                    }

                    workspace.clipboardBlockData = CopyArea(workspace.StartMarker, workspace.EndMarker);
                    Good(Lang.Get("{0} blocks and {1} entities copied", workspace.clipboardBlockData.BlockIds.Count, workspace.clipboardBlockData.EntitiesUnpacked.Count));
                    break;

                case "mposcopy":
                    if (workspace.StartMarker == null || workspace.EndMarker == null)
                    {
                        Bad("Please mark start and end position");
                        break;
                    }

                    BlockPos s = workspace.StartMarker;
                    BlockPos e = workspace.EndMarker;

                    serverChannel.SendPacket(new CopyToClipboardPacket() { Text = string.Format("/we mark {0} {1} {2} {3} {4} {5}", s.X, s.Y, s.Z, e.X, e.Y, e.Z) }, player);

                    break;

                case "mpaste":
                    if (workspace.clipboardBlockData == null)
                    {
                        Bad("No copied block data to paste");
                        break;
                    }

                    PasteBlockData(workspace.clipboardBlockData, workspace.StartMarker, EnumOrigin.StartPos);
                    Good(workspace.clipboardBlockData.BlockIds.Count + " blocks pasted");
                    break;


                case "cpinfo":
                    if (workspace.clipboardBlockData == null)
                    {
                        Bad("No schematic in the clipboard");
                        break;
                    }

                    workspace.clipboardBlockData.Init(workspace.revertableBlockAccess);
                    workspace.clipboardBlockData.LoadMetaInformationAndValidate(workspace.revertableBlockAccess, workspace.world, "(from clipboard)");

                    string sides = "";
                    for (int i = 0; i < workspace.clipboardBlockData.PathwayStarts.Length; i++)
                    {
                        if (sides.Length > 0) sides += ",";
                        sides += workspace.clipboardBlockData.PathwaySides[i].Code + " (" + workspace.clipboardBlockData.PathwayOffsets[i].Length + " blocks)";
                    }
                    if (sides.Length > 0) sides = "Found " + workspace.clipboardBlockData.PathwayStarts.Length + " pathways: " + sides;

                    Good("{0} blocks in clipboard. {1}", workspace.clipboardBlockData.BlockIds.Count, sides);
                    break;


                case "block":
                    if (stack == null || stack.Class == EnumItemClass.Item)
                    {
                        Bad("Please put the desired block in your active hotbar slot");
                        return;
                    }

                    sapi.World.BlockAccessor.SetBlock(stack.Id, centerPos.DownCopy());
                    
                    Good("Block placed");

                    break;


                case "relight":
                    workspace.DoRelight = (bool)args.PopBool(true);
                    workspace.revertableBlockAccess.Relight = workspace.DoRelight;
                    Good("Block relighting now " + ((workspace.DoRelight) ? "on" : "off"));
                    break;

                case "sovp":

                    if (args.Length > 0)
                    {
                        if (!fromPlayer.HasPrivilege(Privilege.controlserver))
                        {
                            Bad("controlserver privilege required to change server overload protection flag");
                            break;
                        }
                        serverOverloadProtection = (bool)args.PopBool(true);
                    }

                    Good("Server overload protection " + (serverOverloadProtection ? "on" : "off"));
                    break;

                case "undo":
                    HandleHistoryChange(args, false);
                    break;

                case "redo":
                    HandleHistoryChange(args, true);
                    break;


                case "on":
                    Good("World edit tools now enabled");
                    workspace.ToolsEnabled = true;
                    ResendBlockHighlights();
                    break;


                case "off":
                    Good("World edit tools now disabled");
                    workspace.ToolsEnabled = false;
                    ResendBlockHighlights();
                    break;


                case "rebuildrainmap":
                    if (!player.HasPrivilege(Privilege.controlserver))
                    {
                        Bad("You lack the controlserver privilege to rebuild the rain map.");
                        return;
                    }

                    Good("Ok, rebuilding rain map on all loaded chunks, this may take some time and lag the server");
                    int rebuilt = RebuildRainMap();
                    Good("Done, rebuilding {0} map chunks", rebuilt);
                    break;


                case "t":
                    string toolname = null;
                    
                    if (args.Length > 0)
                    {
                        string suppliedToolname = args.PopAll();

                        int toolId;
                        if (int.TryParse(suppliedToolname, out toolId))
                        {
                            if (toolId < 0)
                            {
                                Good("World edit tools now disabled");
                                workspace.ToolsEnabled = false;
                                ResendBlockHighlights();
                                return;
                            }

                            toolname = ToolRegistry.ToolTypes.GetKeyAtIndex(toolId);
                        } else
                        {   
                            foreach (string name in ToolRegistry.ToolTypes.Keys)
                            {
                                if (name.ToLowerInvariant().StartsWith(suppliedToolname.ToLowerInvariant()))
                                {
                                    toolname = name;
                                    break;
                                }
                                
                            }
                        }
                    }

                    if (toolname == null)
                    {
                        Bad("No such tool '"+toolname+"' registered");
                        break;
                    }

                    workspace.SetTool(toolname);

                    Good(toolname + " tool selected");

                    workspace.ToolsEnabled = true;
                    ResendBlockHighlights();
                    SendPlayerWorkSpace(fromPlayer.PlayerUID);
                    break;


                case "tom":
                    EnumToolOffsetMode mode = EnumToolOffsetMode.Center;

                    try
                    {
                        int index = 0;
                        if (args.Length > 0)
                        {
                            int.TryParse(args[0], out index);
                        }
                        mode = (EnumToolOffsetMode)index;
                    } catch (Exception) { }

                    workspace.ToolOffsetMode = mode;

                    Good("Set tool offset mode " + mode);

                    ResendBlockHighlights();
                    break;


                case "range":
                    float pickingrange = GlobalConstants.DefaultPickingRange;

                    if (args.Length > 0)
                    {
                        float range;
                        float.TryParse(args[0], out range);
                        pickingrange = range;
                    }

                    fromPlayer.WorldData.PickingRange = pickingrange;
                    fromPlayer.BroadcastPlayerData();

                    Good("Picking range " + pickingrange + " set");
                    break;


                case "mex":
                case "mexc":

                    if (workspace.StartMarker == null || workspace.EndMarker == null)
                    {
                        Bad("Please mark start and end position");
                        break;
                    }

                    if (args.Length < 1)
                    {
                        Bad("Please provide a filename");
                        break;
                    }

                    ExportArea(args[0], workspace.StartMarker, workspace.EndMarker, cmd == "mexc" ? fromPlayer : null);

                    if (args.Length > 1 && args[1] == "c")
                    {
                        BlockPos st = workspace.StartMarker;
                        BlockPos en = workspace.EndMarker;
                        serverChannel.SendPacket(new CopyToClipboardPacket() { Text = string.Format("/we mark {0} {1} {2} {3} {4} {5}\n/we mex {6}", st.X, st.Y, st.Z, en.X, en.Y, en.Z, args[0]) }, player);
                        
                    }
                    break;


                case "mre":

                    if (workspace.StartMarker == null || workspace.EndMarker == null)
                    {
                        Bad("Please mark start and end position");
                        break;
                    }

                    Good("Relighting marked area, this may lag the server for a while...");

                    sapi.WorldManager.FullRelight(workspace.StartMarker, workspace.EndMarker);

                    Good("Ok, relighting complete");
                    break;


                case "imp":

                    if (workspace.StartMarker == null)
                    {
                        Bad("Please mark a start position");
                        break;
                    }

                    if (args.Length < 1)
                    {
                        Bad("Please provide a filename");
                        break;
                    }

                    EnumOrigin origin = EnumOrigin.StartPos;

                    if (args.Length > 1)
                    {
                        try
                        {
                            origin = (EnumOrigin)Enum.Parse(typeof(EnumOrigin), args[1]);
                        }
                        catch (Exception)
                        {

                        }
                    }

                    ImportArea(args[0], workspace.StartMarker, origin);
                    break;


                case "impres":
                    if (args.Length == 0)
                    {
                        Good("Import item/block resolving currently " + (!sapi.ObjectCache.ContainsKey("donotResolveImports") || (bool)sapi.ObjectCache["donotResolveImports"] == false ? "on" : "off"));
                    } else
                    {
                        bool doreplace = (bool)args.PopBool(ReplaceMetaBlocks);
                        sapi.ObjectCache["donotResolveImports"] = !doreplace;
                        Good("Import item/block resolving now globally " + (doreplace ? "on" : "off"));
                    }

                    

                    break;


                case "blu":
                    BlockLineup(centerPos, args);
                    Good("Block lineup created");
                    break;
                

                // Mark start
                case "ms":
                    SetStartPos(centerPos);
                    break;


                // Mark end
                case "me":
                    SetEndPos(centerPos);
                    break;

                case "mark":
                    workspace.StartMarker = args.PopVec3i(null)?.AsBlockPos;
                    workspace.EndMarker = args.PopVec3i(null)?.AsBlockPos;

                    Good("Start and end position marked");
                    EnsureInsideMap(workspace.EndMarker);
                    HighlightSelectedArea(workspace, fromPlayer);
                    break;

                case "gn":
                    ModifyMarker(BlockFacing.NORTH, args);
                    break;
                case "ge":
                    ModifyMarker(BlockFacing.EAST, args);
                    break;
                case "gs":
                    ModifyMarker(BlockFacing.SOUTH, args);
                    break;
                case "gw":
                    ModifyMarker(BlockFacing.WEST, args);
                    break;
                case "gu":
                    ModifyMarker(BlockFacing.UP, args);
                    break;
                case "gd":
                    ModifyMarker(BlockFacing.DOWN, args);
                    break;


               /* case "mr":
                    HandleRotateCommand(args.PopInt(), args.PopSingle());
                    break;
                    */

                case "mmirn":
                    HandleMirrorCommand(BlockFacing.NORTH, args);
                    break;
                case "mmire":
                    HandleMirrorCommand(BlockFacing.EAST, args);
                    break;
                case "mmirs":
                    HandleMirrorCommand(BlockFacing.SOUTH, args);
                    break;
                case "mmirw":
                    HandleMirrorCommand(BlockFacing.WEST, args);
                    break;
                case "mmiru":
                    HandleMirrorCommand(BlockFacing.UP, args);
                    break;
                case "mmird":
                    HandleMirrorCommand(BlockFacing.DOWN, args);
                    break;

                case "mrepn":
                    HandleRepeatCommand(BlockFacing.NORTH, args);
                    break;
                case "mrepe":
                    HandleRepeatCommand(BlockFacing.EAST, args);
                    break;
                case "mreps":
                    HandleRepeatCommand(BlockFacing.SOUTH, args);
                    break;
                case "mrepw":
                    HandleRepeatCommand(BlockFacing.WEST, args);
                    break;
                case "mrepu":
                    HandleRepeatCommand(BlockFacing.UP, args);
                    break;
                case "mrepd":
                    HandleRepeatCommand(BlockFacing.DOWN, args);
                    break;

                case "mmu":
                    HandleMoveCommand(BlockFacing.UP, args);
                    break;

                case "mmd":
                    HandleMoveCommand(BlockFacing.DOWN, args);
                    break;

                case "mmn":
                    HandleMoveCommand(BlockFacing.NORTH, args);
                    break;

                case "mme":
                    HandleMoveCommand(BlockFacing.EAST, args);
                    break;

                case "mms":
                    HandleMoveCommand(BlockFacing.SOUTH, args);
                    break;

                case "mmw":
                    HandleMoveCommand(BlockFacing.WEST, args);
                    break;

                case "mmby":
                    HandleMoveCommand(null, args);
                    break;



                case "smu":
                    HandleShiftCommand(BlockFacing.UP, args);
                    break;

                case "smd":
                    HandleShiftCommand(BlockFacing.DOWN, args);
                    break;

                case "smn":
                    HandleShiftCommand(BlockFacing.NORTH, args);
                    break;

                case "sme":
                    HandleShiftCommand(BlockFacing.EAST, args);
                    break;

                case "sms":
                    HandleShiftCommand(BlockFacing.SOUTH, args);
                    break;

                case "smw":
                    HandleShiftCommand(BlockFacing.WEST, args);
                    break;

                case "smby":
                    HandleShiftCommand(null, args);
                    break;



                // Marked clear
                case "mc":
                    workspace.StartMarker = null;
                    workspace.EndMarker = null;
                    Good("Marked positions cleared");
                    ResendBlockHighlights();
                    break;

                // Marked info
                case "minfo":
                    int sizeX = Math.Abs(workspace.StartMarker.X - workspace.EndMarker.X);
                    int sizeY = Math.Abs(workspace.StartMarker.Y - workspace.EndMarker.Y);
                    int sizeZ = Math.Abs(workspace.StartMarker.Z - workspace.EndMarker.Z);

                    Good(string.Format("Marked area is a cuboid of size {0}x{1}x{2} or a total of {3:n0} blocks", sizeX, sizeY, sizeZ, ((long)sizeX * sizeY * sizeZ)));

                    break;

                // Fill marked
                case "mfill":
                    if (workspace.StartMarker == null || workspace.EndMarker == null)
                    {
                        Bad("Start marker or end marker not set");
                        return;
                    }

                    

                    if (stack == null || stack.Class == EnumItemClass.Item)
                    {
                        Bad("Please put the desired block in your active hotbar slot");
                        return;
                    }

                    int filled = FillArea(stack.Id, workspace.StartMarker, workspace.EndMarker);

                    Good(filled + " marked blocks placed");

                    break;

                // Clear marked
                case "mclear":
                    {
                        if(workspace.StartMarker == null || workspace.EndMarker == null)
                        {
                            Bad("Start marker or end marker not set");
                            return;
                        }

                        int cleared = FillArea(0, workspace.StartMarker, workspace.EndMarker);
                        Good(cleared + " marked blocks cleared");
                    }
                    break;

                // Clear area
                case "clear":
                    {
                        if (args.Length < 1)
                        {
                            Bad("Missing size param");
                            return;
                        }

                        int size = 0;
                        if (!int.TryParse(args[0], out size))
                        {
                            Bad("Invalide size param");
                            return;
                        }

                        int height = 20;
                        if (args.Length > 1)
                        {
                            int.TryParse(args[1], out height);
                        }


                        int cleared = FillArea(0, centerPos.AddCopy(-size, 0, -size), centerPos.AddCopy(size, height, size));

                        Good(cleared + " Blocks cleared");
                    }

                    break;

                default:
                    args.PushSingle(cmd);
                    if (workspace.ToolInstance == null || !workspace.ToolInstance.OnWorldEditCommand(this, args))
                    {
                        Bad("No such function " + cmd + ". Maybe wrong tool selected?");
                    }
                    
                    break;
                        
            }
        }

        public void SetStartPos(BlockPos pos)
        {
            workspace.StartMarker = pos;
            Good("Start position " + workspace.StartMarker + " marked");
            EnsureInsideMap(workspace.StartMarker);
            HighlightSelectedArea(workspace, fromPlayer);
        }

        public void SetEndPos(BlockPos pos)
        {
            workspace.EndMarker = pos;
            Good("End position " + workspace.EndMarker + " marked");
            EnsureInsideMap(workspace.EndMarker);
            HighlightSelectedArea(workspace, fromPlayer);
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
                            Block block = sapi.World.Blocks[chunk.Blocks[index]];

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
            fromPlayer.WorldData.AreaSelectionMode = on;
            fromPlayer.BroadcastPlayerData();
        }

        public void ResendBlockHighlights()
        {
            WorldEditWorkspace workspace = GetOrCreateWorkSpace(fromPlayer);

            HighlightSelectedArea(workspace, fromPlayer);

            if (workspace.ToolsEnabled)
            {
                EnumHighlightBlocksMode mode = EnumHighlightBlocksMode.CenteredToSelectedBlock;
                if (workspace.ToolOffsetMode == EnumToolOffsetMode.Attach) mode = EnumHighlightBlocksMode.AttachedToSelectedBlock;
                if (workspace.ToolInstance != null)
                {
                    sapi.World.HighlightBlocks(fromPlayer, (int)EnumHighlightSlot.Brush, workspace.ToolInstance.GetBlockHighlights(this), workspace.ToolInstance.GetBlockHighlightColors(this), mode, EnumHighlightShape.Arbitrary);
                }   
            } else
            {
                sapi.World.HighlightBlocks(fromPlayer, (int)EnumHighlightSlot.Brush, new List<BlockPos>(), new List<int>());
            }
        }

        private void HighlightSelectedArea(WorldEditWorkspace workspace, IPlayer player)
        {
            if (workspace.StartMarker != null && workspace.EndMarker != null)
            {
                sapi.World.HighlightBlocks(player, (int)EnumHighlightSlot.Selection, new List<BlockPos>(new BlockPos[] { workspace.StartMarker, workspace.EndMarker }), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cube);
            } else
            {
                sapi.World.HighlightBlocks(player, (int)EnumHighlightSlot.Selection, new List<BlockPos>());
            }
        }

        private void HandleHistoryChange(CmdArgs args, bool redo)
        {
            if (redo && workspace.revertableBlockAccess.CurrentyHistoryState == 0)
            {
                Bad("Can't redo. Already on newest history state.");
                return;
            }
            if (!redo && workspace.revertableBlockAccess.CurrentyHistoryState == workspace.revertableBlockAccess.AvailableHistoryStates)
            {
                Bad("Can't undo. Already on oldest available history state.");
                return;
            }

            int prevHstate = workspace.revertableBlockAccess.CurrentyHistoryState;

            int steps = 1;
            if (args.Length > 0)
            {
                int.TryParse(args[0], out steps);
            }

            workspace.revertableBlockAccess.ChangeHistoryState(steps * (redo ? -1 : 1));

            int quantityChanged = Math.Abs(prevHstate - workspace.revertableBlockAccess.CurrentyHistoryState);

            Good(string.Format("Performed {0} {1} times.", redo ? "redo" : "undo", quantityChanged));
        }
        

        private void BlockLineup(BlockPos pos, CmdArgs args)
        {
            List<Block> blocks = sapi.World.Blocks;

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

            FillArea(0, pos.AddCopy(0, 0, 0), pos.AddCopy(width + 1, 10, width + 1));

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

        private void OnDidBreakBlock(IServerPlayer byBplayer, int oldblockId, BlockSelection blockSel)
        {
            this.fromPlayer = byBplayer;
            if (!CanUseWorldEdit(byBplayer)) return;

            workspace = GetOrCreateWorkSpace(byBplayer);
            if (!workspace.ToolsEnabled) return;
            if (workspace.ToolInstance == null) return;

            workspace.ToolInstance.OnBreak(this, oldblockId, blockSel);
        }

        private BlockSchematic CopyArea(BlockPos start, BlockPos end)
        {
            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            BlockPos finalPos = new BlockPos(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));

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

            blockData.PlaceEntitiesAndBlockEntities(workspace.revertableBlockAccess, sapi.World, originPos);
        }

        private void ImportArea(string filename, BlockPos startPos, EnumOrigin origin)
        {
            string infilepath = Path.Combine(exportFolderPath, filename);
            BlockSchematic blockData;

            string error = "";

            blockData = BlockSchematic.LoadFromFile(infilepath, ref error);
            if (blockData == null) { 
                Bad(error);
                return;
            }
            

            PasteBlockData(blockData, startPos, origin);
        }


        private void ExportArea(string filename, BlockPos start, BlockPos end, IServerPlayer sendToPlayer = null)
        {
            BlockSchematic blockdata = CopyArea(start, end);
            int exported = blockdata.BlockIds.Count;

            string outfilepath = Path.Combine(exportFolderPath, filename);

            if (sendToPlayer != null)
            {
                serverChannel.SendPacket<SchematicJsonPacket>(new SchematicJsonPacket() { Filename = filename, JsonCode = blockdata.ToJson() }, sendToPlayer);
                Good(exported + " blocks schematic sent to client.");
            } else
            {
                string error = blockdata.Save(outfilepath);
                if (error != null)
                {
                    Good("Failed exporting: " + error);
                }
                else
                {
                    Good(exported + " blocks exported.");
                }
            }

            
        }



        public bool MayPlace(Block block, int quantityBlocks)
        {
            if (serverOverloadProtection)
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

                if (quantityBlocks > 1000 && block.BlockMaterial == EnumBlockMaterial.Plant)
                {
                    Bad("Operation rejected. Server overload protection is on. Might kill the server when placing that many plants (might cause massive neighbour block updates if one plant is broken).");
                    return false;
                }
            }

            return true;
        }

        public void Good(string message, params object[] args)
        {
            fromPlayer.SendMessage(groupId, string.Format(message, args), EnumChatType.CommandSuccess);
        }

        public void Bad(string message, params object[] args)
        {
            fromPlayer.SendMessage(groupId, string.Format(message, args), EnumChatType.CommandError);
        }

    }
}
