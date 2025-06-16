using System.IO;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable

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
            api.Assets.AddModOrigin(GlobalConstants.DefaultDomain, Path.Combine(GamePaths.AssetsPath, "creative"), api.Side == EnumAppSide.Client ? "textures" : null);
        }

        public override void Start(ICoreAPI api)
        {
            GameVersion.EnsureEqualVersionOrKillExecutable(api, System.Diagnostics.FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion, GameVersion.OverallVersion, "VSCreativeMod");

            base.Start(api);

            api.RegisterItemClass("ItemMagicWand", typeof(ItemMagicWand));
            api.RegisterEntity("EntityTestShip", typeof(EntityTestShip));
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
            ITreeAttribute worldConfig = sapi.WorldManager.SaveGame.WorldConfiguration;
            string mode = worldConfig.GetString("gameMode");

            if (mode == "creative")
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
                sapi.WorldManager.SaveGame.EntitySpawning = false;
            }
        }
    }
}
