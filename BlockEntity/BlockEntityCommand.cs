using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityCommand : BlockEntity
    {
        public string Commands = "";

        GuiDialogBlockEntityCommand clientDialog;

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            Commands = tree.GetString("commands");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("commands", Commands);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (clientDialog != null)
            {
                clientDialog.TryClose();
                clientDialog?.Dispose();
                clientDialog = null;
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (clientDialog != null)
            {
                clientDialog.TryClose();
                clientDialog?.Dispose();
                clientDialog = null;
            }
        }

        public void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer.Entity.Controls.ShiftKey)
            {
                if (Api.Side == EnumAppSide.Client && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative && byPlayer.HasPrivilege("controlserver"))
                {
                    if (clientDialog != null)
                    {
                        clientDialog.TryClose();
                        clientDialog.Dispose();
                        clientDialog = null;
                        return;
                    }

                    clientDialog = new GuiDialogBlockEntityCommand(Pos, Commands, Api as ICoreClientAPI);
                    clientDialog.TryOpen();
                        clientDialog.OnClosed += () => { 
                            clientDialog?.Dispose(); clientDialog = null; 
                       };
                } else
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "noprivilege", "Can only be edited in creative mode and with controlserver privlege");
                }

                return;
            }

            if (Api.Side == EnumAppSide.Server)
            {
                string[] commands = Commands.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var command in commands)
                {
                    string cmd = command
                        .Replace("{pos}", "="+Pos.X+" ="+Pos.Y+"="+Pos.Z+"")
                        .Replace("{plr}", byPlayer.PlayerName)
                    ;

                    if (command.Contains("{plr}"))
                    {
                        (Api as ICoreServerAPI).HandleCommand(byPlayer as IServerPlayer, cmd);
                    } else
                    {
                        (Api as ICoreServerAPI).InjectConsole(cmd);
                    }
                    

                    
                }

                if (commands.Length > 0)
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/toggleswitch"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, false, 16, 0.5f);
                }
            }
        }


        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == 12 && fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative && fromPlayer.HasPrivilege("controlserver"))
            {
                this.Commands = SerializerUtil.Deserialize<string>(data);
                MarkDirty(true);
            }
        }
    }
}
