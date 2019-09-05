using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public enum EnumLineStartPoint
    {
        AsDefined,
        LineStrip
    }

    public class LineTool : ToolBase
    {
        BlockPos startPos;

        public EnumLineStartPoint LineMode
        {
            get { return (EnumLineStartPoint)workspace.IntValues["std.lineStartPoint"]; }
            set { workspace.IntValues["std.lineStartPoint"] = (int)value; }
        }

        public bool PlaceMode
        {
            get { return workspace.IntValues["std.lineRemove"] == 1; }
            set { workspace.IntValues["std.lineRemove"] = value ? 1 : 0; }
        }


        public LineTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            if (!workspace.IntValues.ContainsKey("std.lineStartPoint")) LineMode = EnumLineStartPoint.LineStrip;
            if (!workspace.IntValues.ContainsKey("std.lineRemove")) PlaceMode = false;
        }

        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            switch (args[0])
            {
                case "tremove":
                    PlaceMode = args.Length > 1 && (args[1] == "1" || args[1] == "on");
                    worldEdit.Good(workspace.ToolName + " remove mode now " + (PlaceMode ? "on" : "off"));
                    return true;

                case "tm":
                    EnumLineStartPoint startpoint = EnumLineStartPoint.LineStrip;

                    if (args.Length > 1)
                    {
                        int index;
                        int.TryParse(args[1], out index);
                        if (Enum.IsDefined(typeof(EnumLineStartPoint), index))
                        {
                            startpoint = (EnumLineStartPoint)index;
                        }
                    }

                    LineMode = startpoint;
                    worldEdit.Good(workspace.ToolName + " mode " + startpoint + " set.");
                    worldEdit.ResendBlockHighlights();

                    return true;
            }

            return false;
        }

        public override void OnBreak(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel)
        {
            startPos = blockSel.Position.Copy();
            worldEdit.Good("Line Tool start position set");

            int blockId = blockAccessRev.GetBlockId(blockSel.Position);
            worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);
            blockAccessRev.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, oldBlockId);
            blockAccessRev.Commit();
        }

        public override void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (startPos == null) return;

            BlockPos destPos = blockSel.Position.AddCopy(blockSel.Face.GetOpposite());

            Block block = blockAccessRev.GetBlock(blockSel.Position);
            if (PlaceMode) block = blockAccessRev.GetBlock(0);
            worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);

            if (!worldEdit.MayPlace(block, startPos.ManhattenDistance(destPos))) return;

            plotLine3d(block.BlockId, withItemStack, startPos.X, startPos.Y, startPos.Z, destPos.X, destPos.Y, destPos.Z);

            if (LineMode == EnumLineStartPoint.LineStrip) startPos = destPos.Copy();

            blockAccessRev.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, blockAccessRev.GetBlockId(blockSel.Position));
            blockAccessRev.Commit();

        }

        public override Vec3i Size
        {
            get { return new Vec3i(0, 0, 0); }
        }
        
        // http://members.chello.at/~easyfilter/bresenham.html
        void plotLine3d(int blockId, ItemStack withItemStack, int x0, int y0, int z0, int x1, int y1, int z1)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int dz = Math.Abs(z1 - z0), sz = z0 < z1 ? 1 : -1;
            int dm = GameMath.Max(dx, dy, dz), i = dm; /* maximum difference */
            x1 = y1 = z1 = dm / 2; /* error offset */

            BlockPos pos = new BlockPos();

            for (;;)
            {  /* loop */
                pos.Set(x0, y0, z0);
                blockAccessRev.SetBlock(blockId, pos, withItemStack);
                if (i-- == 0) break;
                x1 -= dx; if (x1 < 0) { x1 += dm; x0 += sx; }
                y1 -= dy; if (y1 < 0) { y1 += dm; y0 += sy; }
                z1 -= dz; if (z1 < 0) { z1 += dm; z0 += sz; }
            }
        }

    }
}
