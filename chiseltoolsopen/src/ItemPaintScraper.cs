using chisel.src;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using VSSurvivalMod.Systems.ChiselModes;

namespace chiseltools.src
{
    /// <summary>
    /// Paint Scraper - remove, condense materials on a chiseled block
    /// - remove & condense, have settable chance to recover blocks
    /// - remove - removes a material from the stack, removing the voxels and materials
    /// - condense - removes materials that are not used by any voxel
    /// </summary>
    public class ItemPaintScraper:Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (handling == EnumHandHandling.PreventDefault) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (blockSel?.Position == null) return;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            BlockEntityChisel bec = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bec == null) { base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);return; }
            if (CleanMaterials(slot, byEntity, bec))
            {
                if (byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, 1);
                }
                byEntity.Api.World.PlaySoundAt(new AssetLocation("sounds/stone_move"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 6, 0.25f);
            }
        }
        /// <summary>
        /// Will remove unused materials from a chiseled block if necessary
        /// </summary>
        /// <param name="slot">Item Slot of tool</param>
        /// <param name="byEntity">Entity using tool</param>
        /// <param name="bec">Chiseled block entity</param>
        /// <returns>false - nothing happenend, true - changes were made</returns>
        public static bool CleanMaterials(ItemSlot slot, EntityAgent byEntity, BlockEntityChisel bec)
        {
            //bunch of conditions where we don't need to (or shouldn't try to) do anything
            if (bec == null || bec.BlockIds == null || bec.VoxelCuboids==null||bec.VoxelCuboids.Count==0|| bec.BlockIds.Length < 2) { return false; }


            //step one we're going to find and compress duplicate materials
            Dictionary<int,byte>MaterialIndexSwaps= new Dictionary<int,byte>();
            for (byte c=0;c<bec.BlockIds.Length;c++)
            {
                if (!MaterialIndexSwaps.Keys.Contains(bec.BlockIds[c])) {
                    MaterialIndexSwaps[bec.BlockIds[c]] = c;
                }//we haven't used this material yet so it's the master version

            }
            //now we cycle thru the cuboids and remove any duplicates
            List <uint> newCuboids= new List<uint>();
            foreach (uint v in bec.VoxelCuboids)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                BlockEntityMicroBlock.FromUint(v, cwm);
                if (cwm == null) continue; //this shouldn't happen but we must check lol
                byte newuse = 0;//default to first material
                if ((int)cwm.Material < bec.BlockIds.Length) //extra check for invalid material indices
                {
                    int usedmat = bec.BlockIds[cwm.Material];
                    if (MaterialIndexSwaps.ContainsKey(usedmat))
                    {
                        newuse = MaterialIndexSwaps[usedmat];
                    }
                    else
                    {
                        newuse = cwm.Material;
                    }
                }
                cwm.Material = newuse;
                newCuboids.Add(BlockEntityMicroBlock.ToUint(cwm));

            }
            bec.VoxelCuboids = newCuboids; //replace old cuboids with new duplicate removed list

            List<byte> usedMaterial = new List<byte>(); //this is a list of references, not blockids!
            List<int> usedBlockIDs = new List<int>(); //this is a list of block IDs
            Dictionary<byte, int> MaterialIndexReference = new Dictionary<byte, int>();


            foreach (uint v in bec.VoxelCuboids)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                BlockEntityMicroBlock.FromUint(v, cwm);
                if (cwm == null) continue;
                if (!MaterialIndexReference.ContainsKey(cwm.Material)) { MaterialIndexReference[cwm.Material] = bec.BlockIds[cwm.Material]; }
            }
            if (MaterialIndexReference.Keys.Count== bec.BlockIds.Length) { return false; } //everything was used do nothing
            //now we need to figure out what wasn't used
            //then figure out what indices need to change, maybe build a new list?
            List<byte> crossreference = new List<byte>();
            int[]newmats=MaterialIndexReference.Values.ToArray();
            for(int c = 0; c < newmats.Length; c++)
            {
                //so we're building the new index value of the old index (ow my brain)
                crossreference.Add((byte)bec.BlockIds.IndexOf(newmats[c]));
            }
            List<uint> newvox = new List<uint>();
            foreach (uint v in bec.VoxelCuboids)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                BlockEntityMicroBlock.FromUint(v, cwm);
                cwm.Material = (byte)Math.Min(crossreference.IndexOf(cwm.Material),newmats.Length-1);
                newvox.Add(BlockEntityMicroBlock.ToUint(cwm));
            }
            
            bec.BlockIds= newmats; 
            bec.VoxelCuboids= newvox;
            if (byEntity.Api is ICoreClientAPI)
            {
                
                bec.RegenMesh(byEntity.Api as ICoreClientAPI);
               
                PantographData pd = new PantographData(byEntity.Api,bec);
                (ChiselToolLoader.loader.chiselnet as IClientNetworkChannel).SendPacket<PantographData>(pd);
            }
            bec.MarkDirty(true);
            
            return true;

        }
        
    }
}
