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
    /// <summary>
    /// TODO (between this and BEBBarrel Cover)
    /// needs to cover shift clicking to insert the covered version
    /// needs to return the covered version when disposed
    /// if no covered version it won't show anything
    /// use material 0 for the contents, reassign the material on the fly?
    /// </summary>
    public class BBBarrelCover:BBChiseledCover
    {
        public BBBarrelCover(Block block) : base(block)
        {
        }
        public override bool TryAddCover(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //if player isn't crouching then pass it on
            if (!byPlayer.Entity.Controls.CtrlKey) { return base.TryAddCover(world, byPlayer, blockSel); }
            
            BlockEntity myBE = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (myBE != null && byPlayer != null)
            {
                BEBBarrelCover mybarrelcover = myBE.GetBehavior<BEBBarrelCover>();
                if (mybarrelcover == null) { return base.TryAddCover(world,byPlayer,blockSel); }
                if (mybarrelcover != null)
                {
                    //see if the cover system accepts what they are holding
                    bool success = mybarrelcover.SetClosedShape(byPlayer.InventoryManager.ActiveHotbarSlot);
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
