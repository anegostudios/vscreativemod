using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VSCreativeMod;

#nullable disable

namespace Vintagestory.ServerMods.WorldEdit
{
    public abstract class ToolBase
    {
        public WorldEditWorkspace workspace;
        public IBlockAccessorRevertable ba;

        public ToolBase()
        {
        }

        public ToolBase(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess)
        {
            this.ba = blockAccess;
            this.workspace = workspace;
        }

        public abstract Vec3i Size { get; }
        public virtual bool ShowSelection { get; } = false;
        public EnumWeToolMode ScrollMode;

        public virtual bool ScrollEnabled => false;

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

            ApplyToolBuild(worldEdit, placedBlock, oldBlockId, blockSel, targetPos, withItemStack);
        }

        public static void PlaceOldBlock(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, Block placedBlock)
        {
            if (oldBlockId >= 0)
            {
                if (placedBlock.ForFluidsLayer)
                {
                    worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position, BlockLayersAccess.Fluid);
                } else
                {
                    worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);
                }
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

            ApplyToolBreak(worldEdit, oldblock, blockSel, targetPos, ref handling);
        }

        public virtual void Load(ICoreAPI api)
        {

        }

        public virtual void Unload(ICoreAPI api)
        {

        }

        public virtual void ApplyToolBuild(WorldEdit worldEdit, Block block, int oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack)
        {
        }

        public virtual void ApplyToolBreak(WorldEdit worldEdit, Block oldblock, BlockSelection blockSel, BlockPos targetPos, ref EnumHandling handling)
        {
        }

        public virtual bool OnWorldEditCommand(WorldEdit worldEdit, TextCommandCallingArgs args)
        {
            return false;
        }

        public virtual List<BlockPos> GetBlockHighlights()
        {
            return new List<BlockPos>();
        }

        public virtual List<int> GetBlockHighlightColors() {
            return new List<int>(new int[] {
            ColorUtil.ToRgba(100,
                (int)(GuiStyle.DialogDefaultBgColor[2] * 255),
                (int)(GuiStyle.DialogDefaultBgColor[1] * 255),
                (int)(GuiStyle.DialogDefaultBgColor[0] * 255))
            });
        }

        public virtual EnumHighlightShape GetBlockHighlightShape()
        {
            return EnumHighlightShape.Arbitrary;
        }

        public virtual void HighlightBlocks(IPlayer player, ICoreServerAPI sapi, EnumHighlightBlocksMode mode)
        {
            sapi.World.HighlightBlocks(player, (int)EnumHighlightSlot.Brush, GetBlockHighlights(), GetBlockHighlightColors(), mode, GetBlockHighlightShape());
        }

        public virtual List<SkillItem> GetAvailableModes(ICoreClientAPI capi)
        {
            return new List<SkillItem>();
        }
    }
}

