using System;
using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public enum EnumAirBrushMode
    {
        Replace,
        Add
    }

    public enum EnumAirBrushApply
    {
        AnyFace,
        SelectedFace
    }

    public class AirBrushTool : ToolBase
    {
        public NormalizedSimplexNoise noiseGen = NormalizedSimplexNoise.FromDefaultOctaves(2, 0.05, 0.8, 0);
        Random rand = new Random();
        LCGRandom lcgRand;

        public float Radius
        {
            get { return workspace.FloatValues["std.airBrushRadius"]; }
            set { workspace.FloatValues["std.airBrushRadius"] = value; }
        }

        public float Quantity
        {
            get { return workspace.FloatValues["std.airBrushQuantity"]; }
            set { workspace.FloatValues["std.airBrushQuantity"] = value; }
        }

        public EnumAirBrushMode PlaceMode
        {
            get { return (EnumAirBrushMode)workspace.IntValues["std.airBrushPlaceMode"]; }
            set { workspace.IntValues["std.airBrushPlaceMode"] = (int)value; }
        }

        public EnumAirBrushApply Apply
        {
            get { return (EnumAirBrushApply)workspace.IntValues["std.airBrushApply"]; }
            set { workspace.IntValues["std.airBrushApply"] = (int)value; }
        }

        public EnumBrushMode BrushMode
        {
            get { return (EnumBrushMode)workspace.IntValues["std.airBrushMode"]; }
            set { workspace.IntValues["std.airBrushMode"] = (int)value; }
        }


        public AirBrushTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccessor) : base(workspace, blockAccessor)
        {
            if (!workspace.FloatValues.ContainsKey("std.airBrushRadius")) Radius = 8;
            if (!workspace.FloatValues.ContainsKey("std.airBrushQuantity")) Quantity = 20f;
            if (!workspace.IntValues.ContainsKey("std.airBrushApply")) Apply = EnumAirBrushApply.AnyFace;
            if (!workspace.IntValues.ContainsKey("std.airBrushPlaceMode")) PlaceMode = EnumAirBrushMode.Add;
            if (!workspace.IntValues.ContainsKey("std.airBrushMode")) BrushMode = EnumBrushMode.ReplaceSelected;

            lcgRand = new LCGRandom(workspace.world.Seed);
        }
        
        public override Vec3i Size
        {
            get {
                int length = (int)(Radius * 2);
                return new Vec3i(length, length, length);
            }
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
                        float.TryParse(args[1], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out size);
                        Radius = size;
                    }

                    worldEdit.Good("Air Brush Radius " + Radius + " set.");

                    return true;


                case "tgr":
                    Radius++;
                    worldEdit.Good("Air Brush Radius " + Radius + " set");
                    return true;

                case "tsr":
                    Radius = Math.Max(0, Radius - 1);
                    worldEdit.Good("Air Brush Radius " + Radius + " set");
                    return true;

                case "tq":
                    Quantity = 0;

                    if (args.Length > 1)
                    {
                        float quant;
                        float.TryParse(args[1], NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out quant);
                        Quantity = quant;
                    }

                    worldEdit.Good("Quantity " + Quantity + " set.");

                    return true;

                case "tm":
                    {
                        EnumBrushMode mode = EnumBrushMode.ReplaceSelected;

                        if (args.Length > 1)
                        {
                            int index;
                            int.TryParse(args[1], out index);
                            if (Enum.IsDefined(typeof(EnumBrushMode), index))
                            {
                                mode = (EnumBrushMode)index;
                            }
                        }

                        BrushMode = mode;
                        worldEdit.Good(workspace.ToolName + " mode " + mode + " set.");
                        workspace.ResendBlockHighlights(worldEdit);
                        return true;
                    }

                case "tmp":
                    {
                        EnumAirBrushMode mode = EnumAirBrushMode.Add;

                        if (args.Length > 1)
                        {
                            int index;
                            int.TryParse(args[1], out index);
                            if (Enum.IsDefined(typeof(EnumAirBrushMode), index))
                            {
                                mode = (EnumAirBrushMode)index;
                            }
                        }

                        PlaceMode = mode;
                        worldEdit.Good(workspace.ToolName + " mode " + mode + " set.");
                        workspace.ResendBlockHighlights(worldEdit);
                        return true;
                    }


                case "ta":
                    EnumAirBrushApply apply = EnumAirBrushApply.AnyFace;

                    if (args.Length > 1)
                    {
                        int index;
                        int.TryParse(args[1], out index);
                        if (Enum.IsDefined(typeof(EnumAirBrushApply), index))
                        {
                            apply = (EnumAirBrushApply)index;
                        }
                    }

                    Apply = apply;
                    worldEdit.Good(workspace.ToolName + " apply " + apply + " set.");
                    workspace.ResendBlockHighlights(worldEdit);
                    return true;


            }

            return false;
        }

        public override void OnBreak(WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            OnApply(worldEdit, -1, blockSel, null, true);
        }

        public override void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            OnApply(worldEdit, oldBlockId, blockSel, withItemStack, false);
        }

        public void OnApply(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack, bool isbreak = false)
        {
            if (Quantity == 0 || Radius == 0) return;

            float radSq = Radius * Radius;
            

            Block selectedBlock = blockSel.DidOffset ? ba.GetBlock(blockSel.Position.AddCopy(blockSel.Face.Opposite)) : ba.GetBlock(blockSel.Position);
            Block block = ba.GetBlock(blockSel.Position);
            if (isbreak) block = ba.GetBlock(0);

            if (!worldEdit.MayPlace(block, (int)radSq * 2)) return;

            if (oldBlockId >= 0) worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);
            lcgRand.SetWorldSeed(rand.Next());
            lcgRand.InitPositionSeed(blockSel.Position.X / ba.ChunkSize, blockSel.Position.Z / ba.ChunkSize);

            int xRadInt = (int)Math.Ceiling(Radius);
            int yRadInt = (int)Math.Ceiling(Radius);
            int zRadInt = (int)Math.Ceiling(Radius);

            HashSet<BlockPos> viablePositions = new HashSet<BlockPos>();
            BlockPos dpos, ddpos;
            Block hereBlock;
            EnumAirBrushMode mode = PlaceMode;
            var bmode = BrushMode;

            for (int dx = -xRadInt; dx <= xRadInt; dx++)
            {
                for (int dy = -yRadInt; dy <= yRadInt; dy++)
                {
                    for (int dz = -zRadInt; dz <= zRadInt; dz++)
                    {
                        if (dx * dx + dy * dy + dz * dz > radSq) continue;

                        dpos = blockSel.Position.AddCopy(dx, dy, dz);
                        hereBlock = ba.GetBlock(dpos);
                        if (hereBlock.Replaceable >= 6000 || (bmode == EnumBrushMode.ReplaceSelected && hereBlock.Id != selectedBlock.Id)) continue;

                        for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
                        {
                            if (Apply == EnumAirBrushApply.SelectedFace && BlockFacing.ALLFACES[i] != blockSel.Face) continue;

                            ddpos = dpos.AddCopy(BlockFacing.ALLFACES[i]);
                            Block dblock = ba.GetBlock(ddpos);
                            if (dblock.Replaceable >= 6000 && (dblock.IsLiquid() == block.IsLiquid()))
                            {
                                // We found an air block beside a solid block -> let's remember that air block and keep looking
                                if (mode == EnumAirBrushMode.Add)
                                {
                                    viablePositions.Add(ddpos);
                                }
                                else
                                // We found an air block beside a solid block -> let's remember that solid block for removal and we can stop here
                                {
                                    viablePositions.Add(dpos);
                                }
                            }
                        }
                    }
                }   
            }

            List<BlockPos> viablePositionsList = new List<BlockPos>(viablePositions);
            float q = GameMath.Clamp(Quantity/100f, 0, 1) * viablePositions.Count;

            while (q-- > 0)
            {
                if (viablePositionsList.Count == 0) break;

                if (q < 1 && rand.NextDouble() > q) break;

                int index = rand.Next(viablePositionsList.Count);
                dpos = viablePositionsList[index];
                viablePositionsList.RemoveAt(index);
                    
                if (mode == EnumAirBrushMode.Add)
                {
                    block.TryPlaceBlockForWorldGen(ba, dpos, BlockFacing.UP, lcgRand);
                } else
                {
                    ba.SetBlock(block.BlockId, dpos, withItemStack);
                }
                
            }

            if (oldBlockId >= 0)
            {
                ba.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, ba.GetBlock(blockSel.Position).Id);
            }

            ba.Commit();


            return;
        }
    }
}
