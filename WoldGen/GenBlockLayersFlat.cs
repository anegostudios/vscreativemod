using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenBlockLayersFlat : ModSystem
    {
        private int chunksize;
        private ICoreServerAPI api;

        ushort[] blockIds;

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

            this.api.Event.InitWorldGenerator(initWorldGen, "superflat");
            this.api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Terrain, "superflat");

            if (!api.ModLoader.IsModEnabled("survival"))
            {
                api.Event.MapRegionGeneration(OnMapRegionGen, "superflat");
            }
        }

        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ)
        {
            mapRegion.ClimateMap = new API.IntMap()
            {
                Data = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                Size = 2
            };
        }

        private void initWorldGen()
        {
            api.WorldManager.SaveGame.EntitySpawning = false;

            IAsset asset = api.Assets.Get("worldgen/layers.json");
            FlatWorldGenConfig flatwgenConfig = asset.ToObject<FlatWorldGenConfig>();

            List<ushort> blockIds = new List<ushort>();

            for (int i = 0; i < flatwgenConfig.blockCodes.Length; i++)
            {
                ushort blockId = api.WorldManager.GetBlockId(flatwgenConfig.blockCodes[i]);
                if (blockId != 0) blockIds.Add(blockId);
            }

            if (blockIds.Count == 0 && flatwgenConfig.blockCodes.Length > 0)
            {
                ushort blockId = api.World.GetBlock(new AssetLocation("creativeblock-1")).BlockId;
                if (blockId != 0)
                {
                    blockIds.Add(blockId);
                    blockIds.Add(blockId);
                    blockIds.Add(blockId);
                    blockIds.Add(blockId);
                }
            }

            this.blockIds = blockIds.ToArray();

            api.WorldManager.SetSealLevel(blockIds.Count);
        }

        private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            // Because blockdata is cached and not cleaned after release
            for (int y = 0; y < chunks.Length; y++)
            {
                ushort[] blockdata = chunks[y].Blocks;
                for (int i = 0; i < blockdata.Length; i++) blockdata[i] = 0;
            }


            ushort[] blocks = chunks[0].Blocks;
            ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;
            ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;


            int yMove = chunksize * chunksize;

            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    int index3d = z * chunksize + x;

                    rainheightmap[index3d] = (ushort)(blockIds.Length-1);
                    terrainheightmap[index3d] = (ushort)(blockIds.Length-1);

                    for (int i = 0; i < blockIds.Length; i++)
                    {
                        blocks[index3d] = blockIds[i];
                        index3d += yMove;
                    }
                }
            }

            chunks[0].MapChunk.YMax = (ushort)blockIds.Length;
        }
    }
}
