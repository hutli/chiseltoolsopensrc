using chisel.src;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace chiseltools.src
{
    public class BlockDecoBarrel:BlockBarrel
    {
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
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            Item hotitem = byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Item;
            if (hotitem != null && hotitem.GetType() == typeof(ItemPantograph))
            {
                return false;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
