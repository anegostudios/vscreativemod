using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.WorldEdit;

namespace Vintagestory.GameContent
{
    public class ItemMagicWand : Item
    {
        ICoreServerAPI sapi;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }
            base.OnLoaded(api);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (byEntity.World.Side == EnumAppSide.Server)
            {
                IServerPlayer plr = (byEntity as EntityPlayer).Player as IServerPlayer;
                if (plr != null)
                {
                    sapi.ModLoader.GetModSystem<WorldEdit>().OnAttackStart(plr, blockSel);
                }
            }

            handling = EnumHandHandling.PreventDefaultAction;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.World.Side == EnumAppSide.Server)
            {
                IServerPlayer plr = (byEntity as EntityPlayer).Player as IServerPlayer;
                if (plr != null)
                {
                    sapi.ModLoader.GetModSystem<WorldEdit>().OnInteractStart(plr, blockSel);
                }
            }

            handling = EnumHandHandling.PreventDefaultAction;
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return base.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSelection, entitySel);
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            base.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSelection, entitySel);
        }
    }
}
