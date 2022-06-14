using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public enum EnumBrushMode
    {
        Fill = 0,
        ReplaceNonAir = 1,
        ReplaceAir = 2,
        ReplaceSelected = 3
    }

    public enum EnumBrushShape
    {
        Ball = 0,
        Cuboid = 1,
        Cylinder = 2,
        HalfBallUp = 3,
        HalfBallDown = 4,
        HalfBallNorth = 5,
        HalfBallEast = 6,
        HalfBallSouth = 7,
        HalfBallWest = 8,
    }

    public enum EnumDepthLimit
    {
        NoLimit,
        Top1,
        Top2,
        Top3,
        Top4
    }

    public class PaintBrushTool : ToolBase
    {
        public static string[][] dimensionNames = new string[][]
        {
            new string[] { "X-Radius", "Y-Radius", "Z-Radius" },
            new string[] { "Width", "Height", "Length" },
            new string[] { "X-Radius", "Height", "Z-Radius" },
            new string[] { "X-Radius", "Y-Radius", "Z-Radius" },
            new string[] { "X-Radius", "Y-Radius", "Z-Radius" },
            new string[] { "X-Radius", "Y-Radius", "Z-Radius" },
            new string[] { "X-Radius", "Y-Radius", "Z-Radius" },
            new string[] { "X-Radius", "Y-Radius", "Z-Radius" },
            new string[] { "X-Radius", "Y-Radius", "Z-Radius" },
        };

        BlockPos[] brushPositions;

        public virtual string Prefix { get { return "std.brush"; } }

        public float BrushDim1 {
            get { return workspace.FloatValues[Prefix + "Dim1"]; }
            set { workspace.FloatValues[Prefix + "Dim1"] = value; }
        }

        public float BrushDim2
        {
            get { return workspace.FloatValues[Prefix + "Dim2"]; }
            set { workspace.FloatValues[Prefix + "Dim2"] = value; }
        }

        public float BrushDim3
        {
            get { return workspace.FloatValues[Prefix + "Dim3"]; }
            set { workspace.FloatValues[Prefix + "Dim3"] = value; }
        }

        public EnumBrushShape BrushShape
        {
            get { return (EnumBrushShape)workspace.IntValues[Prefix + "Shape"]; }
            set { workspace.IntValues[Prefix + "Shape"] = (int)value; }
        }

        public bool PreviewMode
        {
            get { return workspace.IntValues[Prefix + "previewMode"] > 0; }
            set { workspace.IntValues[Prefix + "previewMode"] = value ? 1 : 0; }
        }

        public float CutoutDim1
        {
            get { return workspace.FloatValues[Prefix + "cutoutDim1"]; }
            set { workspace.FloatValues[Prefix + "cutoutDim1"] = value; }
        }

        public float CutoutDim2
        {
            get { return workspace.FloatValues[Prefix + "cutoutDim2"]; }
            set { workspace.FloatValues[Prefix + "cutoutDim2"] = value; }
        }

        public float CutoutDim3
        {
            get { return workspace.FloatValues[Prefix + "cutoutDim3"]; }
            set { workspace.FloatValues[Prefix + "cutoutDim3"] = value; }
        }

        public EnumBrushMode BrushMode
        {
            get { return (EnumBrushMode)workspace.IntValues[Prefix + "Mode"]; }
            set { workspace.IntValues[Prefix + "Mode"] = (int)value; }
        }

        public EnumDepthLimit DepthLimit
        {
            get { return (EnumDepthLimit)workspace.IntValues[Prefix + "DepthLimit"]; }
            set { workspace.IntValues[Prefix + "DepthLimit"] = (int)value; }
        }


        public override Vec3i Size
        {
            get { return size; }
        }

        Vec3i size;

        public PaintBrushTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccessor) : base(workspace, blockAccessor)
        {
            if (!workspace.FloatValues.ContainsKey(Prefix + "Dim1")) BrushDim1 = 4;
            if (!workspace.FloatValues.ContainsKey(Prefix + "Dim2")) BrushDim2 = 4;
            if (!workspace.FloatValues.ContainsKey(Prefix + "Dim3")) BrushDim3 = 4;

            if (!workspace.FloatValues.ContainsKey(Prefix + "cutoutDim1")) CutoutDim1 = 0;
            if (!workspace.FloatValues.ContainsKey(Prefix + "cutoutDim2")) CutoutDim2 = 0;
            if (!workspace.FloatValues.ContainsKey(Prefix + "cutoutDim3")) CutoutDim3 = 0;

            if (!workspace.IntValues.ContainsKey(Prefix + "previewMode")) PreviewMode = true;
            if (!workspace.IntValues.ContainsKey(Prefix + "Mode")) BrushMode = EnumBrushMode.Fill;
            if (!workspace.IntValues.ContainsKey(Prefix + "Shape")) BrushShape = EnumBrushShape.Ball;
            if (!workspace.IntValues.ContainsKey(Prefix + "DepthLimit")) DepthLimit = EnumDepthLimit.NoLimit;

            GenBrush();
        }


        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            switch (args[0])
            {
                case "tm":
                    EnumBrushMode brushMode = EnumBrushMode.Fill;

                    if (args.Length > 1)
                    {
                        int index;
                        int.TryParse(args[1], out index);
                        if (Enum.IsDefined(typeof(EnumBrushMode), index))
                        {
                            brushMode = (EnumBrushMode)index;
                        }
                    }

                    BrushMode = brushMode;
                    worldEdit.Good(workspace.ToolName + " mode " + brushMode + " set.");
                    workspace.ResendBlockHighlights(worldEdit);

                    return true;

                case "tdl":
                    EnumDepthLimit depthLimit = EnumDepthLimit.NoLimit;

                    if (args.Length > 1)
                    {
                        int index;
                        int.TryParse(args[1], out index);
                        if (Enum.IsDefined(typeof(EnumDepthLimit), index))
                        {
                            depthLimit = (EnumDepthLimit)index;
                        }
                    }

                    DepthLimit = depthLimit;
                    worldEdit.Good(workspace.ToolName + " depth limit set to " + depthLimit);
                    workspace.ResendBlockHighlights(worldEdit);
                    return true;

                case "ts":
                    EnumBrushShape shape = EnumBrushShape.Ball;

                    if (args.Length > 1)
                    {
                        int index;
                        int.TryParse(args[1], out index);
                        if (Enum.IsDefined(typeof(EnumBrushShape), index))
                        {
                            shape = (EnumBrushShape)index;
                        }
                    }

                    BrushShape = shape;

                    worldEdit.Good(workspace.ToolName + " shape " + BrushShape + " set.");
                    GenBrush();
                    workspace.ResendBlockHighlights(worldEdit);
                    return true;

                case "tsx":
                case "tsy":
                case "tsz":
                    {
                        float size = 0;
                        if (args.Length > 1) float.TryParse(args[1], out size);

                        if (args[0] == "tsx")
                        {
                            BrushDim1 = size;
                        }
                        if (args[0] == "tsy")
                        {
                            BrushDim2 = size;
                        }
                        if (args[0] == "tsz")
                        {
                            BrushDim3 = size;
                        }

                        string text = dimensionNames[(int)BrushShape][0] + "=" + BrushDim1;
                        text += ", " + dimensionNames[(int)BrushShape][1] + "=" + BrushDim2;
                        text += ", " + dimensionNames[(int)BrushShape][2] + "=" + BrushDim3;

                        worldEdit.Good(workspace.ToolName + " dimensions " + text + " set.");

                        GenBrush();
                        workspace.ResendBlockHighlights(worldEdit);

                        return true;
                    }


                case "tr":
                    {
                        BrushDim1 = 0;
                        float size;

                        if (args.Length > 1 && float.TryParse(args[1], out size))
                        {
                            BrushDim1 = size;
                        }

                        if (args.Length > 2 && float.TryParse(args[2], out size))
                        {
                            BrushDim2 = size;
                        }
                        else
                        {
                            BrushDim2 = BrushDim1;
                        }

                        if (args.Length > 3 && float.TryParse(args[3], out size))
                        {
                            BrushDim3 = size;
                        }
                        else
                        {
                            BrushDim3 = BrushDim2;
                        }

                        string text = dimensionNames[(int)BrushShape][0] + "=" + BrushDim1;
                        text += ", " + dimensionNames[(int)BrushShape][1] + "=" + BrushDim2;
                        text += ", " + dimensionNames[(int)BrushShape][2] + "=" + BrushDim3;

                        worldEdit.Good(workspace.ToolName + " dimensions " + text + " set.");

                        GenBrush();
                        workspace.ResendBlockHighlights(worldEdit);
                    }
                    return true;



                case "tcx":
                case "tcy":
                case "tcz":
                    {
                        float size = 0;
                        if (args.Length > 1) float.TryParse(args[1], out size);

                        if (args[0] == "tcx")
                        {
                            CutoutDim1 = size;
                        }
                        if (args[0] == "tcy")
                        {
                            CutoutDim2 = size;
                        }
                        if (args[0] == "tcz")
                        {
                            CutoutDim3 = size;
                        }

                        string text = dimensionNames[(int)BrushShape][0] + "=" + CutoutDim1;
                        text += ", " + dimensionNames[(int)BrushShape][1] + "=" + CutoutDim2;
                        text += ", " + dimensionNames[(int)BrushShape][2] + "=" + CutoutDim3;

                        worldEdit.Good(workspace.ToolName + " cutout dimensions " + text + " set.");

                        GenBrush();
                        workspace.ResendBlockHighlights(worldEdit);

                        return true;
                    }


                case "tcr":
                    {
                        CutoutDim1 = 0;
                        float size;

                        if (args.Length > 1 && float.TryParse(args[1], out size))
                        {
                            CutoutDim1 = size;
                        }

                        if (args.Length > 2 && float.TryParse(args[2], out size))
                        {
                            CutoutDim2 = size;
                        }
                        else
                        {
                            CutoutDim2 = CutoutDim1;
                        }

                        if (args.Length > 3 && float.TryParse(args[3], out size))
                        {
                           CutoutDim3 = size;
                        }
                        else
                        {
                            CutoutDim3 = CutoutDim2;
                        }

                        string text = dimensionNames[(int)BrushShape][0] + "=" + CutoutDim1;
                        text += ", " + dimensionNames[(int)BrushShape][1] + "=" + CutoutDim2;
                        text += ", " + dimensionNames[(int)BrushShape][2] + "=" + CutoutDim3;

                        worldEdit.Good("Cutout " + workspace.ToolName + " dimensions " + text + " set.");

                        GenBrush();
                        workspace.ResendBlockHighlights(worldEdit);
                    }
                    return true;

                case "tgr":
                    {
                        BrushDim1++;
                        BrushDim2++;
                        BrushDim3++;

                        string text = dimensionNames[(int)BrushShape][0] + "=" + BrushDim1;
                        text += ", " + dimensionNames[(int)BrushShape][1] + "=" + BrushDim2;
                        text += ", " + dimensionNames[(int)BrushShape][2] + "=" + BrushDim3;
                        worldEdit.Good(workspace.ToolName + " dimensions " + text + " set.");
                        GenBrush();
                        workspace.ResendBlockHighlights(worldEdit);
                    }
                    return true;

                case "tsr":
                    {
                        BrushDim1 = Math.Max(0, BrushDim1 - 1);
                        BrushDim2 = Math.Max(0, BrushDim2 - 1);
                        BrushDim3 = Math.Max(0, BrushDim3 - 1);

                        string text = dimensionNames[(int)BrushShape][0] + "=" + BrushDim1;
                        text += ", " + dimensionNames[(int)BrushShape][1] + "=" + BrushDim2;
                        text += ", " + dimensionNames[(int)BrushShape][2] + "=" + BrushDim3;
                        worldEdit.Good(workspace.ToolName + " dimensions " + text + " set.");
                        GenBrush();
                        workspace.ResendBlockHighlights(worldEdit);
                    }
                    return true;
            }

            return false;
        }


        public override bool ApplyToolBuild(WorldEdit worldEdit, Block placedBlock, int oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack)
        {
            return PerformBrushAction(worldEdit, placedBlock, oldBlockId, blockSel, targetPos, withItemStack);
        }

        public bool PerformBrushAction(WorldEdit worldEdit, Block placedBlock, int oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack) { 
            if (BrushDim1 <= 0) return false;

            Block selectedBlock = blockSel.DidOffset ? blockAccessRev.GetBlock(blockSel.Position.AddCopy(blockSel.Face.Opposite)) : blockAccessRev.GetBlock(blockSel.Position);

            int selectedBlockId = selectedBlock.BlockId;

            if (oldBlockId >= 0)
            {
                worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);
            }
            


            EnumBrushMode brushMode = BrushMode;

            int blockId = placedBlock.BlockId;

            if (!worldEdit.MayPlace(blockAccessRev.GetBlock(blockId), brushPositions.Length)) return false;

            EnumDepthLimit depthLimit = DepthLimit;

            for (int i = 0; i < brushPositions.Length; i++)
            { 
                BlockPos dpos = targetPos.AddCopy(brushPositions[i].X, brushPositions[i].Y, brushPositions[i].Z);

                bool skip = false;
                switch (depthLimit)
                {
                    case EnumDepthLimit.Top1:
                        skip = blockAccessRev.GetBlockId(dpos) == 0 || blockAccessRev.GetBlockId(dpos.X, dpos.Y + 1, dpos.Z) != 0;
                        break;
                    case EnumDepthLimit.Top2:
                        skip = blockAccessRev.GetBlockId(dpos) == 0 || (blockAccessRev.GetBlockId(dpos.X, dpos.Y + 1, dpos.Z) != 0 && blockAccessRev.GetBlockId(dpos.X, dpos.Y + 2, dpos.Z) != 0);
                        break;
                    case EnumDepthLimit.Top3:
                        skip = blockAccessRev.GetBlockId(dpos) == 0 || (blockAccessRev.GetBlockId(dpos.X, dpos.Y + 1, dpos.Z) != 0 && blockAccessRev.GetBlockId(dpos.X, dpos.Y + 2, dpos.Z) != 0 && blockAccessRev.GetBlockId(dpos.X, dpos.Y + 3, dpos.Z) != 0);
                        break;
                    case EnumDepthLimit.Top4:
                        skip = blockAccessRev.GetBlockId(dpos) == 0 || (blockAccessRev.GetBlockId(dpos.X, dpos.Y + 1, dpos.Z) != 0 && blockAccessRev.GetBlockId(dpos.X, dpos.Y + 2, dpos.Z) != 0 && blockAccessRev.GetBlockId(dpos.X, dpos.Y + 3, dpos.Z) != 0 && blockAccessRev.GetBlockId(dpos.X, dpos.Y + 4, dpos.Z) != 0);
                        break;                    
                }
                if (skip) continue;


                switch (brushMode)
                {
                    case EnumBrushMode.ReplaceAir:
                        if (blockAccessRev.GetBlockId(dpos) == 0)
                        {
                            blockAccessRev.SetBlock(blockId, dpos, withItemStack);
                        }
                        break;

                    case EnumBrushMode.ReplaceNonAir:
                        if (blockAccessRev.GetBlockId(dpos) != 0)
                        {
                            blockAccessRev.SetBlock(blockId, dpos, withItemStack);
                        }
                        break;

                    case EnumBrushMode.ReplaceSelected:
                        if (blockAccessRev.GetBlockId(dpos) == selectedBlockId)
                        {
                            blockAccessRev.SetBlock(blockId, dpos, withItemStack);
                        }

                        break;

                    default:
                        blockAccessRev.SetBlock(blockId, dpos, withItemStack);
                        break;
                }
            }

            return true;
        }

        public override List<BlockPos> GetBlockHighlights(WorldEdit worldEdit)
        {
            return new List<BlockPos>(brushPositions);
        }
        

        internal void GenBrush()
        {
            List<BlockPos> positions = new List<BlockPos>();

            float dim1 = BrushDim1;
            float dim2 = BrushDim2;
            float dim3 = BrushDim3;
            if (dim2 == 0) dim2 = dim1;
            if (dim3 == 0) dim3 = dim2;

            int xRadInt = (int)Math.Ceiling(dim1);
            int yRadInt = (int)Math.Ceiling(dim2);
            int zRadInt = (int)Math.Ceiling(dim3);

            int dim1Int = (int)dim1;
            int dim2Int = (int)dim2;
            int dim3Int = (int)dim3;

            float xRadSqInv = 1f / (dim1 * dim1);
            float yRadSqInv = 1f / (dim2 * dim2);
            float zRadSqInv = 1f / (dim3 * dim3);


            size = new Vec3i((int)Math.Ceiling(dim1), (int)Math.Ceiling(dim2), (int)Math.Ceiling(dim3));

            int cutoutDim1Int = (int)CutoutDim1;
            int cutoutDim2Int = (int)CutoutDim2;
            int cutoutDim3Int = (int)CutoutDim3;

            float xCutRadSqInv = 1f / (CutoutDim1 * CutoutDim1);
            float yCutRadSqInv = 1f / (CutoutDim2 * CutoutDim2);
            float zCutRadSqInv = 1f / (CutoutDim3 * CutoutDim3);

            int x, y, z;

            switch (BrushShape)
            {
                case EnumBrushShape.Ball:
                    for (int dx = -xRadInt; dx <= xRadInt; dx++)
                    {
                        for (int dy = -yRadInt; dy <= yRadInt; dy++)
                        {
                            for (int dz = -zRadInt; dz <= zRadInt; dz++)
                            {
                                if (dx * dx * xRadSqInv + dy * dy * yRadSqInv + dz * dz * zRadSqInv > 1) continue;
                                if (dx * dx * xCutRadSqInv + dy * dy * yCutRadSqInv + dz * dz * zCutRadSqInv < 1) continue;

                                positions.Add(new BlockPos(dx, dy, dz));
                            }
                        }
                    }

                    size = new Vec3i((int)Math.Ceiling(2 * dim1), (int)Math.Ceiling(2 * dim2), (int)Math.Ceiling(2 * dim3));

                    break;

                case EnumBrushShape.Cuboid:

                    int notminx = (dim1Int - cutoutDim1Int) / 2; 
                    int notmaxx = notminx + cutoutDim1Int;

                    int notminy = (dim2Int - cutoutDim2Int) / 2;
                    int notmaxy = notminy + cutoutDim2Int;

                    int notminz = (dim3Int - cutoutDim3Int) / 2;
                    int notmaxz = notminz + cutoutDim3Int;


                    for (int dx = 0; dx < dim1Int; dx++)
                    {
                        for (int dy = 0; dy < dim2Int; dy++)
                        {
                            for (int dz = 0; dz < dim3Int; dz++)
                            {
                                if (dx >= notminx && dx < notmaxx && dy >= notminy && dy < notmaxy && dz >= notminz && dz < notmaxz) continue;

                                x = dx - dim1Int / 2;
                                y = dy - dim2Int / 2;
                                z = dz - dim3Int / 2;

                                positions.Add(new BlockPos(x, y, z));
                            }
                        }
                    }

                    break;

                case EnumBrushShape.Cylinder:
                    
                    for (int dx = -xRadInt; dx <= xRadInt; dx++)
                    {
                        for (int dz = -zRadInt; dz <= zRadInt; dz++)
                        {
                            if (dx * dx * xRadSqInv + dz * dz * zRadSqInv > 1) continue;
                            if (dx * dx * xCutRadSqInv + dz * dz * zCutRadSqInv < 1) continue;

                            for (int dy = 0; dy < dim2Int; dy++) 
                            {
                                y = dy - dim2Int / 2;
                                if (Math.Abs(y) < cutoutDim2Int) continue;
                                positions.Add(new BlockPos(dx, y, dz));
                            }
                        }
                    }

                    size = new Vec3i((int)Math.Ceiling(2 * dim1), (int)Math.Ceiling(dim2), (int)Math.Ceiling(2 * dim3));
                    break;

                /// North: Negative Z
                /// East: Positive X
                /// South: Positive Z
                /// West: Negative X
                case EnumBrushShape.HalfBallUp:
                    positions = HalfBall(-xRadInt, 0, -zRadInt, xRadInt, yRadInt, zRadInt, 0, -yRadInt / 2, 0, xRadSqInv, yRadSqInv, zRadSqInv, xCutRadSqInv, yCutRadSqInv, zCutRadSqInv);

                    size = new Vec3i((int)Math.Ceiling(2 * dim1), (int)Math.Ceiling(1 * dim2), (int)Math.Ceiling(2 * dim3));
                    break;

                case EnumBrushShape.HalfBallDown:
                    positions = HalfBall(-xRadInt, -yRadInt, -zRadInt, xRadInt, 0, zRadInt, 0, yRadInt / 2, 0, xRadSqInv, yRadSqInv, zRadSqInv, xCutRadSqInv, yCutRadSqInv, zCutRadSqInv);

                    size = new Vec3i((int)Math.Ceiling(2 * dim1), (int)Math.Ceiling(1 * dim2), (int)Math.Ceiling(2 * dim3));
                    break;

                case EnumBrushShape.HalfBallWest:
                    positions = HalfBall(-xRadInt, -yRadInt, -zRadInt, 0, yRadInt, zRadInt, xRadInt / 2, 0, 0, xRadSqInv, yRadSqInv, zRadSqInv, xCutRadSqInv, yCutRadSqInv, zCutRadSqInv);

                    size = new Vec3i((int)Math.Ceiling(1 * dim1), (int)Math.Ceiling(2 * dim2), (int)Math.Ceiling(2 * dim3));
                    break;

                case EnumBrushShape.HalfBallSouth:
                    positions = HalfBall(-xRadInt, -yRadInt, 0, xRadInt, yRadInt, zRadInt, 0, 0, -zRadInt / 2, xRadSqInv, yRadSqInv, zRadSqInv, xCutRadSqInv, yCutRadSqInv, zCutRadSqInv);

                    size = new Vec3i((int)Math.Ceiling(2 * dim1), (int)Math.Ceiling(2 * dim2), (int)Math.Ceiling(1 * dim3));
                    break;

                case EnumBrushShape.HalfBallNorth:
                    positions = HalfBall(-xRadInt, -yRadInt, -zRadInt, xRadInt, yRadInt, 0, 0, 0, zRadInt / 2, xRadSqInv, yRadSqInv, zRadSqInv, xCutRadSqInv, yCutRadSqInv, zCutRadSqInv);

                    size = new Vec3i((int)Math.Ceiling(2 * dim1), (int)Math.Ceiling(2 * dim2), (int)Math.Ceiling(1 * dim3));
                    break;

                case EnumBrushShape.HalfBallEast:
                    positions = HalfBall(0, -yRadInt, -zRadInt, xRadInt, yRadInt, zRadInt, -xRadInt / 2, 0, 0, xRadSqInv, yRadSqInv, zRadSqInv, xCutRadSqInv, yCutRadSqInv, zCutRadSqInv);

                    size = new Vec3i((int)Math.Ceiling(1 * dim1), (int)Math.Ceiling(2 * dim2), (int)Math.Ceiling(1 * dim3));
                    break;
            }


            this.brushPositions = positions.ToArray();
        }


        List<BlockPos> HalfBall(int minx, int miny, int minz, int maxx, int maxy, int maxz, int offX, int offY, int offZ, float xRadSqInv, float yRadSqInv, float zRadSqInv, float xCutRadSqInv, float yCutRadSqInv, float zCutRadSqInv)
        {
            List<BlockPos> positions = new List<BlockPos>();

            size = new Vec3i(maxx - minx, maxy - miny, maxz - minz);

            for (int dx = minx; dx <= maxx; dx++)
            {
                for (int dy = miny; dy <= maxy; dy++)
                {
                    for (int dz = minz; dz <= maxz; dz++)
                    {
                        if (dx * dx * xRadSqInv + dy * dy * yRadSqInv + dz * dz * zRadSqInv > 1) continue;
                        if (dx * dx * xCutRadSqInv + dy * dy * yCutRadSqInv + dz * dz * zCutRadSqInv < 1) continue;

                        positions.Add(new BlockPos(dx + offX, dy + offY, dz + offZ));
                    }
                }
            }

            return positions;
        }

        

    }
}
