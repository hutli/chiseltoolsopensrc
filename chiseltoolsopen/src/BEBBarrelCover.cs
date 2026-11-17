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
    public class BEBBarrelCover : BEBChiseledCover
    {
        ItemStack closedshapestack;
        BlockEntityBarrel mybarrel;
        //return the open/closed item stack as appropriate
        bool lastsealcheck=false;

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            mybarrel=Blockentity as BlockEntityBarrel;
            if (mybarrel != null)
            {
                lastsealcheck = mybarrel.Sealed;
                if (api is ICoreClientAPI)
                {
                    mybarrel.RegisterGameTickListener(OnLongClientTick, 3000);
                }
            }
            base.Initialize(api, properties);
            
        }

        public BEBBarrelCover(BlockEntity blockentity) : base(blockentity)
        {
        }
        public void OnLongClientTick(float dt)
        {
            if (mybarrel.Sealed != lastsealcheck)
            {
                GenMesh();
                lastsealcheck = mybarrel.Sealed;
                mybarrel.MarkDirty(true);
            }
        }
        public override void DumpInventory(bool triggernewmesh)
        {
            if (closedshapestack == null || closedshapestack.StackSize == 0) { base.DumpInventory(triggernewmesh);return; }
            if (sapi != null)
            {
                sapi.World.SpawnItemEntity(closedshapestack, base.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            closedshapestack = null;
            
            Blockentity.MarkDirty(true);
            base.DumpInventory(triggernewmesh);
        }
        
        public virtual void DumpOnlyCoveredShape()
        {
            if (closedshapestack == null || closedshapestack.StackSize == 0) { return; }
            if (sapi != null)
            {
                sapi.World.SpawnItemEntity(closedshapestack, base.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            closedshapestack = null;

            Blockentity.MarkDirty(true);
        }

        public virtual bool SetClosedShape(ItemSlot fromslot)
        {
            if (fromslot == null || fromslot.Empty) { return false; }
            ItemStack shapestack = fromslot.Itemstack;
            if (shapestack.Collectible == null || shapestack.StackSize == 0 || shapestack.Block == null) { return false; }
            if (shapestack.Block.Code.ToString() != "game:chiseledblock") { return false; }
            if (closedshapestack != null && closedshapestack.StackSize > 0 && sapi != null)
            {

                DumpOnlyCoveredShape();

            }
            closedshapestack = shapestack.Clone();
            closedshapestack.StackSize = 1;
            
            Blockentity.MarkDirty(true);


            return true;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("itemstackclosed", closedshapestack);

        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            closedshapestack = tree.GetItemstack("itemstackclosed");
            if (closedshapestack != null)
            {
                closedshapestack.ResolveBlockOrItem(worldAccessForResolve);
                
            }


        }
        
        public override void GenMesh()
        {
            //Problem: This does not get called when the barrel is unsealed (so ontesselate is never called)
            
            if (closedshapestack != null)
            {
                
                if (mybarrel != null && mybarrel.Sealed)
                {
                    meshdata = BEBChiseledCover.CreateModelFromChiseledItemStack(capi,closedshapestack); return;
                }

            }

            base.GenMesh();
            
            //Blockentity.MarkDirty(true);


        }
        
    }
}
