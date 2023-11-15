using System;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods.WorldEdit
{
    public partial class WorldEdit : ModSystem
    {
        protected void registerCommands()
        {
            var parsers = sapi.ChatCommands.Parsers;

            sapi.ChatCommands
                .GetOrCreate("we")
                .IgnoreAdditionalArgs()
                .HandleWith(onToolCommand)
                .RequiresPrivilege("worldedit")
                .WithPreCondition(loadWorkSpace)
                .WithDesc("Creative mode world editing tools")
                .BeginSub("import-rotation")
                    .WithDesc("Set data import angle")
                    .WithAlias("impr")
                    .WithArgs(parsers.WordRange("angle", "-270", "-180", "-90", "0", "90", "180", "270"))
                    .HandleWith(handleImpr)
                .EndSub()
                .BeginSub("import-flip")
                    .WithDesc("Set data import flip mode")
                    .WithAlias("impflip")
                    .HandleWith(handleImpflip)
                .EndSub()
                .BeginSub("constrain")
                    .WithDesc("Constrain all world edit operations")
                    .WithAlias("cs")
                    .WithArgs(parsers.WordRange("constraint type", "none", "selection"))
                    .HandleWith(handleConstrain)
                .EndSub()
                .BeginSub("copy")
                    .WithDesc("Copy marked position to server clipboard")
                    .WithAlias("mcopy").WithAlias("c")
                    .HandleWith(handleMcopy)
                .EndSub()
                .BeginSub("testlaunch")
                    .WithDesc("Copy marked position to movable chunks (like a ship) and delete original")
                    .HandleWith(handleLaunch)
                .EndSub()
                .BeginSub("scp")
                    .WithDesc("Copy a /we mark command text to your local clipboard")
                    .WithAlias("mposcopy").WithAlias("selection-clipboard")
                    .HandleWith(handlemPosCopy)
                .EndSub()
                .BeginSub("paste")
                    .WithDesc("Paste server clipboard data")
                    .WithAlias("mpaste").WithAlias("p").WithAlias("v")
                    .HandleWith(handlemPaste)
                .EndSub()
                .BeginSub("cbi")
                    .WithDesc("Information about marked area in server clipoard")
                    .WithAlias("clipboard-info").WithAlias("cbinfo")
                    .HandleWith(handleCbInfo)
                .EndSub()
                .BeginSub("block")
                    .WithDesc("Places a block below the caller")
                    .WithAlias("b")
                    .HandleWith(handleBlock)
                .EndSub()
                .BeginSub("relight")
                    .WithDesc("Toggle server block relighting. Speeds up operations when doing large scale worldedits")
                    .WithAlias("rl")
                    .WithArgs(parsers.OptionalBool("on/off"))
                    .HandleWith(handleRelight)
                .EndSub()
                .BeginSub("op")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithAlias("overload-protection").WithAlias("sovp")
                    .WithDesc("Toggle server overload protection")
                    .WithArgs(parsers.OptionalBool("on/off"))
                    .HandleWith(handleSovp)
                .EndSub()
                .BeginSub("undo")
                    .WithDesc("Undo last world edit action")
                    .WithAlias("u").WithAlias("z")
                    .WithArgs(parsers.OptionalInt("amount", 1))
                    .HandleWith(handleUndo)
                .EndSub()
                .BeginSub("redo")
                    .WithDesc("Redo last world edit action")
                    .WithAlias("r").WithAlias("y")
                    .WithArgs(parsers.OptionalInt("amount", 1))
                    .HandleWith(handleRedo)
                .EndSub()
                .BeginSub("on")
                    .WithDesc("Enabled world edit tool mode")
                    .HandleWith(handleToolModeOn)
                .EndSub()
                .BeginSub("off")
                    .WithDesc("Disable world edit tool mode")
                    .HandleWith(handleToolModeOff)
                .EndSub()
                .BeginSub("rebuild-rainmap")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithAlias("rebuildrainmap").WithAlias("rrm")
                    .WithDesc("Rebuild rainheightmap on all loaded chunks")
                    .HandleWith(handleRebuildRainmap)
                .EndSub()
                .BeginSub("tool")
                    .WithDesc("Select world edit tool mode")
                    .WithAlias("t")
                    .WithArgs(parsers.All("tool name"))
                    .HandleWith(handleSetTool)
                .EndSub()
                .BeginSub("tool-offset")
                    .WithDesc("Set tool offset mode")
                    .WithAlias("tom").WithAlias("to")
                    .WithArgs(parsers.Int("Mode index number"))
                    .HandleWith(handleTom)
                .EndSub()
                .BeginSub("pr")
                    .WithDesc("Set player picking range (default survival mode value is 4.5)")
                    .WithAlias("player-reach").WithAlias("range")
                    .WithArgs(parsers.DoubleRange("range", 0, 9999))
                    .HandleWith(handleRange)
                .EndSub()
                .BeginSub("export")
                    .WithDesc("Export marked area to server file system")
                    .WithAlias("export").WithAlias("mexp")
                    .WithArgs(parsers.Word("file name"), parsers.OptionalWord("'c' to also copy mark command to client clipboard"))
                    .HandleWith(handleMex)
                .EndSub()
                .BeginSub("export-client")
                    .WithDesc("Export selected area to client file system")
                    .WithAlias("mexc").WithAlias("expc")
                    .WithArgs(parsers.Word("file name"), parsers.OptionalWord("'c' to also copy mark command to client clipboard"))
                    .HandleWith(handleMexc)
                .EndSub()
                .BeginSub("mre")
                    .WithDesc("Relight selected area")
                    .WithAlias("relight-selection").WithAlias("rls")
                    .HandleWith(handleRelightMarked)
                .EndSub()
                .BeginSub("generate-multiblock-code")
                    .WithDesc("Generate multiblock code of selected area")
                    .WithAlias("mgencode").WithAlias("gmc")
                    .HandleWith(handleMgenCode)
                .EndSub()
                .BeginSub("import")
                    .WithDesc("Import schematic by filename to select area")
                    .WithAlias("imp")
                    .WithArgs(parsers.Word("file name"), parsers.OptionalWord("origin mode"))
                    .HandleWith(handleImport)
                .EndSub()
                .BeginSub("resolve-meta")
                    .WithDesc("Toggle resolve meta blocks mode during import")
                    .WithAlias("impres").WithAlias("rm")
                    .WithArgs(parsers.OptionalBool("on/off"))
                    .HandleWith(handleToggleImpres)
                .EndSub()
                
                .BeginSub("start")
                    .WithDesc("Mark start position for selection")
                    .WithAlias("ms").WithAlias("s").WithAlias("1")
                    .HandleWith(handleMarkStart)
                .EndSub()
                .BeginSub("end")
                    .WithDesc("Mark end position for selection")
                    .WithAlias("me").WithAlias("e").WithAlias("2")
                    .HandleWith(handleMarkEnd)
                .EndSub()
                .BeginSub("select")
                    .WithDesc("Select area by coordinates")
                    .WithAlias("mark")
                    .WithArgs(parsers.WorldPosition("start position"), parsers.WorldPosition("end position"))
                    .HandleWith(handleMark)
                .EndSub()
                .BeginSub("resize")
                    .WithDesc("Resize the current selection")
                    .WithAlias("res")
                    .WithArgs(parsers.Word("direction", new string[] { "north", "n", "z", "-x", "l (for look direction)" }), parsers.OptionalInt("amount", 1))
                    .HandleWith(handleResize)
                .EndSub()
                .BeginSubs("gn", "ge", "gs", "gw", "gu", "gd", "gl")
                    .WithDesc("Grow selection in given direction (gl for look direction)")
                    .WithArgs(parsers.OptionalInt("amount", 1))
                    .HandleWith(handleGrowSelection)
                .EndSub()
                .BeginSub("rotate")
                    .WithDesc("Rotate selected area")
                    .WithAlias("mr").WithAlias("rot")
                    .WithArgs(parsers.WordRange("angle", "-270", "-180", "-90", "0", "90", "180", "270"))
                    .HandleWith(handleRotateSelection)
                .EndSub()
                .BeginSub("mirror")
                    .WithDesc("Mirrors the current selection")
                    .WithAlias("mir")
                    .WithArgs(parsers.Word("direction", new string[] { "north", "n", "z", "-x", "l (for look direction)" }))
                    .HandleWith(handleMirrorSelection)
                .EndSub()
                .BeginSub("flip")
                    .WithDesc("Flip selected area in place")
                    .WithArgs(parsers.Word("direction", new string[] { "north", "n", "z", "-x", "l (for look direction)" }))
                    .HandleWith(handleFlipSelection)
                .EndSub()

                .BeginSubs("mmirn", "mmire", "mmirs", "mmirw", "mmiru", "mmird")
                    .WithDesc("Mirror selected area in given direction")
                    .WithArgs(parsers.OptionalWordRange("selection behavior (sn=select new area, gn=grow to include new area)", "sn", "gn"))
                    .HandleWith(handleMirrorShorthand)
                .EndSub()
                .BeginSub("repeat")
                    .WithAlias("rep")
                    .WithDesc("Repeat selected area in given direction")
                    .WithArgs(
                        parsers.Word("direction", new string[] { "north", "n", "z", "-x", "l (for look direction)" }), 
                        parsers.OptionalInt("amount", 1), 
                        parsers.OptionalWordRange("selection behavior (sn=select new area, gn=grow to include new area)", "sn", "gn")
                    )
                    .HandleWith(handleRepeatSelection)
                .EndSub()
                .BeginSubs("mprepn", "mprepe", "mprepe", "mpreps", "mprepu", "mprepd")
                    .WithDesc("Repeat selected area in given direction")
                    .WithArgs(parsers.OptionalInt("amount", 1), parsers.OptionalWordRange("selection behavior (sn=select new area, gn=grow to include new area)", "sn", "gn"))
                    .HandleWith(handleRepeatShorthand)
                .EndSub()
                .BeginSub("move")
                    .WithAlias("m")
                    .WithDesc("Move selected area in given direction")
                    .WithArgs(parsers.Word("direction", new string[] { "north", "n", "z", "-x", "l (for look direction)" }), parsers.OptionalInt("amount", 1))
                    .HandleWith(handleMoveSelection)
                .EndSub()
                .BeginSubs("mmn", "mme", "mms", "mms", "mmw", "mmu", "mmd")
                    .WithDesc("Move selected area in given direction")
                    .WithArgs(parsers.OptionalInt("amount", 1))
                    .HandleWith(handleMoveSelectionShorthand)
                .EndSub()
                .BeginSub("mmby")
                    .WithDesc("Move selected area by given amount")
                    .WithArgs(parsers.IntDirection("direction"))
                    .HandleWith(handleMoveSelectionBy)
                .EndSub()
                .BeginSub("shift")
                    .WithDesc("Shift current selection by given amount (does not move blocks, only the selection)")
                    .WithArgs(parsers.Word("direction", new string[] { "north", "n", "z", "-x", "l (for look direction)" }), parsers.OptionalInt("amount", 1))
                    .HandleWith(handleShiftSelection)
                .EndSub()
                .BeginSubs("smn", "sme", "sms", "sms", "smw", "smu", "smd")
                    .WithDesc("Shift current selection in given direction")
                    .WithArgs(parsers.OptionalInt("amount", 1))
                    .HandleWith(handleShiftSelectionShorthand)
                .EndSub()
                .BeginSub("smby")
                    .WithDesc("Shift current selection by given amount")
                    .WithArgs(parsers.IntDirection("direction"))
                    .HandleWith(handleShiftSelectionBy)
                .EndSub()
                .BeginSub("clear")
                    .WithAlias("mc").WithAlias("cs")
                    .WithDesc("Clear current selection. Does not remove blocks, only the selection.")
                    .HandleWith(handleClearSelection)
                .EndSub()
                .BeginSub("info")
                    .WithAlias("minfo").WithAlias("info-selection").WithAlias("is")
                    .WithDesc("Info about your current selection")
                    .HandleWith(handleSelectionInfo)
                .EndSub()
                .BeginSub("fill")
                    .WithAlias("mfill").WithAlias("f")
                    .WithDesc("Fill current selection with the block you are holding")
                    .HandleWith(handleFillSelection)
                .EndSub()
                .BeginSub("delete")
                    .WithAlias("mdelete").WithAlias("del")
                    .WithDesc("Delete all blocks and entities inside your current selection")
                    .HandleWith(handleDeleteSelection)
                .EndSub()
                .BeginSub("pacify-water")
                    .WithAlias("mpacifywater").WithAlias("pw")
                    .WithDesc("Pacify water in selected area. Turns all flowing water block into still water.")
                    .WithArgs(parsers.OptionalWord("liquid code (default: water)"))
                    .HandleWith(handlePacifyWater)
                .EndSub()
                .BeginSub("deletewater")
                    .WithAlias("delete-water").WithAlias("mdeletewater").WithAlias("delw")
                    .WithDesc("Deletes all water in selected area")
                    .WithArgs(parsers.OptionalWord("liquidcode"))
                    .HandleWith(handleClearWater)
                .EndSub()
                .BeginSub("delete-nearby")
                    .WithDesc("Delete area around caller")
                    .WithArgs(parsers.Int("horizontal size"), parsers.Int("height"))
                    .HandleWith(handleDeleteArea)
                .EndSub()
                .BeginSub("fix-blockentities")
                    .WithAlias("fixbe")
                    .WithDesc("Fix incorrect block entities in selected areas")
                    .HandleWith(validateArea)
                .EndSub()
                .BeginSub("replace-material")
                    .WithAlias("replacemat").WithAlias("repmat")
                    .RequiresPlayer()
                    .WithDesc("Replace a block material with another one, only if supported by the block. Hotbarslot 0: Search material, Active hand slot: Replace material")
                    .HandleWith(onReplaceMaterial)
                .EndSub()
                .Validate()
            ;
        }

        private TextCommandResult handleConstrain(TextCommandCallingArgs args)
        {
            workspace.WorldEditConstraint = (string)args[0] == "selection" ? EnumWorldEditConstraint.Selection : EnumWorldEditConstraint.None;
            return TextCommandResult.Success("Constraint " + workspace.WorldEditConstraint + " set.");
        }

        private TextCommandResult handleMirrorShorthand(TextCommandCallingArgs args)
        {
            var dirchar = args.SubCmdCode[args.SubCmdCode.Length - 1];
            return HandleMirrorCommand(BlockFacing.FromFirstLetter(dirchar), (string)args[0]);
        }

        private TextCommandResult handleMirrorSelection(TextCommandCallingArgs args)
        {
            var facing = blockFacingFromArg((string)args[0], args);
            return HandleMirrorCommand(facing, "sn");
        }

        private TextCommandResult onReplaceMaterial(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            var plr = args.Caller.Player;
            var fromslot = plr.InventoryManager.GetHotbarInventory()[0];
            var toslot = plr.InventoryManager.ActiveHotbarSlot;

            if (fromslot.Empty)
            {
                return TextCommandResult.Error("Not holding target block in inventory slot 0");
            }
            if (toslot.Empty || toslot.Itemstack.Block == null)
            {
                return TextCommandResult.Error("Not holding replace block in hands");
            }

            int corrected = 0;
            iterateOverSelection(pos =>
            {
                var ime = sapi.World.BlockAccessor.GetBlock(pos).GetInterface<IMaterialExchangeable>(sapi.World, pos);
                if (ime != null)
                {
                    if (ime.ExchangeWith(fromslot, toslot))
                    {
                        corrected++;
                    }
                }
            });

            return TextCommandResult.Success(corrected + " block materials exchanged");
        }

        private TextCommandResult handleClearWater(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            string liquidCode = (args.Parsers[0].IsMissing ? "water" : args[0] as string);

            int corrected = 0;
            iterateOverSelection(pos =>
            {
                var block = sapi.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
                if (block.LiquidCode == liquidCode)
                {
                    sapi.World.BlockAccessor.SetBlock(0, pos, BlockLayersAccess.Fluid);
                    corrected++;
                }
            });

            return TextCommandResult.Success(corrected + " water blocks removed");
        }

        private TextCommandResult handlePacifyWater(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            string liquidCode = (args.Parsers[0].IsMissing ? "water" : args[0] as string);

            int corrected = 0;
            Block stillWater = sapi.World.GetBlock(new AssetLocation(liquidCode + "-still-7"));

            iterateOverSelection(pos =>
            {
                var block = sapi.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);

                if (block.Id != stillWater.Id && block.LiquidCode == liquidCode)
                {
                    sapi.World.BlockAccessor.SetBlock(stillWater.Id, pos, BlockLayersAccess.Fluid);
                    corrected++;
                }
            });

            return TextCommandResult.Success(corrected + " liquid blocks pacified");
        }

        private TextCommandResult validateArea(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            int corrected = 0;
            iterateOverSelection((pos) =>
            {
                var block = sapi.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.SolidBlocks);
                var be = sapi.World.BlockAccessor.GetBlockEntity(pos);

                string classname = be == null ? null : sapi.ClassRegistry.GetBlockEntityClass(be.GetType());

                if (block.EntityClass != classname)
                {
                    sapi.World.Logger.Notification("Block {0} at {1}/{2}/{3} ought to have entity class {4} but has {5}", block.Code, pos.X, pos.Y, pos.Z, block.EntityClass == null ? "null" : block.EntityClass, classname == null ? "null" : classname);

                    if (block.EntityClass == null)
                    {
                        sapi.World.BlockAccessor.RemoveBlockEntity(pos);
                    }
                    else
                    {
                        sapi.World.BlockAccessor.SpawnBlockEntity(block.EntityClass, pos);
                    }

                    corrected++;
                }
            });
            
            return TextCommandResult.Success(corrected + " block entities corrected. See log files for more detail.");
        }

        private void iterateOverSelection(Action<BlockPos> onPos)
        {
            var minPos = workspace.StartMarker;
            var maxPos = workspace.EndMarker;
            var worldmap = sapi.World.BlockAccessor;
            int minx = GameMath.Clamp(Math.Min(minPos.X, maxPos.X), 0, worldmap.MapSizeX);
            int miny = GameMath.Clamp(Math.Min(minPos.Y, maxPos.Y), 0, worldmap.MapSizeY);
            int minz = GameMath.Clamp(Math.Min(minPos.Z, maxPos.Z), 0, worldmap.MapSizeZ);
            int maxx = GameMath.Clamp(Math.Max(minPos.X, maxPos.X), 0, worldmap.MapSizeX);
            int maxy = GameMath.Clamp(Math.Max(minPos.Y, maxPos.Y), 0, worldmap.MapSizeY);
            int maxz = GameMath.Clamp(Math.Max(minPos.Z, maxPos.Z), 0, worldmap.MapSizeZ);

            BlockPos tmpPos = new BlockPos();
            for (int x = minx; x < maxx; x++)
            {
                for (int y = miny; y < maxy; y++)
                {
                    for (int z = minz; z < maxz; z++)
                    {
                        tmpPos.Set(x, y, z);
                        onPos(tmpPos);
                    }
                }
            }
        }



        private TextCommandResult onToolCommand(TextCommandCallingArgs args)
        {
            if (args.RawArgs.Length > 0 && (workspace.ToolInstance == null || !workspace.ToolInstance.OnWorldEditCommand(this, args.RawArgs)))
            {
                return TextCommandResult.Error("No such function " + args.RawArgs.PopWord() + ". Maybe wrong tool selected?");
            }

            return TextCommandResult.Success();
        }

        private TextCommandResult loadWorkSpace(TextCommandCallingArgs args)
        {
            if (!CanUseWorldEdit(args.Caller.Player, true)) return TextCommandResult.Error("Caller is not allowed to use world edit");
            this.workspace = GetOrCreateWorkSpace(args.Caller.Player);
            this.fromPlayer = args.Caller.Player as IServerPlayer;
            return TextCommandResult.Success();
        }

        private TextCommandResult handleDeleteArea(TextCommandCallingArgs args)
        {
            int widthlength = (int)args[0];
            int height = (int)args[1];

            var centerPos = args.Caller.Pos.AsBlockPos;
            int cleared = FillArea(null, centerPos.AddCopy(-widthlength, 0, -widthlength), centerPos.AddCopy(widthlength, height, widthlength));

            return TextCommandResult.Success(cleared + " Blocks removed");
        }

        private TextCommandResult handleDeleteSelection(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            int cleared = FillArea(null, workspace.StartMarker, workspace.EndMarker);
            return TextCommandResult.Success(cleared + " marked blocks removed");
        }

        private TextCommandResult handleFillSelection(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Start marker or end marker not set");
            }

            var stack = args.Caller.Player?.InventoryManager.ActiveHotbarSlot.Itemstack;
            if (stack == null || stack.Class == EnumItemClass.Item)
            {
                return TextCommandResult.Error("Please put the desired block in your active hotbar slot");
            }

            int filled = FillArea(stack, workspace.StartMarker, workspace.EndMarker);

            return TextCommandResult.Success(filled + " marked blocks placed");
        }

        private TextCommandResult handleSelectionInfo(TextCommandCallingArgs args)
        {
            int sizeX = Math.Abs(workspace.StartMarker.X - workspace.EndMarker.X);
            int sizeY = Math.Abs(workspace.StartMarker.Y - workspace.EndMarker.Y);
            int sizeZ = Math.Abs(workspace.StartMarker.Z - workspace.EndMarker.Z);

            return TextCommandResult.Success(string.Format("Marked area is a cuboid of size {0}x{1}x{2} or a total of {3:n0} blocks", sizeX, sizeY, sizeZ, ((long)sizeX * sizeY * sizeZ)));
        }

        private TextCommandResult handleClearSelection(TextCommandCallingArgs args)
        {
            workspace.StartMarker = null;
            workspace.EndMarker = null;
            workspace.StartMarkerExact = null;
            workspace.EndMarkerExact = null;
            workspace.ResendBlockHighlights(this);
            return TextCommandResult.Success("Marked positions cleared");
        }

        private TextCommandResult handleShiftSelectionBy(TextCommandCallingArgs args)
        {
            return HandleShiftCommand((Vec3i)args[0]);
        }
        private TextCommandResult handleShiftSelection(TextCommandCallingArgs args)
        {
            var direction = (string)args[0];
            var facing = blockFacingFromArg(direction, args);
            if (facing == null)
            {
                return TextCommandResult.Error("Invalid direction, must be a cardinal, x/y/z or l/look");
            }
            return HandleShiftCommand(facing.Normali);
        }

        private TextCommandResult handleShiftSelectionShorthand(TextCommandCallingArgs args)
        {
            var dirchar = args.SubCmdCode[args.SubCmdCode.Length - 1];
            var amount = args.Parsers[1].IsMissing ? 1 : (int)args[1];
            return HandleShiftCommand(BlockFacing.FromFirstLetter(dirchar).Normali.Clone() * amount);
        }

        private TextCommandResult handleMoveSelectionBy(TextCommandCallingArgs args)
        {
            return HandleMoveCommand((Vec3i)args[0]);
        }

        private TextCommandResult handleMoveSelection(TextCommandCallingArgs args)
        {
            var direction = (string)args[0];
            var amount = args.Parsers[1].IsMissing ? 1 : (int)args[1];
            var facing = blockFacingFromArg(direction, args);
            if (facing == null)
            {
                return TextCommandResult.Error("Invalid direction, must be a cardinal, x/y/z or l/look");
            }
            return HandleMoveCommand(facing.Normali.Clone() * amount);
        }

        private TextCommandResult handleMoveSelectionShorthand(TextCommandCallingArgs args)
        {
            var dirchar = args.SubCmdCode[args.SubCmdCode.Length - 1];
            return HandleMoveCommand(BlockFacing.FromFirstLetter(dirchar).Normali);
        }

        private TextCommandResult handleRepeatSelection(TextCommandCallingArgs args)
        {
            var direction = (string)args[0];
            var facing = blockFacingFromArg(direction, args);
            if (facing == null)
            {
                return TextCommandResult.Error("Invalid direction, must be a cardinal, x/y/z or l/look");
            }

            return HandleRepeatCommand(facing.Normali, (int)args[1], (string)args[2]);
        }

        private TextCommandResult handleRepeatShorthand(TextCommandCallingArgs args)
        {
            var dirchar = args.SubCmdCode[args.SubCmdCode.Length - 1];
            return HandleRepeatCommand(BlockFacing.FromFirstLetter(dirchar).Normali, (int)args[0], (string)args[1]);
        }


        private TextCommandResult handleRotateSelection(TextCommandCallingArgs args)
        {
            return HandleRotateCommand(((string)args[0]).ToInt());
        }

        private TextCommandResult handleFlipSelection(TextCommandCallingArgs args)
        {
            var direction = (string)args[0];
            var facing = blockFacingFromArg(direction, args);
            if (facing == null)
            {
                return TextCommandResult.Error("Invalid direction, must be a cardinal, x/y/z or l/look");
            }

            return HandleFlipCommand(facing.Axis);
        }

        private TextCommandResult handleResize(TextCommandCallingArgs args)
        {
            var direction = (string)args[0];
            var facing = blockFacingFromArg(direction, args);
            if (facing == null)
            {
                return TextCommandResult.Error("Invalid direction, must be a cardinal, x/y/z or l/look");
            }

            var amount = (int)args[1];
            return ModifyMarker(facing, amount);
        }

        private BlockFacing blockFacingFromArg(string direction, TextCommandCallingArgs args)
        {
            BlockFacing facing = BlockFacing.FromFirstLetter(direction[0]);
            if (facing != null) return facing;
            
            /// North: Negative Z
            /// East: Positive X
            /// South: Positive Z
            /// West: Negative X
            /// Up: Positive Y
            /// Down: Negative Y
            switch (direction)
            {
                case "+x":
                case "x":
                    facing = BlockFacing.EAST;
                    break;
                case "-x":
                    facing = BlockFacing.WEST;
                    break;
                case "y":
                case "+y":
                    facing = BlockFacing.UP;
                    break;
                case "-y":
                    facing = BlockFacing.DOWN;
                    break;
                case "+z":
                case "z":
                    facing = BlockFacing.SOUTH;
                    break;
                case "-z":
                    facing = BlockFacing.NORTH;
                    break;
                case "l":
                case "look":
                    var lookVec = args.Caller.Entity.SidedPos.GetViewVector();
                    facing = BlockFacing.FromVector(lookVec.X, lookVec.Y, lookVec.Z);
                    break;

                default:
                    return null;
            }

            return facing;
        }

        private TextCommandResult handleGrowSelection(TextCommandCallingArgs args)
        {
            var dirchar = args.SubCmdCode[args.SubCmdCode.Length - 1];
            var facing = blockFacingFromArg(""+dirchar, args);

            return ModifyMarker(facing, (int)args[0]);
        }

        private TextCommandResult handleMark(TextCommandCallingArgs args)
        {
            workspace.StartMarker = (args[0] as Vec3d).AsBlockPos;
            workspace.EndMarker = (args[1] as Vec3d).AsBlockPos;
            EnsureInsideMap(workspace.EndMarker);
            workspace.HighlightSelectedArea();
            return TextCommandResult.Success("Start and end position marked");
        }

        private TextCommandResult handleMarkEnd(TextCommandCallingArgs args)
        {
            return SetEndPos(args.Caller.Pos);
        }

        private TextCommandResult handleMarkStart(TextCommandCallingArgs args)
        {
            return SetStartPos(args.Caller.Pos);
        }

        private TextCommandResult handleToggleImpres(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success("Import item/block resolving currently " + (!sapi.ObjectCache.ContainsKey("donotResolveImports") || (bool)sapi.ObjectCache["donotResolveImports"] == false ? "on" : "off"));
            }
            else
            {
                bool doreplace = (bool)args[0];
                sapi.ObjectCache["donotResolveImports"] = !doreplace;
                return TextCommandResult.Success("Import item/block resolving now globally " + (doreplace ? "on" : "off"));
            }
        }

        private TextCommandResult handleImport(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null)
            {
                return TextCommandResult.Error("Please mark a start position");
            }

            string filename = (string)args[0];

            EnumOrigin origin = EnumOrigin.StartPos;

            if (!args.Parsers[1].IsMissing)
            {
                try
                {
                    origin = (EnumOrigin)Enum.Parse(typeof(EnumOrigin), (string)args[1]);
                }
                catch { }
            }

            return ImportArea(filename, workspace.StartMarker, origin);
        }

        private TextCommandResult handleMgenCode(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Please mark start and end position");
            }

            if (args.Caller.Player.CurrentBlockSelection == null)
            {
                return TextCommandResult.Error("Please look at a block");
            }

            return GenMarkedMultiblockCode(args.Caller.Player as IServerPlayer);
        }

        private TextCommandResult handleRelightMarked(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Please mark start and end position");
            }

            if (args.Caller.Player is IServerPlayer splr)
            {
                splr.SendMessage(args.Caller.FromChatGroupId, "Relighting marked area, this may lag the server for a while...", EnumChatType.Notification);
            }

            sapi.WorldManager.FullRelight(workspace.StartMarker, workspace.EndMarker);

            return TextCommandResult.Success("Ok, relighting complete");
        }

        private TextCommandResult handleMexc(TextCommandCallingArgs args)
        {
            return export(args, true);
        }

        private TextCommandResult handleMex(TextCommandCallingArgs args)
        {
            return export(args, false);
        }

        private TextCommandResult export(TextCommandCallingArgs args, bool sendToClient)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Please mark start and end position");
            }

            IServerPlayer player = sendToClient ? args.Caller.Player as IServerPlayer : null;

            if ((args[1] as string)?.ToLowerInvariant() == "c")
            {
                BlockPos st = workspace.StartMarker;
                BlockPos en = workspace.EndMarker;
                serverChannel.SendPacket(new CopyToClipboardPacket() { Text = string.Format("/we mark ={0} ={1} ={2} ={3} ={4} ={5}\n/we mex {6}", st.X, st.Y, st.Z, en.X, en.Y, en.Z, args[0]) }, player);
            }

            return ExportArea((string)args[0], workspace.StartMarker, workspace.EndMarker, player);
        }

        private TextCommandResult handleRange(TextCommandCallingArgs args)
        {
            float pickingrange = (float)((double)args[0]);
            fromPlayer.WorldData.PickingRange = pickingrange;
            fromPlayer.BroadcastPlayerData();
            return TextCommandResult.Success("Picking range " + pickingrange + " set");
        }

        private TextCommandResult handleTom(TextCommandCallingArgs args)
        {
            EnumToolOffsetMode mode = EnumToolOffsetMode.Center;

            try
            {
                int index = (int)args[0];
                mode = (EnumToolOffsetMode)index;
            }
            catch (Exception) { }

            workspace.ToolOffsetMode = mode;
            workspace.ResendBlockHighlights(this);
            return TextCommandResult.Success("Set tool offset mode " + mode);
        }

        private TextCommandResult handleSetTool(TextCommandCallingArgs args)
        {
            string toolname = null;
            string suppliedToolname = (string)args[0];

            if (suppliedToolname.Length > 0)
            {
                int toolId;
                if (int.TryParse(suppliedToolname, NumberStyles.Any, GlobalConstants.DefaultCultureInfo, out toolId))
                {
                    if (toolId < 0)
                    {
                        workspace.ToolsEnabled = false;
                        workspace.SetTool(null, sapi);
                        workspace.ResendBlockHighlights(this);
                        return TextCommandResult.Success("World edit tools now disabled");
                    }

                    toolname = ToolRegistry.ToolTypes.GetKeyAtIndex(toolId);
                }
                else
                {
                    foreach (string name in ToolRegistry.ToolTypes.Keys)
                    {
                        if (name.ToLowerInvariant().StartsWith(suppliedToolname.ToLowerInvariant()))
                        {
                            toolname = name;
                            break;
                        }

                    }
                }
            }

            if (toolname == null)
            {
                return TextCommandResult.Error("No such tool '" + suppliedToolname + "' registered");
            }

            workspace.SetTool(toolname, sapi);
            workspace.ToolsEnabled = true;
            workspace.ResendBlockHighlights(this);
            SendPlayerWorkSpace(fromPlayer.PlayerUID);
            return TextCommandResult.Success(toolname + " tool selected");
        }

        private TextCommandResult handleRebuildRainmap(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer splr)
            {
                splr.SendMessage(args.Caller.FromChatGroupId, "Ok, rebuilding rain map on all loaded chunks, this may take some time and lag the server", EnumChatType.Notification);
            }

            int rebuilt = RebuildRainMap();
            return TextCommandResult.Success("Done, rebuilding {0} map chunks", rebuilt);
        }

        private TextCommandResult handleToolModeOff(TextCommandCallingArgs args)
        {
            previewBlocks.ClearChunks();
            previewBlocks.UnloadUnusedServerChunks();
            workspace.ToolsEnabled = false;
            workspace.SetTool(null, sapi);
            workspace.ResendBlockHighlights(this);
            return TextCommandResult.Success("World edit tools now disabled");
        }

        private TextCommandResult handleToolModeOn(TextCommandCallingArgs args)
        {
            workspace.ToolsEnabled = true;
            if (workspace.ToolName != null) workspace.SetTool(workspace.ToolName, sapi);
            workspace.ResendBlockHighlights(this);
            return TextCommandResult.Success("World edit tools now enabled");
        }

        private TextCommandResult handleRedo(TextCommandCallingArgs args)
        {
            return HandleHistoryChange(args, true);
        }

        private TextCommandResult handleUndo(TextCommandCallingArgs args)
        {
            return HandleHistoryChange(args, false);
        }

        private TextCommandResult handleRelight(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success("Block relighting is currently " + (workspace.DoRelight ? "on" : "off"));
            }

            workspace.DoRelight = (bool)args[0];
            workspace.revertableBlockAccess.Relight = workspace.DoRelight;
            return TextCommandResult.Success("Block relighting now " + ((workspace.DoRelight) ? "on" : "off"));
        }

        private TextCommandResult handleSovp(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success("Server overload protection is currently " + (workspace.serverOverloadProtection ? "on" : "off"));
            }

            workspace.serverOverloadProtection = (bool)args[0];
            return TextCommandResult.Success("Server overload protection now " + (workspace.serverOverloadProtection ? "on" : "off"));
        }

        private TextCommandResult handleBlock(TextCommandCallingArgs args)
        {
            var stack = args.Caller.Player?.InventoryManager.ActiveHotbarSlot.Itemstack;
            if (stack == null || stack.Class == EnumItemClass.Item)
            {
                return TextCommandResult.Error("Please put the desired block in your active hotbar slot");
            }

            BlockPos centerPos = args.Caller.Pos.AsBlockPos;
            sapi.World.BlockAccessor.SetBlock(stack.Id, centerPos.DownCopy());

            return TextCommandResult.Success("Block placed");
        }

        private TextCommandResult handleCbInfo(TextCommandCallingArgs args)
        {
            if (workspace.clipboardBlockData == null)
            {
                return TextCommandResult.Error("No schematic in the clipboard");
            }

            workspace.clipboardBlockData.Init(workspace.revertableBlockAccess);
            workspace.clipboardBlockData.LoadMetaInformationAndValidate(workspace.revertableBlockAccess, workspace.world, "(from clipboard)");

            string sides = "";
            for (int i = 0; i < workspace.clipboardBlockData.PathwayStarts.Length; i++)
            {
                if (sides.Length > 0) sides += ",";
                sides += workspace.clipboardBlockData.PathwaySides[i].Code + " (" + workspace.clipboardBlockData.PathwayOffsets[i].Length + " blocks)";
            }
            if (sides.Length > 0) sides = "Found " + workspace.clipboardBlockData.PathwayStarts.Length + " pathways: " + sides;

            return TextCommandResult.Success(Lang.Get("{0} blocks in clipboard. {1}", workspace.clipboardBlockData.BlockIds.Count, sides));
        }

        private TextCommandResult handlemPaste(TextCommandCallingArgs args)
        {
            if (workspace.clipboardBlockData == null)
            {
                return TextCommandResult.Error("No copied block data to paste");
            }

            PasteBlockData(workspace.clipboardBlockData, workspace.StartMarker, EnumOrigin.StartPos);
            return TextCommandResult.Success(workspace.clipboardBlockData.BlockIds.Count + " blocks pasted");
        }

        private TextCommandResult handleLaunch(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Please mark start and end position");
            }

            workspace.clipboardBlockData = CopyArea(workspace.StartMarker, workspace.EndMarker, true);
            FillArea(null, workspace.StartMarker, workspace.EndMarker, true);
            BlockPos startPos = workspace.StartMarker.Copy();
            startPos.Add(workspace.clipboardBlockData.PackedOffset);
            IMiniDimension miniDimension = CreateDimensionFromSchematic(workspace.clipboardBlockData, startPos, EnumOrigin.StartPos);
            if (miniDimension == null) return TextCommandResult.Error("No more Mini Dimensions available");
            Entity launched = GameContent.EntityTestShip.CreateShip(sapi, miniDimension);
            launched.Pos.SetFrom(launched.ServerPos);
            workspace.world.SpawnEntity(launched);

            return TextCommandResult.Success(workspace.clipboardBlockData.BlockIds.Count + " blocks launched");
        }

        private TextCommandResult handlemPosCopy(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Please mark start and end position");
            }

            BlockPos s = workspace.StartMarker;
            BlockPos e = workspace.EndMarker;

            serverChannel.SendPacket(new CopyToClipboardPacket() { Text = string.Format("/we mark ={0} ={1} ={2} ={3} ={4} ={5}", s.X, s.Y, s.Z, e.X, e.Y, e.Z) }, args.Caller.Player as IServerPlayer);
            return TextCommandResult.Success("Ok, sent to client clipboard");
        }

        private TextCommandResult handleMcopy(TextCommandCallingArgs args)
        {
            if (workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Error("Please mark start and end position");
            }

            workspace.clipboardBlockData = CopyArea(workspace.StartMarker, workspace.EndMarker);
            return TextCommandResult.Success(Lang.Get("{0} blocks and {1} entities copied", workspace.clipboardBlockData.BlockIds.Count, workspace.clipboardBlockData.EntitiesUnpacked.Count));
        }

        private TextCommandResult handleImpflip(TextCommandCallingArgs args)
        {
            workspace.ImportFlipped = !workspace.ImportFlipped;
            return TextCommandResult.Success("Ok, import data flip " + (workspace.ImportFlipped ? "on" : "off"));
        }

        private TextCommandResult handleImpr(TextCommandCallingArgs args)
        {
            workspace.ImportAngle = ((string)args[0]).ToInt(0);
            return TextCommandResult.Success(Lang.Get("Ok, set rotation to {0} degrees", workspace.ImportAngle));
        }       
    }
}
