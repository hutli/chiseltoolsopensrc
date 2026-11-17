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
    public class BBChiseledCover : BlockBehavior
    {
        public BBChiseledCover(Block block) : base(block)
        {
        }
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)

        {

            bool result = TryAddCover(world, byPlayer, blockSel);
            
            if (result) { handling = EnumHandling.Handled;return true; }
            
            result= DoWrenchClick(world, byPlayer, blockSel);
            if (result) { handling=EnumHandling.Handled;return true; }

            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

        public virtual bool DoWrenchClick(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer==null||byPlayer.InventoryManager==null||byPlayer.InventoryManager.ActiveTool==null) return false;
            if (byPlayer.InventoryManager.ActiveTool== EnumTool.Wrench)
            {
                
                BlockEntity myBE = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (myBE == null) return false;
                BEBChiseledCover mycover = myBE.GetBehavior<BEBChiseledCover>();
                if (mycover==null) return false;
                if (byPlayer.Entity.Controls.CtrlKey)
                {
                    mycover.ToggleShapeLock();
                    return true;
                }
                if (mycover.ShapeLocked) { return false; }
                mycover.DumpInventory(true);
                
                return true;
            }
            return false;
        }

        public virtual bool TryAddCover(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity myBE = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (myBE != null && byPlayer != null)
            {
                BEBChiseledCover mycover = myBE.GetBehavior<BEBChiseledCover>();
                if (mycover != null)
                {
                    //see if the cover system accepts what they are holding
                    bool success = mycover.SetShape(byPlayer.InventoryManager.ActiveHotbarSlot);
                    if (success)
                    {
                        
                        if (byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        {
                            byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.StackSize--;
                            if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.StackSize == 0)
                            {
                                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = null;
                            }
                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        }
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
