using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public enum EnumGrowShrinkMode
    {
        Any,
        SelectedBlock
    }

    public class GrowShrinkTool : ToolBase
    {
        public float BrushRadius
        {
            get { return workspace.FloatValues["std.growShrinkRadius"]; }
            set { workspace.FloatValues["std.growShrinkRadius"] = value; }
        }

        public EnumGrowShrinkMode GrowShrinkMode
        {
            get { return (EnumGrowShrinkMode)workspace.IntValues["std.growShrinkMode"]; }
            set { workspace.IntValues["std.growShrinkMode"] = (int)value; }
        }

        public GrowShrinkTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            if (!workspace.FloatValues.ContainsKey("std.growShrinkRadius")) BrushRadius = 10;
            if (!workspace.IntValues.ContainsKey("std.growShrinkMode")) GrowShrinkMode = EnumGrowShrinkMode.Any;
        }

        public override Vec3i Size
        {
            get { return new Vec3i((int)BrushRadius, (int)BrushRadius, (int)BrushRadius); }
        }

        public override void OnBreak(WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            GrowShrink(worldEdit, -1, blockSel, null, true);
        }

        public override void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            GrowShrink(worldEdit, oldBlockId, blockSel, withItemStack);
        }

      

        public bool GrowShrink(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack, bool shrink = false)
        {
            if (BrushRadius == 0) return false;

            Block blockToPlace = ba.GetBlock(blockSel.Position);
            if (shrink) blockToPlace = ba.GetBlock(0);

            int selectedBlockID = ba.GetBlockId(blockSel.Position.AddCopy(blockSel.Face.Opposite));

            int radInt = (int)Math.Ceiling(BrushRadius);
            float radSq = BrushRadius * BrushRadius;

            HashSet<BlockPos> viablePositions = new HashSet<BlockPos>();
            BlockPos dpos, ddpos;
            Block blockAtPos;

            for (int dx = -radInt; dx <= radInt; dx++)
            {
                for (int dy = -radInt; dy <= radInt; dy++)
                {
                    for (int dz = -radInt; dz <= radInt; dz++)
                    {
                        if (dx * dx + dy*dy + dz * dz > radSq) continue;

                        dpos = blockSel.Position.AddCopy(dx, dy, dz);
                        blockAtPos = ba.GetBlock(dpos);
                        if (blockAtPos.Replaceable >= 6000) continue;
                        if (GrowShrinkMode == EnumGrowShrinkMode.SelectedBlock && blockAtPos.BlockId != selectedBlockID) continue;

                        for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
                        {
                            ddpos = dpos.AddCopy(BlockFacing.ALLFACES[i]);
                            if (ba.GetBlock(ddpos).Replaceable >= 6000)
                            {
                                // We found an air block beside a solid block -> let's remember that solid block for removal and we can stop here
                                if (shrink)
                                {
                                    viablePositions.Add(dpos);
                                    break;
                                } else
                                // We found an air block beside a solid block -> let's remember that air block and keep looking
                                {
                                    viablePositions.Add(ddpos);
                                }
                            }
                        }
                    }
                }
            }

            foreach (BlockPos p in viablePositions)
            {
                ba.SetBlock(blockToPlace.BlockId, p, withItemStack);
            }

            if (oldBlockId >= 0)
            {
                ba.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, ba.GetBlockId(blockSel.Position));
            }
            ba.Commit();


            return true;
        }



        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            switch (args[0])
            {
                case "tm":
                    EnumGrowShrinkMode mode = EnumGrowShrinkMode.Any;

                    if (args.Length > 1)
                    {
                        int index;
                        int.TryParse(args[1], out index);
                        if (Enum.IsDefined(typeof(EnumGrowShrinkMode), index))
                        {
                            mode = (EnumGrowShrinkMode)index;
                        }
                    }

                    GrowShrinkMode = mode;
                    worldEdit.Good(workspace.ToolName + " mode " + mode + " set.");

                    return true;

                case "tr":
                    BrushRadius = 0;

                    if (args.Length > 1)
                    {
                        float size;
                        float.TryParse(args[1], out size);
                        BrushRadius = size;
                    }

                    worldEdit.Good("Grow/Shrink radius " + BrushRadius + " set");

                    return true;

                case "tgr":
                    BrushRadius++;
                    worldEdit.Good("Grow/Shrink radius " + BrushRadius + " set");
                    return true;


                case "tsr":
                    BrushRadius = Math.Max(0, BrushRadius - 1);
                    worldEdit.Good("Grow/Shrink radius " + BrushRadius + " set");
                    return true;
            }

            return false;
        }
        
    }
}
