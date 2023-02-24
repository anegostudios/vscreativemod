using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public enum EnumHeightToolMode {
        Uniform = 0,
        Pyramid = 1,
        Gaussian = 2,
        Perlin = 3
    }

    public class RaiseLowerTool : ToolBase
    {
        public NormalizedSimplexNoise noiseGen = NormalizedSimplexNoise.FromDefaultOctaves(2, 0.05, 0.8, 0);
        Random rand = new Random();

        public float Radius
        {
            get { return workspace.FloatValues["std.raiseLowerRadius"]; }
            set { workspace.FloatValues["std.raiseLowerRadius"] = value; }
        }

        public float Depth
        {
            get { return workspace.FloatValues["std.raiseLowerDepth"]; }
            set { workspace.FloatValues["std.raiseLowerDepth"] = value; }
        }

        public EnumHeightToolMode Mode
        {
            get { return (EnumHeightToolMode)workspace.IntValues["std.raiseLowerMode"]; }
            set { workspace.IntValues["std.raiseLowerMode"] = (int)value; }
        }
        

        public override Vec3i Size
        {
            get
            {
                //int length = (int)(Radius * 2);
                //return new Vec3i(length, length, length);
                return new Vec3i(0, 0, 0);
            }
        }

        public RaiseLowerTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccessor) : base(workspace, blockAccessor)
        {
            if (!workspace.FloatValues.ContainsKey("std.raiseLowerRadius")) Radius = 4;
            if (!workspace.FloatValues.ContainsKey("std.raiseLowerDepth")) Depth = 3;
            if (!workspace.IntValues.ContainsKey("std.raiseLowerMode")) Mode = EnumHeightToolMode.Uniform;
        }

        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            switch (args[0])
            {
              
                case "tr":
                    Radius = 0;

                    if (args.Length > 1)
                    {
                        float size;
                        float.TryParse(args[1], out size);
                        Radius = size;
                    }

                    worldEdit.Good("Raise/Lower radius " + Radius + " set.");
                    return true;


                case "tgr":
                    Radius++;
                    worldEdit.Good("Raise/Lower radius " + Radius + " set");
                    return true;

                case "tsr":
                    Radius = Math.Max(0, Radius - 1);
                    worldEdit.Good("Raise/Lower radius " + Radius + " set");
                    return true;


                case "tdepth":
                    Depth = 0;

                    if (args.Length > 1)
                    {
                        float size;
                        float.TryParse(args[1], out size);
                        Depth = size;
                    }

                    worldEdit.Good("Raise/Lower depth " + Depth + " set.");

                    return true;

                case "tm":
                    Mode = EnumHeightToolMode.Uniform;

                    if (args.Length > 1)
                    {
                        int mode;
                        int.TryParse(args[1], out mode);
                        try
                        {
                            Mode = (EnumHeightToolMode)mode;
                        } catch (Exception) { }
                    }

                    worldEdit.Good("Raise/Lower mode " + Mode + " set.");

                    return true;
            }

            return false;
        }

       
        public override void OnBreak(WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;
            OnUse(worldEdit, blockSel.Position, 0, -1, null);
        }

        public override void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            OnUse(worldEdit, blockSel.Position, oldBlockId, 1, withItemStack);
        }


        void OnUse(WorldEdit worldEdit, BlockPos pos, int oldBlockId, int sign, ItemStack withItemStack)
        {
            if (Radius <= 0) return;

            int radInt = (int)Math.Ceiling(Radius);
            float radSq = Radius * Radius;

            Block block = ba.GetBlock(pos);
            if (sign > 0)
            {
                worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, pos);
            }

            float maxhgt = Depth;
            EnumHeightToolMode dist = Mode;

            int quantityBlocks = (int)(GameMath.PI * radSq) * (int)maxhgt;
            if (!worldEdit.MayPlace(block, quantityBlocks)) return;

            for (int dx = -radInt; dx <= radInt; dx++)
            {
                for (int dz = -radInt; dz <= radInt; dz++)
                {
                    float distanceSq = dx * dx + dz * dz;
                    if (distanceSq > radSq) continue;

                    BlockPos dpos = pos.AddCopy(dx, 0, dz);

                    float height = sign * maxhgt;
                    switch (dist)
                    {
                        case EnumHeightToolMode.Pyramid:
                            height *= 1 - distanceSq / radSq;
                            break;

                        case EnumHeightToolMode.Gaussian:
                            float sigmaSq = 0.1f;
                            float sigma = GameMath.Sqrt(sigmaSq);
                            float a = 1f / (sigma * GameMath.Sqrt(GameMath.TWOPI));
                            float x = distanceSq / radSq;

                            double gaussValue = a * Math.Exp(-(x * x) / (2 * sigmaSq));

                            height *= (float)gaussValue;
                            break;
                        
                        case EnumHeightToolMode.Perlin:

                            height *= (float)noiseGen.Noise(dpos.X, dpos.Y, dpos.Z);

                            break;
                    }

                    while (dpos.Y > 0 && ba.GetBlock(dpos).Replaceable >= 6000) dpos.Down();
                    
                    
                    if (height < 0)
                    {
                        Erode(-height, dpos);
                    } else
                    {
                        dpos.Up();
                        Grow(worldEdit.sapi.World, height, dpos, block, BlockFacing.UP, withItemStack);
                    }



                }
            }

            ba.SetHistoryStateBlock(pos.X, pos.Y, pos.Z, oldBlockId, ba.GetBlock(pos).Id);
            ba.Commit();
        }

        private void Grow(IWorldAccessor world, float quantity, BlockPos dpos, Block block, BlockFacing face, ItemStack withItemstack)
        {
            while (quantity-- >= 1)
            {
                //if (quantity < 1 && rand.NextDouble() > quantity) break;
                ba.SetBlock(block.BlockId, dpos, withItemstack);
                dpos.Up();
            }
        }

        private void Erode(float quantity, BlockPos dpos)
        {
            while (quantity-- >= 1)
            {
                //if (quantity < 1 && rand.NextDouble() > quantity) break;
                ba.SetBlock(0, dpos);
                dpos.Down();
            }
        }
    }
}
