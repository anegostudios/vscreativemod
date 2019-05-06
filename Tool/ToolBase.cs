using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public interface IWorldEditTool
    {
        void Init(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess);

        void OnBuild(WorldEdit worldEdit, BlockPos pos, ushort oldBlockId, BlockFacing onBlockFace, Vec3f hitPosition);
        void OnBreak(WorldEdit worldEdit, BlockPos pos, ushort oldBlockId, BlockFacing onBlockFace, Vec3f hitPosition);
        bool OnWorldEditCommand(WorldEdit worldEdit, string[] args);
        Vec3i Size { get; }
    }

    public abstract class ToolBase
    {
        public WorldEditWorkspace workspace;
        public IBlockAccessorRevertable blockAccessRev;


        public ToolBase(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess)
        {
            this.blockAccessRev = blockAccess;
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

        public virtual void OnBuild(WorldEdit worldEdit, ushort oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            Block placedBlock = blockAccessRev.GetBlock(blockSel.Position);
            BlockPos targetPos = blockSel.Position.Copy();

            if (workspace.ToolOffsetMode == EnumToolOffsetMode.Attach)
            {
                targetPos.X += (workspace.ToolInstance.Size.X / 2) * blockSel.Face.Normali.X;
                targetPos.Y += (workspace.ToolInstance.Size.Y / 2) * blockSel.Face.Normali.Y;
                targetPos.Z += (workspace.ToolInstance.Size.Z / 2) * blockSel.Face.Normali.Z;
            }

            if (ApplyToolBuild(worldEdit, placedBlock, oldBlockId, blockSel, targetPos, withItemStack))
            {
                blockAccessRev.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, blockAccessRev.GetStagedBlockId(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z));
                blockAccessRev.Commit();
            }
        }

        public virtual void OnBreak(WorldEdit worldEdit, ushort oldBlockId, BlockSelection blockSel)
        {
            Block block = blockAccessRev.GetBlock(blockSel.Position);
            BlockPos targetPos = blockSel.Position.Copy();

            if (workspace.ToolOffsetMode == EnumToolOffsetMode.Attach)
            {
                targetPos.X += workspace.ToolInstance.Size.X / 2 * blockSel.Face.Normali.X;
                targetPos.Y += workspace.ToolInstance.Size.Y / 2 * blockSel.Face.Normali.Y;
                targetPos.Z += workspace.ToolInstance.Size.Z / 2 * blockSel.Face.Normali.Z;
            }

            if (ApplyToolBreak(worldEdit, block, oldBlockId, blockSel, targetPos))
            {
                blockAccessRev.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, blockAccessRev.GetStagedBlockId(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z));
                blockAccessRev.Commit();
            }
        }

        public virtual bool ApplyToolBuild(WorldEdit worldEdit, Block block, ushort oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack)
        {
            return false;
        }

        public virtual bool ApplyToolBreak(WorldEdit worldEdit, Block block, ushort oldBlockId, BlockSelection blockSel, BlockPos targetPos)
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


    }
}

