using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods.WorldEdit
{
    public enum EnumWorldEditConstraint
    {
        None = 0,
        Selection = 1
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public partial class WorldEditWorkspace
    {
        public bool ToolsEnabled;

        public string PlayerUID;
        public EnumWorldEditConstraint WorldEditConstraint;

        BlockPos prevStartMarker;
        BlockPos prevEndMarker;

        private BlockPos startMarker;
        private BlockPos endMarker;

        public BlockPos StartMarker {
            get => startMarker;
            set
            {
                if (world != null) world.Api.ObjectCache["weStartMarker-" + PlayerUID] = value;
                startMarker = value;
            }
        }
        public BlockPos EndMarker
        {
            get => endMarker;
            set
            {
                if (world != null) world.Api.ObjectCache["weEndMarker-" + PlayerUID] = value;
                endMarker = value;
            }
        }

        public Vec3d StartMarkerExact;
        public Vec3d EndMarkerExact;

        private Vec3d _prevStartMarkerExact;
        private Vec3d _prevEndMarkerExact;

        [ProtoIgnore]
        public BlockSchematic PreviewBlockData { get; set; }

        [ProtoIgnore]
        public BlockPos PreviewPos { get; set; }

        public bool Rsp
        {
            get => IntValues["std.settingsRsp"] > 0;
            set => IntValues["std.settingsRsp"] = value ? 1 : 0;
        }

        public int ToolAxisLock
        {
            get => IntValues["std.settingsAxisLock"];
            set => IntValues["std.settingsAxisLock"] = value;
        }

        public int StepSize
        {
            get => IntValues["std.stepSize"];
            set => IntValues["std.stepSize"] = value;
        }

        public int DimensionId
        {
            get => IntValues["std.dimensionId"];
            set => IntValues["std.dimensionId"] = value;
        }

        public int ImportAngle;
        public bool ImportFlipped;
        /// <summary>
        /// If false, removing/placing blocks will not update their light values
        /// </summary>
        public bool DoRelight = true;


        internal IBlockAccessorRevertable revertableBlockAccess;
        internal IWorldAccessor world;

        public bool serverOverloadProtection = true;
        public EnumToolOffsetMode ToolOffsetMode;

        public Dictionary<string, float> FloatValues = new Dictionary<string, float>();
        public Dictionary<string, int> IntValues = new Dictionary<string, int>();
        public Dictionary<string, string> StringValues = new Dictionary<string, string>();
        public Dictionary<string, byte[]> ByteDataValues = new Dictionary<string, byte[]>();

        public string ToolName = null;
        internal ToolBase ToolInstance;
        internal BlockSchematic clipboardBlockData;
        internal IMiniDimension previewBlocks;
        private ICoreServerAPI sapi;

        public WorldEditWorkspace()
        {

        }

        public WorldEditWorkspace(IWorldAccessor world, IBlockAccessorRevertable blockAccessor)
        {
            revertableBlockAccess = blockAccessor;
            this.world = world;

            blockAccessor.OnStoreHistoryState += BlockAccessor_OnStoreHistoryState;
            blockAccessor.OnRestoreHistoryState += BlockAccessor_OnRestoreHistoryState;

        }

        public void Init(ICoreServerAPI api)
        {
            sapi = api;
            if (!IntValues.ContainsKey("std.stepSize")) StepSize = 1;
            if (!IntValues.ContainsKey("std.settingsAxisLock")) ToolAxisLock = 0;
            if (!IntValues.ContainsKey("std.settingsRsp")) Rsp = true;
            if (!IntValues.ContainsKey("std.dimensionId")) DimensionId = -1;

            previewBlocks = world.BlockAccessor.CreateMiniDimension(new Vec3d());
            if (DimensionId == -1)
            {
                DimensionId = sapi.Server.LoadMiniDimension(previewBlocks); // create unique minidimension per player
            }
            else
            {
                sapi.Server.SetMiniDimension(previewBlocks, DimensionId);
            }
            previewBlocks.SetSubDimensionId(DimensionId);
            previewBlocks.BlocksPreviewSubDimension_Server = DimensionId;
        }

        private void BlockAccessor_OnRestoreHistoryState(HistoryState obj, int dir)
        {
            if (dir > 0)
            {
                StartMarker = obj.NewStartMarker?.Copy();
                EndMarker = obj.NewEndMarker?.Copy();

                StartMarkerExact = obj.NewStartMarkerExact?.Clone();
                EndMarkerExact = obj.NewEndMarkerExact?.Clone();
            } else
            {
                StartMarker = obj.OldStartMarker?.Copy();
                EndMarker = obj.OldEndMarker?.Copy();

                StartMarkerExact = obj.OldStartMarkerExact?.Clone();
                EndMarkerExact = obj.OldEndMarkerExact?.Clone();
            }

            HighlightSelectedArea();
        }

        private void BlockAccessor_OnStoreHistoryState(HistoryState obj)
        {
            obj.OldStartMarker = prevStartMarker?.Copy();
            obj.OldEndMarker = prevEndMarker?.Copy();

            obj.NewStartMarker = StartMarker?.Copy();
            obj.NewEndMarker = EndMarker?.Copy();

            prevStartMarker = StartMarker?.Copy();
            prevEndMarker = EndMarker?.Copy();

            obj.OldStartMarkerExact = _prevStartMarkerExact?.Clone();
            obj.OldEndMarkerExact = _prevEndMarkerExact?.Clone();

            obj.NewStartMarkerExact = StartMarkerExact?.Clone();
            obj.NewEndMarkerExact = EndMarkerExact?.Clone();

            _prevStartMarkerExact = StartMarkerExact?.Clone();
            _prevEndMarkerExact = EndMarkerExact?.Clone();
        }

        public void SetTool(string toolname, ICoreAPI api)
        {
            ToolName = toolname;
            if (ToolInstance != null) ToolInstance.Unload(api);
            if (toolname != null)
            {
                ToolInstance = ToolRegistry.InstanceFromType(toolname, this, revertableBlockAccess);
                if (ToolInstance == null)
                {
                    ToolName = null;
                }
                else
                {
                    ToolInstance.Load(api);
                }
            }
        }


        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(ToolsEnabled);
            writer.Write(PlayerUID);

            writer.Write(StartMarker == null);
            if (StartMarker != null)
            {
                writer.Write(StartMarker.X);
                writer.Write(StartMarker.InternalY);
                writer.Write(StartMarker.Z);
            }

            writer.Write(EndMarker == null);
            if (EndMarker != null)
            {
                writer.Write(EndMarker.X);
                writer.Write(EndMarker.InternalY);
                writer.Write(EndMarker.Z);
            }

            writer.Write(FloatValues.Count);
            foreach (var val in FloatValues)
            {
                writer.Write(val.Key);
                writer.Write(val.Value);
            }

            writer.Write(IntValues.Count);
            foreach (var val in IntValues)
            {
                writer.Write(val.Key);
                writer.Write(val.Value);
            }

            writer.Write(StringValues.Count);
            foreach (var val in StringValues)
            {
                writer.Write(val.Value == null);

                if (val.Value == null) continue;

                writer.Write(val.Key);
                writer.Write(val.Value);
            }

            writer.Write(ByteDataValues.Count);
            foreach (var val in ByteDataValues)
            {
                writer.Write(val.Value == null);

                if (val.Value == null) continue;

                writer.Write(val.Key);
                writer.Write(val.Value.Length);
                writer.Write(val.Value);
            }

            writer.Write(ToolName == null);

            if (ToolName != null) {
                writer.Write(ToolName);
            }

            writer.Write((int)ToolOffsetMode);
            writer.Write(DoRelight);

            writer.Write(serverOverloadProtection);
        }

        public void FromBytes(BinaryReader reader)
        {
            try
            {
                ToolsEnabled = reader.ReadBoolean();
                PlayerUID = reader.ReadString();

                if (!reader.ReadBoolean())
                {
                    StartMarker = new BlockPos(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    StartMarkerExact = StartMarker.ToVec3d().Add(0.5);
                }
                else
                {
                    StartMarker = null;
                }

                if (!reader.ReadBoolean())
                {
                    EndMarker = new BlockPos(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    EndMarkerExact = EndMarker.ToVec3d().Add(-0.5);
                }
                else
                {
                    EndMarker = null;

                }

                FloatValues = new Dictionary<string, float>();
                IntValues = new Dictionary<string, int>();
                StringValues = new Dictionary<string, string>();
                ByteDataValues = new Dictionary<string, byte[]>();

                int floatValCount = reader.ReadInt32();
                for (int i = 0; i < floatValCount; i++)
                {
                    FloatValues[reader.ReadString()] = reader.ReadSingle();
                }

                int intValCount = reader.ReadInt32();
                for (int i = 0; i < intValCount; i++)
                {
                    IntValues[reader.ReadString()] = reader.ReadInt32();
                }

                int stringValCount = reader.ReadInt32();
                for (int i = 0; i < stringValCount; i++)
                {
                    if (!reader.ReadBoolean())
                    {
                        StringValues[reader.ReadString()] = reader.ReadString();
                    }

                }

                int byteDataValCount = reader.ReadInt32();
                for (int i = 0; i < byteDataValCount; i++)
                {
                    if (!reader.ReadBoolean())
                    {
                        string key = reader.ReadString();
                        int qbytes = reader.ReadInt32();
                        ByteDataValues[key] = reader.ReadBytes(qbytes);
                    }
                }

                if (!reader.ReadBoolean())
                {
                    ToolName = reader.ReadString();
                    SetTool(ToolName, world.Api);
                }

                ToolOffsetMode = (EnumToolOffsetMode)reader.ReadInt32();
                DoRelight = reader.ReadBoolean();

                revertableBlockAccess.Relight = DoRelight;

                serverOverloadProtection = reader.ReadBoolean();
            }
            catch (Exception) { }
        }

        public BlockPos GetMarkedMinPos()
        {
            return new BlockPos(
                Math.Min(StartMarker.X, EndMarker.X),
                Math.Min(StartMarker.InternalY, EndMarker.InternalY),
                Math.Min(StartMarker.Z, EndMarker.Z)
            );
        }

        public BlockPos GetMarkedMaxPos()
        {
            return new BlockPos(
                Math.Max(StartMarker.X, EndMarker.X),
                Math.Max(StartMarker.InternalY, EndMarker.InternalY),
                Math.Max(StartMarker.Z, EndMarker.Z)
            );
        }

        public void ResendBlockHighlights()
        {
            var player = world.PlayerByUid(PlayerUID);

            var blockPosList = new List<BlockPos>();
            if (ToolsEnabled && ToolInstance != null)
            {
                var mode = EnumHighlightBlocksMode.CenteredToSelectedBlock;
                if (ToolInstance.GetType().Name.Equals("MicroblockTool"))
                {
                    if (ToolOffsetMode == EnumToolOffsetMode.Attach) mode = EnumHighlightBlocksMode.AttachedToBlockSelectionIndex;
                }
                else
                {
                    if (ToolOffsetMode == EnumToolOffsetMode.Attach) mode = EnumHighlightBlocksMode.AttachedToSelectedBlock;
                }

                switch (ToolInstance)
                {
                    case ImportTool when PreviewBlockData != null && PreviewPos != null:
                        CreatePreview(PreviewBlockData, PreviewPos);
                        HighlightSelectedArea();
                        return;
                    case MoveTool:
                        if (PreviewBlockData != null)
                        {
                            CreatePreview(PreviewBlockData, PreviewPos);
                        }
                        HighlightSelectedArea();
                        return;
                    case SelectTool:
                    case RepeatTool:
                        // do not run default
                        break;
                    default:
                        DestroyPreview();
                        break;
                }
                HighlightSelectedArea();
                ToolInstance.HighlightBlocks(player, sapi, mode);
            }
            else
            {
                DestroyPreview();
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Brush, blockPosList, null);
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Selection, blockPosList, null);
                world.HighlightBlocks(player, (int)EnumHighlightSlot.SelectionStart, blockPosList);
                world.HighlightBlocks(player, (int)EnumHighlightSlot.SelectionEnd, blockPosList);
            }
        }

        public void HighlightSelectedArea()
        {
            var player = world.PlayerByUid(PlayerUID);

            var blockPosList = new List<BlockPos>();
            if (StartMarker != null && EndMarker != null)
            {
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Brush, blockPosList);
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Selection, new List<BlockPos>(new BlockPos[] { StartMarker, EndMarker }), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cube);
                world.HighlightBlocks(player, (int)EnumHighlightSlot.SelectionStart, new List<BlockPos>(new[] { StartMarkerExact.AsBlockPos ?? StartMarker }), new List<int>(){ColorUtil.ColorFromRgba(0,255,0,60)});
                world.HighlightBlocks(player, (int)EnumHighlightSlot.SelectionEnd, new List<BlockPos>(new[] { EndMarkerExact.AsBlockPos ?? EndMarker }), new List<int>(){ColorUtil.ColorFromRgba(255,0,0,60)});
            }
            else
            {
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Brush, blockPosList);
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Selection, blockPosList);
                world.HighlightBlocks(player, (int)EnumHighlightSlot.SelectionStart, blockPosList);
                world.HighlightBlocks(player, (int)EnumHighlightSlot.SelectionEnd, blockPosList);
            }
        }

        public void UpdateSelection()
        {
            if (StartMarkerExact != null && EndMarkerExact != null)
            {
                StartMarker = new BlockPos(
                    (int)Math.Min(StartMarkerExact.X, EndMarkerExact.X),
                    (int)Math.Min(StartMarkerExact.Y, EndMarkerExact.Y),
                    (int)Math.Min(StartMarkerExact.Z, EndMarkerExact.Z)
                );

                EndMarker = new BlockPos(
                    (int)Math.Ceiling(Math.Max(StartMarkerExact.X, EndMarkerExact.X)),
                    (int)Math.Ceiling(Math.Max(StartMarkerExact.Y, EndMarkerExact.Y)),
                    (int)Math.Ceiling(Math.Max(StartMarkerExact.Z, EndMarkerExact.Z))
                );

            }
            if(StartMarker == null || EndMarker == null) return;
            EnsureInsideMap(StartMarker);
            EnsureInsideMap(EndMarker);

            HighlightSelectedArea();
            revertableBlockAccess.StoreHistoryState(HistoryState.Empty());
        }

        private void EnsureInsideMap(BlockPos pos)
        {
            pos.X = GameMath.Clamp(pos.X, 0, world.BlockAccessor.MapSizeX - 1);
            pos.Y = GameMath.Clamp(pos.Y, 0, world.BlockAccessor.MapSizeY - 1);
            pos.Z = GameMath.Clamp(pos.Z, 0, world.BlockAccessor.MapSizeZ - 1);
        }

        public void GrowSelection(BlockFacing facing, int amount)
        {
            if (facing == BlockFacing.WEST)
            {
                StartMarkerExact.X -= amount;
            }
            if (facing == BlockFacing.EAST)
            {
                EndMarkerExact.X += amount;
            }

            if (facing == BlockFacing.NORTH)
            {
                StartMarkerExact.Z -= amount;
            }
            if (facing == BlockFacing.SOUTH)
            {
                EndMarkerExact.Z += amount;
            }

            if (facing == BlockFacing.DOWN)
            {
                StartMarkerExact.Y -= amount;
            }
            if (facing == BlockFacing.UP)
            {
                EndMarkerExact.Y += amount;
            }

            UpdateSelection();
        }

        public BlockFacing GetFacing(EntityPos pos)
        {
            BlockFacing facing;

            switch (ToolAxisLock)
            {
                case 0:
                {
                    var lookVec = pos.GetViewVector();
                    facing = BlockFacing.FromVector(lookVec.X, lookVec.Y, lookVec.Z);
                    break;
                }
                case 1:
                {
                    facing = BlockFacing.EAST;
                    break;
                }
                case 2:
                {
                    facing = BlockFacing.UP;
                    break;
                }
                case 3:
                {
                    facing = BlockFacing.NORTH;
                    break;
                }
                case 4:
                {
                    facing = BlockFacing.HorizontalFromYaw(pos.Yaw);
                    break;
                }
                default:
                {
                    facing = BlockFacing.NORTH;
                    break;
                }
            }

            return facing;
        }

        internal int FillArea(ItemStack blockStack, BlockPos start, BlockPos end, bool notLiquids = false)
        {
            var player = (IServerPlayer)world.PlayerByUid(PlayerUID);
            int updated = 0;

            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z), start.dimension);
            BlockPos finalPos = new BlockPos(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z), start.dimension);
            BlockPos curPos = startPos.Copy();

            int dx = finalPos.X - startPos.X;
            int dy = finalPos.Y - startPos.Y;
            int dz = finalPos.Z - startPos.Z;

            int quantityBlocks = dx * dy * dz;
            int blockId = 0;
            Block block = blockStack?.Block;

            if (block != null) blockId = block.Id;
            if (block != null && !MayPlace(block, quantityBlocks)) return 0;

            if (quantityBlocks > 1000)
            {
                WorldEdit.Good(player, (block == null ? "Clearing" : "Placing") + " " + (dx * dy * dz) + " blocks...");
            }

            while (curPos.X < finalPos.X)
            {
                curPos.Y = startPos.Y;

                while (curPos.Y < finalPos.Y)
                {
                    curPos.Z = startPos.Z;
                    while (curPos.Z < finalPos.Z)
                    {
                        if (!notLiquids) revertableBlockAccess.SetBlock(0, curPos, BlockLayersAccess.Fluid);
                        revertableBlockAccess.SetBlock(blockId, curPos, blockStack);
                        curPos.Z++;
                        updated++;
                    }

                    curPos.Y++;
                }

                curPos.X++;
            }

            var updatedBlocks = revertableBlockAccess.Commit();
            // on delete make sure waterfalls are fixed
            if (block == null)
            {
                revertableBlockAccess.PostCommitCleanup(updatedBlocks);
            }

            return updated;
        }

        public bool MayPlace(Block block, int quantityBlocks)
        {
            var player = (IServerPlayer)world.PlayerByUid(PlayerUID);
            if (serverOverloadProtection)
            {
                if (quantityBlocks > 100 && block.LightHsv[2] > 5)
                {
                    WorldEdit.Bad(player,
                        "Operation rejected. Server overload protection is on. Might kill the server to place that many light sources.");
                    return false;
                }

                if (quantityBlocks > 200 * 200 * 200)
                {
                    WorldEdit.Bad(player,
                        "Operation rejected. Server overload protection is on. Might kill the server to (potentially) place that many blocks.");
                    return false;
                }

                ItemStack stack = new ItemStack(block);
                if (quantityBlocks > 1000 && block.GetBlockMaterial(world.BlockAccessor, null, stack) == EnumBlockMaterial.Plant)
                {
                    WorldEdit.Bad(player,
                        "Operation rejected. Server overload protection is on. Might kill the server when placing that many plants (might cause massive neighbour block updates if one plant is broken).");
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Moves selected area. Also offsets start and end arg by the supplied offset.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public int MoveArea(Vec3i offset, BlockPos start, BlockPos end)
        {
            int updated = 0;

            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z), start.dimension);
            BlockPos endPos = new BlockPos(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z), start.dimension);
            BlockPos curPos = startPos.Copy();

            int quantityBlocks = offset.X * offset.Y * offset.Z;
            Block block = world.Blocks[0];
            if (!MayPlace(block, quantityBlocks)) return 0;

            Dictionary<BlockPos, ((int, int), Block[])> blocksByNewPos = new Dictionary<BlockPos, ((int, int), Block[])>();
            Dictionary<BlockPos, TreeAttribute> blockEntityDataByNewPos = new Dictionary<BlockPos, TreeAttribute>();

            revertableBlockAccess.BeginMultiEdit();

            // 1. Read area into dictionaries and delete area fully
            while (curPos.X < endPos.X)
            {
                curPos.Y = startPos.Y;
                while (curPos.Y < endPos.Y)
                {
                    curPos.Z = startPos.Z;
                    while (curPos.Z < endPos.Z)
                    {
                        var newPos = curPos.AddCopy(offset);

                        BlockEntity be = revertableBlockAccess.GetBlockEntity(curPos);
                        if (be != null)
                        {
                            TreeAttribute tree = new TreeAttribute();
                            be.ToTreeAttributes(tree);
                            blockEntityDataByNewPos[newPos] = tree;
                        }

                        var decors = revertableBlockAccess.GetDecors(curPos);
                        var solidBlockId = revertableBlockAccess.GetBlock(curPos).Id;
                        var fluidBlockId = revertableBlockAccess.GetBlock(curPos, BlockLayersAccess.Fluid)?.Id ?? -1;
                        blocksByNewPos[newPos] = ((solidBlockId, fluidBlockId), decors);

                        revertableBlockAccess.SetBlock(0, curPos);
                        if (fluidBlockId > 0)
                            revertableBlockAccess.SetBlock(0, curPos, BlockLayersAccess.Fluid);

                        curPos.Z++;
                    }

                    curPos.Y++;
                }

                curPos.X++;
            }

            var startOriginal = start.Copy();
            var endOriginal = end.Copy();

            start.Add(offset);
            end.Add(offset);

            revertableBlockAccess.Commit();

            // 2. Place area at new position
            foreach (var (pos, ((solidBlockId, fluidBlockId), decors)) in blocksByNewPos)
            {
                revertableBlockAccess.SetBlock(solidBlockId, pos);
                if (fluidBlockId >= 0)
                    revertableBlockAccess.SetBlock(fluidBlockId, pos, BlockLayersAccess.Fluid);
                if (decors == null) continue;
                for (var i = 0; i < decors.Length; i++)
                {
                    if (decors[i] == null) continue;
                    revertableBlockAccess.SetDecor(decors[i], pos, i);
                }
            }

            var updates = revertableBlockAccess.Commit();

            // 3. Store block entity data in commit history
            foreach (var update in updates)
            {
                if (update.OldBlockId == 0)
                {
                    if (blockEntityDataByNewPos.TryGetValue(update.Pos, out TreeAttribute betree))
                    {
                        betree.SetInt("posx", update.Pos.X);
                        betree.SetInt("posy", update.Pos.InternalY);
                        betree.SetInt("posz", update.Pos.Z);

                        update.NewBlockEntityData = betree.ToBytes();
                    }
                }
            }

            // 4. Restore block entity data
            foreach (var val in blockEntityDataByNewPos)
            {
                var pos = val.Key;
                BlockEntity be = revertableBlockAccess.GetBlockEntity(pos);
                if (be != null)
                {
                    val.Value.SetInt("posx", pos.X);
                    val.Value.SetInt("posy", pos.InternalY);
                    val.Value.SetInt("posz", pos.Z);

                    be.FromTreeAttributes(val.Value, world);
                }
            }

            revertableBlockAccess.EndMultiEdit();
            revertableBlockAccess.StoreEntityMoveToHistory(startOriginal, endOriginal, offset);

            return updated;
        }

        public BlockSchematic CopyArea(BlockPos start, BlockPos end, bool notLiquids = false)
        {
            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z), start.dimension);
            BlockSchematic blockdata = new BlockSchematic();
            blockdata.OmitLiquids = notLiquids;
            blockdata.AddArea(sapi.World, start, end);
            blockdata.Pack(sapi.World, startPos);
            return blockdata;
        }

        public void PasteBlockData(BlockSchematic blockData, BlockPos startPos, EnumOrigin origin, IBlockAccessor blockAccessor = null)
        {
            BlockPos originPos = blockData.GetStartPos(startPos, origin);

            EnumAxis? axis = null;
            if (ImportFlipped) axis = EnumAxis.Y;

            if (blockAccessor == null)
            {
                blockAccessor = revertableBlockAccess;
            }

            var rotated = blockData.ClonePacked();
            rotated.TransformWhilePacked(sapi.World, origin, ImportAngle, axis);

            rotated.Init(blockAccessor);
            var enumReplaceMode = EnumReplaceMode.ReplaceAll;

            if (ToolInstance is ImportTool importTool)
            {
               enumReplaceMode = importTool.ReplaceMode;
            }

            rotated.Place(blockAccessor, sapi.World, originPos, enumReplaceMode, WorldEdit.ReplaceMetaBlocks);
            rotated.PlaceDecors(blockAccessor, originPos);

            if (blockAccessor is IBlockAccessorRevertable revertable)
            {
                revertable.Commit();

                blockData.PlaceEntitiesAndBlockEntities(revertable, sapi.World, originPos, blockData.BlockCodes, blockData.ItemCodes,false, null, 0 , null, WorldEdit.ReplaceMetaBlocks);
                revertable.CommitBlockEntityData();
            }
        }

        public TextCommandResult ImportArea(string filename, BlockPos startPos, EnumOrigin origin, bool isLarge)
        {
            string infilepath = Path.Combine(WorldEdit.ExportFolderPath, filename);
            BlockSchematic blockData;

            string error = "";

            blockData = BlockSchematic.LoadFromFile(infilepath, ref error);
            if (blockData == null)
            {
                return TextCommandResult.Error(error);
            }

            if (blockData.SizeX >= 1024 || blockData.SizeY >= 1024 || blockData.SizeZ >= 1024)
            {
                return TextCommandResult.Error("Can not load schematics larger than 1024x1024x1024");
            }

            var worldBlockAccessor = isLarge ? sapi.World.BlockAccessor : null;
            PasteBlockData(blockData, startPos, origin, worldBlockAccessor);
            return TextCommandResult.Success(filename + " imported.");
        }

        public TextCommandResult ExportArea(string filename, BlockPos start, BlockPos end, IServerPlayer sendToPlayer = null)
        {
            var blockdata = CopyArea(start, end);
            var exported = blockdata.BlockIds.Count;
            var exportedEntities = blockdata.Entities.Count;

            var outfilepath = Path.Combine(WorldEdit.ExportFolderPath, filename);

            if (sendToPlayer != null)
            {
                var serverChannel = sapi.Network.GetChannel("worldedit");
                serverChannel.SendPacket(new SchematicJsonPacket()
                {
                    Filename = filename,
                    JsonCode = blockdata.ToJson()
                }, sendToPlayer);
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
                    return TextCommandResult.Success(exported + " blocks and " + exportedEntities + " Entities exported");
                }
            }
        }

        public virtual void CreatePreview(BlockSchematic schematic, BlockPos origin)
        {

            var originPrev = ToolInstance is ImportTool its ? its.Origin : EnumOrigin.StartPos;
            var dim = CreateDimensionFromSchematic(schematic, origin, originPrev);
            previewBlocks.UnloadUnusedServerChunks();
            SendPreviewOriginToClient(origin, dim.subDimensionId);
        }

        /// <summary>
        /// Creates a mini-dimension (a BlockAccessorMovable) and places the schematic in it, this can then be moved and rendered
        /// </summary>
        public IMiniDimension CreateDimensionFromSchematic(BlockSchematic blockData, BlockPos startPos, EnumOrigin origin)
        {
            BlockPos originPos = startPos.Copy();

            if (previewBlocks == null)
            {
                previewBlocks = revertableBlockAccess.CreateMiniDimension(new Vec3d(originPos.X, originPos.Y, originPos.Z));
                sapi.Server.SetMiniDimension(previewBlocks, DimensionId);
            }
            else
            {
                previewBlocks.ClearChunks();
                previewBlocks.CurrentPos.SetPos(originPos);
            }
            previewBlocks.SetSubDimensionId(DimensionId);
            previewBlocks.BlocksPreviewSubDimension_Server = DimensionId;

            originPos.Sub(startPos);
            originPos.SetDimension(Dimensions.MiniDimensions);
            previewBlocks.AdjustPosForSubDimension(originPos);

            EnumAxis? axis = ImportFlipped ? EnumAxis.Y : null;
            BlockSchematic rotated = blockData.ClonePacked();
            rotated.TransformWhilePacked(world, origin, ImportAngle, axis);

            rotated.PasteToMiniDimension(sapi, revertableBlockAccess, previewBlocks, originPos, WorldEdit.ReplaceMetaBlocks);

            return previewBlocks;
        }

        public void DestroyPreview()
        {
            previewBlocks.ClearChunks();
            previewBlocks.UnloadUnusedServerChunks();
            SendPreviewOriginToClient(previewBlocks.selectionTrackingOriginalPos, -1);
        }

        public void SendPreviewOriginToClient(BlockPos origin, int dim, bool trackSelection = false)
        {
            var serverChannel = sapi.Network.GetChannel("worldedit");
            var player = (IServerPlayer)world.PlayerByUid(PlayerUID);
            serverChannel.SendPacket(new PreviewBlocksPacket
            {
                pos = origin,
                dimId = dim,
                TrackSelection = trackSelection
            }, player);
        }

        public TextCommandResult SetStartPos(Vec3d pos, bool update = true)
        {
            StartMarkerExact = pos.Clone();
            if (update)
            {
                UpdateSelection();
            }
            else
            {
                StartMarker = pos.AsBlockPos;
            }

            return TextCommandResult.Success("Start position " + StartMarker + " marked");
        }

        public TextCommandResult SetEndPos(Vec3d pos)
        {
            EndMarkerExact = pos.Clone();
            UpdateSelection();
            return TextCommandResult.Success("End position " + EndMarker + " marked");
        }
    }
}
