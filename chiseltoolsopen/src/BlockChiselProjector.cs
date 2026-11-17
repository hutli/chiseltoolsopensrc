using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace chiseltools.src
{
    public class BlockChiselProjector:BlockDeco
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer == null ) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            if (byPlayer.InventoryManager.ActiveHotbarSlot==null||byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                BEChiselProjector myprojector=world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEChiselProjector;
                if (myprojector==null)
                {
                    return false;
                }
                myprojector.OpenGUI();
                return true;
            }
            
            if (byPlayer.InventoryManager.ActiveTool == EnumTool.Wrench)
            {

                BlockEntity myBE = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (myBE == null) return false;
                BEBChiseledCover mycover = myBE.GetBehavior<BEBChiseledCover>();
                if (mycover == null) return false;
                if (byPlayer.Entity.Controls.CtrlKey)
                {
                    mycover.ToggleShapeLock();
                    return true;
                }
                if (mycover.ShapeLocked) { return false; }
                mycover.DumpInventory(true);

                return true;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
