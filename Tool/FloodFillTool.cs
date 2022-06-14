using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class FloodFillTool : ToolBase
    {
        int mapheight;
        Random rand = new Random();

        public int SearchRadius
        {
            get { return workspace.IntValues["std.floodFillSearchRadius"]; }
            set { workspace.IntValues["std.floodFillSearchRadius"] = value; }
        }

        public bool CheckEnclosure
        {
            get { return workspace.IntValues["std.floodFillCheckEnclosure"] > 0; }
            set { workspace.IntValues["std.floodFillCheckEnclosure"] = value ? 1 : 0; }
        }

        public int Mode
        {
            get { return workspace.IntValues["std.floodFillMode"]; }
            set { workspace.IntValues["std.floodFillMode"] = value; }
        }

        public int ReplaceableLevel
        {
            get { return workspace.IntValues["std.floodFillReplaceableLevel"]; }
            set { workspace.IntValues["std.floodFillReplaceableLevel"] = value; }
        }


        public FloodFillTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            if (!workspace.IntValues.ContainsKey("std.floodFillSearchRadius")) SearchRadius = 32;
            if (!workspace.IntValues.ContainsKey("std.floodFillReplaceableLevel")) ReplaceableLevel = 6000;
            if (!workspace.IntValues.ContainsKey("std.checkEnclosure")) CheckEnclosure = true; ;
            if (!workspace.IntValues.ContainsKey("std.mode")) Mode = 2;
        }


        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            switch (args.PopWord())
            {
                case "tr":
                    {
                        int rad = (int)args.PopInt(32);
                        SearchRadius = rad;

                        worldEdit.Good(workspace.ToolName + " search radius " + SearchRadius + " set.");
                        return true;
                    }

                case "trl":
                    {
                        int rl = (int)args.PopInt(6000);
                        ReplaceableLevel = rl;

                        worldEdit.Good(workspace.ToolName + " replaceable level " + rl + " set.");
                        return true;
                    }

                case "tce":
                    {
                        CheckEnclosure = (bool)args.PopBool(true);
                        worldEdit.Good(workspace.ToolName + " check enclosure set to " + CheckEnclosure);
                        return true;
                    }

                case "tm":
                    {
                        Mode = (int)args.PopInt(2);
                        worldEdit.Good(workspace.ToolName + " mode set to " + Mode + "D");
                        return true;
                    }
            }

            return false;
        }

        public override Vec3i Size
        {
            get { return new Vec3i(0, 0, 0); }
        }
        

        public override void OnBreak(WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            ApplyTool(worldEdit, blockSel.Position, -1, blockSel.Face, null, true);
        }

        public override void OnBuild(WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            ApplyTool(worldEdit, blockSel.Position, oldBlockId, blockSel.Face, withItemStack);
        }

        private void ApplyTool(WorldEdit worldEdit, BlockPos pos, int oldBlockId, BlockFacing onBlockFace, ItemStack withItemstack, bool remove = false)
        {
            mapheight = worldEdit.sapi.WorldManager.MapSizeY;

            Block block = blockAccessRev.GetBlock(pos);
            if (oldBlockId >= 0)
            {
                worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, pos);
            } else
            {
                block = worldEdit.sapi.World.GetBlock(0);
            }

            FloodFillAt(worldEdit, block, withItemstack, pos.X, pos.Y, pos.Z);

            blockAccessRev.Commit();
        }



        Queue<Vec4i> bfsQueue = new Queue<Vec4i>();
        HashSet<BlockPos> fillablePositions = new HashSet<BlockPos>();


        public void FloodFillAt(WorldEdit worldEdit, Block blockToPlace, ItemStack withItemStack, int posX, int posY, int posZ)
        {
            bfsQueue.Clear();
            fillablePositions.Clear();

            
            if (posY <= 0 || posY >= mapheight - 1) return;

            bfsQueue.Enqueue(new Vec4i(posX, posY, posZ, 0));
            fillablePositions.Add(new BlockPos(posX, posY, posZ));

            float radius = SearchRadius;

            int repl = blockToPlace.Id == 0 ? 0 : ReplaceableLevel;

            BlockFacing[] faces = Mode == 2 ? BlockFacing.HORIZONTALS : BlockFacing.ALLFACES;
            if (Mode == 1) faces = BlockFacing.HORIZONTALS.Append(BlockFacing.DOWN);

            BlockPos curPos = new BlockPos();
            
            while (bfsQueue.Count > 0)
            {
                Vec4i bpos = bfsQueue.Dequeue();

                foreach (BlockFacing facing in faces)
                {
                    curPos.Set(bpos.X + facing.Normali.X, bpos.Y + facing.Normali.Y, bpos.Z + facing.Normali.Z);
                    
                    Block block = blockAccessRev.GetBlock(curPos);
                    bool inBounds = bpos.W < radius;

                    if (inBounds)
                    {
                        if ((block.Replaceable >= repl || block.BlockMaterial == EnumBlockMaterial.Plant) && !fillablePositions.Contains(curPos)) 
                        {
                            bfsQueue.Enqueue(new Vec4i(curPos.X, curPos.Y, curPos.Z, bpos.W + 1));
                            fillablePositions.Add(curPos.Copy());
                        }

                    }
                    else
                    {
                        if (CheckEnclosure)
                        {
                            fillablePositions.Clear();
                            bfsQueue.Clear();
                            worldEdit.Bad("Cannot flood fill here, not enclosed area. Enforce enclosed area or disable enclosure check.");
                            break;
                        }
                    }
                }
            }

            foreach (BlockPos p in fillablePositions)
            {
                blockAccessRev.SetBlock(blockToPlace.BlockId, p, withItemStack);
            }

            worldEdit.Bad(fillablePositions.Count + " blocks placed");
        }

    }
}
