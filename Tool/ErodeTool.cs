using System;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class ErodeTool : ToolBase
    {
        public float BrushRadius
        {
            get { return workspace.FloatValues["std.erodeToolBrushRadius"]; }
            set { workspace.FloatValues["std.erodeToolBrushRadius"] = value; }
        }

        public int KernelRadius
        {
            get { return workspace.IntValues["std.erodeToolKernelRadius"]; }
            set { workspace.IntValues["std.erodeToolKernelRadius"] = value; }
        }

        public int Iterations
        {
            get { return workspace.IntValues["std.erodeToolIterations"]; }
            set { workspace.IntValues["std.erodeToolIterations"] = value; }
        }

        public bool UseSelectedBlock
        {
            get { return workspace.IntValues["std.useSelectedBlock"] > 0; }
            set { workspace.IntValues["std.useSelectedBlock"] = value ? 1 : 0; }
        }


        public ErodeTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccessor) : base(workspace, blockAccessor)
        {
            if (!workspace.FloatValues.ContainsKey("std.erodeToolBrushRadius")) BrushRadius = 10;
            if (!workspace.FloatValues.ContainsKey("std.erodeToolKernelRadius")) KernelRadius = 2;
            if (!workspace.IntValues.ContainsKey("std.erodeToolIterations")) Iterations = 1;
            if (!workspace.IntValues.ContainsKey("std.useSelectedBlock")) UseSelectedBlock = false;

            PrecalcKernel();
        }

        double[,] kernel;

        public override Vec3i Size
        {
            get
            {
                int length = (int)(BrushRadius * 2);
                return new Vec3i(length, length, length);
            }
        }


        void PrecalcKernel()
        {
            int blurRad = KernelRadius;

            double sigma = blurRad / 2.0;

            kernel = GameMath.GenGaussKernel(sigma, 2 * blurRad + 1);
        }

        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            switch (args[0])
            {
                case "tusb":
                    bool val = (bool)args.PopBool(false);

                    if (val)
                    {
                        worldEdit.Good("Will use only selected block for placement");
                    } else
                    {
                        worldEdit.Good("Will use erode away placed blocks");
                    }

                    UseSelectedBlock = val;

                    return true;

                case "tr":
                    BrushRadius = 0;

                    if (args.Length > 1)
                    {
                        float size;
                        float.TryParse(args[1], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out size);
                        BrushRadius = size;
                    }

                    worldEdit.Good("Erode radius " + BrushRadius + " set");
                    return true;

                case "tgr":
                    BrushRadius++;
                    worldEdit.Good("Erode radius " + BrushRadius + " set");
                    return true;

                case "tsr":
                    BrushRadius = Math.Max(0, BrushRadius - 1);
                    worldEdit.Good("Erode radius " + BrushRadius + " set");
                    return true;

                case "tkr":
                    KernelRadius = 0;

                    if (args.Length > 1)
                    {
                        int size;
                        int.TryParse(args[1], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out size);
                        KernelRadius = size;
                    }

                    if (KernelRadius > 10)
                    {
                        KernelRadius = 10;
                        worldEdit.SendPlayerWorkSpace(workspace.PlayerUID);
                        worldEdit.Good("Erode kernel radius " + KernelRadius + " set (limited to 10)");
                    }
                    else
                    {
                        worldEdit.Good("Erode kernel radius " + KernelRadius + " set");
                    }

                    PrecalcKernel();

                    return true;

                case "ti":
                    Iterations = 1;

                    if (args.Length > 1)
                    {
                        int iters;
                        int.TryParse(args[1], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out iters);
                        Iterations = iters;
                    }

                    if (Iterations > 10)
                    {
                        Iterations = 10;
                        worldEdit.SendPlayerWorkSpace(workspace.PlayerUID);
                        worldEdit.Good("Iterations " + Iterations + " set (limited to 10)");
                    } else
                    {
                        worldEdit.Good("Iterations " + Iterations + " set");
                    }

                    

                    

                    return true;
            }

            return false;

        }

        public override void OnBreak(WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            
        }

        public override void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (BrushRadius <= 0) return;

            Block blockToPlace = blockAccessRev.GetBlock(blockSel.Position);
            worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);

            int quantityBlocks = (int)((GameMath.PI * BrushRadius * BrushRadius) * (4 * KernelRadius * KernelRadius) * Iterations);

            quantityBlocks *= 4; // because erode is computationally extra expensive

            if (!worldEdit.MayPlace(blockToPlace, quantityBlocks)) return;

            int q = Iterations;
            while (q-- > 0)
            {
                ApplyErode(worldEdit, blockAccessRev, blockSel.Position, blockToPlace, withItemStack);
            }

            blockAccessRev.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, blockAccessRev.GetBlockId(blockSel.Position));
            blockAccessRev.Commit();
        }

        private void ApplyErode(WorldEdit worldEdit, IBlockAccessor blockAccessor, BlockPos pos, Block blockToPlace, ItemStack withItemStack)
        {
            int radInt = (int)Math.Ceiling(BrushRadius);
            float radSq = BrushRadius * BrushRadius;
            int blurRad = KernelRadius;
            int quantitySamples = (2 * blurRad + 1) * (2 * blurRad + 1);

            BlockPos dpos;
            Block prevBlock = blockAccessRev.GetBlock(0);
            bool useSelected = UseSelectedBlock;

            int mapSizeY = worldEdit.sapi.WorldManager.MapSizeY;

            for (int dx = -radInt; dx <= radInt; dx++)
            {
                for (int dz = -radInt; dz <= radInt; dz++)
                {
                    if (dx * dx + dz * dz > radSq) continue;

                    double avgHeight = 0;

                    // Todo: More efficient implementation. This ones super lame.
                    for (int lx = -blurRad; lx <= blurRad; lx++)
                    {
                        for (int lz = -blurRad; lz <= blurRad; lz++)
                        {
                            dpos = pos.AddCopy(dx + lx, 0, dz + lz);
                            while (dpos.Y < mapSizeY && blockAccessor.GetBlockId(dpos) != 0) dpos.Up();
                            while (dpos.Y > 0 && blockAccessor.GetBlockId(dpos.X, dpos.Y, dpos.Z) == 0) dpos.Down();

                            avgHeight += dpos.Y * kernel[lx + blurRad, lz + blurRad];
                        }
                    }


                    dpos = pos.AddCopy(dx, 0, dz);
                    while (dpos.Y < mapSizeY && blockAccessor.GetBlockId(dpos) != 0) dpos.Up();
                    while (dpos.Y > 0 && (prevBlock = blockAccessor.GetBlock(dpos.X, dpos.Y, dpos.Z)).BlockId == 0) dpos.Down();

                    if (Math.Abs(dpos.Y - avgHeight) < 0.36) continue;

                    if (dpos.Y > avgHeight)
                    {
                        blockAccessor.SetBlock(0, dpos);
                    }
                    else
                    {
                        
                        if (useSelected)
                        {
                            blockAccessRev.SetBlock(blockToPlace.BlockId, dpos.Up(), withItemStack);
                        } else {
                            blockAccessRev.SetBlock(prevBlock.BlockId, dpos.Up());
                        }
                        
                    }
                }
            }
        }
    }
}
