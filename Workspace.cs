using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{

    public enum EnumWorldEditConstraint
    {
        None = 0,
        Selection = 1
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WorldEditWorkspace
    {
        public bool ToolsEnabled;

        public string PlayerUID;
        public EnumWorldEditConstraint WorldEditConstraint;

        BlockPos prevStartMarker;
        BlockPos prevEndMarker;

        private BlockPos startMarker;
        private BlockPos endMarker;

        public BlockPos StartMarker { 
            get
            {
                return startMarker;
            } 
            set
            {
                if (world != null) world.Api.ObjectCache["weStartMarker-" + PlayerUID] = value;
                startMarker = value;
            }
        }
        public BlockPos EndMarker
        {
            get
            {
                return endMarker;
            }
            set
            {
                if (world != null) world.Api.ObjectCache["weEndMarker-" + PlayerUID] = value;
                endMarker = value;
            }
        }

        public Vec3d StartMarkerExact;
        public Vec3d EndMarkerExact;

        public int ImportAngle;
        public bool ImportFlipped;
        /// <summary>
        /// If false, removing/placing blocks will not update their light values
        /// </summary>
        public bool DoRelight = true;


        internal IBlockAccessorRevertable revertableBlockAccess;
        internal IWorldAccessor world;

        public bool serverOverloadProtection = true;
        public EnumToolOffsetMode ToolOffsetMode;

        public Dictionary<string, float> FloatValues = new Dictionary<string, float>();
        public Dictionary<string, int> IntValues = new Dictionary<string, int>();
        public Dictionary<string, string> StringValues = new Dictionary<string, string>();
        public Dictionary<string, byte[]> ByteDataValues = new Dictionary<string, byte[]>();

        public string ToolName = null;
        internal ToolBase ToolInstance;
        internal BlockSchematic clipboardBlockData;

        public WorldEditWorkspace()
        {
            
        }

        public WorldEditWorkspace(IWorldAccessor world, IBlockAccessorRevertable blockAccessor)
        {
            this.revertableBlockAccess = blockAccessor;
            this.world = world;

            blockAccessor.OnStoreHistoryState += BlockAccessor_OnStoreHistoryState;
            blockAccessor.OnRestoreHistoryState += BlockAccessor_OnRestoreHistoryState;
        }

        private void BlockAccessor_OnRestoreHistoryState(HistoryState obj, int dir)
        {
            if (dir > 0)
            {
                StartMarker = obj.NewStartMarker?.Copy();
                EndMarker = obj.NewEndMarker?.Copy();
            } else
            {
                StartMarker = obj.OldStartMarker?.Copy();
                EndMarker = obj.OldEndMarker?.Copy();
            }
            
            HighlightSelectedArea();
        }

        private void BlockAccessor_OnStoreHistoryState(HistoryState obj)
        {
            obj.OldStartMarker = prevStartMarker?.Copy();
            obj.OldEndMarker = prevEndMarker?.Copy();

            obj.NewStartMarker = StartMarker?.Copy();
            obj.NewEndMarker = EndMarker?.Copy();

            prevStartMarker = StartMarker?.Copy();
            prevEndMarker = EndMarker?.Copy();
        }

        public void SetTool(string toolname, ICoreAPI api)
        {
            this.ToolName = toolname;
            if (ToolInstance != null) ToolInstance.Unload(api);
            if (toolname != null)
            {
                ToolInstance = ToolRegistry.InstanceFromType(toolname, this, revertableBlockAccess);
                ToolInstance.Load(api);
            }
        }


        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(ToolsEnabled);
            writer.Write(PlayerUID);

            writer.Write(StartMarker == null);
            if (StartMarker != null)
            {
                writer.Write(StartMarker.X);
                writer.Write(StartMarker.Y);
                writer.Write(StartMarker.Z);
            }

            writer.Write(EndMarker == null);
            if (EndMarker != null)
            {
                writer.Write(EndMarker.X);
                writer.Write(EndMarker.Y);
                writer.Write(EndMarker.Z);
            }

            writer.Write(FloatValues.Count);
            foreach (var val in FloatValues)
            {
                writer.Write(val.Key);
                writer.Write(val.Value);
            }

            writer.Write(IntValues.Count);
            foreach (var val in IntValues)
            {
                writer.Write(val.Key);
                writer.Write(val.Value);
            }
            
            writer.Write(StringValues.Count);
            foreach (var val in StringValues)
            {
                writer.Write(val.Value == null);

                if (val.Value == null) continue;

                writer.Write(val.Key);
                writer.Write(val.Value);
            }

            writer.Write(ByteDataValues.Count);
            foreach (var val in ByteDataValues)
            {
                writer.Write(val.Value == null);

                if (val.Value == null) continue;

                writer.Write(val.Key);
                writer.Write(val.Value.Length);
                writer.Write(val.Value);
            }

            writer.Write(ToolName == null);

            if (ToolName != null) {
                writer.Write(ToolName);
            }
                
            writer.Write((int)ToolOffsetMode);
            writer.Write(DoRelight);

            writer.Write(serverOverloadProtection);
        }

        public void FromBytes(BinaryReader reader)
        {
            try
            {
                ToolsEnabled = reader.ReadBoolean();
                PlayerUID = reader.ReadString();

                if (!reader.ReadBoolean())
                {
                    StartMarker = new BlockPos(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                }
                else
                {
                    StartMarker = null;
                }

                if (!reader.ReadBoolean())
                {
                    EndMarker = new BlockPos(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                }
                else
                {
                    EndMarker = null;

                }

                FloatValues = new Dictionary<string, float>();
                IntValues = new Dictionary<string, int>();
                StringValues = new Dictionary<string, string>();
                ByteDataValues = new Dictionary<string, byte[]>();

                int floatValCount = reader.ReadInt32();
                for (int i = 0; i < floatValCount; i++)
                {
                    FloatValues[reader.ReadString()] = reader.ReadSingle();
                }

                int intValCount = reader.ReadInt32();
                for (int i = 0; i < intValCount; i++)
                {
                    IntValues[reader.ReadString()] = reader.ReadInt32();
                }

                int stringValCount = reader.ReadInt32();
                for (int i = 0; i < stringValCount; i++)
                {
                    if (!reader.ReadBoolean())
                    {
                        StringValues[reader.ReadString()] = reader.ReadString();
                    }
                    
                }

                int byteDataValCount = reader.ReadInt32();
                for (int i = 0; i < byteDataValCount; i++)
                {
                    if (!reader.ReadBoolean())
                    {
                        string key = reader.ReadString();
                        int qbytes = reader.ReadInt32();
                        ByteDataValues[key] = reader.ReadBytes(qbytes);
                    }
                }

                if (!reader.ReadBoolean())
                {
                    ToolName = reader.ReadString();
                    SetTool(ToolName, world.Api);
                }

                ToolOffsetMode = (EnumToolOffsetMode)reader.ReadInt32();
                DoRelight = reader.ReadBoolean();

                revertableBlockAccess.Relight = DoRelight;

                serverOverloadProtection = reader.ReadBoolean();
            }
            catch (Exception) { }
        }

        public BlockPos GetMarkedMinPos()
        {
            return new BlockPos(
                Math.Min(StartMarker.X, EndMarker.X), 
                Math.Min(StartMarker.Y, EndMarker.Y), 
                Math.Min(StartMarker.Z, EndMarker.Z)
            );
        }

        public BlockPos GetMarkedMaxPos()
        {
            return new BlockPos(
                Math.Max(StartMarker.X, EndMarker.X), 
                Math.Max(StartMarker.Y, EndMarker.Y), 
                Math.Max(StartMarker.Z, EndMarker.Z)
            );
        }

        public void ResendBlockHighlights(WorldEdit we)
        {
            var player = world.PlayerByUid(PlayerUID);
            
            HighlightSelectedArea();
            if (!(ToolInstance is ImportTool && ToolsEnabled))
            {
                we.previewBlocks.ClearChunks();
                we.previewBlocks.UnloadUnusedServerChunks();
            }

            if (ToolsEnabled && ToolInstance != null)
            {
                EnumHighlightBlocksMode mode = EnumHighlightBlocksMode.CenteredToSelectedBlock;
                if (ToolOffsetMode == EnumToolOffsetMode.Attach) mode = EnumHighlightBlocksMode.AttachedToSelectedBlock;
                ToolInstance.HighlightBlocks(player, we, mode);
            }
            else
            {
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Brush, new List<BlockPos>(), new List<int>());
            }
        }



        public void HighlightSelectedArea()
        {
            var player = world.PlayerByUid(PlayerUID);

            if (StartMarker != null && EndMarker != null)
            {
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Selection, new List<BlockPos>(new BlockPos[] { StartMarker, EndMarker }), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cube);
            }
            else
            {
                world.HighlightBlocks(player, (int)EnumHighlightSlot.Selection, new List<BlockPos>());
            }
        }
    }
}
