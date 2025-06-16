using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods.WorldEdit
{
    public class EraserTool : PaintBrushTool
    {
        public override string Prefix
        {
            get { return "std.eraser"; }
        }

        public EraserTool()
        {
        }

        public EraserTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccessor) : base(workspace, blockAccessor)
        {

        }

        public override void ApplyToolBuild(WorldEdit worldEdit, Block placedBlock, int oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack)
        {
            PlaceOldBlock(worldEdit, oldBlockId, blockSel, placedBlock);
        }

        public override void ApplyToolBreak(WorldEdit worldEdit, Block oldblock, BlockSelection blockSel, BlockPos targetPos, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            PerformBrushAction(worldEdit, oldblock, oldblock.Id, blockSel, targetPos, null);
            ba.Commit();
        }


    }
}
