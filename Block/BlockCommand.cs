using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockCommand : Block
    {
        public override void OnLoaded(ICoreAPI api)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityCommand bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCommand;
            bec?.OnInteract(byPlayer);

            return true;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "Edit (Requires creative mode)",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right
                },
                new WorldInteraction()
                {
                    ActionLangCode = "Run commands",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }

    }
}
