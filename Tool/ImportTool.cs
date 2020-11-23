﻿using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class ImportTool : ToolBase
    {
        Random rand = new Random();
        BlockSchematic[] blockDatas;

        int nextRnd = 0;

        public string BlockDataFilenames
        {
            get { return workspace.StringValues["std.pasteToolFilenames"]; }
            set { workspace.StringValues["std.pasteToolFilenames"] = value; }
        }

        public EnumOrigin Origin
        {
            get { return (EnumOrigin)workspace.IntValues["std.pasteToolOrigin"]; }
            set { workspace.IntValues["std.pasteToolOrigin"] = (int)value; }
        }


        public EnumReplaceMode ReplaceMode
        {
            get { return (EnumReplaceMode)workspace.IntValues["std.importReplaceMode"]; }
            set { workspace.IntValues["std.importReplaceMode"] = (int)value; }
        }


        public bool RandomRotate
        {
            get { return (EnumOrigin)workspace.IntValues["std.pasteToolRandomRotate"] > 0; }
            set { workspace.IntValues["std.pasteToolRandomRotate"] = value ? 1 : 0; }
        }

        public override Vec3i Size
        {
            get
            {
                if (blockDatas == null) return new Vec3i(0, 0, 0);
                BlockSchematic bd = blockDatas[nextRnd];
                return new Vec3i(bd.SizeX, bd.SizeY, bd.SizeZ);
            }
        }

        public ImportTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccessor) : base(workspace, blockAccessor)
        {
            if (!workspace.StringValues.ContainsKey("std.pasteToolFilenames")) BlockDataFilenames = null;
            if (!workspace.IntValues.ContainsKey("std.pasteToolOrigin")) Origin = EnumOrigin.BottomCenter;
            if (!workspace.IntValues.ContainsKey("std.importReplaceMode")) ReplaceMode = EnumReplaceMode.Replaceable;
            if (!workspace.IntValues.ContainsKey("std.pasteToolRandomRotate")) RandomRotate = false;
        }

        public void LoadBlockdatas(ICoreServerAPI api, WorldEdit worldEdit = null)
        {
            this.blockDatas = new BlockSchematic[0];

            if (BlockDataFilenames == null) return;
            string[] filenames = BlockDataFilenames.Split(',');

            List<BlockSchematic> blockDatas = new List<BlockSchematic>();
            string exportFolderPath = api.GetOrCreateDataPath("WorldEdit");

            int failed = 0;

            for (int i = 0; i < filenames.Length; i++)
            {
                string infilepath = Path.Combine(exportFolderPath, filenames[i]);

                string error = "";
                BlockSchematic blockData = BlockSchematic.LoadFromFile(infilepath, ref error);
                if (blockData == null)
                {
                    worldEdit?.Bad(error);
                    failed++;
                } else
                {
                    blockDatas.Add(blockData);
                }
            }

            if (failed > 0)
            {
                worldEdit?.Bad(failed + " schematics couldn't be loaded.");
            }

            this.blockDatas = blockDatas.ToArray();
        }

        public void SetBlockDatas(WorldEdit worldEdit, BlockSchematic[] schematics)
        {
            this.blockDatas = schematics;
            nextRnd = rand.Next(blockDatas.Length);
            worldEdit.ResendBlockHighlights();
        }

        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            switch (args[0])
            {
                case "imc":
                    if (workspace.clipboardBlockData != null)
                    {
                        this.blockDatas = new BlockSchematic[] { workspace.clipboardBlockData };
                        worldEdit.Good("Ok, using copied blockdata");
                        nextRnd = 0;
                        worldEdit.ResendBlockHighlights();
                    } else
                    {
                        worldEdit.Good("No copied block data available");
                    }
                    return true;

                case "ims":
                    string exportFolderPath = worldEdit.sapi.GetOrCreateDataPath("WorldEdit");
                    List<string> filenames = new List<string>();

                    for (int i = 1; i < args.Length; i++)
                    {
                        string filename = Path.GetFileName(args[i]);
                        string filepath = Path.Combine(exportFolderPath, args[i]);

                        if (!filename.EndsWith("*") && !filename.EndsWith("/") && !filename.EndsWith(".json")) filename += ".json";

                        try
                        {
                            string[] foundFilePaths = Directory.GetFiles(Path.GetDirectoryName(filepath), filename);

                            for (int j = 0; j < foundFilePaths.Length; j++)
                            {
                                filenames.Add(foundFilePaths[j].Substring(exportFolderPath.Length + 1));
                            }
                        } catch (Exception)
                        {
                            worldEdit.Bad("Unable to read files from this source");
                            return true;
                        }
                    }

                    

                    if (filenames.Count > 0)
                    {
                        BlockDataFilenames = string.Join(",", filenames);
                        LoadBlockdatas(worldEdit.sapi, worldEdit);
                        worldEdit.Good("Ok, found " + filenames.Count + " block data source files");
                    } else
                    {
                        BlockDataFilenames = null;
                        this.blockDatas = new BlockSchematic[0];

                        worldEdit.Good("No source files under this name/wildcard found");
                    }

                    nextRnd = rand.Next(blockDatas.Length);

                    worldEdit.ResendBlockHighlights();

                    return true;

                case "imo":
                    Origin = EnumOrigin.BottomCenter;

                    if (args.Length > 1)
                    {
                        int origin;
                        int.TryParse(args[1], out origin);
                        if (Enum.IsDefined(typeof(EnumOrigin), origin))
                        {
                            Origin = (EnumOrigin)origin;
                        }
                    }

                    worldEdit.Good("Paste origin " + Origin + " set.");

                    worldEdit.ResendBlockHighlights();

                    return true;

                case "tm":
                    ReplaceMode = EnumReplaceMode.Replaceable;

                    if (args.Length > 1)
                    {
                        int replaceable = 0;
                        int.TryParse(args[1], out replaceable);
                        if (Enum.IsDefined(typeof(EnumReplaceMode), replaceable))
                        {
                            ReplaceMode = (EnumReplaceMode)replaceable;
                        }
                    }

                    worldEdit.Good("Replace mode " + ReplaceMode + " set.");
                    worldEdit.ResendBlockHighlights();

                    return true;

                case "imrrand":
                    RandomRotate = args.Length > 1 && (args[1] == "1" || args[1] == "true" || args[1] == "on");

                    worldEdit.Good("Random rotation now " + (RandomRotate ? "on" : "off"));

                    SetRandomAngle(worldEdit.sapi.World);

                    worldEdit.ResendBlockHighlights();

                    return true;

                case "imn":
                    nextRnd = rand.Next(blockDatas.Length);
                    worldEdit.ResendBlockHighlights();
                    break;


                case "imr":
                    if (blockDatas == null || blockDatas.Length == 0)
                    {
                        worldEdit.Bad("Please define a block data source first.");
                        return true;
                    }

                    int angle = 90;

                    if (args.Length > 1)
                    {
                        if (!int.TryParse(args[1], out angle))
                        {
                            worldEdit.Bad("Invalid Angle (not a number)");
                            return true;
                        }
                    }
                    if (angle < 0) angle += 360;

                    if (angle != 0 && angle != 90 && angle != 180 && angle != 270)
                    {
                        worldEdit.Bad("Invalid Angle, allowed values are -270, -180, -90, 0, 90, 180 and 270");
                        return true;
                    }

                    for (int i = 0; i < blockDatas.Length; i++)
                    {
                        blockDatas[i].TransformWhilePacked(worldEdit.sapi.World, EnumOrigin.BottomCenter, angle, null);
                    }

                    worldEdit.ResendBlockHighlights();

                    worldEdit.Good("Ok, all schematics rotated by " + angle + " degrees");

                    return true;


                case "imflip":
                    if (blockDatas == null || blockDatas.Length == 0)
                    {
                        worldEdit.Bad("Please define a block data source first.");
                        return true;
                    }

                    for (int i = 0; i < blockDatas.Length; i++)
                    {
                        blockDatas[i].TransformWhilePacked(worldEdit.sapi.World, EnumOrigin.BottomCenter, 0, EnumAxis.Y);
                    }

                    worldEdit.ResendBlockHighlights();

                    worldEdit.Good("Ok, imported sources flipped");


                    return true;


                case "immirror":
                    if (blockDatas == null || blockDatas.Length == 0)
                    {
                        worldEdit.Bad("Please define a block data source first.");
                        return true;
                    }

                    EnumAxis axis = EnumAxis.X;
                    if (args.PopWord().ToLowerInvariant() == "z") axis = EnumAxis.Z;

                    for (int i = 0; i < blockDatas.Length; i++)
                    {
                        blockDatas[i].TransformWhilePacked(worldEdit.sapi.World, EnumOrigin.BottomCenter, 0, axis);
                    }

                    worldEdit.ResendBlockHighlights();

                    worldEdit.Good("Ok, imported sources mirrored around " + axis + " axis");


                    return true;
            }

            return false;
        }

        public override void OnBreak(WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            
        }

        public override void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (blockDatas == null)
            {
                LoadBlockdatas(worldEdit.sapi);
            }
            if (blockDatas.Length == 0) return;

            worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);

            BlockSchematic blockData = blockDatas[nextRnd];
            nextRnd = rand.Next(blockDatas.Length);

            BlockPos originPos = blockData.GetStartPos(blockSel.Position, Origin);
            blockData.Init(blockAccessRev);
            blockData.Place(blockAccessRev, worldEdit.sapi.World, originPos, ReplaceMode, worldEdit.ReplaceMetaBlocks);

            blockAccessRev.SetHistoryStateBlock(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, oldBlockId, blockAccessRev.GetStagedBlockId(blockSel.Position));
            blockAccessRev.Commit();
            blockData.PlaceEntitiesAndBlockEntities(blockAccessRev, worldEdit.sapi.World, originPos);

            if (RandomRotate) SetRandomAngle(worldEdit.sapi.World);
            worldEdit.ResendBlockHighlights();
        }


        void SetRandomAngle(IWorldAccessor world)
        {
            int rndAngle = rand.Next(4) * 90;

            for (int i = 0; i < blockDatas.Length; i++)
            {
                blockDatas[i].TransformWhilePacked(world, EnumOrigin.BottomCenter, rand.Next(4) * 90, null);
            }
        }

        public override List<BlockPos> GetBlockHighlights(WorldEdit worldEdit)
        {
            if (blockDatas == null)
            {
                LoadBlockdatas(worldEdit.sapi, worldEdit);
            }
            if (blockDatas.Length == 0) return new List<BlockPos>();

            BlockSchematic blockData = blockDatas[nextRnd];

            BlockPos origin = blockData.GetStartPos(new BlockPos(), Origin);
            BlockPos[] pos = blockData.GetJustPositions(origin);

            return new List<BlockPos>(pos);
        }

        
    }
}
