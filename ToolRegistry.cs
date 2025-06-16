using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.ServerMods.WorldEdit
{
    public class ToolRegistry
    {
        public static OrderedDictionary<string, Type> ToolTypes = new ();
        public static int NextFreeToolId = 1;

        public static ToolBase InstanceFromType(string toolname, WorldEditWorkspace workspace, IBlockAccessor blockAccessor)
        {
            if (!ToolTypes.ContainsKey(toolname)) return null;

            return (ToolBase)Activator.CreateInstance(ToolTypes[toolname], new object[] { workspace, blockAccessor });
        }

        public static void RegisterToolType(string name, Type type)
        {
            bool isNew = ToolTypes.ContainsKey(name);

            ToolTypes[name] = type;

            if (isNew) NextFreeToolId++;
        }

        public static bool HasTool(int toolId)
        {
            return ToolTypes.GetKeyAtIndex(toolId) != null;
        }

        public static void RegisterDefaultTools()
        {
            RegisterToolType("move", typeof(MoveTool));
            RegisterToolType("repeat", typeof(RepeatTool));
            RegisterToolType("select", typeof(SelectTool));
            RegisterToolType("paintbrush", typeof(PaintBrushTool));
            RegisterToolType("airbrush", typeof(AirBrushTool));
            RegisterToolType("line", typeof(LineTool));
            RegisterToolType("eraser", typeof(EraserTool));
            RegisterToolType("floodfill", typeof(FloodFillTool));
            RegisterToolType("raiselower", typeof(RaiseLowerTool));
            RegisterToolType("growshrink", typeof(GrowShrinkTool)); 
            RegisterToolType("erode", typeof(ErodeTool));
            RegisterToolType("import", typeof(ImportTool));
        }
    }
}
