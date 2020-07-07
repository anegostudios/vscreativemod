using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class WorldEditClientHandler
    {
        public ICoreClientAPI capi;

        public GuiJsonDialog toolBarDialog;
        public GuiJsonDialog controlsDialog;
        public GuiJsonDialog toolOptionsDialog;
        public GuiJsonDialog settingsDialog;

        JsonDialogSettings toolBarsettings;
        JsonDialogSettings toolOptionsSettings;

        IClientNetworkChannel clientChannel;
        WorldEditWorkspace ownWorkspace;

        bool isComposing;
        bool beforeAmbientOverride;
        
        public WorldEditClientHandler(ICoreClientAPI capi)
        {
            this.capi = capi;
            capi.RegisterCommand("we", "World edit toolbar", "", CmdEditClient);
            capi.Input.RegisterHotKey("worldedit", "World Edit", GlKeys.Tilde, HotkeyType.CreativeTool);
            capi.Input.SetHotKeyHandler("worldedit", OnHotkeyWorldEdit);
            capi.Event.LeaveWorld += Event_LeaveWorld;
            capi.Event.FileDrop += Event_FileDrop;

            clientChannel =
                capi.Network.RegisterChannel("worldedit")
                .RegisterMessageType(typeof(RequestWorkSpacePacket))
                .RegisterMessageType(typeof(WorldEditWorkspace))
                .RegisterMessageType(typeof(ChangePlayerModePacket))
                .RegisterMessageType(typeof(CopyToClipboardPacket))
                .RegisterMessageType(typeof(SchematicJsonPacket))
                .SetMessageHandler<WorldEditWorkspace>(OnServerWorkspace)
                .SetMessageHandler<CopyToClipboardPacket>(OnClipboardCopy)
                .SetMessageHandler<SchematicJsonPacket>(OnReceivedSchematic)
            ;

            if (!capi.Settings.Int.Exists("schematicMaxUploadSizeKb"))
            {
                capi.Settings.Int["schematicMaxUploadSizeKb"] = 75;
            }

        }

        private void OnReceivedSchematic(SchematicJsonPacket message)
        {
            bool allow = capi.Settings.Bool["allowSaveFilesFromServer"];

            if (!allow)
            {
                capi.ShowChatMessage("Server tried to send a schematic file, but it was rejected for safety reasons. To accept, set allowSaveFilesFromServer to true in clientsettings.json, or type '.clientconfigcreate allowSavefilesFromServer bool true' but be aware of potential security implications!");
                return;
            }

            try
            {
                string exportFolderPath = capi.GetOrCreateDataPath("WorldEdit");
                string outfilepath = Path.Combine(exportFolderPath, Path.GetFileName(message.Filename));

                if (!outfilepath.EndsWith(".json"))
                {
                    outfilepath += ".json";
                }

                using (TextWriter textWriter = new StreamWriter(outfilepath))
                {
                    textWriter.Write(message.JsonCode);
                    textWriter.Close();
                }

                capi.ShowChatMessage(string.Format("Schematic file {0} received and saved", message.Filename));
            }
            catch (IOException e)
            {
                capi.ShowChatMessage("Server sent a schematic file, but failed to save it: " + e.Message);
            }
        }

        private void Event_FileDrop(FileDropEvent ev)
        {
            FileInfo info = null;
            long bytes = 0;

            try
            {
                info = new FileInfo(ev.Filename);
                bytes = info.Length;
            } catch (Exception ex)
            {
                capi.TriggerIngameError(this, "importfailed", string.Format("Unable to import schematic: ", ex));
                return;
            }

            if (ownWorkspace != null && ownWorkspace.ToolsEnabled && ownWorkspace.ToolName == "Import")
            {

                int schematicMaxUploadSizeKb = capi.Settings.Int.Get("schematicMaxUploadSizeKb", 75);
            
                // Limit to 50kb
                if (bytes / 1024 > schematicMaxUploadSizeKb)
                {
                    capi.TriggerIngameError(this, "schematictoolarge", Lang.Get("Importing of schematics above {0} KB disabled.", schematicMaxUploadSizeKb));
                    return;
                }

                string err = null;
                BlockSchematic schematic = BlockSchematic.LoadFromFile(ev.Filename, ref err);
                if (err != null)
                {
                    capi.TriggerIngameError(this, "importerror", err);
                    return;
                }

                string json = "";
                using (TextReader textReader = new StreamReader(ev.Filename))
                {
                    json = textReader.ReadToEnd();
                    textReader.Close();
                }

                if (json.Length < 1024 * 100)
                {
                    capi.World.Player.ShowChatNotification(Lang.Get("Sending {0} bytes of schematicdata to the server...", json.Length));
                }
                else
                {
                    capi.World.Player.ShowChatNotification(Lang.Get("Sending {0} bytes of schematicdata to the server, this may take a while...", json.Length));
                }

                capi.Event.RegisterCallback((dt) =>
                {
                    clientChannel.SendPacket<SchematicJsonPacket>(new SchematicJsonPacket() { Filename = info.Name, JsonCode = json });
                }, 20);
            }
        }

        private void OnClipboardCopy(CopyToClipboardPacket msg)
        {
            Clipboard.SetText(msg.Text);
            capi.World.Player.ShowChatNotification("Ok, copied to your clipboard");
        }

        private void OnServerWorkspace(WorldEditWorkspace workspace)
        {
            ownWorkspace = workspace;

            isComposing = true;
            if (toolBarDialog != null && toolBarDialog.IsOpened())
            {
                toolBarDialog.Recompose();
            }
            if (toolOptionsDialog != null && toolOptionsDialog.IsOpened())
            {
                toolOptionsDialog.Recompose();
                toolOptionsDialog.UnfocusElements();
            }
            if (ownWorkspace != null && ownWorkspace.ToolName != null && ownWorkspace.ToolName.Length > 0 && ownWorkspace.ToolsEnabled && toolBarDialog?.IsOpened() == true)
            {
                OpenToolOptionsDialog("" + ownWorkspace.ToolName);
            }


            isComposing = false;
        }

        private bool OnHotkeyWorldEdit(KeyCombination t1)
        {
            TriggerWorldEditDialog();
            return true;
        }

        private void CmdEditClient(int groupId, CmdArgs args)
        {
            TriggerWorldEditDialog();
        }

        private void TriggerWorldEditDialog()
        {
            try
            {
                if (toolBarDialog == null || !toolBarDialog.IsOpened())
                {
                    clientChannel.SendPacket(new RequestWorkSpacePacket());

                    if (toolBarsettings == null || capi.Settings.Bool.Get("developerMode", false))
                    {
                        capi.Assets.Reload(AssetCategory.dialog);
                        toolBarsettings = capi.Assets.Get<JsonDialogSettings>(new AssetLocation("dialog/worldedit-toolbar.json"));
                        toolBarsettings.OnGet = OnGetValueToolbar;
                        toolBarsettings.OnSet = OnSetValueToolbar;
                    }

                    toolBarDialog = new GuiJsonDialog(toolBarsettings, capi);
                    toolBarDialog.TryOpen();
                    if (toolBarDialog != null)
                    {
                        toolBarDialog.OnClosed += () => {
                            toolOptionsDialog?.TryClose();
                            settingsDialog?.TryClose();
                            controlsDialog?.TryClose();
                            clientChannel.SendPacket(new RequestWorkSpacePacket());
                        };
                    }

                    if (ownWorkspace != null && ownWorkspace.ToolName != null && ownWorkspace.ToolName.Length > 0 && ownWorkspace.ToolsEnabled)
                    {
                        OpenToolOptionsDialog("" + ownWorkspace.ToolName);
                    }

                    JsonDialogSettings dlgsettings = capi.Assets.Get<JsonDialogSettings>(new AssetLocation("dialog/worldedit-settings.json"));
                    dlgsettings.OnGet = OnGetValueSettings;
                    dlgsettings.OnSet = OnSetValueSettings;

                    settingsDialog = new GuiJsonDialog(dlgsettings, capi);
                    settingsDialog.TryOpen();

                    JsonDialogSettings controlsSettings = capi.Assets.Get<JsonDialogSettings>(new AssetLocation("dialog/worldedit-controls.json"));
                    controlsSettings.OnSet = OnSetValueControls;
                    controlsDialog = new GuiJsonDialog(controlsSettings, capi);
                    controlsDialog.TryOpen();
                }
                else
                {
                    toolBarDialog?.TryClose();
                    toolOptionsDialog?.TryClose();
                    settingsDialog?.TryClose();
                    controlsDialog?.TryClose();
                }
            } catch (Exception e)
            {
                capi.World.Logger.Error("Unable to load json dialogs: {0}", e);
            }
            
        }

        private void OnSetValueControls(string elementCode, string newValue)
        {
            switch (elementCode)
            {
                case "undo":
                    capi.SendChatMessage("/we undo");
                    break;
                case "redo":
                    capi.SendChatMessage("/we redo");
                    break;
            }
        }

        private void OnSetValueSettings(string elementCode, string newValue)
        {
            AmbientModifier amb = capi.Ambient.CurrentModifiers["serverambient"];

            switch (elementCode)
            {
                case "timeofday":
                    float time = newValue.ToFloat(0);
                    time = time / 24 * capi.World.Calendar.HoursPerDay;
                    capi.SendChatMessage("/time set " + time + ":00");
                    break;
                case "foglevel":
                    amb.FogDensity.Weight = 1;
                    amb.FogDensity.Value = newValue.ToFloat(0) / 2000f;
                    SendGlobalAmbient();
                    break;

                case "flatfoglevel":
                    amb.FlatFogDensity.Weight = 1;
                    amb.FlatFogDensity.Value = newValue.ToFloat() / 250f;
                    SendGlobalAmbient();
                    break;

                case "flatfoglevelypos":
                    amb.FlatFogYPos.Weight = 1;
                    amb.FlatFogYPos.Value = newValue.ToFloat();
                    SendGlobalAmbient();
                    break;


                case "fogred":
                case "foggreen":
                case "fogblue":
                    float[] color = amb.FogColor.Value;
                    if (elementCode == "fogred") color[0] = newValue.ToInt() / 255f;
                    if (elementCode == "foggreen") color[1] = newValue.ToInt() / 255f;
                    if (elementCode == "fogblue") color[2] = newValue.ToInt() / 255f;

                    amb.FogColor.Weight = 1;
                    SendGlobalAmbient();
                    break;
                case "cloudlevel":

                    amb.CloudDensity.Value = newValue.ToFloat() / 100f;
                    amb.CloudDensity.Weight = 1;

                    SendGlobalAmbient();
                    break;
                case "cloudypos":
                    amb.CloudYPos.Value = newValue.ToFloat() / 255f;
                    amb.CloudYPos.Weight = 1;
                    SendGlobalAmbient();
                    break;
                case "cloudbrightness":
                    amb.CloudBrightness.Value = newValue.ToFloat() / 100f;
                    amb.CloudBrightness.Weight = 1;
                    SendGlobalAmbient();
                    break;
                case "movespeed":
                    capi.World.Player.WorldData.MoveSpeedMultiplier = newValue.ToFloat();
                    break;
                case "axislock":
                    capi.World.Player.WorldData.FreeMovePlaneLock = (EnumFreeMovAxisLock)newValue.ToInt();
                    clientChannel.SendPacket(new ChangePlayerModePacket() { axisLock = capi.World.Player.WorldData.FreeMovePlaneLock });
                    break;
                case "pickingrange":
                    capi.World.Player.WorldData.PickingRange = newValue.ToFloat();
                    clientChannel.SendPacket(new ChangePlayerModePacket() { pickingRange = capi.World.Player.WorldData.PickingRange });
                    break;
                case "liquidselectable":
                    capi.World.ForceLiquidSelectable = newValue == "1" || newValue == "true";
                    break;
                case "tooloffsetmode":
                    int num = 0;
                    int.TryParse(newValue, out num);
                    ownWorkspace.ToolOffsetMode = (EnumToolOffsetMode)num;
                    capi.SendChatMessage("/we tom " + num);
                    break;

                case "ambientparticles":
                    capi.World.AmbientParticles = newValue == "1" || newValue == "true";
                    break;
                case "flymode":
                    bool fly = newValue == "1" || newValue == "2";
                    bool noclip = newValue == "2";
                    capi.World.Player.WorldData.FreeMove = fly;
                    capi.World.Player.WorldData.NoClip = noclip;

                    clientChannel.SendPacket(new ChangePlayerModePacket() { fly = fly, noclip = noclip });
                    break;
                case "overrideambient":
                    SendGlobalAmbient((newValue == "1" || newValue == "true"));

                    break;
            }
        }


        void SendGlobalAmbient(bool enable = true)
        {
            AmbientModifier amb = capi.Ambient.CurrentModifiers["serverambient"];
            float newWeight = enable ? 1 : 0;
            amb.AmbientColor.Weight = 0;
            amb.FogColor.Weight = newWeight;
            amb.FogDensity.Weight = newWeight;
            amb.FogMin.Weight = newWeight;

            amb.FlatFogDensity.Weight = newWeight;
            amb.FlatFogYPos.Weight = newWeight;

            amb.CloudBrightness.Weight = newWeight;
            amb.CloudDensity.Weight = newWeight;
            amb.CloudYPos.Weight = newWeight;

            string jsoncode = JsonConvert.SerializeObject(amb);
            capi.SendChatMessage("/setambient " + jsoncode);

            if (!beforeAmbientOverride) settingsDialog.ReloadValues();
            beforeAmbientOverride = enable;
        }

        private string OnGetValueSettings(string elementCode)
        {
            AmbientModifier amb = capi.Ambient.CurrentModifiers["serverambient"];

            switch (elementCode)
            {
                case "timeofday":
                    return "" + (int)(capi.World.Calendar.FullHourOfDay / capi.World.Calendar.HoursPerDay * 24);
                case "foglevel":
                    return ""+ (int)(amb.FogDensity.Value * 2000);


                case "flatfoglevel":
                    return "" + (int)(amb.FlatFogDensity.Value * 250);

                case "flatfoglevelypos":
                    return "" + (int)(amb.FlatFogYPos.Value);

                case "fogred":
                    return ""+(int)(amb.FogColor.Value[0] * 255);
                case "foggreen":
                    return ""+(int)(amb.FogColor.Value[1] * 255);
                case "fogblue":
                    return ""+(int)(amb.FogColor.Value[2] * 255);
                case "cloudlevel":
                    return ""+ (int)(amb.CloudDensity.Value * 100);
                case "cloudypos":
                    return "" + (int)(amb.CloudYPos.Value * 255);
                case "cloudbrightness":
                    return ""+ (int)(amb.CloudBrightness.Value * 100);
                case "movespeed":
                    return ""+capi.World.Player.WorldData.MoveSpeedMultiplier;
                case "axislock":
                    return "" + (int)capi.World.Player.WorldData.FreeMovePlaneLock;
                case "pickingrange":
                    return "" + (float)capi.World.Player.WorldData.PickingRange;
                case "liquidselectable":
                    return (capi.World.ForceLiquidSelectable ? "1" : "0");
                case "ambientparticles":
                    return (capi.World.AmbientParticles ? "1" : "0");
                case "flymode":
                    bool fly = capi.World.Player.WorldData.FreeMove;
                    bool noclip = capi.World.Player.WorldData.NoClip;
                    if (fly && !noclip) return "1";
                    if (fly && noclip) return "2";
                    return "0";
                case "overrideambient":
                    return amb.FogColor.Weight >= 0.99f ? "1" : "0";
                case "tooloffsetmode":
                    if (ownWorkspace == null) return "0";

                    return ((int)ownWorkspace.ToolOffsetMode) + "";
            }

            return "";
        }

        void OpenToolOptionsDialog(string toolname)
        {
            if (toolOptionsDialog != null) toolOptionsDialog.TryClose();

            int index = Array.FindIndex(toolBarsettings.Rows[0].Elements[0].Values, w => w.Equals(toolname.ToLowerInvariant()));
            string code = toolBarsettings.Rows[0].Elements[0].Icons[index];

            toolOptionsDialog?.TryClose();

            capi.Assets.Reload(AssetCategory.dialog);
            toolOptionsSettings = capi.Assets.TryGet("dialog/worldedit-tooloptions-" + code + ".json")?.ToObject<JsonDialogSettings>();
            if (toolOptionsSettings == null)
            {
                return;
            }

            toolOptionsSettings.OnSet = (elem, newval) => { OnSetValueToolOptions(code, elem, newval); };
            toolOptionsSettings.OnGet = OnGetValueToolOptions;

            isComposing = true;
            toolOptionsDialog = new GuiJsonDialog(toolOptionsSettings, capi);
            toolOptionsDialog.TryOpen();
            isComposing = false;
        }


        private void OnSetValueToolbar(string elementCode, string newValue)
        {
            if (isComposing) return;

            switch (elementCode)
            {
                case "tooltype":
                    capi.SendChatMessage("/we t " + newValue);
                    OpenToolOptionsDialog(newValue);
                    break;
            }
        }

        
        private void OnSetValueToolOptions(string code, string elem, string newval)
        {
            if (isComposing) return;

            toolOptionsSettings = capi.Assets.TryGet("dialog/worldedit-tooloptions-" + code + ".json")?.ToObject<JsonDialogSettings>();
            if (toolOptionsSettings == null) return;

            DialogRow[] rows = toolOptionsSettings.Rows;
            int index = 0;
            int row = 0;
            for (row = 0; row < rows.Length; row++)
            {
                index = Array.FindIndex(rows[row].Elements, el => el.Code.Equals(elem));
                if (index != -1) break;
            }

            string cmd = rows[row].Elements[index].Param;
            capi.SendChatMessage(cmd + " " + newval);


            if (ownWorkspace.FloatValues.ContainsKey(elem))
            {
                float val = 0;
                if (float.TryParse(newval, out val))
                {
                    ownWorkspace.FloatValues[elem] = val;
                }
                
            }
            if (ownWorkspace.IntValues.ContainsKey(elem))
            {
                int val = 0;
                if (int.TryParse(newval, out val))
                {
                    ownWorkspace.IntValues[elem] = val;
                }
            }
            if (ownWorkspace.StringValues.ContainsKey(elem)) ownWorkspace.StringValues[elem] = newval;

        }

        private string OnGetValueToolbar(string elementCode)
        {
            if (ownWorkspace == null) return "";

            if (elementCode == "tooltype")
            {
                if (ownWorkspace.ToolName == null || ownWorkspace.ToolName.Length == 0 || !ownWorkspace.ToolsEnabled) return "-1";
                return ownWorkspace.ToolName.ToLowerInvariant();
            }
            
            return "";
        }


        private string OnGetValueToolOptions(string elementCode)
        {
            if (ownWorkspace == null) return "";

            if (ownWorkspace.FloatValues.ContainsKey(elementCode)) return "" + ownWorkspace.FloatValues[elementCode];
            if (ownWorkspace.IntValues.ContainsKey(elementCode)) return "" + ownWorkspace.IntValues[elementCode];
            if (ownWorkspace.StringValues.ContainsKey(elementCode)) return "" + ownWorkspace.StringValues[elementCode];

            return "";
        }
        
        private void Event_LeaveWorld()
        {
            toolBarDialog?.Dispose();
            controlsDialog?.Dispose();
            toolOptionsDialog?.Dispose();
            settingsDialog?.Dispose();
        }

    }


    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RequestWorkSpacePacket
    {

    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ChangePlayerModePacket
    {
        public EnumFreeMovAxisLock? axisLock;
        public float? pickingRange;
        public bool? fly;
        public bool? noclip;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CopyToClipboardPacket
    {
        public string Text;
    }


    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SchematicJsonPacket
    {
        public string Filename;
        public string JsonCode;
    }


}
