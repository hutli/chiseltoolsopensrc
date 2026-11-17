using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Server;

namespace chiseltools.src
{
    public class BlockDecoPlantContainer:BlockPlantContainer
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (TryDecoInteract(world, byPlayer, blockSel)) return true;
            BEDecoPlantContainer be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEDecoPlantContainer;

            if (byPlayer.InventoryManager?.ActiveHotbarSlot?.Empty == false && be != null)
            {
                return be.TryPutContents(byPlayer.InventoryManager.ActiveHotbarSlot, byPlayer);
            }

            return false;
            
        }

        public bool TryDecoInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BBChiseledCover mybb = this.GetBehavior<BBChiseledCover>();
            if (mybb != null)
            {
                //handle clicks with chiseled block and wrench
                bool handled = mybb.TryAddCover(world, byPlayer, blockSel);
                if (handled) { return true; }
                if (mybb.DoWrenchClick(world, byPlayer, blockSel)) {  return true; }

            }
            return false;
        }
        
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {

            if (world.Side is EnumAppSide.Server)
            {
                BlockEntity myrack = world.BlockAccessor.GetBlockEntity(pos);
                if (myrack != null)
                {
                    BEBChiseledCover cover = myrack.GetBehavior<BEBChiseledCover>();
                    if (cover != null) { cover.OnBlockBroken(byPlayer); }
                }
            }
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BEDecoPlantContainer bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEDecoPlantContainer;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float deg22dot5rad = GameMath.PIHALF / 4;
                    float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngle = roundRad;
                }
            }

            return val;
        }

        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos)
        {
            base.OnDecalTesselation(world, decalMesh, pos);
            BEDecoPlantContainer bept = world.BlockAccessor.GetBlockEntity(pos) as BEDecoPlantContainer;
            if (bept != null)
            {
                decalMesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, bept.MeshAngle, 0);
            }
        }
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be != null)
            {
                BEBChiseledCover beb = be.Block.GetBEBehavior<BEBChiseledCover>(pos);
                if (beb != null)
                {
                    string chiselname = beb.GetChiseledName();
                    if (chiselname != "") { return chiselname; }
                }
            }
            return base.GetPlacedBlockName(world, pos);
        }
    }
}
