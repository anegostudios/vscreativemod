using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.ServerMods
{
    /// <summary>
    /// This class contains core settings for the creative mod
    /// </summary>
    public class Core : ModSystem
	{
        ICoreServerAPI sapi;
            
        public override double ExecuteOrder()
        {
            return 0.001;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            
            return true;
        }

        public override void StartPre(ICoreAPI api)
        {
            api.Assets.AddPathOrigin("game", Path.Combine(GamePaths.AssetsPath, "creative"));
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterItemClass("ItemMagicWand", typeof(ItemMagicWand));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            api.Event.SaveGameCreated += Event_SaveGameCreated;
            api.Event.PlayerCreate += Event_PlayerCreate;
        }

        private void Event_PlayerCreate(IServerPlayer byPlayer)
        {
            if (sapi.WorldManager.SaveGame.PlayStyle == "creativebuilding")
            {
                byPlayer.WorldData.CurrentGameMode = EnumGameMode.Creative;
                byPlayer.WorldData.PickingRange = 100;
                byPlayer.BroadcastPlayerData();
            }
        }

        private void Event_SaveGameCreated()
        {
            if (sapi.WorldManager.SaveGame.PlayStyle == "creativebuilding")
            {
                sapi.WorldManager.SaveGame.AllowCreativeMode = true;
                sapi.WorldManager.SaveGame.EntitySpawning = false;
            }
        }
    }
}
