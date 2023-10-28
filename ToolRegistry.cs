using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class ToolRegistry
    {
        public static OrderedDictionary<string, Type> ToolTypes = new OrderedDictionary<string, Type>();
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
            RegisterToolType("Move", typeof(MoveTool));
            RegisterToolType("Repeat", typeof(RepeatTool));
            RegisterToolType("Select", typeof(SelectTool));
            RegisterToolType("Paint brush", typeof(PaintBrushTool));
            RegisterToolType("Air brush", typeof(AirBrushTool));
            RegisterToolType("Line", typeof(LineTool));
            RegisterToolType("Eraser", typeof(EraserTool));
            RegisterToolType("Flood Fill", typeof(FloodFillTool));
            RegisterToolType("Raise/lower", typeof(RaiseLowerTool));
            RegisterToolType("Grow/shrink", typeof(GrowShrinkTool)); 
            RegisterToolType("Erode", typeof(ErodeTool));
            RegisterToolType("Import", typeof(ImportTool));
        }
    }
}
