using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods.WorldEdit
{

    public class GuiDialogConfirmAcceptFile : GuiDialog
    {
        string text;
        Action<string> DidPressButton;
        public override double DrawOrder => 2;

        static int index = 0;

        public override string ToggleKeyCombinationCode
        {
            get { return null; }
        }


        public GuiDialogConfirmAcceptFile(ICoreClientAPI capi, string text, Action<string> DidPressButton) : base(capi)
        {
            this.text = text;
            this.DidPressButton = DidPressButton;
            Compose();
        }

        private void Compose()
        {
            ElementBounds textBounds = ElementStdBounds.Rowed(0.4f, 0, EnumDialogArea.LeftFixed).WithFixedWidth(500);
            ElementBounds bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding);
            TextDrawUtil util = new TextDrawUtil();
            CairoFont font = CairoFont.WhiteSmallText();

            float y = (float)util.GetMultilineTextHeight(font, text, textBounds.fixedWidth);

            SingleComposer =
                capi.Gui
                .CreateCompo("confirmdialog-" + (index++), ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("Please Confirm"), OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddStaticText(text, font, textBounds)

                    .AddSmallButton(Lang.Get("Ignore all files"), () => { DidPressButton("ignore"); TryClose(); return true; }, ElementStdBounds.MenuButton((y + 80) / 80f).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(6), EnumButtonStyle.Normal)
                    .AddSmallButton(Lang.Get("Accept file"), () => { DidPressButton("accept"); TryClose(); return true; }, ElementStdBounds.MenuButton((y + 80) / 80f).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(6), EnumButtonStyle.Normal)
                    .AddSmallButton(Lang.Get("Accept next 10 files"), () => { DidPressButton("accept10"); TryClose(); return true; }, ElementStdBounds.MenuButton((y + 80) / 80f).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(6).WithFixedAlignmentOffset(-100, 0), EnumButtonStyle.Normal)
                .EndChildElements()
                .Compose()
            ;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public override void OnGuiOpened()
        {
            Compose();
            base.OnGuiOpened();
        }

    }



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
        public WorldEditWorkspace ownWorkspace;

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
            capi.Event.LevelFinalize += Event_LevelFinalize;
            capi.Input.InWorldAction += Input_InWorldAction;

            clientChannel =
                capi.Network.GetChannel("worldedit")
                .SetMessageHandler<WorldEditWorkspace>(OnServerWorkspace)
                .SetMessageHandler<CopyToClipboardPacket>(OnClipboardCopy)
                .SetMessageHandler<SchematicJsonPacket>(OnReceivedSchematic)
            ;

            if (!capi.Settings.Int.Exists("schematicMaxUploadSizeKb"))
            {
                capi.Settings.Int["schematicMaxUploadSizeKb"] = 200;
            }
        }

        private void Event_LevelFinalize()
        {
            capi.Gui.Icons.CustomIcons["worldedit/chiselbrush"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("textures/icons/worldedit/chiselbrush.svg"));
        }

        private void Input_InWorldAction(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (on && ownWorkspace?.ToolsEnabled == true && ownWorkspace.ToolName == "chiselbrush" && (action == EnumEntityAction.InWorldLeftMouseDown || action == EnumEntityAction.InWorldRightMouseDown))
            {
                var blockSel = capi.World.Player.CurrentBlockSelection;

                handled = EnumHandling.PreventDefault;
                clientChannel.SendPacket(new WorldInteractPacket()
                {
                    Position = blockSel.Position,
                    DidOffset = blockSel.DidOffset,
                    Face = blockSel.Face.Index,
                    HitPosition = blockSel.HitPosition,
                    SelectionBoxIndex = blockSel.SelectionBoxIndex,
                    Mode = action == EnumEntityAction.InWorldLeftMouseDown ? 0 : 1
                });
            }
        }

        Queue<SchematicJsonPacket> receievedSchematics = new Queue<SchematicJsonPacket>();
        GuiDialogConfirmAcceptFile acceptDlg;
        private void OnReceivedSchematic(SchematicJsonPacket message)
        {
            int allowCount = capi.Settings.Int["allowSaveFilesFromServer"];

            if (allowCount > 0)
            {
                receiveFile(message);
                return;
            }

            receievedSchematics.Enqueue(message);

            if (allowCount == 0)
            {
                if ((acceptDlg == null || !acceptDlg.IsOpened()))
                {
                    capi.ShowChatMessage("Server tried to send a schematic file, please confirm."); //To accept, set allowSaveFilesFromServer to true in clientsettings.json, or type '.clientconfigcreate allowSavefilesFromServer bool true' but be aware of potential security implications!
                    acceptDlg = new GuiDialogConfirmAcceptFile(capi, Lang.Get("The server wants to send you a schematic file. Please confirm to accept the file.") + "\n\n" + Lang.Get("{0}.json ({1} Kb)", message.Filename, message.JsonCode.Length / 1024), (code) => onConfirm(code));
                    acceptDlg.TryOpen();
                }
                return;
            }

            capi.ShowChatMessage("Server tried to send a schematic file, but it was ignored as a precaution. To enable acceptance, set <a href=\"chattype://.clientconfig allowSaveFilesFromServer 1\">allowSaveFilesFromServer to 1</a>");
        }


        private void onConfirm(string code)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (code == "ignore") capi.Settings.Int["allowSaveFilesFromServer"] = -1;

                if (code == "accept" || code == "accept10")
                {
                    if (code == "accept10")
                    {
                        capi.Settings.Int["allowSaveFilesFromServer"] = 10;
                    }
                    else
                    {
                        capi.Settings.Int["allowSaveFilesFromServer"] = 1;
                    }

                    var sdf = new Queue<SchematicJsonPacket>(receievedSchematics);
                    receievedSchematics.Clear();
                    while (sdf.Count > 0)
                    {
                        if (capi.Settings.Int["allowSaveFilesFromServer"] > 0)
                        {
                            receiveFile(sdf.Dequeue());
                        }
                        else
                        {
                            OnReceivedSchematic(sdf.Dequeue());
                        }
                    }
                }
            }, "acceptfiles");
        }

        private void receiveFile(SchematicJsonPacket message) 
        { 
            try
            {
                string exportFolderPath = capi.GetOrCreateDataPath("WorldEdit");
                string outfilepath = Path.Combine(exportFolderPath, Path.GetFileName(message.Filename));
#if DEBUG
                outfilepath = Path.Combine(exportFolderPath, message.Filename); // Allows use of subfolders. I'm too chicken to allow this in release mode
#endif

                if (!outfilepath.EndsWith(".json"))
                {
                    outfilepath += ".json";
                }

                using (TextWriter textWriter = new StreamWriter(outfilepath))
                {
                    textWriter.Write(message.JsonCode);
                    textWriter.Close();
                }

                capi.Settings.Int["allowSaveFilesFromServer"]--;
                capi.ShowChatMessage(string.Format("Schematic file {0} received and saved. Accepting {1} more.", message.Filename, capi.Settings.Int["allowSaveFilesFromServer"]));
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

                int schematicMaxUploadSizeKb = capi.Settings.Int.Get("schematicMaxUploadSizeKb", 200);
            
                // Limit the file size
                if (bytes / 1024 > schematicMaxUploadSizeKb)
                {
                    capi.TriggerIngameError(this, "schematictoolarge", Lang.Get("Importing of schematics above {0} KB disabled, adjust config schematicMaxUploadSizeKb to change.", schematicMaxUploadSizeKb));
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
            if (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative) return;

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

            // no longer used
            amb.CloudBrightness.Weight = 0;
            amb.CloudDensity.Weight = 0;

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
                case "precipitation":
                    capi.SendChatMessage("/weather setprecip " + newValue.ToFloat()/100f);
                    SendGlobalAmbient();

                    break;
                case "cloudypos":
                    capi.SendChatMessage("/weather cloudypos " + newValue.ToFloat()/255f);
                    break;
                case "weatherpattern":
                    capi.SendChatMessage("/weather seti " + newValue);
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
                case "serveroverloadprotection":
                    capi.SendChatMessage("/we sovp " + newValue);
                    break;
                case "tooloffsetmode":
                    int num;
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
                    bool on = (newValue == "1" || newValue == "true");
                    SendGlobalAmbient(on);

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

            string jsoncode = JsonConvert.SerializeObject(amb);
            capi.SendChatMessage("/setambient " + jsoncode);

            if (!beforeAmbientOverride) settingsDialog.ReloadValues();
            

            if (!enable && beforeAmbientOverride) capi.SendChatMessage("/weather setprecip auto");
            if (enable && !beforeAmbientOverride)
            {
                capi.SendChatMessage("/weather acp 0");
            }
            if (!enable && beforeAmbientOverride) { 
                capi.SendChatMessage("/weather acp 1");
            }

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
                    return "" + (int)(1 * 255);
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
                case "serveroverloadprotection":
                    if (ownWorkspace == null) return "1";
                    return ownWorkspace.serverOverloadProtection ? "1" : "0";
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
            if (index < 0) return;

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

    [ProtoContract]
    public class WorldInteractPacket
    {
        [ProtoMember(1)]
        public int Mode; // 0 = break, 1 = build
        [ProtoMember(2)]
        public BlockPos Position;
        [ProtoMember(3)]
        public int Face;
        [ProtoMember(4)]
        public Vec3d HitPosition;
        [ProtoMember(5)]
        public int SelectionBoxIndex;
        [ProtoMember(6)]
        public bool DidOffset;
    }


}
