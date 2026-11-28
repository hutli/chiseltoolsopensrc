using chisel.src;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Vintagestory.ServerMods;
using VSSurvivalMod.Systems.ChiselModes;

namespace chiseltools.src
{
    internal class TrueChisel : ItemChisel
    {
        /*
         Can we send this packet ourselves?? 
         * if (packetid == 1010)
            {
                Vec3i voxelPos;
                bool isBreak;
                BlockFacing facing;
                using (MemoryStream input = new MemoryStream(data))
                {
                    BinaryReader binaryReader = new BinaryReader(input);
                    voxelPos = new Vec3i(binaryReader.ReadInt32(), binaryReader.ReadInt32(), binaryReader.ReadInt32());
                    isBreak = binaryReader.ReadBoolean();
                    facing = BlockFacing.ALLFACES[binaryReader.ReadInt16()];
                    nowmaterialIndex = binaryReader.ReadByte();
                }

                UpdateVoxel(player, player.InventoryManager.ActiveHotbarSlot, voxelPos, facing, isBreak);
            }

        */
        public override void OnLoaded(ICoreAPI api)
        {

            base.OnLoaded(api);
            ToolModes = ObjectCacheUtil.GetOrCreate(api, "truechiselToolModes", delegate
            {
                SkillItem[] array = new SkillItem[12]
                {
                new SkillItem
                {
                    Code = new AssetLocation("1size"),
                    Name = Lang.Get("1x1x1"),
                    Data = new OneByChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("2size"),
                    Name = Lang.Get("2x2x2"),
                    Data = new TwoByChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("4size"),
                    Name = Lang.Get("4x4x4"),
                    Data = new FourByChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("8size"),
                    Name = Lang.Get("8x8x8"),
                    Data = new EightByChiselModeData()
                },
                new SkillItem
                {
                    Code = new AssetLocation("rotate"),
                    Name = Lang.Get("Rotate"),
                    Data = new RotateChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("flip"),
                    Name = Lang.Get("Flip"),
                    Data = new FlipChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("rename"),
                    Name = Lang.Get("Set name"),
                    Data = new RenameChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("draw1"),
                    Name = Lang.Get("chiseltools:drawmode1x1"),
                    Data = new RenameChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("draw2"),
                    Name = Lang.Get("chiseltools:drawmode2x2"),
                    Data = new RenameChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("draw4"),
                    Name = Lang.Get("chiseltools:drawmode4x4"),
                    Data = new RenameChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("draw8"),
                    Name = Lang.Get("chiseltools:drawmode8x8"),
                    Data = new RenameChiselMode()
                },
                new SkillItem
                {
                    Code = new AssetLocation("textureflip"),
                    Name = Lang.Get("chiseltools:textureflip"),
                    Data = new RenameChiselMode()
                }

                };
                ICoreClientAPI capi2 = api as ICoreClientAPI;
                if (capi2 != null)
                {
                    array = array.Select(delegate (SkillItem i)
                    {
                        ChiselMode chiselMode = (ChiselMode)i.Data;
                        if (i.Code.ToString() == "game:draw1")
                        {
                            return i.WithIcon(capi2, capi2.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/draw1x1.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                        }
                        else if (i.Code.ToString() == "game:draw2")
                        {
                            return i.WithIcon(capi2, capi2.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/draw2x2.svg"), 48, 48, 5, ColorUtil.WhiteArgb));

                        }
                        else if (i.Code.ToString() == "game:draw4")
                        {
                            return i.WithIcon(capi2, capi2.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/draw4x4.svg"), 48, 48, 5, ColorUtil.WhiteArgb));

                        }
                        else if (i.Code.ToString() == "game:draw8")
                        {
                            return i.WithIcon(capi2, capi2.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/draw8x8.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                        }
                        else if (i.Code.ToString() == "game:textureflip")
                        {
                            return i.WithIcon(capi2, capi2.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/textureflip.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                        }
                        return i.WithIcon(capi2, chiselMode.DrawAction(capi2));
                    }).ToArray();
                }

                return array;
            });

        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {

            
            bool needshammer = slot.Itemstack.Item.Attributes["needshammer"].AsBool(true);

            if (handling == EnumHandHandling.PreventDefault) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (byPlayer == null) return;


            if (blockSel?.Position == null) return;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            //Victory over the Tyrony of hammers bwahahahahh (j/k ofc)
            if (needshammer && byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Tool != EnumTool.Hammer && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "nohammer", Lang.Get("Requires a hammer in the off hand"));
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }


            if (api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(blockSel.Position) == true)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            bool carvingTime = false;

            if (block is BlockGroundStorage)
            {
                BlockEntityGroundStorage begs = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
                var neslot = begs.Inventory.FirstNonEmptySlot;
                //1.19
                if (neslot != null && neslot.Itemstack.Block != null && QPIsChiselingAllowedFor(api, blockSel.Position, neslot.Itemstack.Block, byPlayer,needshammer))
                {
                    block = neslot.Itemstack.Block;
                }

                if (AllowHalloweenEvent && block.Code.Path == "pumpkin-fruit-4")
                {
                    carvingTime = DateTime.Now.Month == 10 || DateTime.Now.Month == 11;
                    if (!carvingTime)
                    {
                        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        api.World.BlockAccessor.MarkBlockDirty(blockSel.Position);
                        return;
                    }
                }
            }

            if (!QPIsChiselingAllowedFor(api, blockSel.Position, block, byPlayer,needshammer))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (block.Resistance > 100)
            {
                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "tootoughtochisel", Lang.Get("This material is too strong to chisel"));
                }
                return;
            }


            if (blockSel == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }


            string blockName = block.GetPlacedBlockName(byEntity.World, blockSel.Position);


            if (block is BlockChisel)
            {
                //paint voxel mode
                int mode = slot.Itemstack.Attributes.GetInt("toolMode", -1);
                int matid = slot.Itemstack.Attributes.GetInt("materialId", -1);
                BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
                if (mode == 11)
                {
                    Vec3i hit = ItemPaintBrush.GetVoxelHit(blockSel);
                    int gotmat = bmb.GetVoxelMaterialAt(hit);
                    int cyclemat= ItemPantograph.GetNextSubstitute(api, gotmat);
                    if (cyclemat != gotmat)
                    {
                        //we have a new material possibility
                        ChiselToolLoader.MicroBlockSnapshot before = ChiselToolLoader.Snapshot(bmb);
                        for (int mc = 0; mc < bmb.BlockIds.Length; mc++)
                        {
                            if (bmb.BlockIds[mc] == gotmat)
                            {
                                bmb.BlockIds[mc] = cyclemat;
                                ChiselToolLoader.UpdateBlock(bmb, before);
                                handling = EnumHandHandling.Handled;
                                return;
                                //todo: check for duplicate materials!!
                            }
                        }
                    }
                    return;
                }
                else if (mode >= 7 && mode <= 10 && bmb != null) //if player is holding shift, we're in the correct mode, and have a valid material id then do paint mode
                {
                    int blockIndexAlreadyInStack = bmb.BlockIds.IndexOf(matid);
                    if (matid == -1) { blockIndexAlreadyInStack = 0; } //if no matid has been set for the stack (ie a new chisel)
                    //auto add material in creative
                    else if (blockIndexAlreadyInStack == -1 && byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        
                            bool full = true;

                        ChiselToolLoader.AddMaterial(bmb, byPlayer.Entity.Api.World.GetBlock(matid), out full);
                        handling = EnumHandHandling.Handled;
                        return;
                        blockIndexAlreadyInStack = bmb.BlockIds.IndexOf(matid);
                        byEntity.Api.World.PlaySoundAt(new AssetLocation("sounds/player/gluerepair1"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                        if (blockIndexAlreadyInStack == -1) { return; }

                    }
                    else if (blockIndexAlreadyInStack == -1)
                    {
                        //certain creative blocks can be created with dye
                        Item dye = ItemPaintBrush.GetDyeFromCreative(byEntity.Api, matid, false);


                        foreach (IInventory pi in byPlayer.InventoryManager.Inventories.Values)
                        {
                            if (pi == null || pi.ClassName == "creative") { continue; }//** INTERESTING there's a creative inventory


                            bool founddye = false;
                            
                            var paletteslots = pi.Where(x => x != null && (!x.Empty) && x.Itemstack != null && x.Itemstack.Collectible != null && x.Itemstack.Collectible.Code.ToString().StartsWith("chiseltools:palette")).ToArray();
                            
                            if (paletteslots == null||paletteslots.Count()==0) {
                                //if no palette then check for the actual item
                                var materialslots = pi.Where(x => x != null && (!x.Empty) && x.Itemstack != null && x.Itemstack.Collectible != null && x.Itemstack.Block!=null &&x.Itemstack.Block.BlockId==matid).ToArray();
                                if (materialslots != null && materialslots.Length > 0)
                                {
                                    materialslots[0].Itemstack.StackSize--;
                                    if (materialslots[0].StackSize == 0)
                                    {
                                        materialslots[0].Itemstack = null;
                                        
                                        
                                    }
                                    materialslots[0].MarkDirty();
                                    founddye = true;
                                    bool full2 = false;
                                    ChiselToolLoader.AddMaterial(bmb, byPlayer.Entity.Api.World.GetBlock(matid), out full2);
                                    blockIndexAlreadyInStack = bmb.BlockIds.IndexOf(matid);
                                    byEntity.Api.World.PlaySoundAt(new AssetLocation("sounds/effect/water-fill"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                                    return;
                                }
                                continue;
                            }
                            for (int pc = 0; pc < paletteslots.Count(); pc++)
                            {
                                var paletteslot = paletteslots[pc];
                                bool usedpalette = false;
                                if (paletteslot != null && ItemPalette.GetPaletteBlockIds(api, paletteslot).Contains(matid) &&
                                    paletteslot.Itemstack.Collectible.GetRemainingDurability(paletteslot.Itemstack) > 1)
                                {
                                    founddye = true;
                                    usedpalette = true;
                                    paletteslot.Itemstack.Collectible.DamageItem(api.World, null, paletteslot, 1);
                                    paletteslot.MarkDirty();
                                }
                                if (dye != null && !usedpalette)
                                {
                                    //get all containers and buckets
                                    var containerslots = pi.Where(x => (x != null && (!x.Empty) && x.Itemstack != null && x.Itemstack.Block != null && (x.Itemstack.Block.Class == "BlockBucket" || x.Itemstack.Block.Class == "BlockLiquidContainerTopOpened")));
                                    if (containerslots == null)
                                    {
                                        if (api.Side is EnumAppSide.Client)
                                        {
                                            (api as ICoreClientAPI).ShowChatMessage("Null Container Slot " + dye.Code.ToString());
                                        }
                                    }
                                    foreach (ItemSlot inkslot in containerslots)
                                    {
                                        if (inkslot == null) { continue; }
                                        BlockLiquidContainerTopOpened container = inkslot.Itemstack.Block as BlockLiquidContainerTopOpened;
                                        if (container == null) { continue; }
                                        ItemStack containercontents = container.GetContent(inkslot.Itemstack);
                                        if (containercontents == null) { continue; }
                                        if (containercontents != null && containercontents.StackSize > 0 && containercontents.Item == dye)
                                        {
                                            containercontents.StackSize -= 10;
                                            if (containercontents.StackSize <= 0) { container.SetContent(inkslot.Itemstack, null); }
                                            inkslot.MarkDirty();
                                            founddye = true;
                                            break;
                                        }
                                    }
                                    
                                }
                                if (founddye) { break; }
                            }
                            
                            if (founddye)
                            {
                                bool full2 = false;
                                ChiselToolLoader.AddMaterial(bmb, byPlayer.Entity.Api.World.GetBlock(matid), out full2);
                                blockIndexAlreadyInStack = bmb.BlockIds.IndexOf(matid);
                                byEntity.Api.World.PlaySoundAt(new AssetLocation("sounds/effect/water-fill"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                                handling = EnumHandHandling.Handled;
                                return;
                            }
                            if (api.Side is EnumAppSide.Client && dye != null)
                            {
                                (api as ICoreClientAPI).ShowChatMessage("Attempting to use dye but couldn't find any dye. " + dye.Code.ToString());
                            }

                            return;


                            
                        }
                        return; //nothing was found (should probably do error!)
                    }
                    Vec3i hit = ItemPaintBrush.GetVoxelHit(blockSel);
                    CuboidWithMaterial cwm = new CuboidWithMaterial();

                    //need to offset the selection properly for larger voxel sizes
                    int size = 1;
                    if (mode == 8)
                    {
                        size = 2;

                    }
                    else if (mode == 9) { size = 4; }
                    else if (mode == 10) { size = 8; }

                    hit.X = hit.X / size; hit.X *= size;
                    hit.Y = hit.Y / size; hit.Y *= size;
                    hit.Z = hit.Z / size; hit.Z *= size;
                    bmb.SetVoxel(hit, false, (byte)blockIndexAlreadyInStack, size);
                    bmb.SetVoxel(hit, true, (byte)blockIndexAlreadyInStack, size);
                    byEntity.Api.World.PlaySoundAt(new AssetLocation("sounds/player/knap" + (byEntity.Api.World.Rand.Next(2) > 0 ? 1 : 2)), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);


                    //for somereason it's only ICoreClientAPI if ctrl or shift is held
                    if (byEntity.Api is ICoreClientAPI)
                    {
                        bmb.RegenMesh(byEntity.Api as ICoreClientAPI);
                        PantographData pd = new PantographData(byEntity.Api, bmb);
                        (ChiselToolLoader.loader.chiselnet as IClientNetworkChannel).SendPacket<PantographData>(pd);
                    }
                    else
                    {
                        bmb.MarkDirty(true);
                    }

                    return;
                }

                OnBlockInteract(byEntity.World, byPlayer, blockSel, false, ref handling);
                return;
            }

            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));

            byEntity.World.BlockAccessor.SetBlock(chiseledblock.BlockId, blockSel.Position);

            BlockEntityChisel be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (be == null) return;

            be.WasPlaced(block, blockName);

            if (carvingTime)
            {
                ChiselToolLoader.UpdateChiseledBlock(be, (int[])be.BlockIds.Append(api.World.GetBlock(new AssetLocation("creativeglow-35")).Id), null);
            }

            handling = EnumHandHandling.PreventDefaultAction;
            return;
            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
        public static bool QPIsChiselingAllowedFor(ICoreAPI api, BlockPos pos, Block block, IPlayer player,bool needshammer)
        {
            if (block is BlockMicroBlock)
            {
                if (block is BlockChisel)
                {
                    return true;
                }

                return false;
            }
            bool valid = QPIsValidChiselingMaterial(api, pos, block, player,needshammer);
            return valid;
        }
        public static bool QPIsValidChiselingMaterial(ICoreAPI api, BlockPos pos, Block block, IPlayer player,bool needshammer)
        {
            if (block is BlockChisel)
            {
                return false;
            }

            string @string = api.World.Config.GetString("microblockChiseling");
            if (@string == "off")
            {
                return false;
            }

            //this must be where the hammer check is done?
            IConditionalChiselable conditionalChiselable = block as IConditionalChiselable;
            if ((conditionalChiselable != null || (conditionalChiselable = block.BlockBehaviors.FirstOrDefault((BlockBehavior bh) => bh is IConditionalChiselable) as IConditionalChiselable) != null)
                && ((conditionalChiselable != null && !conditionalChiselable.CanChisel(api.World, pos, player, out var errorCode)) || (conditionalChiselable != null && !conditionalChiselable.CanChisel(api.World, pos, player, out errorCode))))
            {
                (api as ICoreClientAPI)?.TriggerIngameError(conditionalChiselable, errorCode, Lang.Get(errorCode));
                return false;
            }

            bool flag = block.Attributes?["canChisel"].Exists ?? false;
            bool flag2 = block.Attributes?["canChisel"].AsBool() ?? false;
            if (flag2)
            {
                return true;
            }

            if (flag && !flag2)
            {
                return false;
            }

            if (block.DrawType != EnumDrawType.Cube && block.Shape?.Base.Path != "block/basic/cube")
            {
                return false;
            }

            if (block.HasBehavior<BlockBehaviorDecor>())
            {
                return false;
            }

            if (player != null && (player.WorldData.CurrentGameMode == EnumGameMode.Creative||!needshammer))
            {
                return true;
            }

            if (@string == "stonewood")
            {
                if (block.Code.Path.Contains("mudbrick"))
                {
                    return true;
                }

                if (block.BlockMaterial != EnumBlockMaterial.Wood && block.BlockMaterial != EnumBlockMaterial.Stone && block.BlockMaterial != EnumBlockMaterial.Ore)
                {
                    return block.BlockMaterial == EnumBlockMaterial.Ceramic;
                }

                return true;
            }

            return true;
        }
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            bool needshammer = this.Attributes["needshammer"].AsBool(false);
            if (needshammer && byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Tool != EnumTool.Hammer && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "nohammer", Lang.Get("Requires a hammer in the off hand"));
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            if (blockSel?.Position == null) return;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(blockSel.Position) == true)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }
            //1.19
            if (!QPIsChiselingAllowedFor(api, blockSel.Position, block, byPlayer,needshammer))
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }


            if (blockSel == null)
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }
            int mode = slot.Itemstack.Attributes.GetInt("toolMode", -1);

            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (mode == 11)
            {
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            if (mode >= 7 && mode <= 10 && bmb != null)//set current mat
            {

                Vec3i hit = ItemPaintBrush.GetVoxelHit(blockSel);
                int gotmat = bmb.GetVoxelMaterialAt(hit);
                slot.Itemstack.Attributes.SetInt("materialId", gotmat);
                handling = EnumHandHandling.PreventDefaultAction;
                byEntity.Api.World.PlaySoundAt(new AssetLocation("sounds/player/gluerepair2"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, false, 12, 1);
                return;
            }
            if (block is BlockChisel)
            {

                OnBlockInteract(byEntity.World, byPlayer, blockSel, true, ref handling);
                return;
            }
        }

    }
}
