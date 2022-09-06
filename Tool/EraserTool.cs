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

        public override bool ApplyToolBuild(WorldEdit worldEdit, Block placedBlock, int oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack)
        {
            return false;
        }

        public override bool ApplyToolBreak(WorldEdit worldEdit, Block oldblock, BlockSelection blockSel, BlockPos targetPos, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            oldblock = ba.GetBlock(0);
            return PerformBrushAction(worldEdit, oldblock, -1, blockSel, targetPos, null);
        }
        
        
    }
}
