using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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

        public LineTool()
        {
        }

        public LineTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            if (!workspace.IntValues.ContainsKey("std.lineStartPoint")) LineMode = EnumLineStartPoint.LineStrip;
            if (!workspace.IntValues.ContainsKey("std.lineRemove")) PlaceMode = false;
        }

        public override bool OnWorldEditCommand(WorldEdit worldEdit, TextCommandCallingArgs callerArgs)
        {
            var player = (IServerPlayer)callerArgs.Caller.Player;
            var args = callerArgs.RawArgs;
            switch (args[0])
            {
                case "tremove":
                    PlaceMode = args.Length > 1 && (args[1] == "1" || args[1] == "on");
                    WorldEdit.Good(player, workspace.ToolName + " remove mode now " + (PlaceMode ? "on" : "off"));
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
                    WorldEdit.Good(player, workspace.ToolName + " mode " + startpoint + " set.");
                    workspace.ResendBlockHighlights();

                    return true;
            }

            return false;
        }

        public override void OnBreak(WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            startPos = blockSel.Position.Copy();
            var player = (IServerPlayer)worldEdit.sapi.World.PlayerByUid(workspace.PlayerUID);
            WorldEdit.Good(player, "Line Tool start position set");
        }

        public override void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (startPos == null) return;

            var destPos = blockSel.Position.AddCopy(blockSel.Face.Opposite);

            var block = PlaceMode ? ba.GetBlock(0) : withItemStack.Block;

            worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);

            if (!workspace.MayPlace(block, startPos.ManhattenDistance(destPos))) return;

            GameMath.BresenHamPlotLine3d(startPos.X, startPos.Y, startPos.Z, destPos.X, destPos.Y, destPos.Z, (pos) => ba.SetBlock(block.BlockId, pos, withItemStack));

            if (LineMode == EnumLineStartPoint.LineStrip) startPos = destPos.Copy();

            ba.Commit();
        }

        public override Vec3i Size
        {
            get { return new Vec3i(0, 0, 0); }
        }
    }
}
