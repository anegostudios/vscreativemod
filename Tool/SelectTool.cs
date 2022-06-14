using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class SelectTool : ToolBase
    {
        public virtual string Prefix { get { return "std.select"; } }


        IBlockAccessor blockAccess;
        HashSet<Block> edgeBlocksCache = new HashSet<Block>();

        public bool MagicSelect
        {
            get { return workspace.IntValues[Prefix + "magicSelect"] > 0; }
            set { workspace.IntValues[Prefix + "magicSelect"] = value ? 1 : 0; }
        }

        public string[] EdgeBlocks
        {
            get { return workspace.StringValues[Prefix + "magicEdgeBlocks"].Split(','); }
            private set { workspace.StringValues[Prefix + "magicEdgeBlocks"] = string.Join(",", value); }
        }

        public SelectTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            if (!workspace.IntValues.ContainsKey(Prefix + "magicSelect")) MagicSelect = false;
            if (!workspace.StringValues.ContainsKey(Prefix + "magicEdgeBlocks")) SetEdgeBlocks(workspace.world, new string[] { "air", "soil-*", "tallgrass-*" });
            else SetEdgeBlocks(workspace.world, EdgeBlocks);
        }

        public void SetEdgeBlocks(IWorldAccessor world, string[] blockCodes)
        {
            this.EdgeBlocks = blockCodes;

            edgeBlocksCache = new HashSet<Block>();

            AssetLocation[] blockCodesA = new AssetLocation[blockCodes.Length];
            for (int i = 0; i < blockCodesA.Length; i++)
            {
                blockCodesA[i] = new AssetLocation(blockCodes[i]);
            }

            foreach (Block block in world.Blocks)
            {
                if (block?.Code == null) continue;

                for (int i = 0; i < blockCodesA.Length; i++)
                { 
                    if (block.WildCardMatch(blockCodesA[i]))
                    {
                        edgeBlocksCache.Add(block);
                    }
                }
            }
        }



        public override bool OnWorldEditCommand(WorldEdit worldEdit, CmdArgs args)
        {
            switch (args.PopWord())
            {
                case "magic":
                    {
                        MagicSelect = (bool)args.PopBool(false);

                        worldEdit.Good("Magic select now " + (MagicSelect ? "on" : "off"));
                        return true;
                    }

                case "edgeblocks":
                    {
                        string arg = args.PopWord("list");

                        switch (arg)
                        {
                            case "list":
                                worldEdit.Good("Edge blocks: " + string.Join(", ", EdgeBlocks));
                                break;

                            case "add":
                                string blockcode = args.PopAll();

                                if (matchesAnyBlock(worldEdit.sapi, blockcode))
                                {
                                    EdgeBlocks = EdgeBlocks.Append(args.PopAll());
                                    worldEdit.Good("Ok, edge block '" + blockcode + "' added.");
                                    SetEdgeBlocks(worldEdit.sapi.World, EdgeBlocks);
                                } else
                                {
                                    worldEdit.Good("Error, block code/wildcard '" + blockcode + "' does not match any known blocks.");
                                }

                                
                                break;

                            case "remove":

                                List<string> elems = new List<string>(EdgeBlocks);
                                if (elems.Remove(args.PopAll()))
                                {
                                    worldEdit.Good("Ok, edge block removed.");
                                    SetEdgeBlocks(worldEdit.sapi.World, elems.ToArray());
                                } else
                                {
                                    worldEdit.Good("No such edge block in list.");
                                }

                                break;

                            default:
                                worldEdit.Bad("Invalid arg. Syntax: /we edgeblocks or /we edgeblocks [list|add|remove] [blockcode]");
                                break;
                        }
                    }
                    return true;
                    
            }

            return false;
        }

        private bool matchesAnyBlock(ICoreAPI api, string blockcode)
        {
            AssetLocation loc = new AssetLocation(blockcode);
            foreach (Block block in api.World.Blocks)
            {
                if (block.WildCardMatch(loc)) return true;
            }
            return false;
        }

        public override void OnAttackStart(WorldEdit worldEdit, BlockSelection blockSelection)
        {
            if (blockSelection == null) return;

            if (!MagicSelect)
            {
                BlockPos startPos = blockSelection.Position;

                if (workspace.EndMarker != null)
                {
                    if (workspace.EndMarker.X <= startPos.X) startPos.X++;
                    if (workspace.EndMarker.Y <= startPos.Y) startPos.Y++;
                    if (workspace.EndMarker.Z <= startPos.Z) startPos.Z++;
                }
                
                worldEdit.SetStartPos(startPos);
            }
        }

        public override void OnInteractStart(WorldEdit worldEdit, BlockSelection blockSelection)
        {
            if (blockSelection == null) return;

            blockAccess = worldEdit.sapi.World.BlockAccessor;

            if (MagicSelect)
            {
                Cuboidi sele = MagiSelect(blockSelection);
                worldEdit.SetStartPos(sele.Start.AsBlockPos);
                worldEdit.SetEndPos(sele.End.AsBlockPos);

                workspace.revertableBlockAccess.StoreHistoryState(new List<BlockUpdate>());
            }
            else
            {
                BlockPos endpos = blockSelection.Position.AddCopy(blockSelection.Face);
                if (workspace.StartMarker != null)
                {
                    if (workspace.StartMarker.X <= endpos.X) endpos.X++;
                    if (workspace.StartMarker.Y <= endpos.Y) endpos.Y++;
                    if (workspace.StartMarker.Z <= endpos.Z) endpos.Z++;
                }
                worldEdit.SetEndPos(endpos);

                workspace.revertableBlockAccess.StoreHistoryState(new List<BlockUpdate>());
            }

            base.OnInteractStart(worldEdit, blockSelection);
        }

        

        private Cuboidi MagiSelect(BlockSelection blockSelection)
        {
            BlockPos pos = blockSelection.Position;
            Cuboidi selection = new Cuboidi(pos, pos.AddCopy(1, 1, 1));
            bool didGrowAny = true;
            
            int i = 0;

            while (didGrowAny && i < 600)
            {
                didGrowAny =
                    TryGrowX(selection, true) ||
                    TryGrowX(selection, false) ||
                    TryGrowY(selection, true) ||
                    TryGrowY(selection, false) ||
                    TryGrowZ(selection, true) ||
                    TryGrowZ(selection, false)
                ;
                i++;
            }

            return selection;
        }

        private bool TryGrowX(Cuboidi selection, bool positive)
        {
            bool shouldInclude = false;

            for (int y = selection.Y1; !shouldInclude && y < selection.Y2; y++)
            {
                for (int z = selection.Z1; !shouldInclude && z < selection.Z2; z++)
                {
                    int x = positive ? (selection.X2) : (selection.X1 - 1);

                    Block block = blockAccess.GetBlock(x, y, z);

                    shouldInclude |= !edgeBlocksCache.Contains(block);
                }
            }

            if (shouldInclude)
            {
                if (positive)
                {
                    selection.X2++;
                } else
                {
                    selection.X1--;
                }

                return true;
            }

            return false;
        }



        private bool TryGrowY(Cuboidi selection, bool positive)
        {
            bool shouldInclude = false;

            for (int x = selection.X1; !shouldInclude && x < selection.X2; x++)
            {
                for (int z = selection.Z1; !shouldInclude && z < selection.Z2; z++)
                {
                    int y = positive ? (selection.Y2) : (selection.Y1 - 1);

                    Block block = blockAccess.GetBlock(x, y, z);

                    shouldInclude |= !edgeBlocksCache.Contains(block);
                }
            }

            if (shouldInclude)
            {
                if (positive)
                {
                    selection.Y2++;
                }
                else
                {
                    selection.Y1--;
                }

                return true;
            }

            return false;
        }


        private bool TryGrowZ(Cuboidi selection, bool positive)
        {
            bool shouldInclude = false;

            for (int y = selection.Y1; !shouldInclude && y < selection.Y2; y++)
            {
                for (int x = selection.X1; !shouldInclude && x < selection.X2; x++)
                {
                    int z = positive ? (selection.Z2) : (selection.Z1 - 1);

                    Block block = blockAccess.GetBlock(x, y, z);

                    shouldInclude |= !edgeBlocksCache.Contains(block);
                }
            }

            if (shouldInclude)
            {
                if (positive)
                {
                    selection.Z2++;
                }
                else
                {
                    selection.Z1--;
                }

                return true;
            }

            return false;
        }


        public override void OnSelected(WorldEdit worldEdit)
        {
            worldEdit.SelectionMode(true);
        }


        public override void OnDeselected(WorldEdit worldEdit)
        {
            worldEdit.SelectionMode(false);
        }


        public override Vec3i Size
        {
            get { return new Vec3i(0, 0, 0); }
        }
    }
}
