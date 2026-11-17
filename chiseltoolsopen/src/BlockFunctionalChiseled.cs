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
    class BlockFunctionalChiseled:BlockMicroBlock
    {
        

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);
            if (pos != null)
            {
                BEFunctionChiseled bfc = world.BlockAccessor.GetBlockEntity(pos) as BEFunctionChiseled;
                bfc?.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);
            }
            
        }
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            
            BlockFunctionalChiseled bfc = blockAccessor.GetBlock(pos) as BlockFunctionalChiseled;
            BEFunctionChiseled befcc=blockAccessor.GetBlockEntity(pos) as BEFunctionChiseled;
            
            if (bfc == null || befcc == null|| !befcc.Passable) { return base.GetCollisionBoxes(blockAccessor, pos); }
            
            return null;
            //return base.GetCollisionBoxes(blockAccessor, pos);
        }
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockFunctionalChiseled bfc = blockAccessor.GetBlock(pos) as BlockFunctionalChiseled;
            BEFunctionChiseled befcc = blockAccessor.GetBlockEntity(pos) as BEFunctionChiseled;
            if (bfc != null && befcc != null && !befcc.Passable && befcc.VoxelCuboids!=null&&befcc.VoxelCuboids.Count!=0)
            {
                return base.GetSelectionBoxes(blockAccessor, pos);
            }

            Cuboidf[] cube = { new Cuboidf(0, 0, 0, 1, 1, 1) };

            return cube;
        }
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEFunctionChiseled befcc = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEFunctionChiseled;
            //if the player is holding a pantograph do the pantograph's thing instead
            
            

            Item helditem= byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Item;
            
            
            if (helditem != null && helditem.Code.ToString().Contains("wrench")&&befcc!=null)
            {
                //TODO - open UI
                befcc.OpenGUI();
                return false;
            }
            else if (helditem != null && helditem.Code.ToString().Contains("doortool"))
            {

               return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }
            //if player is holding sneak do the item's thing
            if (byPlayer.Entity.Controls.Sneak) { return base.OnBlockInteractStart(world, byPlayer, blockSel); }
            //otherwise we'll open/close the door
            if (befcc != null)
            {
                if (befcc.TriggerSettings.triggerByInteraction)
                {
                    befcc.ToggleOpenClosed();
                }
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        public override bool AllowSnowCoverage(IWorldAccessor world, BlockPos blockPos)
        {
            return false;
        }
        public override float GetSnowLevel(BlockPos pos)
        {
            return 0;
        }

        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        {
            switch (type){
                case EnumRetentionType.Heat:return 1;break;
                case EnumRetentionType.Water:return 0;break;
                case EnumRetentionType.Sound:return 1;break;
                
            
            }
                
            return base.GetRetention(pos, facing, type);
        }

    }
}
