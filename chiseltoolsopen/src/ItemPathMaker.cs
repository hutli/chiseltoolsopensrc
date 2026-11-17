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

namespace chisel.src
{
    
    class ItemPathMaker:Item
    {
        //TODO - add tool wear in survival
        //  - add paintbrush graphic and sound effect
        //  - add check to not use paintbrush if materials match
        
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            handling = EnumHandHandling.PreventDefaultAction;
            if (blockSel == null) { return; }
            
            //Do nothing if we don't have access to selected block
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }
            if (api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(blockSel.Position) == true)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }
            
            //TODO Add Survival Mode damage and ladder draw
            BlockChisel bc = api.World.BlockAccessor.GetBlock(blockSel.Position) as BlockChisel;
            if (bc == null) { return; }
            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bmb == null) { return; }
            if (bmb.VoxelCuboids==null||bmb.BlockIds==null||bmb.VoxelCuboids.Count==0||bmb.BlockIds.Count()==0) { return; }
            //First Copy the original block
            string copiedname = bmb.BlockName;
            List<uint> copiedblockvoxels = new List<uint>(bmb.VoxelCuboids);
            List<int> copiedblockmaterials = new List<int>(bmb.BlockIds);

            int copiedvolume = (int)(bmb.VolumeRel * 16f * 16f * 16f);
            //Then Create our new ladder block
            AssetLocation al = new AssetLocation("chiseltools:pathedchiseledblock");
            Block nb = api.World.GetBlock(al);
            if (bc.BlockId == nb.BlockId) { return; }
            //Check for inventory
            bool hasinv = false;
            bool iscreative = byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative;
            Block pathblock = api.World.GetBlock(new AssetLocation("game:stonepath-free"));
            if (!iscreative)
            {
                //HERE WE HAVE TO FIND A PATH BLOCK AND CONSUME IT, OR RETURN IF NOT AVAILABLE
                foreach (IInventory pi in byPlayer.InventoryManager.Inventories.Values)
                {
                    if (pi == null || pi.ClassName == "creative") { continue; }//** INTERESTING there's a creative inventory

                    ItemSlot hit = pi.FirstOrDefault(x => (x != null) && (!x.Empty) && x.Itemstack != null && x.Itemstack.Block != null && x.Itemstack.Block.Id == pathblock.BlockId && x.StackSize > 0);

                    if (hit != null&&!hit.Empty&&hit.Itemstack!=null&&hit.Itemstack.StackSize>0)
                    {
                        hit.Itemstack.StackSize--;
                        if (hit.Itemstack.StackSize == 0) { hit.Itemstack = null; }
                        
                        hit.MarkDirty();
                        
                        hasinv = true;
                        break;
                    }
                    
                }
                if (!hasinv) { return; }
            }
            api.World.BlockAccessor.SetBlock(nb.BlockId, blockSel.Position);
            bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            bmb.BlockName = copiedname;
            //Then Paste shape and material data into new block
            bmb.BlockIds = copiedblockmaterials.ToArray();
            bmb.VoxelCuboids = new List<uint>(copiedblockvoxels);
            
            api.World.BlockAccessor.MarkBlockEntityDirty(blockSel.Position);
            api.World.BlockAccessor.MarkBlockDirty(blockSel.Position);
            // need our own version of this: bmb.RegenSelectionBoxes(byPlayer);
            bmb.RegenSelectionBoxes(api.World,null);
            bmb.MarkDirty(true);
            if (api is ICoreServerAPI && !iscreative)
            {
                int dmg = 1;
                this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, dmg);
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            }
            api.World.PlaySoundAt(new AssetLocation("sounds/stone_move"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefaultAction;
            return;
            
        }

        


    }
}
