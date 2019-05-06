using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.WorldEdit
{
    public class ShapeToPositionList
    {
        public static List<BlockPos> Cuboid(BlockPos start, BlockPos end)
        {
            List<BlockPos> positions = new List<BlockPos>();

            BlockPos startPos = new BlockPos(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            BlockPos finalPos = new BlockPos(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));

            BlockPos curPos = startPos.Copy();

            while (curPos.X < finalPos.X)
            {
                curPos.Y = startPos.Y;

                while (curPos.Y < finalPos.Y)
                {
                    curPos.Z = startPos.Z;
                    while (curPos.Z < finalPos.Z)
                    {
                        positions.Add(curPos.Copy());
                        curPos.Z++;
                    }

                    curPos.Y++;
                }
                curPos.X++;
            }

            return positions;
        }

        public static List<BlockPos> Ball(BlockPos center, float radius)
        {
            List<BlockPos> positions = new List<BlockPos>();

            int radInt = (int)Math.Ceiling(radius / 2f);
            float radSq = radius * radius / 4f;

            for (int dx = -radInt; dx <= radInt; dx++)
            {
                for (int dy = -radInt; dy <= radInt; dy++)
                {
                    for (int dz = -radInt; dz <= radInt; dz++)
                    {
                        if (dx * dx + dy * dy + dz * dz > radSq) continue;
                        positions.Add(center.AddCopy(dx, dy, dz));
                    }
                }
            }

            return positions;
        }
    }
}
