using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods.WorldEdit
{
    public enum EnumRepeatToolMode
    {
        Repeat = 0,
        Mirror = 1,
    }

    public enum EnumRepeatSelectionMode
    {
        Keep = 0,
        Move = 1,
        Grow = 2
    }

    public class RepeatTool : ToolBase
    {
        public virtual string Prefix { get { return "std.repeat"; } }

        
        public EnumRepeatToolMode RepeatMode
        {
            get { return (EnumRepeatToolMode)workspace.IntValues[Prefix + "Mode"]; }
            set { workspace.IntValues[Prefix + "Mode"] = (int)value; }
        }

        public EnumRepeatSelectionMode SelectionMode
        {
            get { return (EnumRepeatSelectionMode)workspace.IntValues[Prefix + "SelectionMode"]; }
            set { workspace.IntValues[Prefix + "SelectionMode"] = (int)value; }
        }

        public int Amount
        {
            get { return workspace.IntValues[Prefix + "Amount"]; }
            private set { workspace.IntValues[Prefix + "Amount"] = value; }
        }
        

        public RepeatTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            if (!workspace.IntValues.ContainsKey(Prefix + "Mode")) RepeatMode = EnumRepeatToolMode.Repeat;
            if (!workspace.IntValues.ContainsKey(Prefix + "Amount")) Amount = 1;
            if (!workspace.IntValues.ContainsKey(Prefix + "SelectionMode")) SelectionMode = EnumRepeatSelectionMode.Keep;
        }
        


        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            string cmd = args.PopWord();
            switch (cmd)
            {
                case "tm":
                    {
                        EnumRepeatToolMode mode = EnumRepeatToolMode.Repeat;

                        if (args.Length > 0)
                        {
                            int index;
                            int.TryParse(args[0], out index);
                            if (Enum.IsDefined(typeof(EnumRepeatToolMode), index))
                            {
                                mode = (EnumRepeatToolMode)index;
                            }
                        }

                        RepeatMode = mode;
                        worldEdit.Good(Lang.Get("Repeat Tool mode now set to {0}", mode));
                        return true;
                    }

                case "sm":
                    {
                        EnumRepeatSelectionMode mode = EnumRepeatSelectionMode.Keep;

                        if (args.Length > 0)
                        {
                            int index;
                            int.TryParse(args[0], out index);
                            if (Enum.IsDefined(typeof(EnumRepeatSelectionMode), index))
                            {
                                mode = (EnumRepeatSelectionMode)index;
                            }
                        }

                        SelectionMode = mode;
                        worldEdit.Good(Lang.Get("Repeat Tool Selection mode now set to {0}", mode));
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
                        Handle(worldEdit, BlockFacing.FromCode(cmd), Amount);
                        return true;
                    }
            }

            return false;
        }


        private void Handle(WorldEdit worldedit, BlockFacing blockFacing, int amount)
        {
            Vec3i vec = blockFacing.Normali;
            bool selectNewArea = SelectionMode == EnumRepeatSelectionMode.Move;
            bool growToArea = SelectionMode == EnumRepeatSelectionMode.Grow;

            switch (RepeatMode)
            {
                case EnumRepeatToolMode.Mirror:
                    worldedit.MirrorArea(workspace.GetMarkedMinPos(), workspace.GetMarkedMaxPos(), blockFacing, selectNewArea, growToArea);
                    break;

                case EnumRepeatToolMode.Repeat:
                    worldedit.RepeatArea(workspace.GetMarkedMinPos(), workspace.GetMarkedMaxPos(), vec, amount, selectNewArea, growToArea);
                    break;
            }
        }


        public override void OnInteractStart(WorldEdit worldEdit, BlockSelection blockSelection)
        {
            if (blockSelection == null) return;

            BlockPos center = (workspace.StartMarker + workspace.EndMarker) / 2;
            center.Y = Math.Min(workspace.StartMarker.Y, workspace.EndMarker.Y);

            Vec3i offset = (blockSelection.Position - center).ToVec3i();
            BlockFacing facing;
            int amount;

            if (Math.Abs(offset.X) > Math.Abs(offset.Y))
            {
                if (Math.Abs(offset.X) > Math.Abs(offset.Z))
                {
                    facing = offset.X >= 0 ? BlockFacing.EAST : BlockFacing.WEST;
                    amount = Math.Abs(offset.X) / Math.Abs(workspace.StartMarker.X - workspace.EndMarker.X);
                }
                else
                {
                    facing = offset.Z >= 0 ? BlockFacing.SOUTH : BlockFacing.NORTH;
                    amount = Math.Abs(offset.Z) / Math.Abs(workspace.StartMarker.Z - workspace.EndMarker.Z);
                }
            } else
            {
                if (Math.Abs(offset.Y) > Math.Abs(offset.Z))
                {
                    facing = offset.Y >= 0 ? BlockFacing.UP : BlockFacing.DOWN;
                    amount = Math.Abs(offset.Y) / Math.Abs(workspace.StartMarker.Y - workspace.EndMarker.Y);
                } else
                {
                    facing = offset.Z >= 0 ? BlockFacing.SOUTH : BlockFacing.NORTH;
                    amount = Math.Abs(offset.Z) / Math.Abs(workspace.StartMarker.Z - workspace.EndMarker.Z);
                }
            }
            

            Handle(worldEdit, facing, amount);
        }


        public override Vec3i Size
        {
            get { return new Vec3i(0, 0, 0); }
        }
    }
}
