using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.ServerMods.WorldEdit;

#nullable disable

namespace VSCreativeMod;

public class WorldEditScrollToolMode : GuiDialog
{
    private readonly WorldEditClientHandler _worldEditClientHandler;
    private List<SkillItem> _multilineItems;

    public WorldEditScrollToolMode(ICoreClientAPI capi, WorldEditClientHandler worldEditClientHandler) : base(capi)
    {
        _worldEditClientHandler = worldEditClientHandler;
    }

    public override string ToggleKeyCombinationCode { get; }

    public override void OnGuiOpened()
    {
        ComposeDialog();
    }

    private void ComposeDialog()
    {
        ClearComposers();

        int cols = 2;
        double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGrid.unscaledSlotPadding;
        double innerWidth = cols * size;
        int rows = 2;

        _multilineItems = _worldEditClientHandler.ownWorkspace.ToolInstance.GetAvailableModes(capi);
        foreach (var val in _multilineItems)
        {
            innerWidth = Math.Max(innerWidth,
                CairoFont.WhiteSmallishText().GetTextExtents(val.Name).Width / RuntimeEnv.GUIScale + 1);
        }

        var title = "WorldEdit Scroll tool mode";
        innerWidth = Math.Max(innerWidth,
            CairoFont.WhiteSmallishText().GetTextExtents(title).Width / RuntimeEnv.GUIScale + 1);
        ElementBounds skillGridBounds = ElementBounds.Fixed(0, 30, innerWidth, rows * size);
        ElementBounds textBounds = ElementBounds.Fixed(0, rows * (size + 2) + 5, innerWidth, 25);
        ElementBounds textBounds1 = ElementBounds.Fixed(0, 0, innerWidth, 25);


        SingleComposer =
            capi.Gui
                .CreateCompo("toolmodeselect", ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding / 2), false)
                .BeginChildElements()
                .AddStaticText(title, CairoFont.WhiteSmallishText(), textBounds1)
            ;

        SingleComposer.AddSkillItemGrid(_multilineItems, _multilineItems.Count, 1, (num) => OnSlotClick(num),
            skillGridBounds, "skillitemgrid-1");
        SingleComposer.GetSkillItemGrid("skillitemgrid-1").OnSlotOver = OnSlotOver;

        SingleComposer
            .AddDynamicText("", CairoFont.WhiteSmallishText(), textBounds, "name")
            .EndChildElements()
            .Compose()
            ;
    }

    private void OnSlotOver(int num)
    {
        if (num >= _multilineItems.Count)
            return;

        SingleComposer.GetDynamicText("name").SetNewText(_multilineItems[num].Name);
    }


    private void OnSlotClick(int num)
    {
        var name = _worldEditClientHandler.ownWorkspace.ToolInstance.GetAvailableModes(capi)[num].Name;
        Enum.TryParse<EnumWeToolMode>(name, out var mode);
        _worldEditClientHandler.ownWorkspace.ToolInstance.ScrollMode = mode;
        if (mode == EnumWeToolMode.MoveFar || mode == EnumWeToolMode.MoveNear)
        {
            capi.SendChatMessage("/we normalize quiet");
        }
        TryClose();
    }
}
