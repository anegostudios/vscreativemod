using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods.WorldEdit
{
    public partial class WorldEditWorkspace
    {
        public TextCommandResult ModifyMarker(BlockFacing facing, int amount, bool quiet = false)
        {
            if (StartMarker == null || EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            GrowSelection(facing, amount);

            return quiet ? TextCommandResult.Success() : TextCommandResult.Success($"Area grown by {amount} blocks towards {facing}");
        }


        public TextCommandResult HandleRotateCommand(int angle)
        {
            if (StartMarker == null || EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            EnumOrigin origin = EnumOrigin.BottomCenter;
            BlockPos mid = (StartMarker + EndMarker) / 2;
            mid.Y = StartMarker.Y;

            BlockSchematic schematic = CopyArea(StartMarker, EndMarker);

            revertableBlockAccess.BeginMultiEdit();

            schematic.TransformWhilePacked(sapi.World, origin, angle, null);
            FillArea(null, StartMarker, EndMarker);

            PasteBlockData(schematic, mid, origin);

            StartMarker = schematic.GetStartPos(mid, origin);
            EndMarker = StartMarker.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ);

            revertableBlockAccess.EndMultiEdit();

            HighlightSelectedArea();

            return TextCommandResult.Success(string.Format("Selection rotated by {0} degrees.", angle));
        }

        public TextCommandResult HandleFlipCommand(EnumAxis axis)
        {
            if (StartMarker == null || EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            EnumOrigin origin = EnumOrigin.BottomCenter;
            BlockPos mid = (StartMarker + EndMarker) / 2;
            mid.Y = StartMarker.Y;

            BlockSchematic schematic = CopyArea(StartMarker, EndMarker);

            revertableBlockAccess.BeginMultiEdit();

            schematic.TransformWhilePacked(sapi.World, origin, 0, axis);
            FillArea(null, StartMarker, EndMarker);

            PasteBlockData(schematic, mid, origin);

            StartMarker = schematic.GetStartPos(mid, origin);
            EndMarker = StartMarker.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ);

            revertableBlockAccess.EndMultiEdit();

            HighlightSelectedArea();

            return TextCommandResult.Success(string.Format("Selection flipped in {0} axis.", axis));
        }


        public TextCommandResult HandleRepeatCommand(Vec3i dir, int repeats, string selectMode)
        {
            if (StartMarker == null || EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            bool selectNewArea = selectMode == "sn";
            bool growNewArea = selectMode == "gn";

            BlockPos startPos = GetMarkedMinPos();
            BlockPos endPos = GetMarkedMaxPos();

            return RepeatArea(startPos, endPos, dir, repeats, selectNewArea, growNewArea);
        }

        public TextCommandResult RepeatArea(BlockPos startPos, BlockPos endPos, Vec3i dir, int repeats, bool selectNewArea, bool growNewArea)
        {
            if (StartMarker == null || EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }
            int curRepeats = 0;

            Dictionary<BlockPos, TreeAttribute> blockEntityData = new Dictionary<BlockPos, TreeAttribute>();

            BlockPos offset = null;
            while (curRepeats++ < repeats)
            {
                var curPos = startPos.Copy();

                offset = new BlockPos(
                    (endPos.X - startPos.X) * dir.X * curRepeats,
                    (endPos.Y - startPos.Y) * dir.Y * curRepeats,
                    (endPos.Z - startPos.Z) * dir.Z * curRepeats
                );
                var pos = new BlockPos();

                while (curPos.X < endPos.X)
                {
                    curPos.Y = startPos.Y;

                    while (curPos.Y < endPos.Y)
                    {
                        curPos.Z = startPos.Z;

                        while (curPos.Z < endPos.Z)
                        {
                            var block = revertableBlockAccess.GetBlock(curPos);
                            var blockFluid = revertableBlockAccess.GetBlock(curPos, BlockLayersAccess.Fluid);
                            var decors = revertableBlockAccess.GetDecors(curPos);

                            if (block.EntityClass != null)
                            {
                                TreeAttribute tree = new TreeAttribute();
                                revertableBlockAccess.GetBlockEntity(curPos)?.ToTreeAttributes(tree);
                                blockEntityData[curPos + offset] = tree;
                            }

                            pos.Set(curPos.X + offset.X, curPos.Y + offset.Y, curPos.Z + offset.Z);

                            revertableBlockAccess.SetBlock(block.Id, pos);
                            if (blockFluid != null)
                            {
                                revertableBlockAccess.SetBlock(blockFluid.Id, pos, BlockLayersAccess.Fluid);
                            }
                            if (decors != null)
                            {
                                for (var i = 0; i < decors.Length; i++)
                                {
                                    if(decors[i] == null) continue;
                                    revertableBlockAccess.SetDecor(decors[i], pos, i);
                                }
                            }

                            curPos.Z++;
                        }
                        curPos.Y++;
                    }
                    curPos.X++;
                }
            }

            var originalStart = startPos.Copy();

            if (selectNewArea)
            {
                StartMarker.Add(offset);
                EndMarker.Add(offset);
                StartMarkerExact = StartMarker.ToVec3d().Add(0.5);
                EndMarkerExact = EndMarker.ToVec3d().Add(-1.0);
                ResendBlockHighlights();
            }

            if (growNewArea)
            {
                StartMarker.Set(
                    startPos.X + (offset.X < 0 ? offset.X : 0),
                    startPos.Y + (offset.Y < 0 ? offset.Y : 0),
                    startPos.Z + (offset.Z < 0 ? offset.Z : 0)
                );

                EndMarker.Set(
                    endPos.X + (offset.X > 0 ? offset.X : 0),
                    endPos.Y + (offset.Y > 0 ? offset.Y : 0),
                    endPos.Z + (offset.Z > 0 ? offset.Z : 0)
                );

                StartMarkerExact = StartMarker.ToVec3d().Add(0.5);
                EndMarkerExact = EndMarker.ToVec3d().Add(-1.0);
                ResendBlockHighlights();
            }

            revertableBlockAccess.Commit();

            curRepeats = 0;
            while (curRepeats++ < repeats)
            {
                var curPos = new EntityPos(originalStart.X, originalStart.Y, originalStart.Z);

                offset = new BlockPos(
                    (endPos.X - originalStart.X) * dir.X * curRepeats,
                    (endPos.Y - originalStart.Y) * dir.Y * curRepeats,
                    (endPos.Z - originalStart.Z) * dir.Z * curRepeats
                );
                var pos = new EntityPos();
                var entitiesInsideCuboid = world.GetEntitiesInsideCuboid(originalStart, endPos, (e) => !(e is EntityPlayer));
                foreach (var entity in entitiesInsideCuboid)
                {
                    curPos.SetPos(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

                    pos.SetPos(curPos.X + offset.X, curPos.Y + offset.Y, curPos.Z + offset.Z);

                    var newEntity = world.ClassRegistry.CreateEntity(entity.Properties);
                    newEntity.DidImportOrExport(pos.AsBlockPos.Copy());
                    newEntity.ServerPos.SetPos(pos);
                    newEntity.ServerPos.SetAngles(entity.ServerPos);
                    world.SpawnEntity(newEntity);
                    revertableBlockAccess.StoreEntitySpawnToHistory(newEntity);
                }
            }

            foreach (var val in blockEntityData)
            {
                BlockEntity be = revertableBlockAccess.GetBlockEntity(val.Key);
                val.Value.SetInt("posx", val.Key.X);
                val.Value.SetInt("posy", val.Key.Y);
                val.Value.SetInt("posz", val.Key.Z);

                be?.FromTreeAttributes(val.Value, world);
            }

            return TextCommandResult.Success("Marked area repeated " + repeats + ((repeats > 1) ? " times" : " time"));
        }


        public TextCommandResult HandleMirrorCommand(BlockFacing face, string selectMode)
        {
            if (StartMarker == null || EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            bool selectNewArea = selectMode == "sn";
            bool growNewArea = selectMode == "gn";

            BlockPos startPos = GetMarkedMinPos();
            BlockPos endPos = GetMarkedMaxPos();

            MirrorArea(startPos, endPos, face, selectNewArea, growNewArea);
            return TextCommandResult.Success("Marked area mirrored " + face.Code);
        }

        public void MirrorArea(BlockPos startPos, BlockPos endPos, BlockFacing dir, bool selectNewArea, bool growNewArea)
        {
            var curPos = startPos.Copy();

            var offset = new BlockPos(
                (endPos.X - startPos.X) * + dir.Normali.X,
                (endPos.Y - startPos.Y) * + dir.Normali.Y,
                (endPos.Z - startPos.Z) * + dir.Normali.Z
            );

            var pos = new BlockPos();
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
                        var blockId = revertableBlockAccess.GetBlockId(curPos);
                        var decors = revertableBlockAccess.GetDecors(curPos);

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

                        BlockEntity be = revertableBlockAccess.GetBlockEntity(curPos);
                        if (be != null)
                        {
                            TreeAttribute tree = new TreeAttribute();
                            be.ToTreeAttributes(tree);
                            blockEntityData[pos.Copy()] = tree;
                        }


                        revertableBlockAccess.SetBlock(block.BlockId, pos);

                        if (decors != null)
                        {
                            for (var i = 0; i < decors.Length; i++)
                            {
                                if(decors[i] == null) continue;

                                var blockFacing = BlockFacing.ALLFACES[i];
                                if (blockFacing.Axis == dir.Axis)
                                {
                                    revertableBlockAccess.SetDecor(decors[i], pos, blockFacing.Opposite.Index);
                                }
                                else
                                {
                                    revertableBlockAccess.SetDecor(decors[i], pos, i);
                                }
                            }
                        }

                        curPos.Z++;
                    }
                    curPos.Y++;
                }
                curPos.X++;
            }

            if (selectNewArea)
            {
                StartMarker.Add(offset);
                EndMarker.Add(offset);
                StartMarkerExact = StartMarker.ToVec3d().Add(0.5);
                EndMarkerExact = EndMarker.ToVec3d().Add(-1.0);
                ResendBlockHighlights();
            }

            var startOriginal = startPos.Copy();
            var endOriginal = endPos.Copy();
            if (growNewArea)
            {
                StartMarker.Set(
                    startPos.X + (offset.X < 0 ? offset.X : 0),
                    startPos.Y + (offset.Y < 0 ? offset.Y : 0),
                    startPos.Z + (offset.Z < 0 ? offset.Z : 0)
                );

                EndMarker.Set(
                    endPos.X + (offset.X > 0 ? offset.X : 0),
                    endPos.Y + (offset.Y > 0 ? offset.Y : 0),
                    endPos.Z + (offset.Z > 0 ? offset.Z : 0)
                );

                StartMarkerExact = StartMarker.ToVec3d().Add(0.5);
                EndMarkerExact = EndMarker.ToVec3d().Add(-1.0);
                ResendBlockHighlights();
            }

            revertableBlockAccess.Commit();

            var curPosE = new EntityPos();
            var posE = new EntityPos();

            var entitiesInsideCuboid = world.GetEntitiesInsideCuboid(startOriginal, endOriginal, (e) => !(e is EntityPlayer));
            foreach (var entity in entitiesInsideCuboid)
            {
                curPosE.SetPos(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

                // Mirrored position inside the same area
                var mX = dir.Axis == EnumAxis.X ? startOriginal.X + (endOriginal.X - curPosE.X) : curPosE.X;
                var mY = dir.Axis == EnumAxis.Y ? startOriginal.Y + (endOriginal.Y - curPosE.Y) : curPosE.Y;
                var mZ = dir.Axis == EnumAxis.Z ? startOriginal.Z + (endOriginal.Z - curPosE.Z) : curPosE.Z;

                posE.SetPos(mX + offset.X, mY + offset.Y, mZ + offset.Z);

                var newEntity = world.ClassRegistry.CreateEntity(entity.Properties);
                newEntity.DidImportOrExport(posE.AsBlockPos.Copy());
                newEntity.ServerPos.SetPos(posE);
                newEntity.ServerPos.SetAngles(entity.ServerPos);
                world.SpawnEntity(newEntity);
                revertableBlockAccess.StoreEntitySpawnToHistory(newEntity);
            }

            // restore block entity data
            foreach (var val in blockEntityData)
            {
                BlockEntity be = revertableBlockAccess.GetBlockEntity(val.Key);
                if (be != null)
                {
                    ITreeAttribute tree = val.Value;

                    tree.SetInt("posx", val.Key.X);
                    tree.SetInt("posy", val.Key.Y);
                    tree.SetInt("posz", val.Key.Z);

                    if (be is IRotatable rotatable)
                    {
                        var empty = new Dictionary<int, AssetLocation>();
                        rotatable.OnTransformed(sapi.World ,tree, 0, empty, empty, dir.Axis);
                    }

                    be.FromTreeAttributes(tree, sapi.World);
                }
            }
        }

        public TextCommandResult HandleMoveCommand(Vec3i offset, bool quiet = false)
        {
            if (PreviewPos == null)
            {
                if (StartMarker == null || EndMarker == null)
                    return TextCommandResult.Success("You need to have at least an active selection");
                PreviewPos = StartMarker.Copy();
                PreviewBlockData = CopyArea(StartMarker, EndMarker);
                CreatePreview(PreviewBlockData, PreviewPos);
            }
            PreviewPos.Add(offset);
            SendPreviewOriginToClient(PreviewPos, previewBlocks.subDimensionId);

            return quiet ? TextCommandResult.Success() : TextCommandResult.Success("Moved marked area by x/y/z = " + offset.X + "/" + offset.Y + "/" + offset.Z);
        }

        public TextCommandResult HandleShiftCommand(Vec3i dir, bool quiet = false)
        {
            if (StartMarker == null || EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            StartMarkerExact.Add(dir.X, dir.Y, dir.Z);
            EndMarkerExact.Add(dir.X, dir.Y, dir.Z);
            UpdateSelection();
            return quiet ? TextCommandResult.Success() : TextCommandResult.Success("Shifted marked area by x/y/z = " + dir.X + "/" + dir.Y + "/" + dir.Z);
        }
    }
}
