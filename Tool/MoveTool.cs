using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VSCreativeMod;

#nullable disable

namespace Vintagestory.ServerMods.WorldEdit
{
    public class MoveTool : ToolBase
    {
        public override bool ScrollEnabled => true;

        public EnumOrigin Origin
        {
            get { return (EnumOrigin)workspace.IntValues["std.moveToolOrigin"]; }
            set { workspace.IntValues["std.moveToolOrigin"] = (int)value; }
        }

        public MoveTool()
        {
            // keep for client side GetAvailableModes
        }

        public MoveTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            if (!workspace.IntValues.ContainsKey("std.moveToolOrigin")) Origin = EnumOrigin.BottomCenter;
        }

        public override bool OnWorldEditCommand(WorldEdit worldEdit, TextCommandCallingArgs callerArgs)
        {
            var player = (IServerPlayer)callerArgs.Caller.Player;
            var args = callerArgs.RawArgs;
            var cmd = args.PopWord();
            switch (cmd)
            {
                case "north":
                case "east":
                case "west":
                case "south":
                case "up":
                case "down":
                {
                    if (CreatePreview(worldEdit, player))
                    {
                        return true;
                    }
                    BlockFacing facing = BlockFacing.FromCode(cmd);
                    Move(facing.Normali);
                    return true;
                }
                case "look":
                {
                    if (CreatePreview(worldEdit, player))
                    {
                        return true;
                    }
                    var lookVec = player.Entity.SidedPos.GetViewVector();
                    var facing = BlockFacing.FromVector(lookVec.X, lookVec.Y, lookVec.Z);
                    Move(facing.Normali);
                    return true;
                }
                case "imr":
                    if (CreatePreview(worldEdit, player))
                    {
                        return true;
                    }
                    int angle = 90;

                    if (args.Length > 0)
                    {
                        if (!int.TryParse(args[0], out angle))
                        {
                            WorldEdit.Bad(player, "Invalid Angle (not a number)");
                            return true;
                        }
                    }

                    if (angle < 0) angle += 360;

                    if (angle != 0 && angle != 90 && angle != 180 && angle != 270)
                    {
                        WorldEdit.Bad(player, "Invalid Angle, allowed values are -270, -180, -90, 0, 90, 180 and 270");
                        return true;
                    }

                    workspace.PreviewBlockData.TransformWhilePacked(worldEdit.sapi.World, EnumOrigin.BottomCenter, angle, null);
                    workspace.ResendBlockHighlights();

                    return true;
                case "imo":
                    Origin = EnumOrigin.BottomCenter;

                    if (args.Length > 0)
                    {
                        int.TryParse(args[0], out int origin);
                        if (Enum.IsDefined(typeof(EnumOrigin), origin))
                        {
                            Origin = (EnumOrigin)origin;
                        }
                    }

                    workspace.ResendBlockHighlights();
                    WorldEdit.Good(player, "Paste origin " + Origin + " set.");
                    return true;
                case "preview":
                {
                    CreatePreview(worldEdit, player);
                    return true;
                }
                case "apply":
                case "place":
                case "commit":
                {
                    if (MoveBlocks(worldEdit))
                    {
                        return true;
                    }
                    workspace.ResendBlockHighlights();
                    return true;
                }
            }

            return false;
        }

        private bool CreatePreview(WorldEdit worldEdit, IServerPlayer player)
        {
            if (workspace.PreviewBlockData != null) return false;
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                WorldEdit.Bad(player, "Please select a area first and create a preview");
                return true;
            }

            workspace.PreviewBlockData = CopyArea(worldEdit.sapi, workspace.StartMarker, workspace.EndMarker);
            if (workspace.PreviewPos == null)
            {
                workspace.PreviewPos = workspace.StartMarker.Copy();
            }

            workspace.CreatePreview(workspace.PreviewBlockData, workspace.PreviewPos);
            return false;
        }

        private bool MoveBlocks(WorldEdit worldEdit)
        {
            if (workspace.WorldEditConstraint == EnumWorldEditConstraint.Selection)
            {
                var player = worldEdit.sapi.World.PlayerByUid(workspace.PlayerUID);
                (player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, "You can not use the move tool while the worldedit constain is enabled", EnumChatType.OthersMessage);
                return true;
            }

            if (workspace.PreviewBlockData == null || workspace.PreviewPos == null || workspace.StartMarker == null || workspace.EndMarker == null) return true;

            var startPos = workspace.PreviewBlockData.GetStartPos(workspace.PreviewPos, EnumOrigin.StartPos);

            workspace.revertableBlockAccess.BeginMultiEdit();
            workspace.FillArea(null, workspace.StartMarker, workspace.EndMarker);

            workspace.PreviewBlockData.Init(ba);
            workspace.PreviewBlockData.Place(ba, worldEdit.sapi.World, startPos, EnumReplaceMode.ReplaceAll, WorldEdit.ReplaceMetaBlocks);

            workspace.PreviewBlockData.PlaceDecors(ba, startPos);
            ba.Commit();
            workspace.PreviewBlockData.PlaceEntitiesAndBlockEntities(ba, worldEdit.sapi.World, startPos,
                workspace.PreviewBlockData.BlockCodes, workspace.PreviewBlockData.ItemCodes, false, null, 0, null, WorldEdit.ReplaceMetaBlocks);
            ba.CommitBlockEntityData();

            workspace.PreviewPos = startPos.Copy();
            workspace.StartMarker = startPos.Copy();
            workspace.EndMarker = startPos.AddCopy(workspace.PreviewBlockData.SizeX, workspace.PreviewBlockData.SizeY, workspace.PreviewBlockData.SizeZ);
            workspace.StartMarkerExact = workspace.StartMarker.ToVec3d().Add(0.5);
            workspace.EndMarkerExact = workspace.StartMarkerExact.AddCopy(workspace.PreviewBlockData.SizeX, workspace.PreviewBlockData.SizeY, workspace.PreviewBlockData.SizeZ).Add(-1);


            workspace.revertableBlockAccess.EndMultiEdit();
            workspace.PreviewPos = null;
            workspace.PreviewBlockData = null;

            workspace.DestroyPreview();
            return false;
        }

        private void Move(Vec3i dir)
        {
            if (workspace.PreviewPos == null)
            {
                if (workspace.StartMarker == null) return;
                workspace.PreviewPos = workspace.StartMarker.Copy();
            }

            var vec = dir * workspace.StepSize;

            workspace.PreviewPos.Add(vec);
            workspace.SendPreviewOriginToClient(workspace.PreviewPos, workspace.previewBlocks.subDimensionId);
        }

        public override void OnInteractStart(WorldEdit worldEdit, BlockSelection blockSelection)
        {
            if (workspace.PreviewBlockData == null || workspace.PreviewPos == null || workspace.StartMarker == null || workspace.EndMarker == null) return;

            MoveBlocks(worldEdit);
            workspace.ResendBlockHighlights();
        }

        public override void OnAttackStart(WorldEdit worldEdit, BlockSelection blockSelection)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null) return;

            if (workspace.PreviewPos == null)
            {
                var player = worldEdit.sapi.World.PlayerByUid(workspace.PlayerUID);
                workspace.PreviewPos = player.Entity.Pos.AsBlockPos;
            }

            var offset = (blockSelection.Position - workspace.PreviewPos).ToVec3i();

            offset.Add(blockSelection.Face);

            workspace.PreviewPos.Add(offset);

            if (workspace.PreviewBlockData == null)
            {
                workspace.PreviewBlockData = CopyArea(worldEdit.sapi, workspace.StartMarker, workspace.EndMarker);
                workspace.PreviewPos = workspace.PreviewBlockData.AdjustStartPos(workspace.PreviewPos, Origin);
                workspace.CreatePreview(workspace.PreviewBlockData, workspace.PreviewPos);
            }
            else
            {
                workspace.PreviewPos = workspace.PreviewBlockData.AdjustStartPos(workspace.PreviewPos, Origin);
                workspace.CreatePreview(workspace.PreviewBlockData, workspace.PreviewPos);
            }
        }

        public override Vec3i Size => new(0, 0, 0);

        private BlockSchematic CopyArea(ICoreServerAPI api, BlockPos start, BlockPos end)
        {
            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            BlockSchematic blockdata = new BlockSchematic();
            blockdata.AddArea(api.World, start, end);
            blockdata.Pack(api.World, startPos);
            return blockdata;
        }

        public override List<SkillItem> GetAvailableModes(ICoreClientAPI capi)
        {
            var move = EnumWeToolMode.Move.ToString();
            var rotate = EnumWeToolMode.Rotate.ToString();
            var multilineItems = new List<SkillItem>()
            {
                new()
                {
                    Name = Lang.Get(move),
                    Code = new AssetLocation(move)
                },
                new()
                {
                    Texture = capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/worldedit/rotate.svg"), 48, 48, 5, ColorUtil.WhiteArgb),
                    Name = Lang.Get(rotate),
                    Code = new AssetLocation(rotate)
                }
            };
            multilineItems[0].WithIcon(capi, "move");
            return multilineItems;
        }

        public override void Unload(ICoreAPI api)
        {
            workspace.PreviewPos = null;
            workspace.PreviewBlockData = null;
        }
    }
}
