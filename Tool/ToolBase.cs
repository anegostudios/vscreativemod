﻿using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public interface IWorldEditTool
    {
        void Init(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess);

        void OnBuild(WorldEdit worldEdit, BlockPos pos, int oldBlockId, BlockFacing onBlockFace, Vec3f hitPosition);
        void OnBreak(WorldEdit worldEdit, BlockPos pos, BlockFacing onBlockFace, Vec3f hitPosition, ref EnumHandling handling);
        bool OnWorldEditCommand(WorldEdit worldEdit, string[] args);
        Vec3i Size { get; }
    }

    public abstract class ToolBase
    {
        public WorldEditWorkspace workspace;
        public IBlockAccessorRevertable ba;


        public ToolBase(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess)
        {
            this.ba = blockAccess;
            this.workspace = workspace;
        }

        public abstract Vec3i Size { get; }
        public virtual bool ShowSelection { get; } = false;

        public virtual void OnSelected(WorldEdit worldEdit)
        {

        }

        public virtual void OnDeselected(WorldEdit worldEdit)
        {

        }

        public virtual void OnInteractStart(WorldEdit worldEdit, BlockSelection blockSelection)
        {
            
        }

        public virtual void OnAttackStart(WorldEdit worldEdit, BlockSelection blockSelection)
        {

        }

        public virtual void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            Block placedBlock = ba.GetBlock(blockSel.Position);
            BlockPos targetPos = blockSel.Position.Copy();

            if (workspace.ToolOffsetMode == EnumToolOffsetMode.Attach)
            {
                targetPos.X += (workspace.ToolInstance.Size.X / 2) * blockSel.Face.Normali.X;
                targetPos.Y += (workspace.ToolInstance.Size.Y / 2) * blockSel.Face.Normali.Y;
                targetPos.Z += (workspace.ToolInstance.Size.Z / 2) * blockSel.Face.Normali.Z;
            }

            if (ApplyToolBuild(worldEdit, placedBlock, oldBlockId, blockSel, targetPos, withItemStack))
            {
                if (oldBlockId >= 0) ba.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, ba.GetStagedBlockId(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z));
                ba.Commit();
            }
        }

        public virtual void OnBreak(WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            Block oldblock = ba.GetBlock(blockSel.Position);
            BlockPos targetPos = blockSel.Position.Copy();

            if (workspace.ToolOffsetMode == EnumToolOffsetMode.Attach)
            {
                targetPos.X += workspace.ToolInstance.Size.X / 2 * blockSel.Face.Normali.X;
                targetPos.Y += workspace.ToolInstance.Size.Y / 2 * blockSel.Face.Normali.Y;
                targetPos.Z += workspace.ToolInstance.Size.Z / 2 * blockSel.Face.Normali.Z;
            }

            if (ApplyToolBreak(worldEdit, oldblock, blockSel, targetPos, ref handling))
            {
                if (handling == EnumHandling.PassThrough)
                {
                    ba.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldblock.Id, ba.GetStagedBlockId(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z));
                }
                ba.Commit();
            }
        }

        public virtual void Load(ICoreAPI api)
        {
            
        }

        public virtual void Unload(ICoreAPI api)
        {
            
        }

        public virtual bool ApplyToolBuild(WorldEdit worldEdit, Block block, int oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack)
        {
            return false;
        }

        public virtual bool ApplyToolBreak(WorldEdit worldEdit, Block oldblock, BlockSelection blockSel, BlockPos targetPos, ref EnumHandling handling)
        {
            return false;
        }

        public virtual bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            return false;
        }



        public virtual List<BlockPos> GetBlockHighlights(WorldEdit worldEdit) { return new List<BlockPos>(); }

        public virtual List<int> GetBlockHighlightColors(WorldEdit worldEdit) { return new List<int>(new int[] {
            ColorUtil.ToRgba(48, (int)(GuiStyle.DialogDefaultBgColor[2] * 255), (int)(GuiStyle.DialogDefaultBgColor[1] * 255), (int)(GuiStyle.DialogDefaultBgColor[0] * 255))
        } ); }

        public virtual EnumHighlightShape GetBlockHighlightShape(WorldEdit we)
        {
            return EnumHighlightShape.Arbitrary;
        }

        public virtual void HighlightBlocks(IPlayer player, WorldEdit we, EnumHighlightBlocksMode mode)
        {
            we.sapi.World.HighlightBlocks(player, (int)EnumHighlightSlot.Brush, GetBlockHighlights(we), GetBlockHighlightColors(we), mode, GetBlockHighlightShape(we));
        }
    }
}

