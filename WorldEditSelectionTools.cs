using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public partial class WorldEdit
    {

        void ModifyMarker(BlockFacing facing, CmdArgs args)
        {
            int length = 1;
            if (args.Length > 0 && !int.TryParse(args[0], out length)) { length = 1; }

            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                Bad("Start marker or end marker not set");
                return;
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
                minMarkers[0].X -= length;
            }
            if (facing == BlockFacing.EAST)
            {
                maxMarkers[0].X += length;
            }
            if (facing == BlockFacing.NORTH)
            {
                minMarkers[2].Z -= length;
            }
            if (facing == BlockFacing.SOUTH)
            {
                maxMarkers[2].Z += length;
            }
            if (facing == BlockFacing.DOWN)
            {
                minMarkers[1].Y -= length;
            }
            if (facing == BlockFacing.UP)
            {
                maxMarkers[1].Y += length;
            }

            EnsureInsideMap(workspace.StartMarker);
            EnsureInsideMap(workspace.EndMarker);

            Good(string.Format("Area grown by {0} blocks towards {1}", length, facing));

            ResendBlockHighlights();
        }


        private void HandleRotateCommand(int? maybeangle, string centerarg)
        {
            //EnumOrigin origin = (centerarg != null && centerarg.StartsWith("c")) ? EnumOrigin.BottomCenter : EnumOrigin.StartPos;

            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                Bad("Start marker or end marker not set");
                return;
            }

            int angle = 90;

            if (maybeangle != null)
            {
                if (((int)maybeangle / 90) * 90 != (int)maybeangle)
                {
                    Bad("Only intervals of 90 degrees are allowed angles");
                    return;
                }
            }

            EnumOrigin origin = EnumOrigin.BottomCenter;
            BlockPos mid = (workspace.StartMarker + workspace.EndMarker) / 2;

            BlockSchematic schematic = CopyArea(workspace.StartMarker, workspace.EndMarker);
            schematic.TransformWhilePacked(sapi.World, origin, angle, null);
            FillArea(null, workspace.StartMarker, workspace.EndMarker);

            PasteBlockData(schematic, mid, origin);

            ResendBlockHighlights();
        }
        



        private void HandleRepeatCommand(BlockFacing face, CmdArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                Bad("Start marker or end marker not set");
                return;
            }

            int repeats = 1;
            if (args.Length > 0 && !int.TryParse(args[0], out repeats)) { repeats = 1; }
            if (repeats < 1) repeats = 1;

            bool selectNewArea = args.Length > 2 && (args[1] == "sn");
            bool growNewArea = args.Length > 2 && (args[1] == "gn");

            BlockPos startPos = workspace.GetMarkedMinPos();
            BlockPos endPos = workspace.GetMarkedMaxPos();

            RepeatArea(startPos, endPos, face, repeats, selectNewArea, growNewArea);
        }

        public void RepeatArea(BlockPos startPos, BlockPos endPos, BlockFacing dir, int repeats, bool selectNewArea, bool growNewArea)
        { 
            int curRepeats = 0;

            Dictionary<BlockPos, TreeAttribute> blockEntityData = new Dictionary<BlockPos, TreeAttribute>();

            BlockPos offset = null;
            while (curRepeats++ < repeats)
            {
                BlockPos curPos = startPos.Copy();

                offset = new BlockPos(
                    (endPos.X - startPos.X) * dir.Normali.X * curRepeats,
                    (endPos.Y - startPos.Y) * dir.Normali.Y * curRepeats,
                    (endPos.Z - startPos.Z) * dir.Normali.Z * curRepeats
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
                            int blockId = workspace.revertableBlockAccess.GetBlockId(curPos);

                            if (workspace.world.Blocks[blockId].EntityClass != null)
                            {
                                TreeAttribute tree = new TreeAttribute();
                                workspace.revertableBlockAccess.GetBlockEntity(curPos)?.ToTreeAttributes(tree);
                                blockEntityData[curPos + offset] = tree;                              
                            }

                            pos.Set(curPos.X + offset.X, curPos.Y + offset.Y, curPos.Z + offset.Z);

                            workspace.revertableBlockAccess.SetBlock(blockId, pos);


                            curPos.Z++;
                        }
                        curPos.Y++;
                    }
                    curPos.X++;
                }
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

            if (repeats > 1) Good("Marked area repeated " + repeats + ((repeats > 1) ? " times" : " time"));


            if (selectNewArea)
            {
                workspace.StartMarker.Add(offset);
                workspace.EndMarker.Add(offset);
                ResendBlockHighlights();
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

                ResendBlockHighlights();
            }
        }


        private void HandleMirrorCommand(BlockFacing face, CmdArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                Bad("Start marker or end marker not set");
                return;
            }

            bool selectNewArea = args.Length > 0 && (args[0] == "sn");
            bool growNewArea = args.Length > 0 && (args[0] == "gn");

            BlockPos startPos = workspace.GetMarkedMinPos();
            BlockPos endPos = workspace.GetMarkedMaxPos();

            MirrorArea(startPos, endPos, face, selectNewArea, growNewArea);
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

                        workspace.revertableBlockAccess.SetBlock(block.BlockId, pos);

                        curPos.Z++;
                    }
                    curPos.Y++;
                }
                curPos.X++;
            }

            workspace.revertableBlockAccess.Commit();
            Good("Marked area mirrored " + dir);

            if (selectNewArea)
            {
                workspace.StartMarker.Add(offset);
                workspace.EndMarker.Add(offset);
                ResendBlockHighlights();
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

                ResendBlockHighlights();
            }
        }

        private void HandleMoveCommand(BlockFacing face, CmdArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                Bad("Start marker or end marker not set");
                return;
            }

            int dx = 0, dy = 0, dz = 0;

            if (face != null)
            {
                dx = face.Normali.X;
                dy = face.Normali.Y;
                dz = face.Normali.Z;
                int length = 1;
                if (args.Length > 0 && int.TryParse(args[0], out length))
                {
                    dx *= length;
                    dy *= length;
                    dz *= length;
                }

            }
            else
            {
                if (args.Length == 3)
                {
                    int.TryParse(args[0], out dx);
                    int.TryParse(args[1], out dy);
                    int.TryParse(args[2], out dz);
                }
            }

            if (MoveArea(dx, dy, dz, workspace.StartMarker, workspace.EndMarker) > 0)
            {
                Good("Moved marked area by x/y/z = " + dx + "/" + dy + "/" + dz);
            }

            workspace.StartMarker.Add(dx, dy, dz);
            workspace.EndMarker.Add(dx, dy, dz);
            ResendBlockHighlights();
        }
        
        private void HandleShiftCommand(BlockFacing face, CmdArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                Bad("Start marker or end marker not set");
                return;
            }

            int dx = 0, dy = 0, dz = 0;

            if (face != null)
            {
                dx = face.Normali.X;
                dy = face.Normali.Y;
                dz = face.Normali.Z;
                int length = 1;
                if (args.Length > 0 && int.TryParse(args[0], out length))
                {
                    dx *= length;
                    dy *= length;
                    dz *= length;
                }

            }
            else
            {
                if (args.Length == 3)
                {
                    int.TryParse(args[0], out dx);
                    int.TryParse(args[1], out dy);
                    int.TryParse(args[2], out dz);
                }
            }

            workspace.StartMarker.Add(dx, dy, dz);
            workspace.EndMarker.Add(dx, dy, dz);
            ResendBlockHighlights();
            Good("Shifted marked area by x/y/z = " + dx + "/" + dy + "/" + dz);
        }


        public int MoveArea(int dx, int dy, int dz, BlockPos start, BlockPos end)
        {
            int updated = 0;

            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            BlockPos endPos = new BlockPos(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));
            BlockPos curPos = startPos.Copy();

            int wx = endPos.X - startPos.X;
            int wy = endPos.Y - startPos.Y;
            int wz = endPos.Z - startPos.Z;

            int quantityBlocks = dx * dy * dz;
            Block block = sapi.World.Blocks[0];
            if (!MayPlace(block, quantityBlocks)) return 0;


            BlockPos newPos = new BlockPos();
            BlockPos prevPos = new BlockPos();

            Dictionary<BlockPos, ITreeAttribute> blockEntityData = new Dictionary<BlockPos, ITreeAttribute>();

            // Clear area
            while (curPos.X < endPos.X)
            {
                curPos.Y = startPos.Y;
                while (curPos.Y < endPos.Y)
                {
                    curPos.Z = startPos.Z;
                    while (curPos.Z < endPos.Z)
                    {
                        BlockEntity be = workspace.revertableBlockAccess.GetBlockEntity(curPos);
                        if (be != null)
                        {
                            TreeAttribute tree = new TreeAttribute();
                            be.ToTreeAttributes(tree);
                            blockEntityData[curPos.AddCopy(dx, dy, dz)] = tree;
                        }

                        workspace.revertableBlockAccess.SetBlock(0, curPos);

                        curPos.Z++;
                    }

                    curPos.Y++;
                }

                curPos.X++;
            }


            curPos = startPos.Copy();

            // move selection
            while (curPos.X < endPos.X)
            {
                curPos.Y = startPos.Y;
                while (curPos.Y < endPos.Y)
                {
                    curPos.Z = startPos.Z;
                    while (curPos.Z < endPos.Z)
                    {
                        newPos.Set(curPos.X + dx, curPos.Y + dy, curPos.Z + dz);
                        int prevBlockId = workspace.revertableBlockAccess.GetBlockId(curPos);
                        workspace.revertableBlockAccess.SetBlock(prevBlockId, newPos);
                        
                        curPos.Z++;
                    }

                    curPos.Y++;
                }

                curPos.X++;
            }
           
            workspace.revertableBlockAccess.Commit();

            // restore block entity data
            foreach (var val in blockEntityData)
            {
                BlockEntity be = workspace.revertableBlockAccess.GetBlockEntity(val.Key);
                if (be != null)
                {
                    val.Value.SetInt("posx", val.Key.X);
                    val.Value.SetInt("posy", val.Key.Y);
                    val.Value.SetInt("posz", val.Key.Z);

                    be.FromTreeAttributes(val.Value, this.sapi.World);
                }
            }

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
