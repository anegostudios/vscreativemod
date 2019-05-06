using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class EraserTool : PaintBrushTool
    {
        public override string Prefix
        {
            get { return "std.eraser"; }
        }

        public EraserTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccessor) : base(workspace, blockAccessor)
        {
            
        }

        public override bool ApplyToolBuild(WorldEdit worldEdit, Block placedBlock, ushort oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack)
        {
            return false;
        }

        public override bool ApplyToolBreak(WorldEdit worldEdit, Block block, ushort oldBlockId, BlockSelection blockSel, BlockPos targetPos)
        {
            block = blockAccessRev.GetBlock(0);
            return PerformBrushAction(worldEdit, block, oldBlockId, blockSel, targetPos, null);
        }
        
        
    }
}
