using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityTestShip : EntityChunky
    {
        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
        }

        public static EntityChunky CreateShip(ICoreServerAPI sapi, IMiniDimension dimension)
        {
            EntityChunky entity = (EntityChunky)sapi.World.ClassRegistry.CreateEntity("EntityTestShip");
            entity.Code = new AssetLocation("testship");
            entity.AssociateWithDimension(dimension);
            return entity;
        }


        public override void OnGameTick(float dt)
        {
            if (this.blocks == null || this.SidedPos == null) return;
            base.OnGameTick(dt);
            // Ship test simulation - test motion
            this.SidedPos.Motion.X = 0.01;
            Pos.Y = (int)Pos.Y + 0.5;
            Pos.Yaw = (float)(Pos.X % 6.3) / 20;
            Pos.Pitch = (float)GameMath.Sin(Pos.X % 6.3) / 5;
            Pos.Roll = (float)GameMath.Sin(Pos.X % 12.6) / 3;
            SidedPos.Pitch = Pos.Pitch;
            SidedPos.Roll = Pos.Roll;
            SidedPos.Y = Pos.Y;
            ServerPos.SetFrom(Pos);
        }
    }
}
