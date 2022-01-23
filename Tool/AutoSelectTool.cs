using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class AutoSelectTool : ToolBase
    {
        public AutoSelectTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {

        }

        public override Vec3i Size
        {
            get
            {
                return new Vec3i(0, 0, 0);
            }
        }
    }
}
