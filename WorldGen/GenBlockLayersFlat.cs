using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenBlockLayersFlat : ModSystem
    {
        private int chunksize;
        private ICoreServerAPI api;

        int[] blockIds;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override double ExecuteOrder()
        {
            return 0.4;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            chunksize = api.WorldManager.ChunkSize;

            api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, loadGamePre);

            if (!api.ModLoader.IsModEnabled("survival"))
            {
                api.Event.InitWorldGenerator(initWorldGen, "superflat");
                api.Event.MapRegionGeneration(OnMapRegionGen, "superflat");
            }

            this.api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Terrain, "superflat");
        }

        private void initWorldGen()
        {
            // Needed to set up "superflat" worldgen group
        }

        private void loadGamePre()
        {
            if (api.WorldManager.SaveGame.WorldType != "superflat") return;

            api.WorldManager.SaveGame.EntitySpawning = false;

            IAsset asset = api.Assets.Get("worldgen/layers.json");
            FlatWorldGenConfig flatwgenConfig = asset.ToObject<FlatWorldGenConfig>();

            List<int> blockIds = new List<int>();

            for (int i = 0; i < flatwgenConfig.blockCodes.Length; i++)
            {
                int blockId = api.WorldManager.GetBlockId(flatwgenConfig.blockCodes[i]);
                if (blockId != 0) blockIds.Add(blockId);
            }

            if (blockIds.Count == 0 && flatwgenConfig.blockCodes.Length > 0)
            {
                int blockId = api.World.GetBlock(new AssetLocation("creativeblock-1")).BlockId;
                if (blockId != 0)
                {
                    blockIds.Add(blockId);
                    blockIds.Add(blockId);
                    blockIds.Add(blockId);
                    blockIds.Add(blockId);
                }
            }

            this.blockIds = blockIds.ToArray();

            api.WorldManager.SetSeaLevel(blockIds.Count);
        }

        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            mapRegion.ClimateMap = new IntDataMap2D()
            {
                Data = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                Size = 2
            };
            mapRegion.ForestMap = new IntDataMap2D()
            {
                Data = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                Size = 2
            };
            mapRegion.ShrubMap = new IntDataMap2D()
            {
                Data = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                Size = 2
            };
        }


        private void OnChunkColumnGeneration(IChunkColumnGenerateRequest request)
        {
            var chunks = request.Chunks;
            // Because blockdata is cached and not cleaned after release
            for (int y = 0; y < chunks.Length; y++)
            {
                IServerChunk chunk = chunks[y];
                chunk.Data.ClearBlocksAndPrepare();
            }


            IServerChunk botChunk = chunks[0];
            ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;
            ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;


            int yMove = chunksize * chunksize;
            ushort height = (ushort)(blockIds.Length - 1);

            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    int index3d = z * chunksize + x;

                    rainheightmap[index3d] = height;
                    terrainheightmap[index3d] = height;

                    for (int i = 0; i < blockIds.Length; i++)
                    {
                        botChunk.Data.SetBlockUnsafe(index3d, blockIds[i]);
                        index3d += yMove;
                    }
                }
            }

            chunks[0].MapChunk.YMax = (ushort)blockIds.Length;
        }
    }
}
