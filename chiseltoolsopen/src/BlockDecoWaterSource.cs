using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace chiseltools.src
{
    public class BlockDecoWaterSource:BlockDeco
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer == null || byPlayer.InventoryManager == null || byPlayer.InventoryManager.ActiveHotbarSlot==null||byPlayer.InventoryManager.ActiveHotbarSlot.Empty) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            ItemSlot holdingslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (holdingslot.Itemstack.Block is BlockLiquidContainerTopOpened)
            {
                BlockLiquidContainerTopOpened bb = holdingslot.Itemstack.Block as BlockLiquidContainerTopOpened;
                //if player holding CTRL and bucket has water, then empty it
                ItemStack contents = bb.GetContent(holdingslot.Itemstack);
                
                    
                    if (contents != null && contents.Item != null && contents.Item.Code == "game:waterportion")
                    {
                        bb.SetContents(holdingslot.Itemstack,null); //stacksize1000
                        holdingslot.MarkDirty();
                        api.World.PlaySoundAt(new AssetLocation("sounds/effect/water-pour"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                    return true;
                    }
                    
                
                    else if (contents == null || contents.StackSize == 0)
                    {
                        
                        contents = new ItemStack(api.World.GetItem(new AssetLocation("game:waterportion")), (int)bb.CapacityLitres*100);
                        bb.SetContent(holdingslot.Itemstack,contents);
                        holdingslot.MarkDirty();
                        api.World.PlaySoundAt(new AssetLocation("sounds/block/water"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                    return true;

                    }
                
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            BEDecoGeneric deco = blockAccessor.GetBlockEntity(pos) as BEDecoGeneric;
            if (deco != null&&deco.cover!=null&&deco.cover.ChiseledItemStack!=null)
            {
                return deco.cover.GetCollisionBoxes(blockAccessor,pos);
            }
            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BEDecoGeneric deco = blockAccessor.GetBlockEntity(pos) as BEDecoGeneric;
            if (deco != null && deco.cover != null && deco.cover.ChiseledItemStack != null)
            {
                return deco.cover.GetCollisionBoxes(blockAccessor, pos);
            }
            return base.GetSelectionBoxes(blockAccessor, pos);
        }
    }
}
