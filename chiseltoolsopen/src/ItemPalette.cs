using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Server;
using ProtoBuf;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.ComponentModel;
using chisel.src;
using System.Drawing;
using System.Reflection;
namespace chiseltools.src
{
    public class ItemPalette : Item
    {
        ICoreClientAPI capi;
        public enum enModes { NORMAL };
        SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api is ICoreClientAPI) { capi = api as ICoreClientAPI; }
            toolModes = ObjectCacheUtil.GetOrCreate(api, "paletteToolModes", () =>
            {
                SkillItem[] modes;

                modes = new SkillItem[1];
                modes[(int)enModes.NORMAL] = new SkillItem() { Code = new AssetLocation(enModes.NORMAL.ToString()), Name = Lang.Get("Normal Mode") };
                if (capi != null)
                {
                    modes[(int)enModes.NORMAL].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/paste and paint.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                }

                return modes;
            });
            base.OnLoaded(api);
        }
        static Dictionary<int,int[]> paletteBlockIDs;
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefaultAction;
            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
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
            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bmb == null)
            {
                TryChangeBlockToChisel(blockSel, byEntity, byPlayer);
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            
            
            ItemSlot inkslot = slot;
            int inkmat = GetPaletteInk(api, inkslot,false);
            if (inkmat == -1)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }
            if (byPlayer.Entity.Controls.ShiftKey)
            {
                //in this case we are adding a material and not changing anything else

                //material exists so we aren't going to do anything
                if (bmb.BlockIds.Contains(inkmat)) { return; }
                Block matblock = api.World.GetBlock(inkmat);
                if (matblock == null) { return; }
                if (matblock.GetCollisionBoxes(api.World.BlockAccessor, blockSel.Position) == null) { return; }
                api.World.PlaySoundAt(new AssetLocation("game:sounds/player/knap" + (api.World.Rand.Next(2) > 0 ? 1 : 2)), byPlayer.Entity.Pos.X, byPlayer.Entity.Pos.Y, byPlayer.Entity.Pos.Z, byPlayer, true, 12, 1);

                bmb.AddMaterial(matblock);

                bmb.AvailMaterialQuantities = new ushort[bmb.BlockIds.Length];
                for (int c = 0; c < bmb.BlockIds.Length; c++)
                {
                    bmb.AvailMaterialQuantities[c] = 16 * 16 * 16;
                }
                
                /*if (bmb.AvailMaterialQuantities != null)
                {
                    int index = bmb.BlockIds.IndexOf(inkmat);
                    index = Math.Clamp(index, 0, bmb.BlockIds.Length);
                    bmb.AvailMaterialQuantities[index] = 16 * 16 * 16;
                }*/

                bmb.MarkDirty();

            }
            else
            {
                Vec3i s = GetVoxelHit(blockSel);
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                byte selectedmat = 0;

                foreach (uint voxint in bmb.VoxelCuboids)
                {
                    //this static method converts the uint into a CubioidWithMaterial
                    BlockEntityMicroBlock.FromUint(voxint, cwm);
                    //if we're looking at this voxel we want to grab this material to use
                    if (cwm.Contains(s.X, s.Y, s.Z)) { selectedmat = cwm.Material; break; }
                }
                //if materials paint we don't want to do anything
                if (bmb.BlockIds[selectedmat] == inkmat)
                {
                    handling = EnumHandHandling.PreventDefaultAction;
                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                    return;
                }
                List<int> newmats = new List<int>(bmb.BlockIds);
                //code for checking an old map and lighting it up
                
                newmats[selectedmat] = inkmat;
                bmb.BlockIds = newmats.ToArray();
            }
            bmb.MarkDirty(true);
            ChiselToolLoader.Relight(api as ICoreServerAPI, blockSel.Position);
            if (api is ICoreServerAPI && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                //Do tool wear
                int dmg = 1;
                this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, dmg);
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                //inventory usage
                
            }
            handling = EnumHandHandling.PreventDefaultAction;
        }
        //returns voxel coordinates of a block selection, accounting for facing etc
        public static Vec3i GetVoxelHit(BlockSelection blockSel)
        {
            //convert the hit point into voxel coordinates
            Vec3i s = new Vec3i((int)(blockSel.HitPosition.X * 16f), (int)(blockSel.HitPosition.Y * 16f), (int)(blockSel.HitPosition.Z * 16f));
            if (blockSel.Face == BlockFacing.SOUTH) { s.Z--; }
            if (blockSel.Face == BlockFacing.UP) { s.Y--; }
            if (blockSel.Face == BlockFacing.EAST) { s.X--; }
            return s;
        }
        public bool TryChangeBlockToChisel(BlockSelection blockSel, Entity byEntity, IPlayer byPlayer)
        {
            if (blockSel.Block is BlockFunctionalChiseled) return false;
            Block bl = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (bl.Code.ToString() == "game:mantle") { return false; }
            string blockName = bl.GetPlacedBlockName(byEntity.World, blockSel.Position);
            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));
            if (!IsChiselingAllowedFor(bl, byPlayer)) { return false; }
            byEntity.World.BlockAccessor.SetBlock(chiseledblock.BlockId, blockSel.Position);
            BlockEntityChisel be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (be == null) return false;

            be.WasPlaced(bl, blockName);
            return true;
        }
        public bool IsChiselingAllowedFor(Block block, IPlayer player)
        {
            if (block is BlockChisel) return true;

            // First priority: microblockChiseling disabled
            ITreeAttribute worldConfig = api.World.Config;
            string mode = worldConfig.GetString("microblockChiseling");
            if (mode == "off") return false;


            // Second priority: canChisel flag

            if (block.DrawType != Vintagestory.API.Client.EnumDrawType.Cube) return false;
            bool canChiselSet = block.Attributes?["canChisel"].Exists == true;
            bool canChisel = block.Attributes?["canChisel"].AsBool(false) == true;

            if (canChisel) return true;
            if (canChiselSet && !canChisel) return false;


            // Third prio: Never non cubic blocks
            if (block.DrawType != EnumDrawType.Cube) return false;

            // Fourth prio: Never tinted blocks (because then the chiseled block would have the wrong colors)
            if (block.SeasonColorMap != null || block.ClimateColorMap != null) return false;

            // Otherwise if in creative mode, sure go ahead
            if (player?.WorldData.CurrentGameMode == EnumGameMode.Creative) return true;


            // Lastly go by the config value
            if (mode == "stonewood")
            {
                // Saratys definitely required Exception to the rule #312
                if (block.Code.Path.Contains("mudbrick")) return true;

                return block.BlockMaterial == EnumBlockMaterial.Wood || block.BlockMaterial == EnumBlockMaterial.Stone || block.BlockMaterial == EnumBlockMaterial.Ore || block.BlockMaterial == EnumBlockMaterial.Ceramic;
            }

            return true;
        }
        public static int[] GetPaletteBlockIds(ICoreAPI capi,ItemSlot slot)
        {
            if (slot==null||slot.Itemstack==null) return null;
            ItemStack forpalette = slot.Itemstack;
            if (capi == null || forpalette == null || forpalette.Item == null) { return null; }
            //this will initialize the paletteBlockID 
            if (paletteBlockIDs == null)
            {
                paletteBlockIDs = new Dictionary<int,int[]> ();
                List<int> tempBlockIDS = new List<int>();
                for (int cc = 0; cc < 80; cc++)
                {
                    AssetLocation al = new AssetLocation("game:creativeblock-" + cc);
                    int thisid = capi.World.GetBlock(al).BlockId;
                    if (thisid > 0) { tempBlockIDS.Add(thisid); }
                    
                }
                paletteBlockIDs[0]=tempBlockIDS.ToArray();//this is the default mat list
            }
            //find this palette's dictionary entry or make it
            if (!paletteBlockIDs.ContainsKey(forpalette.Item.ItemId))
            {
                List<int>tempBlockIDS = new List<int>();
                
                string[] customblocks = forpalette.Item.Attributes["inks"].AsArray<string>();
                if (customblocks!=null&&customblocks.Length > 0)
                {
                    for (int cbc = 0; cbc < customblocks.Length; cbc++)
                    {
                        AssetLocation al = new AssetLocation(customblocks[cbc]);
                        Block cb = capi.World.GetBlock(al);
                        if (cb != null)
                        {
                            tempBlockIDS.Add(cb.BlockId);
                        }
                    }
                    
                }
                else
                {
                    return paletteBlockIDs[0];
                }
                paletteBlockIDs[forpalette.Item.ItemId] = tempBlockIDS.ToArray();
            }
            return paletteBlockIDs[forpalette.Item.ItemId];
            
        }
        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            
            //creativeblock
            
            
            SkillItem[] mats = new SkillItem[GetPaletteBlockIds(capi,slot).Length];
            for (int i = 0; i < mats.Length; i++)
            {
                Block block = api.World.GetBlock(GetPaletteBlockIds(capi,slot)[i]);
                ItemSlot dummySlot = new DummySlot();
                dummySlot.Itemstack = new ItemStack(block);
                //Linebreak = i % 7 == 0,
                mats[i] = new SkillItem()
                {
                    Code = block.Code,
                    Data = GetPaletteBlockIds(capi,slot)[i],
                    Linebreak = i % 25 == 0,
                    Name = block.GetHeldItemName(dummySlot.Itemstack),
                    RenderHandler = (AssetLocation code, float dt, double atPosX, double atPosY) =>
                    {
                        float wdt = (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
                        ICoreClientAPI capi = api as ICoreClientAPI;
                        capi.Render.RenderItemstackToGui(dummySlot, atPosX + wdt / 2, atPosY + wdt / 2, 50, wdt / 2, ColorUtil.WhiteArgb, true, false, false);
                    }
                };
            }
            return mats;
            //return toolModes.Append(mats);

        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {

            int mode = Math.Min(toolModes.Length - 1, slot.Itemstack.Attributes.GetInt("toolMode"));


            return mode;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            for (int i = 0; toolModes != null && i < toolModes.Length; i++)
            {
                toolModes[i]?.Dispose();
            }
            

        }
        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
            int mode = Math.Min(toolModes.Length - 1, slot.Itemstack.Attributes.GetInt("toolMode"));
        }
        
        
        public Block GetSelectedBlock(ItemStack stack)
        {
            if (capi == null) { return null; }
            int blockid=GetPaletteInk(capi, capi.World.Player.InventoryManager.ActiveHotbarSlot, false);
            if (blockid == -1) { return null; }
            Block block = capi.World.GetBlock(blockid);

            return block;
        }
        public override string GetHeldItemName(ItemStack itemStack)
        {
            string basename=base.GetHeldItemName(itemStack);
            Block selectedblock = GetSelectedBlock(itemStack);
            
            if (selectedblock != null)
            {
                basename += "-" + selectedblock.GetHeldItemName(new ItemStack(selectedblock,1));
            }
            
            return basename;
        }

        public static int GetPaletteInk(ICoreAPI api, ItemSlot forslot, bool usedurability = true)
        {
            int result = -1;
            try
            {
                if (forslot == null || forslot.Itemstack == null || forslot.Itemstack.Item == null||forslot.Itemstack.Item.Code==null) { return -1; }
                if (forslot.Itemstack.Item != null && forslot.Itemstack.Item.Code.ToString().StartsWith("chiseltools:palette"))
                {
                    if (ItemPalette.GetPaletteBlockIds(api, forslot) == null) { return result; }
                    if (ItemPalette.GetPaletteBlockIds(api, forslot).Length == 0) { return result; }
                    int mode = Math.Min(ItemPalette.GetPaletteBlockIds(api, forslot).Length - 1, forslot.Itemstack.Attributes.GetInt("toolMode"));
                    if (usedurability)
                    {
                        forslot.Itemstack.Collectible.DamageItem(api.World, null, forslot, 1);
                        forslot.MarkDirty();
                    }
                    return ItemPalette.GetPaletteBlockIds(api, forslot)[mode];
                }
            }
            catch { return -1; }
            return result;
        }
    }
    
}
