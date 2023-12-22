using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public enum EnumMoveToolMode
    {
        MoveBlocks = 0,
        MoveSelection = 1,
    }


    public class MoveTool : ToolBase
    {
        public virtual string Prefix { get { return "std.move"; } }
        

        public EnumMoveToolMode MoveRepeatMode
        {
            get { return (EnumMoveToolMode)workspace.IntValues[Prefix + "Mode"]; }
            set { workspace.IntValues[Prefix + "Mode"] = (int)value; }
        }

        public int Amount
        {
            get { return workspace.IntValues[Prefix + "Amount"]; }
            private set { workspace.IntValues[Prefix + "Amount"] = value; }
        }

        public MoveTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            if (!workspace.IntValues.ContainsKey(Prefix + "Mode")) MoveRepeatMode = EnumMoveToolMode.MoveBlocks;
            if (!workspace.IntValues.ContainsKey(Prefix + "Amount")) Amount = 1;
        }
        


        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            string cmd = args.PopWord();
            switch (cmd)
            {
                case "tm":
                    {
                        EnumMoveToolMode mode = EnumMoveToolMode.MoveBlocks;

                        if (args.Length > 0)
                        {
                            int index;
                            int.TryParse(args[0], out index);
                            if (Enum.IsDefined(typeof(EnumMoveToolMode), index))
                            {
                                mode = (EnumMoveToolMode)index;
                            }
                        }

                        MoveRepeatMode = mode;
                        worldEdit.Good(Lang.Get("Tool mode now set to {0}", mode));
                        return true;
                    }

                case "am":
                    {
                        Amount = (int)args.PopInt(1);
                        worldEdit.Good(Lang.Get("Amount set to {0}", Amount));
                        return true;
                    }

                case "north":
                case "east":
                case "west":
                case "south":
                case "up":
                case "down":
                    {
                        BlockFacing facing = BlockFacing.FromCode(cmd);
                        Handle(worldEdit, facing.Normali);
                        return true;
                    }

                case "look":
                    {
                        var player = worldEdit.sapi.World.PlayerByUid(workspace.PlayerUID);
                        var lookVec = player.Entity.SidedPos.GetViewVector();
                        var facing = BlockFacing.FromVector(lookVec.X, lookVec.Y, lookVec.Z);
                        Handle(worldEdit, facing.Normali);
                        return true;
                    }
            }

            return false;
        }


        private void Handle(WorldEdit worldedit, Vec3i dir)
        {
            Vec3i vec = dir * Amount;

            switch (MoveRepeatMode)
            {
                case EnumMoveToolMode.MoveBlocks:
                    worldedit.MoveArea(vec, workspace.StartMarker, workspace.EndMarker);
                    workspace.ResendBlockHighlights(worldedit);
                    break;

                case EnumMoveToolMode.MoveSelection:
                    workspace.StartMarker.Add(vec);
                    workspace.EndMarker.Add(vec);
                    workspace.revertableBlockAccess.StoreHistoryState(HistoryState.Empty());
                    workspace.ResendBlockHighlights(worldedit);
                    break;
            }
        }



        public override void OnInteractStart(WorldEdit worldEdit, BlockSelection blockSelection)
        {
            if (blockSelection == null || workspace.StartMarker == null || workspace.EndMarker == null) return;

            BlockPos center = (workspace.StartMarker + workspace.EndMarker) / 2;
            center.Y = Math.Min(workspace.StartMarker.Y, workspace.EndMarker.Y);

            Vec3i offset = (blockSelection.Position - center).ToVec3i();
            offset.Add(blockSelection.Face);
            var amount = Amount;
            Amount = 1;
            Handle(worldEdit, offset);
            Amount = amount;
        }




        public override Vec3i Size
        {
            get { return new Vec3i(0, 0, 0); }
        }
    }
}
