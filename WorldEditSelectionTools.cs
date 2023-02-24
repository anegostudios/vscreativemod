using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public partial class WorldEdit
    {
        TextCommandResult ModifyMarker(EnumAxis axis, int amount)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            BlockPos[] minMarkers = new BlockPos[] {
                (workspace.StartMarker.X < workspace.EndMarker.X) ? workspace.StartMarker : workspace.EndMarker,
                (workspace.StartMarker.Y < workspace.EndMarker.Y) ? workspace.StartMarker : workspace.EndMarker,
                (workspace.StartMarker.Z < workspace.EndMarker.Z) ? workspace.StartMarker : workspace.EndMarker
            };
            BlockPos[] maxMarkers = new BlockPos[] {
                (workspace.StartMarker.X >= workspace.EndMarker.X) ? workspace.StartMarker : workspace.EndMarker,
                (workspace.StartMarker.Y >= workspace.EndMarker.Y) ? workspace.StartMarker : workspace.EndMarker,
                (workspace.StartMarker.Z >= workspace.EndMarker.Z) ? workspace.StartMarker : workspace.EndMarker
            };

            if (axis == EnumAxis.X)
            {
                maxMarkers[0].X += amount;
            }
            if (axis == EnumAxis.Y)
            {
                maxMarkers[0].Y += amount;
            }
            if (axis == EnumAxis.Z)
            {
                maxMarkers[2].Z += amount;
            }

            EnsureInsideMap(workspace.StartMarker);
            EnsureInsideMap(workspace.EndMarker);

            workspace.HighlightSelectedArea();

            // Stores the markers as a history state
            workspace.revertableBlockAccess.StoreHistoryState(HistoryState.Empty);

            return TextCommandResult.Success(string.Format("Area grown by {0} blocks towards {1}", amount, axis));
        }

        TextCommandResult ModifyMarker(BlockFacing facing, int amount)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            BlockPos[] minMarkers = new BlockPos[] {
                (workspace.StartMarker.X < workspace.EndMarker.X) ? workspace.StartMarker : workspace.EndMarker,
                (workspace.StartMarker.Y < workspace.EndMarker.Y) ? workspace.StartMarker : workspace.EndMarker,
                (workspace.StartMarker.Z < workspace.EndMarker.Z) ? workspace.StartMarker : workspace.EndMarker
            };
            BlockPos[] maxMarkers = new BlockPos[] {
                (workspace.StartMarker.X >= workspace.EndMarker.X) ? workspace.StartMarker : workspace.EndMarker,
                (workspace.StartMarker.Y >= workspace.EndMarker.Y) ? workspace.StartMarker : workspace.EndMarker,
                (workspace.StartMarker.Z >= workspace.EndMarker.Z) ? workspace.StartMarker : workspace.EndMarker
            };

            if (facing == BlockFacing.WEST)
            {
                minMarkers[0].X -= amount;
            }
            if (facing == BlockFacing.EAST)
            {
                maxMarkers[0].X += amount;
            }
            if (facing == BlockFacing.NORTH)
            {
                minMarkers[2].Z -= amount;
            }
            if (facing == BlockFacing.SOUTH)
            {
                maxMarkers[2].Z += amount;
            }
            if (facing == BlockFacing.DOWN)
            {
                minMarkers[1].Y -= amount;
            }
            if (facing == BlockFacing.UP)
            {
                maxMarkers[1].Y += amount;
            }

            EnsureInsideMap(workspace.StartMarker);
            EnsureInsideMap(workspace.EndMarker);

            workspace.HighlightSelectedArea();

            // Stores the markers as a history state
            workspace.revertableBlockAccess.StoreHistoryState(HistoryState.Empty);

            return TextCommandResult.Success(string.Format("Area grown by {0} blocks towards {1}", amount, facing));
        }


        private TextCommandResult HandleRotateCommand(int angle)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            EnumOrigin origin = EnumOrigin.BottomCenter;
            BlockPos mid = (workspace.StartMarker + workspace.EndMarker) / 2;
            mid.Y = workspace.StartMarker.Y;

            BlockSchematic schematic = CopyArea(workspace.StartMarker, workspace.EndMarker);

            workspace.revertableBlockAccess.BeginMultiEdit();

            schematic.TransformWhilePacked(sapi.World, origin, angle, null);
            FillArea(null, workspace.StartMarker, workspace.EndMarker);

            PasteBlockData(schematic, mid, origin);

            workspace.StartMarker = schematic.GetStartPos(mid, origin);
            workspace.EndMarker = workspace.StartMarker.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ);

            workspace.revertableBlockAccess.EndMultiEdit();

            workspace.HighlightSelectedArea();

            return TextCommandResult.Success(string.Format("Selection rotated by {0} degrees.", angle));
        }


        private TextCommandResult HandleRepeatCommand(Vec3i dir, int repeats, string selectMode)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            bool selectNewArea = selectMode == "sn";
            bool growNewArea = selectMode == "gn";

            BlockPos startPos = workspace.GetMarkedMinPos();
            BlockPos endPos = workspace.GetMarkedMaxPos();

            return RepeatArea(startPos, endPos, dir, repeats, selectNewArea, growNewArea);
        }

        public TextCommandResult RepeatArea(BlockPos startPos, BlockPos endPos, Vec3i dir, int repeats, bool selectNewArea, bool growNewArea)
        { 
            int curRepeats = 0;

            Dictionary<BlockPos, TreeAttribute> blockEntityData = new Dictionary<BlockPos, TreeAttribute>();

            BlockPos offset = null;
            while (curRepeats++ < repeats)
            {
                BlockPos curPos = startPos.Copy();

                offset = new BlockPos(
                    (endPos.X - startPos.X) * dir.X * curRepeats,
                    (endPos.Y - startPos.Y) * dir.Y * curRepeats,
                    (endPos.Z - startPos.Z) * dir.Z * curRepeats
                );
                BlockPos pos = new BlockPos();

                while (curPos.X < endPos.X)
                {
                    curPos.Y = startPos.Y;

                    while (curPos.Y < endPos.Y)
                    {
                        curPos.Z = startPos.Z;

                        while (curPos.Z < endPos.Z)
                        {
                            var block = workspace.revertableBlockAccess.GetBlock(curPos);

                            if (block.EntityClass != null)
                            {
                                TreeAttribute tree = new TreeAttribute();
                                workspace.revertableBlockAccess.GetBlockEntity(curPos)?.ToTreeAttributes(tree);
                                blockEntityData[curPos + offset] = tree;                              
                            }

                            pos.Set(curPos.X + offset.X, curPos.Y + offset.Y, curPos.Z + offset.Z);

                            workspace.revertableBlockAccess.SetBlock(block.Id, pos);


                            curPos.Z++;
                        }
                        curPos.Y++;
                    }
                    curPos.X++;
                }
            }


            if (selectNewArea)
            {
                workspace.StartMarker.Add(offset);
                workspace.EndMarker.Add(offset);
                workspace.ResendBlockHighlights(this);
            }

            if (growNewArea)
            {
                workspace.StartMarker.Set(
                    startPos.X + (offset.X < 0 ? offset.X : 0),
                    startPos.Y + (offset.Y < 0 ? offset.Y : 0),
                    startPos.Z + (offset.Z < 0 ? offset.Z : 0)
                );

                workspace.EndMarker.Set(
                    endPos.X + (offset.X > 0 ? offset.X : 0),
                    endPos.Y + (offset.Y > 0 ? offset.Y : 0),
                    endPos.Z + (offset.Z > 0 ? offset.Z : 0)
                );

                workspace.ResendBlockHighlights(this);
            }

            workspace.revertableBlockAccess.Commit();

            foreach (var val in blockEntityData)
            {
                BlockEntity be = workspace.revertableBlockAccess.GetBlockEntity(val.Key);
                val.Value.SetInt("posx", val.Key.X);
                val.Value.SetInt("posy", val.Key.Y);
                val.Value.SetInt("posz", val.Key.Z);

                be?.FromTreeAttributes(val.Value, workspace.world);
            }

            return TextCommandResult.Success("Marked area repeated " + repeats + ((repeats > 1) ? " times" : " time"));
        }


        private TextCommandResult HandleMirrorCommand(BlockFacing face, string selectMode)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            bool selectNewArea = selectMode == "sn";
            bool growNewArea = selectMode == "gn";

            BlockPos startPos = workspace.GetMarkedMinPos();
            BlockPos endPos = workspace.GetMarkedMaxPos();

            MirrorArea(startPos, endPos, face, selectNewArea, growNewArea);
            return TextCommandResult.Success("Marked area mirrored " + face.Code);
        }

        public void MirrorArea(BlockPos startPos, BlockPos endPos, BlockFacing dir, bool selectNewArea, bool growNewArea)
        { 
            BlockPos curPos = startPos.Copy();

            BlockPos offset = new BlockPos(
                (endPos.X - startPos.X) * dir.Normali.X,
                (endPos.Y - startPos.Y) * dir.Normali.Y,
                (endPos.Z - startPos.Z) * dir.Normali.Z
            );

            BlockPos pos = new BlockPos();
            Block block;

            Dictionary<BlockPos, ITreeAttribute> blockEntityData = new Dictionary<BlockPos, ITreeAttribute>();

            while (curPos.X < endPos.X)
            {
                curPos.Y = startPos.Y;

                while (curPos.Y < endPos.Y)
                {
                    curPos.Z = startPos.Z;

                    while (curPos.Z < endPos.Z)
                    {
                        int blockId = workspace.revertableBlockAccess.GetBlockId(curPos);

                        if (dir.Axis == EnumAxis.Y)
                        {
                            block = sapi.World.GetBlock(sapi.World.Blocks[blockId].GetVerticallyFlippedBlockCode());
                        } else
                        {
                            block = sapi.World.GetBlock(sapi.World.Blocks[blockId].GetHorizontallyFlippedBlockCode(dir.Axis));
                        }

                        // Mirrored position inside the same area
                        int mX = dir.Axis == EnumAxis.X ? startPos.X + (endPos.X - curPos.X) - 1 : curPos.X;
                        int mY = dir.Axis == EnumAxis.Y ? startPos.Y + (endPos.Y - curPos.Y) - 1 : curPos.Y;
                        int mZ = dir.Axis == EnumAxis.Z ? startPos.Z + (endPos.Z - curPos.Z) - 1 : curPos.Z;

                        pos.Set(mX + offset.X, mY + offset.Y, mZ + offset.Z);

                        BlockEntity be = workspace.revertableBlockAccess.GetBlockEntity(curPos);
                        if (be != null)
                        {
                            TreeAttribute tree = new TreeAttribute();
                            be.ToTreeAttributes(tree);
                            blockEntityData[pos.Copy()] = tree;
                        }


                        workspace.revertableBlockAccess.SetBlock(block.BlockId, pos);

                        curPos.Z++;
                    }
                    curPos.Y++;
                }
                curPos.X++;
            }



            if (selectNewArea)
            {
                workspace.StartMarker.Add(offset);
                workspace.EndMarker.Add(offset);
                workspace.ResendBlockHighlights(this);
            }

            if (growNewArea)
            {
                workspace.StartMarker.Set(
                    startPos.X + (offset.X < 0 ? offset.X : 0),
                    startPos.Y + (offset.Y < 0 ? offset.Y : 0),
                    startPos.Z + (offset.Z < 0 ? offset.Z : 0)
                );

                workspace.EndMarker.Set(
                    endPos.X + (offset.X > 0 ? offset.X : 0),
                    endPos.Y + (offset.Y > 0 ? offset.Y : 0),
                    endPos.Z + (offset.Z > 0 ? offset.Z : 0)
                );

                workspace.ResendBlockHighlights(this);
            }

            workspace.revertableBlockAccess.Commit();

            // restore block entity data
            foreach (var val in blockEntityData)
            {
                BlockEntity be = workspace.revertableBlockAccess.GetBlockEntity(val.Key);
                if (be != null)
                {
                    ITreeAttribute tree = val.Value;

                    tree.SetInt("posx", val.Key.X);
                    tree.SetInt("posy", val.Key.Y);
                    tree.SetInt("posz", val.Key.Z);

                    if (be is IRotatable)
                    {
                        (be as IRotatable).OnTransformed(tree, 0, dir.Axis);
                    }

                    be.FromTreeAttributes(tree, this.sapi.World);
                }
            }
        }

        private TextCommandResult HandleMoveCommand(Vec3i dir)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }
            return MoveAndUpdate(dir);
        }

        private TextCommandResult MoveAndUpdate(Vec3i offset)
        {
            MoveArea(offset, workspace.StartMarker, workspace.EndMarker);
            workspace.ResendBlockHighlights(this);
            return TextCommandResult.Success("Moved marked area by x/y/z = " + offset.X + "/" + offset.Y + "/" + offset.Z);
        }

        private TextCommandResult HandleShiftCommand(Vec3i dir)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            workspace.StartMarker.Add(dir.X, dir.Y, dir.Z);
            workspace.EndMarker.Add(dir.X, dir.Y, dir.Z);
            workspace.ResendBlockHighlights(this);
            workspace.revertableBlockAccess.StoreHistoryState(HistoryState.Empty);
            return TextCommandResult.Success("Shifted marked area by x/y/z = " + dir.X + "/" + dir.Y + "/" + dir.Z);
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

            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            BlockPos endPos = new BlockPos(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));
            BlockPos curPos = startPos.Copy();

            int wx = endPos.X - startPos.X;
            int wy = endPos.Y - startPos.Y;
            int wz = endPos.Z - startPos.Z;

            int quantityBlocks = offset.X * offset.Y * offset.Z;
            Block block = sapi.World.Blocks[0];
            if (!MayPlace(block, quantityBlocks)) return 0;


            Dictionary<BlockPos, int> blocksByNewPos = new Dictionary<BlockPos, int>();
            Dictionary<BlockPos, TreeAttribute> blockEntityDataByNewPos = new Dictionary<BlockPos, TreeAttribute>();

            workspace.revertableBlockAccess.BeginMultiEdit();

            // 1. Read area into dictionaries and clear it fully
            while (curPos.X < endPos.X)
            {
                curPos.Y = startPos.Y;
                while (curPos.Y < endPos.Y)
                {
                    curPos.Z = startPos.Z;
                    while (curPos.Z < endPos.Z)
                    {
                        var newPos = curPos.AddCopy(offset);

                        BlockEntity be = workspace.revertableBlockAccess.GetBlockEntity(curPos);
                        if (be != null)
                        {
                            TreeAttribute tree = new TreeAttribute();
                            be.ToTreeAttributes(tree);
                            blockEntityDataByNewPos[newPos] = tree;
                        }

                        blocksByNewPos[newPos] = workspace.revertableBlockAccess.GetBlock(curPos).Id;

                        workspace.revertableBlockAccess.SetBlock(0, curPos);

                        curPos.Z++;
                    }

                    curPos.Y++;
                }

                curPos.X++;
            }

            workspace.revertableBlockAccess.Commit();

            // 2. Place area at new position
            foreach (var val in blocksByNewPos)
            {
                workspace.revertableBlockAccess.SetBlock(val.Value, val.Key);
            }
           
            var updates = workspace.revertableBlockAccess.Commit();

            // 3. Store block entity data in commit history
            foreach (var update in updates)
            {
                if (update.OldBlockId == 0)
                {
                    TreeAttribute betree;
                    if (blockEntityDataByNewPos.TryGetValue(update.Pos, out betree))
                    {
                        betree.SetInt("posx", update.Pos.X);
                        betree.SetInt("posy", update.Pos.Y);
                        betree.SetInt("posz", update.Pos.Z);

                        update.NewBlockEntityData = betree.ToBytes();
                    }
                }
                
            }

            // 4. Restore block entity data
            foreach (var val in blockEntityDataByNewPos)
            {
                var pos = val.Key;
                BlockEntity be = workspace.revertableBlockAccess.GetBlockEntity(pos);
                if (be != null)
                {
                    val.Value.SetInt("posx", pos.X);
                    val.Value.SetInt("posy", pos.Y);
                    val.Value.SetInt("posz", pos.Z);

                    be.FromTreeAttributes(val.Value, this.sapi.World);
                }
            }
            
            start.Add(offset);
            end.Add(offset);

            workspace.revertableBlockAccess.EndMultiEdit();

            return updated;
        }


        private int FillArea(ItemStack blockStack, BlockPos start, BlockPos end)
        {
            int updated = 0;

            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            BlockPos finalPos = new BlockPos(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));
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
                Good((block == null ? "Clearing" : "Placing") + " " + (dx * dy * dz) + " blocks...");
            }

            while (curPos.X < finalPos.X)
            {
                curPos.Y = startPos.Y;

                while (curPos.Y < finalPos.Y)
                {
                    curPos.Z = startPos.Z;
                    while (curPos.Z < finalPos.Z)
                    {
                        workspace.revertableBlockAccess.SetBlock(0, curPos, BlockLayersAccess.Fluid);
                        workspace.revertableBlockAccess.SetBlock(blockId, curPos, blockStack);
                        curPos.Z++;
                        updated++;
                    }

                    curPos.Y++;
                }
                curPos.X++;
            }

            workspace.revertableBlockAccess.Commit();

            return updated;
        }


    }
}
