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
using chiseltools.src;

namespace chisel.src
{

    class ItemPaintBrush : Item
    {
        //TODO - add tool wear in survival
        //  - add paintbrush graphic and sound effect
        //  - add check to not use paintbrush if materials match
        //  - creative glow system  game:creativelight-35
        public const int inkslotnumber = 0;
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
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

            //do nothing if we don't have a valid dye/ink block in slot 0
            if (byPlayer.InventoryManager.ActiveHotbarSlotNumber == inkslotnumber) { return; }


            ItemSlot inkslot = byPlayer.InventoryManager.GetHotbarInventory()[inkslotnumber];
            bool lightbooster = false;
            int inkmat = GetInkMat(api, inkslot, out lightbooster);
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

                ChiselToolLoader.AddMaterial(bmb, matblock);

                if (bmb.AvailMaterialQuantities != null)
                {
                    int index = bmb.BlockIds.IndexOf(inkmat);
                    if (index < bmb.AvailMaterialQuantities.Length&&index>=0)
                    {
                        bmb.AvailMaterialQuantities[index] = 16 * 16 * 16;
                    }
                }
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
                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                    return;
                }
                List<int> newmats = new List<int>(bmb.BlockIds);
                //code for checking an old map and lighting it up
                if (lightbooster)
                {
                    string existcode = api.World.GetBlock(newmats[selectedmat]).Code.ToString();
                    if (WhiteDyeBoostXRef.ContainsKey(existcode))
                    {
                        inkmat = api.World.GetBlock(new AssetLocation(WhiteDyeBoostXRef[existcode])).BlockId;
                    }
                }
                newmats[selectedmat] = inkmat;
                ChiselToolLoader.UpdateChiseledBlock(bmb, newmats.ToArray(), null);
            }

            if (api is ICoreServerAPI && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                //Do tool wear
                int dmg = CalcDamage();
                this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, dmg);
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                //inventory usage
                UseInventory(api, inkslot);
            }
            handling = EnumHandHandling.PreventDefaultAction;
        }
        public bool TryChangeBlockToChisel(BlockSelection blockSel, Entity byEntity, IPlayer byPlayer)
        {
            if (blockSel.Block is BlockFunctionalChiseled) return false;
            Block bl = api.World.BlockAccessor.GetBlock(blockSel.Position);
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

        //crossreferences items and blocks to a relevant block to use for a material

        List<string> bannedPaintingMaterials;

        public virtual int GetInkMat(ICoreAPI api, ItemSlot forslot, out bool lightbooster)
        {
            lightbooster = false;
            int result = -1;
            if (forslot.Itemstack == null || forslot.Itemstack.StackSize == 0) { return result; }
            int ispalette = ItemPalette.GetPaletteInk(api, forslot, true);
            if (ispalette != -1) { return ispalette; }
            if (forslot.Itemstack.Block == null || forslot.Itemstack.Block is BlockChisel) { return result; }
            if (forslot.Itemstack.Block is BlockLiquidContainerTopOpened) {
                bool isJonas = (this as ItemJonasPaintBrush) != null;
                return DyeCrossReference(api, forslot, isJonas, out lightbooster);

            }
            
            if (bannedPaintingMaterials == null)
            {
                bannedPaintingMaterials = this.Attributes["blacklist"].AsArray<string>().ToList<string>();
            }
            if (bannedPaintingMaterials.Where(x => forslot.Itemstack.Block.FirstCodePart() == x).Count()>0) { return -1; }

            //bool canChiselSet = forslot.Itemstack.Block.Attributes?["canChisel"].Exists == true;
            //bool canChisel = forslot.Itemstack.Block.Attributes?["canChisel"].AsBool(false) == true;

            //if (canChiselSet && !canChisel) return -1;
            //if (forslot.Itemstack.Block.DrawType != EnumDrawType.Cube) return -1;
            if (forslot.Itemstack.Block != null) { return forslot.Itemstack.Block.Id; }
            if (forslot.Itemstack.Block.SeasonColorMap != null || forslot.Itemstack.Block.ClimateColorMap != null) return -1;

            //Block testblock = api.World.BlockAccessor.GetBlock(new AssetLocation("game:creativeblock-18"));



            return result;
        }
        public static Dictionary<string, string> DyeToCreativeBlockXRef = new Dictionary<string, string>()
        {
            {"game:dye-blue","game:creativeblock-18" },
            {"game:dye-red","game:creativeblock-32" },
            {"game:dye-yellow","game:creativeblock-36" },
            {"game:dye-purple","game:creativeblock-26" },
            {"game:dye-brown","game:creativeblock-2" },
            {"game:dye-green","game:creativeblock-43" },
            {"game:dye-orange","game:creativeblock-35" },
            {"game:dye-black","game:creativeblock-65" },
            {"game:dye-gray","game:creativeblock-72" },
            {"game:dye-pink","game:creativeblock-63" },
            {"game:dye-white","game:creativeblock-79" },
            {"game:dye-woad","game:creativeblock-52" }
        };

        public static Dictionary<string, string> DyeToGlowCreativeBlockXRef = new Dictionary<string, string>()
        {
            {"game:dye-blue","game:creativeglow-53" },
            {"game:dye-red","game:creativeglow-32" },
            {"game:dye-yellow","game:creativeglow-36" },
            {"game:dye-purple","game:creativeglow-56" },
            {"game:dye-brown","game:creativeglow-33" },
            {"game:dye-green","game:creativeglow-43" },
            {"game:dye-orange","game:creativeglow-34" },
            {"game:dye-black","game:creativeglow-79" },
            {"game:dye-gray","game:creativeglow-48" },
            {"game:dye-pink","game:creativeglow-59" },
            {"game:dye-white","chiseltools:halfcreativelight-79" },
            {"game:dye-woad","game:creativeglow-53" }
        };

        public static Dictionary<string, string> WhiteDyeBoostXRef = new Dictionary<string, string>()
        {
            {"game:creativeglow-53","chiseltools:halfcreativelight-53"}, //blue
            { "game:creativeglow-32", "chiseltools:halfcreativelight-32" }, //red
            { "game:creativeglow-36", "chiseltools:halfcreativelight-36" }, //yellow
            {"game:creativeglow-56","chiseltools:halfcreativelight-56" }, //purple
            {"game:creativeglow-33","chiseltools:halfcreativelight-33" }, //brown
            {"game:creativeglow-43","chiseltools:halfcreativelight-43" },//green
            {"game:creativeglow-34","chiseltools:halfcreativelight-34" },//orange
            {"game:creativeglow-79","chiseltools:halfcreativelight-79" },//black
            {"game:creativeglow-48","chiseltools:halfcreativelight-48" },//gray
            {"game:creativeglow-59","chiseltools:halfcreativelight-59" }//pink
            
        };

        

        public static int DyeCrossReference(ICoreAPI api, ItemSlot forslot, bool isJonas, out bool lightbooster)
        {
            lightbooster = false;
            if (forslot == null || forslot.Itemstack == null || forslot.Itemstack.Block == null) { return -1; }
            BlockLiquidContainerTopOpened container = forslot.Itemstack.Block as BlockLiquidContainerTopOpened;
            if (container == null) { return -1; }
            ItemStack containercontents = container.GetContent(forslot.Itemstack);
            if (containercontents == null || containercontents.StackSize == 0||containercontents.Item==null) { return -1; }
            string key = containercontents.Item.Code.ToString();
            if (key== "game:dye-white")
            {
                lightbooster = true;
            }
            if (!(isJonas)&& DyeToCreativeBlockXRef.ContainsKey(key)) { return api.World.GetBlock(new AssetLocation(DyeToCreativeBlockXRef[key])).Id; }
            else if (isJonas && DyeToGlowCreativeBlockXRef.ContainsKey(key)) { return api.World.GetBlock(new AssetLocation(DyeToGlowCreativeBlockXRef[key])).Id; }


            return -1;
        }
        public static Item GetDyeFromCreative(ICoreAPI api, int forBlockID, bool isJonas)
        {
            Block forblock = api.World.GetBlock(forBlockID);
            string value = forblock.Code.ToString();
            
            if (!(isJonas) && DyeToCreativeBlockXRef.ContainsValue(value)) {
                var reverse=DyeToCreativeBlockXRef.ToDictionary(x => x.Value, x => x.Key);
                return api.World.GetItem(new AssetLocation(reverse[value]));
            }
            
            return null;
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

        //return offset voxel coordinates so we can Add a voxel at an appropriate location
        public static Vec3i GetAddVoxelHit(BlockSelection blockSel)
        {
            //convert the hit point into voxel coordinates
            Vec3i s = new Vec3i((int)(blockSel.HitPosition.X * 16f), (int)(blockSel.HitPosition.Y * 16f), (int)(blockSel.HitPosition.Z * 16f));
            
            return s;
        }

        protected virtual int CalcDamage()
        {

            return ChiselToolLoader.serverconfig.paintBrushUseRate;
        }

        public static void UseInventory(ICoreAPI api,ItemSlot inkslot)
        {
        
            if (inkslot == null) { return; }
            if (inkslot.Itemstack == null) { return; }
            if (inkslot.Itemstack.Block == null) { return; }
            BlockLiquidContainerTopOpened container = inkslot.Itemstack.Block as BlockLiquidContainerTopOpened;
            if (container != null)
            {
                ItemStack containercontents = container.GetContent(inkslot.Itemstack);
                if (containercontents == null) { return; }
                containercontents.StackSize-=ChiselToolLoader.serverconfig.paintBrushLiquidMultiplier;
                if (containercontents.StackSize <= 0) { container.SetContent(inkslot.Itemstack,null); }
                inkslot.MarkDirty();
                return;
            }
            inkslot.Itemstack.StackSize-=1;
            if (inkslot.Itemstack.StackSize <= 0) { inkslot.Itemstack = null; }
            inkslot.MarkDirty();
        }
    }
}
