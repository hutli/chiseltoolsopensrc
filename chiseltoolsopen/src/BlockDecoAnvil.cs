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
    public class BlockDecoAnvil:BlockAnvil
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (TryDecoInteract(world, byPlayer, blockSel)) { return true; }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        public bool TryDecoInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BBChiseledCover mybb = this.GetBehavior<BBChiseledCover>();
            if (mybb != null)
            {
                if (mybb.DoWrenchClick(world, byPlayer, blockSel)) { return true; }
                //handle clicks with chiseled block and wrench
                bool handled = mybb.TryAddCover(world, byPlayer, blockSel);
                if (handled) { return true; }
                

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
