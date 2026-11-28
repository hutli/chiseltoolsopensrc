using chiseltools.src;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace chisel.src
{
    /// <summary>
    /// First copy a block - hold left click to copy, maybe use some cloth and dye?
    /// Work a new block into that shape, hold right click to apply it
    /// </summary>
    /// Door Setup flow:
    /// From door tool mode:
    ///    shift + right click - clear all selections
    ///    right click - highlight & add door to selected door list
    ///    left click - set door as master (for highlighted doors, clear highlighted list)
    ///    
    // do i need this later? Api.World.BlockAccessor.GetChunkAtBlockPos(Pos.X, Pos.Y, Pos.Z).MarkModified();
    class ItemPantograph : Item
    {
        protected BlockPos lastpos;
        protected BlockFacing lastfacing = BlockFacing.DOWN;
        public const string copiedblockvoxelsK = "copiedblockvoxels";
        public const string copiedblocknameK = "copiedblockname";
        public const string copiedmaterialistK = "copiedmateriallist";
        public const string materialpasteokk = "materialpasteok";
        public const string copiedblockcodesK = "copiedblockcodes";
        public SkillItem[] toolModes;
        public WorldInteraction[] interactions;
        protected MeshData objectmesh;
        protected MeshRef objectmeshref;
        protected ICoreClientAPI capi;
        protected List<BlockPos> flaggedblocks;

        protected static string[] suffixSwapList = { "ns", "ud", "we" };
        public virtual int MaxDoorParts => 30;
        public virtual int MaxDoorDistance => 50;
        string[] localmodes = {"SHAPEPASTE", "PASTEALL", "ADDPASTE","ADDPASTEALL", "UNDO", "CHANGEMAT", "CLOSEDDOOR", "OPENDOOR", "DOORTOOL", "LOCK", "NAME", "MIRRORPASTE","PLANEALL", "PLANEMATERIAL", "PLANEISLAND","WEDGEMOVE", "WEDGEFLIP", "WEDGEROTATE" };
        public virtual string[] AvailableModes =>localmodes;
        public virtual string ToolModeID=> "pantographToolModes";
        public virtual string InteractionsKey => "PantographInteractions";
        
        //this is the list of icons, static for all pantographs (maybe can make it settable later?)
        protected Dictionary<string, string> iconlist = new Dictionary<string, string>()
        {
            {"SHAPEPASTE","textures/icons/copy.svg" },
            {"PASTEALL","textures/icons/paste and paint.svg" },
            {"ADDPASTE","textures/icons/add.svg" },
            {"ADDPASTEALL","textures/icons/addall.svg" },
            {"UNDO","textures/icons/undo.svg" },
            {"CHANGEMAT","textures/icons/paintbrush.svg" },
            { "CLOSEDDOOR","textures/icons/closeddoor.svg"},
            { "OPENDOOR","textures/icons/opendoor.svg" },
            { "DOORTOOL","textures/icons/doortool.svg" },
            { "LOCK","textures/icons/copyprotect.svg"},
            { "NAME","textures/icons/namemode.svg"},
            { "MIRRORPASTE", "textures/icons/mirror and paste.svg"},
            { "SCRAPE", "textures/icons/paintscraper.svg"},
            { "PLANEALL", "textures/icons/plane.svg"},
            { "PLANEMATERIAL", "textures/icons/material.svg"},
            { "PLANEISLAND", "textures/icons/islandmode.svg"},
            { "WEDGEMOVE", "textures/icons/push.svg"},
            { "WEDGEFLIP", "textures/icons/flip.svg"},
            { "WEDGEROTATE", "textures/icons/rotation.svg"}
        };
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            
            if (api is ICoreClientAPI) { capi = api as ICoreClientAPI; }
            
            toolModes = ObjectCacheUtil.GetOrCreate(api, ToolModeID+this.Code, () =>
            {
                if (this.Attributes == null)
                {
                    api.Logger.Log(EnumLogType.Error, "PANTOGRAPH HAS NO ATTRIBUTES ON LOAD");
                }
                
                if (this.Attributes != null && this.Attributes.KeyExists("availablemodes"))
                {
                    localmodes = Attributes["availablemodes"].AsArray<string>(localmodes);
                }
                SkillItem[] modes;
                
                modes = new SkillItem[AvailableModes.Length];
                for (int mc = 0; mc < AvailableModes.Length; mc++)
                {
                    modes[mc]= new SkillItem() { Code = new AssetLocation(AvailableModes[mc]), Name = Lang.Get($"chiseltools:{AvailableModes[mc]}") };
                    if (capi != null)
                    {
                        string iconasset = "textures/icons/paste and paint.svg";
                        if (iconlist.ContainsKey(AvailableModes[mc]))
                        {
                            iconasset= iconlist[AvailableModes[mc]];  
                        }
                        modes[mc].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation(iconasset), 48, 48, 5, ColorUtil.WhiteArgb));
                        modes[mc].TexturePremultipliedAlpha = false;
                    }
                }
                
                return modes;
            });
            interactions = ObjectCacheUtil.GetOrCreate(api, InteractionsKey+this.Code, () =>
            {

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "Perform Action",
                        MouseButton = EnumMouseButton.Right,

                    },
                   
                };
            });
           
        }
        
        //this is for pantograph copy protection
        public virtual bool ModifableBy(BlockSelection blockSelection, string playerID)
        {
            if (blockSelection == null) { return false; } //not sure if best to return true or false for this
            
            BELockedMicroblock belocked = api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) as BELockedMicroblock;
            if (belocked == null) { return true; }
            if (belocked.LockedByID == playerID) {
                if (capi != null) { capi.ShowChatMessage(Lang.Get("chiseltools:COPYGRANT")); }
                return true;
            }
            if (!belocked.IsLocked) { return true; }
            
            if (capi != null)
            {
                capi.ShowChatMessage(Lang.Get("chiseltools:COPYDENY"));
            }
            return false;
        }
        
        /// <summary>
        /// Return a named toolmode from an item slot - replaces the enum search
        /// </summary>
        /// <param name="slot">Item slot probably containing a pantograph</param>
        /// <returns>name of the tool mode or the first available toolmode</returns>
        public string GetToolModeFromSlot(ItemSlot slot)
        {
            string result = AvailableModes[0];
            if (slot != null && slot.Itemstack!=null&&slot.Itemstack.Attributes!=null)
            {
                int modeint=slot.Itemstack.Attributes.GetInt("toolMode", 0);
                modeint = Math.Clamp(modeint,0, AvailableModes.Length - 1);
                result=AvailableModes[modeint];
            }

            return result;
        }
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
           
            if (blockSel == null) { return; }
            Block bl = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (bl.Code.ToString() == "game:mantle") { return; }

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!ModifableBy(blockSel, byPlayer.PlayerUID)) { handling = EnumHandHandling.PreventDefaultAction; return; }
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

            //for functional chiseled blocks this would set the targetted block as a control block
            string tm = GetToolModeFromSlot(slot);
            
            if ( tm=="DOORTOOL"){
                //nothing selected
                
                BEFunctionChiseled bfc = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BEFunctionChiseled;
                if (bfc == null)
                {
                    handling = EnumHandHandling.PreventDefaultAction;
                    return;
                }
                if (flaggedblocks!=null&&flaggedblocks.Contains(blockSel.Position))
                {
                    flaggedblocks.Remove(blockSel.Position);
                }
                
                //send flagged block list to the new contol block (via the server)
                if (api is ICoreClientAPI)
                {
                    double distance = 0;
                    if (flaggedblocks == null || flaggedblocks.Count == 0)
                    {
                        (api as ICoreClientAPI).Network.SendBlockEntityPacket(blockSel.Position, (int)BEFunctionChiseled.enPacketCode.SETCONTROLLED, null);
                    }
                    else
                    {
                        Vec3d origin = flaggedblocks[0].ToVec3d();
                        distance = origin.DistanceTo(blockSel.Position.ToVec3d());

                    }
                    if (distance > MaxDoorDistance)
                    {
                        (ChiselToolLoader.loader.textnet as IClientNetworkChannel).SendPacket<string>("Block is too far to add to be Door Controller!");
                    }
                    else
                    {
                        byte[] data = SerializerUtil.Serialize<List<BlockPos>>(flaggedblocks);
                        (api as ICoreClientAPI).Network.SendBlockEntityPacket(blockSel.Position, (int)BEFunctionChiseled.enPacketCode.SETCONTROLLED, data);
                        flaggedblocks = new List<BlockPos>();
                        api.World.HighlightBlocks(byPlayer, 1, flaggedblocks);
                        api.World.PlaySoundAt(new AssetLocation("sounds/filtercopy"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                    }
                }
                
                handling = EnumHandHandling.PreventDefaultAction;
                
                return;
            }
            
            BlockEntityMicroBlock bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMicroBlock;
            if (bmb == null)
            {
                TryChangeBlockToChisel(blockSel, byEntity, byPlayer);
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            if (tm.StartsWith("PLANE"))
            {
                int cutvoxels = 0;
                //BACKUP DATA AND STORE FOR UNDO FUNCTION
                List<uint> undovoxels = new List<uint>(bmb.VoxelCuboids);
                List<int> undomats = new List<int>(bmb.BlockIds);
                BlockPos undoposition = blockSel.Position;
                MakeUndoCopy(api, slot, undoposition, undovoxels, undomats);
                //Backup(slot, blockSel.Position);
                cutvoxels = ModifyChiseledBlock(blockSel, tm, false, byPlayer);
                if (cutvoxels > 0)
                {
                    api.World.PlaySoundAt(new AssetLocation("sounds/stone_move"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 6, 0.25f);

                }
                if (api is ICoreServerAPI)
                {
                    if (byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        handling = EnumHandHandling.PreventDefaultAction;
                    }
                    else
                    {
                        this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, CalcDamage(cutvoxels));
                    }

                }

                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            if (tm.StartsWith("WEDGE"))
            {
                //BACKUP DATA AND STORE FOR UNDO FUNCTION
                List<uint> undovoxels = new List<uint>(bmb.VoxelCuboids);
                List<int> undomats = new List<int>(bmb.BlockIds);
                BlockPos undoposition = blockSel.Position;
                MakeUndoCopy(api, slot, undoposition, undovoxels, undomats);
                int cutvoxels = 0;
                if (tm=="WEDGEMOVE")
                {
                    cutvoxels = MoveChiseledBlock(blockSel);
                }
                else if (tm=="WEDGEFLIP")
                {
                    cutvoxels = MirrorBlock(blockSel);
                }
                else if (tm=="WEDGEROTATE")
                {
                    cutvoxels = RotateBlock(blockSel);
                }
                if (cutvoxels > 0)
                {
                    api.World.PlaySoundAt(new AssetLocation("sounds/stone_move"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 0.25f);
                }
                if (api is ICoreServerAPI)
                {
                    if (byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        handling = EnumHandHandling.PreventDefaultAction;
                    }
                    else
                    {
                        this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, CalcDamage(cutvoxels));
                    }
                }

                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            MakeCopy(slot, byEntity, blockSel, entitySel, ref handling); 
            handling = EnumHandHandling.PreventDefaultAction;
            api.World.PlaySoundAt(new AssetLocation("sounds/filtercopy"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);

        }
       
        //Vec3f displayoffset = new Vec3f(0.25f, 0, -0.25f);
        Vec3f displayoffset = new Vec3f(0, 0, 0);
        
        public virtual void MakeCopy(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            
            //Check for a copy protected block and reject if necessary
            //int cpblockid = api.World.GetBlock(new AssetLocation(ChiselToolLoader.copyprotectedblockname)).BlockId;
            //if (api.World.BlockAccessor.GetBlock(blockSel.Position).Id == cpblockid) {
            //    if (byPlayer != null && api is ICoreServerAPI)
            //    {
            //        (api as ICoreServerAPI).SendMessage(byPlayer, 0, Lang.Get("chiseltools:thisblockiscopyprotected!"), EnumChatType.Notification);
            //    }
            //    return;
            //}
            BlockEntityMicroBlock bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMicroBlock;
            
            if (bmb == null || bmb.VoxelCuboids == null || bmb.VoxelCuboids.Count == 0) { base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling); return; }
            
            
            string copiedname = bmb.BlockName;
            //if (copiedname == "") { copiedname = "copy"; }
            List<uint>copiedblockvoxels = new List<uint>(bmb.VoxelCuboids);
            List<int>copiedblockmaterials = new List<int>(bmb.BlockIds);
            string[] copiedblockcodes = new string[copiedblockmaterials.Count];

            for (int c=0;c<copiedblockcodes.Length;c++) 
            {
                copiedblockcodes[c] = api.World.GetBlock(copiedblockmaterials[c]).Code;
            }

            int copiedvolume = (int)(bmb.VolumeRel * 16f * 16f * 16f);
            
            if (api is ICoreServerAPI) {
                byte[]bvox = SerializerUtil.Serialize<List<uint>>(copiedblockvoxels);
                byte[] bmat = SerializerUtil.Serialize<List<int>>(copiedblockmaterials);
                byte[] bcode = SerializerUtil.Serialize<string[]>(copiedblockcodes);
                slot.Itemstack.Attributes.SetBytes(copiedblockvoxelsK,bvox);
                slot.Itemstack.Attributes.SetString(copiedblocknameK, copiedname);
                slot.Itemstack.Attributes.SetBytes(copiedmaterialistK, bmat);
                slot.Itemstack.Attributes.SetBytes(copiedblockcodesK, bcode);
                slot.Itemstack.Attributes.SetBool(materialpasteokk, true);
                slot.MarkDirty();
            }
            
            api.World.PlaySoundAt(new AssetLocation("sounds/filtercopy"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            string tm = GetToolModeFromSlot(slot);

            if (blockSel == null) { return; }
            Block bl = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (bl.Code.ToString() == "game:mantle") { return; }
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(blockSel.Position) == true)
            {
                (ChiselToolLoader.loader.textnet as IClientNetworkChannel).SendPacket<string>("Cannot use pantograph, this block is reinforced!");
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                (ChiselToolLoader.loader.textnet as IClientNetworkChannel).SendPacket<string>("You do not have access to this block!");
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }
            BlockEntity be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (be != null)
            {
                BEBChiseledCover bcc = be.GetBehavior<BEBChiseledCover>();

                if (bcc != null && tm == "SHAPEPASTE")
                {
                    CoverPasteShape(slot, byPlayer, blockSel, bcc);
                    handling = EnumHandHandling.PreventDefaultAction;
                    return;
                }
            }
            BlockEntityMicroBlock bmb = be as BlockEntityMicroBlock;
            if (bmb == null)
            {
                TryChangeBlockToChisel(blockSel,byEntity,byPlayer);
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }


            //BACKUP DATA AND STORE FOR UNDO FUNCTION
            if (bmb==null|| bmb.VoxelCuboids == null || bmb.BlockIds == null) { return; }
            List<uint> undovoxels = new List<uint>(bmb.VoxelCuboids);
            List<int> undomats = new List<int>(bmb.BlockIds);
            BlockPos undoposition = blockSel.Position;
            MakeUndoCopy(api, slot, undoposition, undovoxels, undomats);
            if (tm.StartsWith("WEDGE"))
            {
                
                int cutvoxels = 0;
                if (tm == "WEDGEMOVE")
                {
                    cutvoxels = MoveChiseledBlock(blockSel);
                }
                else if (tm == "WEDGEFLIP")
                {
                    cutvoxels = MirrorBlock(blockSel);
                }
                else if (tm == "WEDGEROTATE")
                {
                    cutvoxels = RotateBlock(blockSel);
                }
                if (cutvoxels > 0)
                {
                    api.World.PlaySoundAt(new AssetLocation("sounds/stone_move"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 0.25f);
                }
                if (api is ICoreServerAPI)
                {
                    if (byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        handling = EnumHandHandling.PreventDefaultAction;
                    }
                    else
                    {
                        this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, CalcDamage(cutvoxels));
                    }
                }

                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            //Copy Protect an item
            if (tm=="LOCK")
            {
                if (api is ICoreServerAPI)
                {
                    MakeCopy(slot, byEntity, blockSel, entitySel, ref handling);
                    int cpblockid = api.World.GetBlock(new AssetLocation(ChiselToolLoader.copyprotectedblockname)).BlockId;


                    BlockEntityChisel bec = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
                    if (bec == null) { return; }
                    if (bec.Block.BlockId == cpblockid) { return; }
                    DoorData data = new DoorData();
                    data.pos = blockSel.Position;
                    data.matdata = new List<int>(bec.BlockIds);
                    data.voxeldata = new List<uint>(bec.VoxelCuboids);
                    data.name = bec.BlockName;
                    if (data.matdata == null || data.matdata.Count == 0 || data.voxeldata == null || data.voxeldata.Count == 0) { return; }
                    api.World.BlockAccessor.SetBlock(cpblockid, data.pos);
                    BELockedMicroblock cbec = api.World.BlockAccessor.GetBlockEntity(data.pos) as BELockedMicroblock;
                    if (cbec == null) { return; }
                    ChiselToolLoader.UpdateChiseledBlock(cbec, data.matdata.ToArray(), new List<uint>(data.voxeldata));
                    cbec.SetLock(byPlayer.PlayerUID, byPlayer.PlayerName, true);

                }
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            if (tm.StartsWith("PLANE"))
            {
                int cutvoxels = 0;
                
                //Backup(slot, blockSel.Position);
                cutvoxels = ModifyChiseledBlock(blockSel, tm, true, byPlayer);
                if (cutvoxels > 0)
                {
                    api.World.PlaySoundAt(new AssetLocation("sounds/stone_move"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 6, 0.25f);

                }
                if (api is ICoreServerAPI)
                {
                    if (byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        handling = EnumHandHandling.PreventDefaultAction;
                    }
                    else
                    {
                        this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, CalcDamage(cutvoxels));
                    }

                }

                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            //changemat
            if (tm == "CHANGEMAT")
            {
                PasteMaterials(slot, blockSel,byPlayer);
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            else if (tm == "SCRAPE")
            {
                bool didscrape = ItemPaintScraper.CleanMaterials(slot, byEntity, bmb as BlockEntityChisel);
                if (didscrape) api.World.PlaySoundAt(new AssetLocation("sounds/stone_move"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);

            }
            else if (tm == "NAME")
            {
                PasteName(slot, blockSel,byPlayer);
                api.World.PlaySoundAt(new AssetLocation("sounds/player/chalkdraw1"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            //closedoor
            else if (tm == "CLOSEDDOOR")
            {
                
                SetDoor(slot, blockSel,BEFunctionChiseled.closename, byPlayer.Entity.Controls.Sneak);
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            //opendoor
            else if (tm == "OPENDOOR")
            {
                SetDoor(slot, blockSel,BEFunctionChiseled.openname, byPlayer.Entity.Controls.Sneak);
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            //door tool
            else if (tm == "DOORTOOL")
            {
                if (!(api is ICoreClientAPI)) { return; }
                if (flaggedblocks == null||byPlayer.Entity.Controls.Sneak) { flaggedblocks = new List<BlockPos>(); }
                
                if (flaggedblocks.Contains(blockSel.Position))
                {
                    flaggedblocks.Remove(blockSel.Position);
                }
                //number of doorparts check
                else if (flaggedblocks.Count<=MaxDoorParts) {
                    //distance check
                    double distance = 0;
                    if (flaggedblocks.Count > 0)
                    {
                        Vec3d origin = flaggedblocks[0].ToVec3d();
                        distance = origin.DistanceTo(blockSel.Position.ToVec3d());
                        
                       
                    }
                    if (distance <= MaxDoorDistance)
                    {
                        flaggedblocks.Add(blockSel.Position);
                    }
                    else
                    {
                        (ChiselToolLoader.loader.textnet as IClientNetworkChannel).SendPacket<string>("Block is too far to add to Door!");
                    }

                }
                else
                {
                    (ChiselToolLoader.loader.textnet as IClientNetworkChannel).SendPacket<string>("Too many blocks selected for door!");
                }
                //api.World.HighlightBlocks(byPlayer, HighlightSlotId, blocks, colors);
                
                api.World.HighlightBlocks(byPlayer, 1, flaggedblocks);
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            
                List<uint> copiedblockvoxels = GetCopiedBlockVoxels(slot);
            List<int> copiedblockmaterials = GetCopiedBlockMaterials(slot);
            
            
            if (copiedblockmaterials==null||copiedblockvoxels==null|| bmb == null || bmb.VoxelCuboids == null || bmb.VoxelCuboids.Count == 0) { base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);handling =EnumHandHandling.NotHandled;  return; }
            
            int copiedblockmaterialcount = copiedblockmaterials.Count;
            
            
            
            int changedvoxels = 0;
            int originalvolume = (int)(bmb.VolumeRel*16f*16f*16f);
            string copyname = slot.Itemstack.Attributes.GetString("copiedblockname", "");
            //copy - OVERRIDING THIS WITH PASTEALL
            if (tm == "PASTEALL")
            {


                // Only check and charge for the materials here; the block itself is updated in one go below
                if (PasteMaterials(slot, blockSel, byPlayer, applyMaterials: false))
                {
                    int[] mats = copiedblockmaterials.ToArray();

                    // Keep AvailMaterialQuantities in step with BlockIds: existing slots keep their
                    // quantity, new slots get a full block's worth (what AddMaterial gave them before)
                    if (bmb is BlockEntityChisel bec && bec.AvailMaterialQuantities != null)
                    {
                        ushort[] q = new ushort[mats.Length];
                        for (int i = 0; i < mats.Length; i++)
                            q[i] = i < bec.AvailMaterialQuantities.Length ? bec.AvailMaterialQuantities[i] : (ushort)(16 * 16 * 16);
                        bec.AvailMaterialQuantities = q;
                    }

                    ChiselToolLoader.UpdateChiseledBlock(bmb, mats, new List<uint>(copiedblockvoxels), copyname != "" ? copyname : null);

                    api.World.PlaySoundAt(new AssetLocation("sounds/player/chalkdraw1"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                    if (api is ICoreServerAPI && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        int dmg = 1;
                        this.DamageItem(api.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, dmg);
                        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    }
                }

            }
            //full paste

            else if (tm == "SHAPEPASTE")
            {
                if (bmb.BlockIds.Length < copiedblockmaterialcount) //if we have a material mismatch make sure to only use as many mats as available
                {


                    List<uint> newcuboids = new List<uint>();

                    foreach (uint su in copiedblockvoxels)
                    {
                        CuboidWithMaterial cwm = new CuboidWithMaterial();
                        BlockEntityMicroBlock.FromUint(su, cwm);

                        cwm.Material = (byte)Math.Min(bmb.BlockIds.Length - 1, cwm.Material);
                        uint newb = BlockEntityMicroBlock.ToUint(cwm);
                        newcuboids.Add(newb);
                    }
                    ChiselToolLoader.UpdateChiseledBlock(bmb, null, newcuboids);
                }
                else { ChiselToolLoader.UpdateChiseledBlock(bmb, null, new List<uint>(copiedblockvoxels)); }
                changedvoxels = originalvolume;

            }
            //boolean merge
            else if (tm == "ADDPASTE")
            {
                byte maxindex = (byte)bmb.BlockIds.Length;
                //cycle thru source cuboids - turn into individual voxels, add to destination object
                foreach (uint su in copiedblockvoxels)
                {
                    CuboidWithMaterial cwm = new CuboidWithMaterial();
                    BlockEntityMicroBlock.FromUint(su, cwm);

                    if (cwm.Material >= maxindex) { cwm.Material = (byte)(maxindex - 1); } //make sure source materials aren't out of range
                    bool setthisvoxel = true;
                    //cycle through each voxel of the source cuboid and see if it's safe to write to the destination block


                    for (int xc = cwm.X1; xc < cwm.X2; xc++)
                    {
                        for (int yc = cwm.Y1; yc < cwm.Y2; yc++)
                        {
                            for (int zc = cwm.Z1; zc < cwm.Z2; zc++)
                            {
                                //this is a lot of recursion but should be ok - could do a check of intersecting cuboids first but probably doesn't save much
                                setthisvoxel = true;
                                foreach (uint du in bmb.VoxelCuboids)
                                {
                                    CuboidWithMaterial dcwm = new CuboidWithMaterial();
                                    BlockEntityMicroBlock.FromUint(du, dcwm);
                                    if (dcwm.Contains(xc, yc, zc))
                                    {
                                        setthisvoxel = false;
                                        break;
                                    }
                                }
                                if (setthisvoxel)
                                {
                                    bmb.SetVoxel(new Vec3i(xc, yc, zc), true, cwm.Material, 1);
                                    changedvoxels++;
                                }
                            }

                        }

                    }

                }

            }
            else if (tm == "ADDPASTEALL")
            {
                List<int> newblockids = bmb.BlockIds.ToList();
                Dictionary<byte,byte> mapCopyToNew= new Dictionary<byte, byte>();
                for (int copyc=0;copyc<copiedblockmaterials.Count;copyc++)
                {
                    if (!newblockids.Contains(copiedblockmaterials[copyc]))
                    {
                        newblockids.Add(copiedblockmaterials[copyc]);

                    }
                    if (newblockids.Contains(copiedblockmaterials[copyc]))
                    {
                        mapCopyToNew[(byte)copyc] = (byte)newblockids.IndexOf(copiedblockmaterials[copyc]);

                    }
                    else
                    {
                        int f = 1; //that's an error lol
                    }
                    
                }

                if (!PasteMaterials(newblockids, blockSel, byPlayer)) { return; }

                byte maxindex = (byte)newblockids.Count ;
                
                ChiselToolLoader.UpdateChiseledBlock(bmb, newblockids.ToArray(), null);
                //cycle thru source cuboids - turn into individual voxels, add to destination object
                foreach (uint su in copiedblockvoxels)
                {
                    CuboidWithMaterial cwm = new CuboidWithMaterial();
                    //cwm.Material = mapCopyToNew[cwm.Material];
                    BlockEntityMicroBlock.FromUint(su, cwm);

                    if (cwm.Material >= maxindex) { cwm.Material = (byte)(maxindex - 1); } //make sure source materials aren't out of range
                    bool setthisvoxel = true;

                    

                    //cycle through each voxel of the source cuboid and see if it's safe to write to the destination block


                    for (int xc = cwm.X1; xc < cwm.X2; xc++)
                    {
                        for (int yc = cwm.Y1; yc < cwm.Y2; yc++)
                        {
                            for (int zc = cwm.Z1; zc < cwm.Z2; zc++)
                            {
                                //this is a lot of recursion but should be ok - could do a check of intersecting cuboids first but probably doesn't save much
                                setthisvoxel = true;
                                byte usemat = 0;
                                foreach (uint du in bmb.VoxelCuboids)
                                {
                                    CuboidWithMaterial dcwm = new CuboidWithMaterial();
                                    BlockEntityMicroBlock.FromUint(du, dcwm);
                                    usemat = (byte)mapCopyToNew[cwm.Material];
                                    if (dcwm.Contains(xc, yc, zc))
                                    {
                                        setthisvoxel = false;
                                        break;
                                    }
                                }
                                if (setthisvoxel)
                                {
                                    
                                    bmb.SetVoxel(new Vec3i(xc, yc, zc), true, usemat, 1);
                                    changedvoxels++;
                                }
                            }

                        }

                    }
                    
                    
                    // 
                    // maybe do this first in the copied data?
                    // 
                    //TODO - CHECK PLAYER INVENTORY FOR MERGED SHAPES

                }
                
            }
            else if (tm == "MIRRORPASTE")
            {
                changedvoxels = 1;
                //if (api is ICoreClientAPI)
                {
                    ///TODO MOVE TO CLIENT SIDE, SEND FINISHED RESULT ONLY TO SERVER
                    //delete voxels
                    if (blockSel.Face == BlockFacing.WEST || blockSel.Face == BlockFacing.EAST)
                    {
                        if (blockSel.HitPosition.Z > 0.5)
                        {
                            bmb.SetVoxel(new Vec3i(0, 0, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 0, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(0, 8, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 8, 0), false, 0, 8);
                        }
                        else
                        {
                            bmb.SetVoxel(new Vec3i(0, 0, 8), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 0, 8), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(0, 8, 8), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 8, 8), false, 0, 8);
                        }
                    }
                    else if (blockSel.Face == BlockFacing.NORTH || blockSel.Face == BlockFacing.SOUTH)
                    {
                        if (blockSel.HitPosition.X > 0.5)
                        {
                            bmb.SetVoxel(new Vec3i(0, 0, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(0, 0, 8), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(0, 8, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(0, 8, 8), false, 0, 8);
                        }
                        else
                        {
                            bmb.SetVoxel(new Vec3i(8, 0, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 0, 8), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 8, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 8, 8), false, 0, 8);
                        }
                    }
                    else if (blockSel.Face == BlockFacing.UP || blockSel.Face == BlockFacing.DOWN)
                    {
                        if (blockSel.HitPosition.Y > 0.5)
                        {
                            bmb.SetVoxel(new Vec3i(0, 0, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(0, 0, 8), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 0, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 0, 8), false, 0, 8);
                        }
                        else
                        {
                            bmb.SetVoxel(new Vec3i(0, 8, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(0, 8, 8), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 8, 0), false, 0, 8);
                            bmb.SetVoxel(new Vec3i(8, 8, 8), false, 0, 8);
                        }
                    }

                    //copy other voxels
                    List<uint> tempcopiedblockvoxels = new List<uint>(bmb.VoxelCuboids);

                    //mirror appropriately
                    MirrorBlock(blockSel, ref bmb);

                    //paste new voxels
                    int maxindex = (byte)bmb.BlockIds.Length;
                    //cycle thru source cuboids - turn into individual voxels, add to destination object
                    foreach (uint su in tempcopiedblockvoxels)
                    {
                        CuboidWithMaterial cwm = new CuboidWithMaterial();
                        BlockEntityMicroBlock.FromUint(su, cwm);

                        if (cwm.Material >= maxindex) { cwm.Material = (byte)(maxindex - 1); } //make sure source materials aren't out of range
                        bool setthisvoxel = true;
                        //cycle through each voxel of the source cuboid and see if it's safe to write to the destination block


                        for (int xc = cwm.X1; xc < cwm.X2; xc++)
                        {
                            for (int yc = cwm.Y1; yc < cwm.Y2; yc++)
                            {
                                for (int zc = cwm.Z1; zc < cwm.Z2; zc++)
                                {
                                    //this is a lot of recursion but should be ok - could do a check of intersecting cuboids first but probably doesn't save much
                                    setthisvoxel = true;
                                    foreach (uint du in bmb.VoxelCuboids)
                                    {
                                        CuboidWithMaterial dcwm = new CuboidWithMaterial();
                                        BlockEntityMicroBlock.FromUint(du, dcwm);
                                        if (dcwm.Contains(xc, yc, zc))
                                        {
                                            setthisvoxel = false;
                                            break;
                                        }
                                    }
                                    if (setthisvoxel)
                                    {
                                        bmb.SetVoxel(new Vec3i(xc, yc, zc), true, cwm.Material, 1);
                                        changedvoxels++;
                                    }
                                }

                            }

                        }

                    }


                    if (copyname != "")
                    {
                        bmb.BlockName = copyname;
                    }
                    bmb.MarkDirty(true);
                    PantographData newpd = new PantographData();
                    newpd.voxeldata = SerializerUtil.Serialize<List<uint>>(bmb.VoxelCuboids);
                    newpd.pos = blockSel.Position;
                    api.World.PlaySoundAt(new AssetLocation("sounds/player/chalkdraw1"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, true, 12, 1);
                    //(ChiselToolLoader.loader.pastenet as IClientNetworkChannel).SendPacket<PantographData>(newpd);
                }
                //here we should be client side and just need to send the data to the server?

            }
            
            bmb.MarkDirty(true);
            handling = EnumHandHandling.PreventDefaultAction;

            return;
        }
        public static void MakeUndoCopy(ICoreAPI api, ItemSlot slot, BlockPos pos,List<uint>undovoxels,List<int>undomats)
        {
            
       
                slot.Itemstack.Attributes.SetBytes("undovoxels", SerializerUtil.Serialize<List<uint>>(undovoxels));
                slot.Itemstack.Attributes.SetBytes("undomats", SerializerUtil.Serialize<List<int>>(undomats));
                slot.Itemstack.Attributes.SetBlockPos("undoposition", pos);
                slot.MarkDirty();
            
        }
        private void CoverPasteShape(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, BEBChiseledCover bcc)
        {
            List<int> copyblockmat=GetCopiedBlockMaterials(slot);
            List<uint> copyblockvox=GetCopiedBlockVoxels(slot);
            if (copyblockmat == null || copyblockmat.Count == 0 || copyblockvox == null || copyblockvox.Count == 0) { return; }
            ItemStack forStack = bcc.ChiseledItemStack;
            if (forStack == null) return;
            ITreeAttribute tree = forStack.Attributes;
            int[] materials = BlockEntityMicroBlock.MaterialIdsFromAttributes(tree, api.World);
            if (materials == null || materials.Length < copyblockmat.Count) {
                if (capi != null)
                {
                    capi.ShowChatMessage(Lang.Get("chiseltools:notenoughmaterials") );
                }
                return;
            }//should probably send a message here

            tree["cuboids"] = new IntArrayAttribute(copyblockvox.ToArray());
            forStack.Attributes = tree;
            DummyInventory di = new DummyInventory(api, 1);
            di[0].Itemstack = forStack;
            bcc.SetShape(di[0],true);
        }

        public virtual int MirrorBlock(BlockSelection blockSel, ref BlockEntityMicroBlock bmb)
        {
            
            if (bmb == null) { return 0; }
            if (bmb.VoxelCuboids == null || bmb.VoxelCuboids.Count == 0) { return 0; }
            List<uint> cuboids = new List<uint>();

            foreach (uint voxint in bmb.VoxelCuboids)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                //this static method converts the uint into a CubioidWithMaterial
                BlockEntityMicroBlock.FromUint(voxint, cwm);
                //x&z should stay the same
                if (blockSel.Face == BlockFacing.UP || blockSel.Face == BlockFacing.DOWN)
                {
                    int y1 = 16 - cwm.Y2;
                    int y2 = 16 - cwm.Y1;
                    cwm.Y1 = y1;
                    cwm.Y2 = y2;
                }
                else if (blockSel.Face == BlockFacing.EAST || blockSel.Face == BlockFacing.WEST)
                {
                    int z1 = 16 - cwm.Z2;
                    int z2 = 16 - cwm.Z1;
                    cwm.Z1 = z1;
                    cwm.Z2 = z2;
                }
                else if (blockSel.Face == BlockFacing.NORTH || blockSel.Face == BlockFacing.SOUTH)
                {
                    int x1 = 16 - cwm.X2;
                    int x2 = 16 - cwm.X1;
                    cwm.X1 = x1;
                    cwm.X2 = x2;
                }
                cuboids.Add(BlockEntityMicroBlock.ToUint(cwm.MinX, cwm.MinY, cwm.MinZ, cwm.MaxX, cwm.MaxY, cwm.MaxZ, cwm.Material));

            }


            if (cuboids == null || cuboids.Count == 0) { return 0; }
            ChiselToolLoader.UpdateChiseledBlock(bmb, null, cuboids);
            return 16 * 16; //todo calculate actual voxels based on the bounds of the cuboids
        }
        public bool TryChangeBlockToChisel(BlockSelection blockSel, Entity byEntity, IPlayer byPlayer)
        {
            Block bl = api.World.BlockAccessor.GetBlock(blockSel.Position);
            
            if (bl.Code.ToString() == "game:mantle") { return false; }
            BEFunctionChiseled bfctest = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BEFunctionChiseled;
            if (bfctest != null) { return false; }
            string blockName = bl.GetPlacedBlockName(byEntity.World, blockSel.Position);
            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));


            //if (!IsChiselingAllowedFor(bl, byPlayer)) { return false; }
            if (!TrueChisel.QPIsValidChiselingMaterial(api, blockSel.Position, bl, byPlayer, false)) { return false; }
            
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
        protected virtual int CalcDamage(int numvoxels)
        {

            if (ChiselToolLoader.serverconfig.fixedToolWear) { return (int)ChiselToolLoader.serverconfig.pantographMinimumDamagePerOp; }
            int basedamage = (int)(ChiselToolLoader.serverconfig.pantographBaseDurabilityMultiplier * (float)numvoxels);
            
            basedamage = Math.Max(ChiselToolLoader.serverconfig.pantographMinimumDamagePerOp, basedamage);
            basedamage = Math.Min(basedamage, 10); //hardcode max damage hits
            return basedamage;
        }
        public virtual void Undo(ItemSlot slot)
        {
            if (api is ICoreClientAPI) { return; }
            List<uint> undovoxels = null;
            List<int> undomats = null;
            BlockPos undoposition = null;
            try
            {
                
                undovoxels = SerializerUtil.Deserialize<List<uint>>(slot.Itemstack.Attributes.GetBytes("undovoxels", null));
                undomats = SerializerUtil.Deserialize<List<int>>(slot.Itemstack.Attributes.GetBytes("undomats",null));

                undoposition = slot.Itemstack.Attributes.GetBlockPos("undoposition", null);
                
                //slot.Itemstack.Attributes.GetBytes("undovoxels", SerializerUtil.Deserialize<List<uint>>(undovoxels));
                //slot.Itemstack.Attributes.SetBlockPos("undoposition", blockSel.Position);
            }
            catch
            {
                return;
            }

                if (undovoxels != null && undoposition != null && undovoxels != null)
                {
                    BlockEntityMicroBlock bmb = api.World.BlockAccessor.GetBlockEntity(undoposition) as BlockEntityMicroBlock;
                    if (bmb == null||bmb.VoxelCuboids==null) { undovoxels = null; }
                    else
                    {
                        ChiselToolLoader.UpdateChiseledBlock(bmb, undomats?.ToArray(), undovoxels);
                    }
                }

        }
        
        /// <summary>
        /// Returns a list of compressed cuboids all using the supplied material index
        /// </summary>
        /// <param name="originalcuboids">List of CuboidsWithMaterials packed into uint</param>
        /// <param name="newmat">Material to use</param>
        /// <returns></returns>
        public static List<uint> CuboidStripMaterials(List<uint> originalcuboids,byte newmat)
        {
            List<uint> newcuboid = new List<uint>();
            foreach (uint og in originalcuboids)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                BlockEntityMicroBlock.FromUint(og, cwm);
                
                newcuboid.Add(BlockEntityMicroBlock.ToUint(cwm.MinX, cwm.MinY, cwm.MinZ, cwm.MaxX, cwm.MaxY, cwm.MaxZ, newmat));
            }
            return newcuboid;
        }

        

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            
            
            
            return toolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            
            int mode= Math.Min(toolModes.Length-1, slot.Itemstack.Attributes.GetInt("toolMode"));
            

            return mode;
        }
        public override void OnUnloaded(ICoreAPI api)
        {
            for (int i = 0; toolModes != null && i < toolModes.Length; i++)
            {
                toolModes[i]?.Dispose();
            }
            objectmeshref?.Dispose();
           
        }
        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
            string tm = GetToolModeFromSlot(slot);
            if (tm=="UNDO")
            {
                Undo(slot);
                int lasttoolmode = slot.Itemstack.Attributes.GetInt("lastToolMode", 0);
                lasttoolmode = Math.Clamp(lasttoolmode, 0, AvailableModes.Length);
                SetToolMode(slot, byPlayer, blockSel, lasttoolmode);
            }
            
            else
            {
                slot.Itemstack.Attributes.SetInt("lastToolMode", toolMode);
            }
        }



        //helper functions to grab Copied Voxel info from a pantograph
        public static List<uint>GetCopiedBlockVoxels(ItemStack stack)
        {
            if (stack == null || stack.Attributes == null) { return null; }
            byte[] copvox = stack.Attributes.GetBytes(copiedblockvoxelsK, null);
            List<uint> copiedblockvoxels = null;
            try { copiedblockvoxels = SerializerUtil.Deserialize<List<uint>>(copvox); }
            catch { }
            return copiedblockvoxels;
        }

        public static List<int> GetCopiedBlockMaterials(ItemStack stack)
        {
            if (stack == null || stack.Attributes == null) { return null; }
            List<int> copiedmaterials = null;
            byte[] copmat = stack.Attributes.GetBytes(copiedmaterialistK, null);
            try { copiedmaterials = SerializerUtil.Deserialize<List<int>>(copmat); }
            catch { }

            return copiedmaterials;
        }

        public static List<uint>GetCopiedBlockVoxels(ItemSlot slot)
        {
            if (slot == null) { return null; }
            return GetCopiedBlockVoxels(slot.Itemstack);
        }
        
        public static List<int>GetCopiedBlockMaterials(ItemSlot slot)
        {
            if (slot == null) { return null; }
            return GetCopiedBlockMaterials(slot.Itemstack);
        }

        bool IsNonPortfolioPaste(ItemSlot slot)
        {
            if (slot == null || slot.Itemstack == null || slot.Itemstack.Attributes == null) return false;
            return slot.Itemstack.Attributes.GetBool(materialpasteokk, true);
        }

        bool PasteName(ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer)
        {
            if (api is ICoreClientAPI) { return true; }
            PantographData p = PantographData.TryGrabFromSlot(api, slot);
            if (p == null || p.name == "") {  return false; }
            BlockEntityChisel bec = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bec == null) { return false; }
            
            bec.BlockName = p.name;
            bec.MarkDirty(true);
            return true;
        }

        /// <summary>
        /// This will take the saved list of materials and apply it to the targeted block
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="blockSel"></param>
        /// <param name="byPlayer"></param>
        /// <param name="applyMaterials"></param>
        bool PasteMaterials(ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer, bool applyMaterials = true)
        {
            
            if (slot == null || blockSel == null || byPlayer == null) return false;
            List<int> copiedmaterials = GetCopiedBlockMaterials(slot);
            if (copiedmaterials == null||copiedmaterials.Count==0) return false;
            if (!IsNonPortfolioPaste(slot)) {
                if (capi != null) { capi.ShowChatMessage(Lang.Get("chiseltools:sorrycannotpastematerialsfromportfolioshapes")); }
                return false;
            }
            
            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bmb == null||bmb.BlockIds==null||bmb.BlockIds.Length==0) { return false; }

            //Materials list already match do nothing
            if (copiedmaterials.Equals(bmb.BlockIds)) {
                
                return true;
            }

            //If in survival mode we need to use up materials
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) {
                bool oktopaste = false;
                string missingmat = "";
                //Check and see if the destination block has everything that is in the copied data, and allow paste
                // to continue if true (ie we're just shuffling materials around)
                ItemSlot palslot = byPlayer.InventoryManager.GetHotbarInventory()[ItemPaintBrush.inkslotnumber];
                bool haspalette = palslot != null && !palslot.Empty && palslot.Itemstack != null && palslot.Itemstack.Collectible != null
                    && palslot.Itemstack.Collectible.Code.ToString().StartsWith("chiseltools:palette");
                
                
                List<int> blockIDCheckList = new List<int>();
                List<string> blockCodeCheckList = new List<string>();
                //see if this material is covered and doesn't require using up inventory
                foreach (int blockid in copiedmaterials)
                {
                    
                    if (bmb.BlockIds.Contains(blockid)) { continue; }// if they match it's fine
                    
                    else if (!blockIDCheckList.Contains(blockid)) { blockIDCheckList.Add(blockid); }//otherwise we need it
                    
                }

                //if we have everything already we're good to paste (even if out of order)
                if (blockIDCheckList.Count == 0) { oktopaste = true; }

                
                //check inventory for required materials
                //  - check for palettes first
                //  - check for dyes
                //  - check for actual blocks

                Dictionary<int, ItemSlot> slotreference = new Dictionary<int, ItemSlot>();
                Dictionary<int, IInventory> inventoryreference = new Dictionary<int, IInventory>();
                Dictionary<int, ItemSlot> dyebuckets = new Dictionary<int,ItemSlot>(); //List of dye containers that need to be relieve
                List<ItemSlot> hitpalettes = new List<ItemSlot>(); //List of palettes that should be damaged if used
                //BS null checks *just in case*
                if (byPlayer.InventoryManager==null||byPlayer.InventoryManager.Inventories == null) { return false; }
                
                //We don't have the required materials so check inventory
                if (!oktopaste)
                {
                    oktopaste = true;
                    //go thru each copied material
                    foreach (int blockid in blockIDCheckList)
                    {
                        //go thru each player inventory to see if this item id is there
                        bool foundthis = false;
                        
                        foreach (IInventory pi in byPlayer.InventoryManager.Inventories.Values)
                        {
                            if (pi == null || pi.ClassName=="creative") {  continue; }//** INTERESTING there's a creative inventory
                            //first see if we have palettes
                            var paletteslots = pi.Where(x => x != null && (!x.Empty) && x.Itemstack != null && x.Itemstack.Collectible != null && x.Itemstack.Collectible.Code.ToString().StartsWith("chiseltools:palette")).ToArray();
                            if (paletteslots != null && paletteslots.Count() > 0)
                            {
                                for (int pc = 0; pc < paletteslots.Count(); pc++)
                                {
                                    var paletteslot = paletteslots[pc];
                                    if (paletteslot != null && ItemPalette.GetPaletteBlockIds(api, paletteslot).Contains(blockid) &&
                                    paletteslot.Itemstack.Collectible.GetRemainingDurability(paletteslot.Itemstack) > 0)
                                    {
                                        //we have a hit
                                        foundthis = true;
                                        hitpalettes.Add(paletteslot);
                                        break;
                                    }
                                }
                            }
                            //we found the item in the palettes
                            if (foundthis) { continue; }

                            //next see if we have actual blocks
                            ItemSlot blockFoundSlot = pi.FirstOrDefault(x => (x != null) && (!x.Empty) && x.Itemstack != null && x.Itemstack.Block != null && (x.Itemstack.Block.Id == blockid||IsAcceptableSubstitute(api,x.Itemstack.Block.Id,blockid)) && x.StackSize>0);
                            
                            if (blockFoundSlot != null)
                            {
                                slotreference[blockid] = blockFoundSlot;
                                inventoryreference[blockid] = pi;
                                foundthis = true;
                                break;
                            }

                            //Check for matching block orientations
                            Block checkblock = api.World.GetBlock(blockid);
                            
                            if (suffixSwapList.Contains(checkblock.Code.EndVariant()))
                            {
                                string  checkcode =new AssetLocation( checkblock.Code.ToString().Replace(checkblock.Code.EndVariant(), ""));
                                
                                blockFoundSlot = pi.FirstOrDefault(x => (x != null) && (!x.Empty) && x.Itemstack != null && x.Itemstack.Block != null && x.Itemstack.Block.Code.PathStartsWith(checkcode) && x.StackSize > 0);
                                if (blockFoundSlot != null)
                                {
                                    slotreference[blockid] = blockFoundSlot;
                                    inventoryreference[blockid] = pi;
                                    foundthis = true;
                                    break;
                                }
                            }

                            //Now check and see if we have dye buckets to process
                            foreach (ItemSlot bucketslot in pi)
                            {
                                if (bucketslot == null || bucketslot.Empty || bucketslot.Itemstack == null || bucketslot.Itemstack.Block == null) { continue; }
                                //if we found a bucket or bowl then check if it has what we want
                                if (bucketslot.Itemstack.Block is BlockLiquidContainerTopOpened)
                                {
                                    BlockLiquidContainerTopOpened container = bucketslot.Itemstack.Block as BlockLiquidContainerTopOpened;
                                    ItemStack containercontents = container.GetContent(bucketslot.Itemstack);
                                    if (containercontents == null || containercontents.StackSize == 0 || containercontents.Item == null) { continue; }
                                    string key = containercontents.Item.Code.ToString();
                                    //we have found a bucket with dye
                                    if (ItemPaintBrush.DyeToCreativeBlockXRef.ContainsKey(key))
                                    {
                                        int matchkey = api.World.GetBlock(new AssetLocation(ItemPaintBrush.DyeToCreativeBlockXRef[key])).Id;
                                        if (matchkey == blockid)
                                        {
                                            foundthis = true;
                                            inventoryreference[blockid] = pi;
                                            dyebuckets[blockid] = bucketslot;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (!foundthis) {
                            missingmat = api.World.GetBlock(blockid).Code.ToString();
                            oktopaste=false;
                            break;
                        } //we didnt' find teh appropriate block return
                        
                    }
                    //we have found everything, now we need to draw from inventory
                    foreach (int blockid in copiedmaterials){
                        if (slotreference.ContainsKey(blockid) && slotreference[blockid].Itemstack!= null)
                        {
                            
                            slotreference[blockid].Itemstack.StackSize--;
                            if (slotreference[blockid].Itemstack.StackSize == 0) { slotreference[blockid].Itemstack = null;  }
                            slotreference[blockid].MarkDirty();
                        }
                        else if (dyebuckets.ContainsKey(blockid))
                        {
                            ItemSlot inkslot = dyebuckets[blockid];
                            BlockLiquidContainerTopOpened container = inkslot.Itemstack.Block as BlockLiquidContainerTopOpened;
                            if (container != null && api is ICoreServerAPI)
                            {
                                ItemStack containercontents = container.GetContent(inkslot.Itemstack);
                                if (containercontents == null) { return false; }
                                containercontents.StackSize -= ChiselToolLoader.serverconfig.paintBrushLiquidMultiplier;
                                if (containercontents.StackSize <= 0) { container.SetContent(inkslot.Itemstack, null); }
                                inkslot.MarkDirty();
                             
                            }
                        }
                    }
                }

                //not ok to paste so skip this, should probably send a message
                if (!oktopaste) {
                    if (capi != null)
                    {
                        capi.ShowChatMessage(Lang.Get("chiseltools:notenoughmaterialstopastefirstmissingmat") + missingmat);
                    }
                    return false;
                }
                if (oktopaste && blockIDCheckList.Count > 0)
                {
                    foreach (ItemSlot paletteSlotToDamage in hitpalettes)
                    {
                        if (paletteSlotToDamage == null) { continue; }
                        try
                        {
                            paletteSlotToDamage.Itemstack.Collectible.DamageItem(api.World, null, paletteSlotToDamage, 1);
                            if (paletteSlotToDamage != null) { paletteSlotToDamage.MarkDirty(); }
                        }
                        catch { }
                    }
                }
            }
            //CuboidWithMaterial cwm = new CuboidWithMaterial();
            //BlockEntityMicroBlock.FromUint(bmb, cwm);
            //replace or add materials
            if (applyMaterials)
            {
                ChiselToolLoader.MicroBlockSnapshot before = ChiselToolLoader.Snapshot(bmb);
                for (int c = 0; c < copiedmaterials.Count(); c++)
                {
                    //first copy what fits
                    if (c < bmb.BlockIds.Count())
                    {
                        bmb.BlockIds[c] = copiedmaterials[c];
                    }
                    //then append new materials as required
                    else
                    {
                        //** This is where we seem to have a problem with adding rotated wood materials, a blank material seems to be returned
                        bool isFull = true;
                        Block newblock = api.World.GetBlock(copiedmaterials[c]);
                        //bmb.BlockIds = bmb.BlockIds.Append(copiedmaterials[c]);
                        bmb.AddMaterial(newblock, out isFull,false);
                        
                    }
                }
                ChiselToolLoader.UpdateBlock(bmb, before);
            }
            return true;
        }

        /// <summary>
        /// This will take the saved list of materials and apply it to the targeted block
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="blockSel"></param>
        bool PasteMaterials(List<int> copiedmaterials, BlockSelection blockSel, IPlayer byPlayer)
        {

            if (blockSel == null || byPlayer == null) return false;
            
            if (copiedmaterials == null || copiedmaterials.Count == 0) return false;
            
            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bmb == null || bmb.BlockIds == null || bmb.BlockIds.Length == 0) { return false; }

            //Materials list already match do nothing
            if (copiedmaterials.Equals(bmb.BlockIds))
            {

                return true;
            }

            //If in survival mode we need to use up materials
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                bool oktopaste = false;
                string missingmat = "";
                //Check and see if the destination block has everything that is in the copied data, and allow paste
                // to continue if true (ie we're just shuffling materials around)
                ItemSlot palslot = byPlayer.InventoryManager.GetHotbarInventory()[ItemPaintBrush.inkslotnumber];
                bool haspalette = palslot != null && !palslot.Empty && palslot.Itemstack != null && palslot.Itemstack.Collectible != null
                    && palslot.Itemstack.Collectible.Code.ToString().StartsWith("chiseltools:palette");


                List<int> blockIDCheckList = new List<int>();
                List<string> blockCodeCheckList = new List<string>();
                //see if this material is covered and doesn't require using up inventory
                foreach (int blockid in copiedmaterials)
                {

                    if (bmb.BlockIds.Contains(blockid)) { continue; }// if they match it's fine

                    else if (!blockIDCheckList.Contains(blockid)) { blockIDCheckList.Add(blockid); }//otherwise we need it

                }

                //if we have everything already we're good to paste (even if out of order)
                if (blockIDCheckList.Count == 0) { oktopaste = true; }


                //check inventory for required materials
                //  - check for palettes first
                //  - check for dyes
                //  - check for actual blocks

                Dictionary<int, ItemSlot> slotreference = new Dictionary<int, ItemSlot>();
                Dictionary<int, IInventory> inventoryreference = new Dictionary<int, IInventory>();
                Dictionary<int, ItemSlot> dyebuckets = new Dictionary<int, ItemSlot>(); //List of dye containers that need to be relieve
                List<ItemSlot> hitpalettes = new List<ItemSlot>(); //List of palettes that should be damaged if used
                //BS null checks *just in case*
                if (byPlayer.InventoryManager == null || byPlayer.InventoryManager.Inventories == null) { return false; }

                //We don't have the required materials so check inventory
                if (!oktopaste)
                {
                    oktopaste = true;
                    //go thru each copied material
                    foreach (int blockid in blockIDCheckList)
                    {
                        //go thru each player inventory to see if this item id is there
                        bool foundthis = false;

                        foreach (IInventory pi in byPlayer.InventoryManager.Inventories.Values)
                        {
                            if (pi == null || pi.ClassName == "creative") { continue; }//** INTERESTING there's a creative inventory
                            //first see if we have palettes
                            var paletteslots = pi.Where(x => x != null && (!x.Empty) && x.Itemstack != null && x.Itemstack.Collectible != null && x.Itemstack.Collectible.Code.ToString().StartsWith("chiseltools:palette")).ToArray();
                            if (paletteslots != null && paletteslots.Count() > 0)
                            {
                                for (int pc = 0; pc < paletteslots.Count(); pc++)
                                {
                                    var paletteslot = paletteslots[pc];
                                    if (paletteslot != null && ItemPalette.GetPaletteBlockIds(api, paletteslot).Contains(blockid) &&
                                    paletteslot.Itemstack.Collectible.GetRemainingDurability(paletteslot.Itemstack) > 0)
                                    {
                                        //we have a hit
                                        foundthis = true;
                                        hitpalettes.Add(paletteslot);
                                        break;
                                    }
                                }
                            }
                            //we found the item in the palettes
                            if (foundthis) { continue; }

                            //next see if we have actual blocks
                            ItemSlot blockFoundSlot = pi.FirstOrDefault(x => (x != null) && (!x.Empty) && x.Itemstack != null && x.Itemstack.Block != null && (x.Itemstack.Block.Id == blockid || IsAcceptableSubstitute(api, x.Itemstack.Block.Id, blockid)) && x.StackSize > 0);

                            if (blockFoundSlot != null)
                            {
                                slotreference[blockid] = blockFoundSlot;
                                inventoryreference[blockid] = pi;
                                foundthis = true;
                                break;
                            }

                            //Check for matching block orientations
                            Block checkblock = api.World.GetBlock(blockid);

                            if (suffixSwapList.Contains(checkblock.Code.EndVariant()))
                            {
                                string checkcode = new AssetLocation(checkblock.Code.ToString().Replace(checkblock.Code.EndVariant(), ""));

                                blockFoundSlot = pi.FirstOrDefault(x => (x != null) && (!x.Empty) && x.Itemstack != null && x.Itemstack.Block != null && x.Itemstack.Block.Code.PathStartsWith(checkcode) && x.StackSize > 0);
                                if (blockFoundSlot != null)
                                {
                                    slotreference[blockid] = blockFoundSlot;
                                    inventoryreference[blockid] = pi;
                                    foundthis = true;
                                    break;
                                }
                            }

                            //Now check and see if we have dye buckets to process
                            foreach (ItemSlot bucketslot in pi)
                            {
                                if (bucketslot == null || bucketslot.Empty || bucketslot.Itemstack == null || bucketslot.Itemstack.Block == null) { continue; }
                                //if we found a bucket or bowl then check if it has what we want
                                if (bucketslot.Itemstack.Block is BlockLiquidContainerTopOpened)
                                {
                                    BlockLiquidContainerTopOpened container = bucketslot.Itemstack.Block as BlockLiquidContainerTopOpened;
                                    ItemStack containercontents = container.GetContent(bucketslot.Itemstack);
                                    if (containercontents == null || containercontents.StackSize == 0 || containercontents.Item == null) { continue; }
                                    string key = containercontents.Item.Code.ToString();
                                    //we have found a bucket with dye
                                    if (ItemPaintBrush.DyeToCreativeBlockXRef.ContainsKey(key))
                                    {
                                        int matchkey = api.World.GetBlock(new AssetLocation(ItemPaintBrush.DyeToCreativeBlockXRef[key])).Id;
                                        if (matchkey == blockid)
                                        {
                                            foundthis = true;
                                            inventoryreference[blockid] = pi;
                                            dyebuckets[blockid] = bucketslot;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (!foundthis)
                        {
                            missingmat = api.World.GetBlock(blockid).Code.ToString();
                            oktopaste = false;
                            break;
                        } //we didnt' find teh appropriate block return

                    }
                    //we have found everything, now we need to draw from inventory
                    foreach (int blockid in copiedmaterials)
                    {
                        if (slotreference.ContainsKey(blockid) && slotreference[blockid].Itemstack != null)
                        {

                            slotreference[blockid].Itemstack.StackSize--;
                            if (slotreference[blockid].Itemstack.StackSize == 0) { slotreference[blockid].Itemstack = null; }
                            slotreference[blockid].MarkDirty();
                        }
                        else if (dyebuckets.ContainsKey(blockid))
                        {
                            ItemSlot inkslot = dyebuckets[blockid];
                            BlockLiquidContainerTopOpened container = inkslot.Itemstack.Block as BlockLiquidContainerTopOpened;
                            if (container != null && api is ICoreServerAPI)
                            {
                                ItemStack containercontents = container.GetContent(inkslot.Itemstack);
                                if (containercontents == null) { return false; }
                                containercontents.StackSize -= ChiselToolLoader.serverconfig.paintBrushLiquidMultiplier;
                                if (containercontents.StackSize <= 0) { container.SetContent(inkslot.Itemstack, null); }
                                inkslot.MarkDirty();

                            }
                        }
                    }
                }

                //not ok to paste so skip this, should probably send a message
                if (!oktopaste)
                {
                    if (capi != null)
                    {
                        capi.ShowChatMessage(Lang.Get("chiseltools:notenoughmaterialstopastefirstmissingmat") + missingmat);
                    }
                    return false;
                }
                if (oktopaste && blockIDCheckList.Count > 0)
                {
                    foreach (ItemSlot paletteSlotToDamage in hitpalettes)
                    {
                        if (paletteSlotToDamage == null) { continue; }
                        try
                        {
                            paletteSlotToDamage.Itemstack.Collectible.DamageItem(api.World, null, paletteSlotToDamage, 1);
                            if (paletteSlotToDamage != null) { paletteSlotToDamage.MarkDirty(); }
                        }
                        catch { }
                    }
                }
            }
            //CuboidWithMaterial cwm = new CuboidWithMaterial();
            //BlockEntityMicroBlock.FromUint(bmb, cwm);
            //replace or add materials
            if (api is ICoreServerAPI)
            {
                ChiselToolLoader.MicroBlockSnapshot before = ChiselToolLoader.Snapshot(bmb);
                for (int c = 0; c < copiedmaterials.Count(); c++)
                {
                    //first copy what fits
                    if (c < bmb.BlockIds.Count())
                    {
                        bmb.BlockIds[c] = copiedmaterials[c];
                    }
                    //then append new materials as required
                    else
                    {
                        //** This is where we seem to have a problem with adding rotated wood materials, a blank material seems to be returned
                        bool isFull = true;
                        Block newblock = api.World.GetBlock(copiedmaterials[c]);
                        //bmb.BlockIds = bmb.BlockIds.Append(copiedmaterials[c]);
                        bmb.AddMaterial(newblock, out isFull, false);

                    }
                }
                ChiselToolLoader.UpdateBlock(bmb, before);
            }
            return true;
        }



        //Will Setup a functional chiseled block with an open or closed frame
        void SetDoor(ItemSlot slot, BlockSelection blockSel, string state, bool transparent=false)
        {
            if (api is ICoreServerAPI) { return; }
            ICoreClientAPI capi = api as ICoreClientAPI;
            //Do nothing if we have no stored voxel information
            List<int> copiedmaterials = GetCopiedBlockMaterials(slot);
            if ((copiedmaterials == null||copiedmaterials.Count==0)&&!transparent) { return; }
            List<uint> copiedblockvoxels = GetCopiedBlockVoxels(slot);
            if ((copiedblockvoxels == null||copiedblockvoxels.Count==0)&&!transparent) { return; }
            bool passable = transparent;
            //if (state == BEFunctionChiseled.openname) { passable = true; }
            //Create a function chiseled block if necessary
            BEFunctionChiseled bfc = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BEFunctionChiseled;
            if (bfc == null) {
                if (state != "CLOSE")
                {
                    capi.ShowChatMessage(Lang.Get("chiseltools:DOORERROR"));
                    return;
                }
                BlockEntityChisel bec = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
                if (bec == null) { return; }
                
                DoorData newdoor = new DoorData();
                newdoor.pos = blockSel.Position;
                newdoor.matdata = new List<int>(bec.BlockIds);
                newdoor.voxeldata = new List<uint>(bec.VoxelCuboids);
                newdoor.state = state;
                newdoor.passable = passable;
                newdoor.transparent = transparent;
                newdoor.name = bec.BlockName;
                (ChiselToolLoader.loader.chiselnet as IClientNetworkChannel).SendPacket<DoorData>(newdoor);
                return;
            }
            
            DoorData dat = new DoorData();
            dat.matdata = copiedmaterials;
            dat.voxeldata = copiedblockvoxels;
            dat.state = state;
            dat.passable = passable;
            dat.transparent = transparent;
            byte[] datbyte = SerializerUtil.Serialize<DoorData>(dat);
            (api as ICoreClientAPI).Network.SendBlockEntityPacket(bfc.Pos, (int)BEFunctionChiseled.enPacketCode.ADDSTATE, datbyte);
            
        }
       public bool CheckForAcceptableSubsitute(int[]checkagainstlist,int matchto)
        {
            string matchtocode = api.World.GetBlock(matchto).Code.ToString();
            foreach (int i in checkagainstlist)
            {
                string c=api.World.GetBlock(i).Code.ToString();
                if (IsAcceptableSubstitute(c, matchtocode)) { return true; }
            }
            return false;
        }
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            dsc.AppendLine(Lang.Get("chiseltools:leftclicktocopyrightclicktopaste"));
            dsc.AppendLine(Lang.Get("chiseltools:copying") + inSlot.Itemstack.Attributes.GetString("copiedname","")); 
            
           
        }
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }

        /// <summary>
        /// Checks to see if a code can be swapped for another
        /// eg: wood-aged-ud for wood-aged-ns etc
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsAcceptableSubstitute(string a, string b)
        {
            //if they're the same code we're good to go
            if (a==b) return true;

            //check and see the two strings end with valid suffixes
            string aSuff = "";
            string bSuff = "";
            foreach (string s in suffixSwapList)
            {
                if (a.EndsWith(s)) { aSuff=s; }
                if (b.EndsWith(s)) { bSuff=s; }
            }
            if (aSuff==""||bSuff=="") return false; //if both codes don't have suffixes then it's no good
            //both have some sort of suffix, will they match with suffixes removed?
            a = a.Replace(aSuff, "");
            b = b.Replace(bSuff, "");
            if (a == b) { return true; }
            return false;
        }
        /// <summary>
        /// see if two blocks are acceptable replacements using block id
        /// </summary>
        /// <param name="api"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsAcceptableSubstitute(ICoreAPI api, int a, int b)
        {
            string ac = api.World.GetBlock(a).Code.ToString();
            string bc = api.World.GetBlock(b).Code.ToString();
            return IsAcceptableSubstitute(ac,bc);
        }

        public static int GetNextSubstitute(ICoreAPI api, int origin)
        {
            if (api == null||api.World==null) return origin;
            string suff = "";
            int suffIndex = -1;

            string origincode=api.World.GetBlock(origin).Code.ToString();
            if (origincode.Length == 0) { return origin; }
            for (int c=0;c<suffixSwapList.Length;c++)
            {
                
                if (origincode.EndsWith(suffixSwapList[c])) {
                    suff=suffixSwapList[c];
                    suffIndex = c;
                }
                
            }
            if (suff == "") { return origin; } //this is not a valid code
            string choppedCode = origincode.Replace(suff, "");
            int[] blockids=new int[suffixSwapList.Length];
            for (int c = 0; c < suffixSwapList.Length; c++)
            {
                AssetLocation al=new AssetLocation(choppedCode+suffixSwapList[c]);
                Block b=api.World.GetBlock(al);
                if (b.BlockId == 0) { return origin; } //found an invalid block
                blockids[c] = b.BlockId;
            }

            int chkctr = 0; int newid = origin; int testingindex = suffIndex;

            while (chkctr < suffixSwapList.Length )
            {
                chkctr++;
                testingindex++;
                if (testingindex >= suffixSwapList.Length)
                {
                    testingindex = 0;
                    
                }
                if (blockids[testingindex] != 0 && blockids[testingindex] != origin)
                {
                    newid = blockids[testingindex];
                    break;
                }
            }
                return newid;
        }
        /// <summary>
        /// Add or remove faces only matching a certain material
        /// Returns voxels cut
        /// </summary>
        /// <param name="mode">Mode selector for the function</param>

        public virtual int ModifyChiseledBlock(BlockSelection blockSel, string usemode, bool addmode, IPlayer byPlayer)
        {
            bool onlymatching = (usemode == "PLANEMATERIAL" || usemode == "PLANEISLAND");

            if (blockSel == null)
            {
                lastpos = null; return 0;
            }
            if (lastpos == null || lastpos != blockSel.Position || lastfacing != blockSel.Face)
            {
                lastpos = blockSel.Position;
                lastfacing = blockSel.Face;

            }
            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bmb == null) { lastpos = null; return 0; }
            byte useindex = 0;
            CuboidWithMaterial cwm = new CuboidWithMaterial();
            //convert the hit point into voxel coordinates
            Vec3i playerTargetedVoxel = new Vec3i((int)(blockSel.HitPosition.X * 16f), (int)(blockSel.HitPosition.Y * 16f), (int)(blockSel.HitPosition.Z * 16f));
            if (blockSel.Face == BlockFacing.SOUTH) { playerTargetedVoxel.Z--; }
            if (blockSel.Face == BlockFacing.UP) { playerTargetedVoxel.Y--; }
            if (blockSel.Face == BlockFacing.EAST) { playerTargetedVoxel.X--; }

            List<CuboidWithMaterial> cuboidsInPlane = new List<CuboidWithMaterial>();
            Cuboidi checkplane = new Cuboidi();
            Vec3i startco = new Vec3i(playerTargetedVoxel.X, playerTargetedVoxel.Y, playerTargetedVoxel.Z); //start coordinates of our build plane
            Vec3i endco = new Vec3i(playerTargetedVoxel.X, playerTargetedVoxel.Y, playerTargetedVoxel.Z); //end coordinates of our build plane
            Vec3i writeoffset = new Vec3i(0, 0, 0);

            //adjust the plane based on facing
            if (blockSel.Face == BlockFacing.NORTH || blockSel.Face == BlockFacing.SOUTH)
            {
                startco.X = 0; startco.Y = 0;
                endco.X = 16; endco.Y = 16;
                endco.Z += 1;
                if (addmode)
                {
                    if (blockSel.Face == BlockFacing.SOUTH) { writeoffset.Z = 1; }
                    else { writeoffset.Z = -1; }
                }
            }
            else if (blockSel.Face == BlockFacing.EAST || blockSel.Face == BlockFacing.WEST)
            {
                startco.Z = 0; startco.Y = 0;
                endco.Z = 16; endco.Y = 16;
                endco.X += 1;
                if (addmode)
                {
                    if (blockSel.Face == BlockFacing.EAST) { writeoffset.X = 1; }
                    else { writeoffset.X = -1; }
                }
            }
            else if (blockSel.Face == BlockFacing.DOWN || blockSel.Face == BlockFacing.UP)
            {
                startco.X = 0; startco.Z = 0;
                endco.X = 16; endco.Z = 16;
                endco.Y += 1;
                if (addmode)
                {
                    if (blockSel.Face == BlockFacing.UP) { writeoffset.Y = 1; }
                    else { writeoffset.Y = -1; }
                }
            }
            //Loop thru the cuboids and find the material we are looking at
            foreach (uint voxint in bmb.VoxelCuboids)
            {
                //this static method converts the uint into a CubioidWithMaterial
                BlockEntityMicroBlock.FromUint(voxint, cwm);
                //if we're looking at this voxel we want to grab this material to use
                if (cwm.Contains(playerTargetedVoxel.X, playerTargetedVoxel.Y, playerTargetedVoxel.Z)) { useindex = cwm.Material; break; }
            }
            //island mode:
            // - get targeted voxel
            // - spreadout in two directions (flood fill like) and find connected voxels of same material
            // - with map of connected voxels extend or remove only those

            if (usemode == "PLANEISLAND")
            {
                int changecounter = 0; //tracks how many voxels altered for tool wear

                int materialtochange = bmb.GetVoxelMaterialAt(playerTargetedVoxel);
                if (materialtochange == 0)
                {
                    return 0;
                }
                List<Vec3i> voxelsWeNeedToCheck = new List<Vec3i>(); //LIST OF VOXELS TO CHECK (checklist)
                List<Vec3i> voxelsToAlter = new List<Vec3i>(); //LIST OF VOXELS TO ACTUALLY CHANGE
                voxelsWeNeedToCheck.Add(playerTargetedVoxel);
                List<Vec3i> voxelsAlreadyChecked = new List<Vec3i>(); //LIST OF VOXELS THAT HAVE BEEN CHECKED
                voxelsAlreadyChecked.Add(playerTargetedVoxel);

                //currently not really iterating past first level
                while (voxelsWeNeedToCheck.Count > 0)
                {
                    if (voxelsWeNeedToCheck.Count == 0) { break; }
                    Vec3i voxeltotest = voxelsWeNeedToCheck[0];

                    if (bmb.GetVoxelMaterialAt(voxeltotest) == materialtochange)
                    {
                        voxelsToAlter.Add(voxeltotest);

                        if (blockSel.Face == BlockFacing.NORTH || blockSel.Face == BlockFacing.SOUTH)
                        {
                            Vec3i temp = voxeltotest.Clone().Add(1, 0, 0);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(-1, 0, 0);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(0, 1, 0);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(0, -1, 0);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                        }
                        else if (blockSel.Face == BlockFacing.UP || blockSel.Face == BlockFacing.DOWN)
                        {
                            Vec3i temp = voxeltotest.Clone().Add(1, 0, 0);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(-1, 0, 0);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(0, 0, 1);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(0, 0, -1);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                        }
                        else //must be e/w
                        {
                            Vec3i temp = voxeltotest.Clone().Add(0, 1, 0);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(0, -1, 0);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(0, 0, 1);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                            temp = voxeltotest.Clone().Add(0, 0, -1);
                            if (!(voxelsAlreadyChecked.Contains(temp)) && InsideBounds(temp)) { voxelsWeNeedToCheck.Add(temp); voxelsAlreadyChecked.Add(temp); }
                        }
                    }
                    voxelsWeNeedToCheck.RemoveAt(0);
                }

                //now we should have a list of voxels to edit
                foreach (Vec3i v in voxelsToAlter)
                {
                    if (!addmode)
                    {
                        bmb.SetVoxel(v, false, byPlayer, 0);
                        changecounter++;
                    }
                    else
                    {
                        Vec3i offsetv = v + writeoffset;
                        if (InsideBounds(offsetv))
                        {
                            if (bmb.GetVoxelMaterialAt(offsetv) == 0)//we only want to write to air blocks
                            {
                                bmb.SetVoxel(offsetv, true, byPlayer, useindex);
                                changecounter++;
                            }
                        }
                    }

                }


                bmb.MarkDirty(true);
                return changecounter;
            }

            //begin processing hand planer for full plane and material plane functions            
            checkplane.Set(startco, endco);

            //now we go thru the cuboids again - we need to grab cuboids that intersect with our plane and that have the appropriate material
            foreach (uint voxint in bmb.VoxelCuboids)
            {

                CuboidWithMaterial checkcuboid = new CuboidWithMaterial();
                BlockEntityMicroBlock.FromUint(voxint, checkcuboid);//convert raw data into a cuboid we can read
                if (onlymatching && checkcuboid.Material != useindex) { continue; }//reject cuboids that don't share our material
                if (checkcuboid.Intersects(checkplane)) { cuboidsInPlane.Add(checkcuboid); }//if it's in the check plane then add it
            }
            if (onlymatching && cuboidsInPlane.Count == 0) { return 0; } //I don't think this is necessary, but maybe if there's a race condition?

            //loop thru 16 x 16 voxel plane (xc/yc will be swapped with other coordinates depending on the direction we are facing)
            //tell the microblock to add each voxel
            //adjust the start and end coordinates to work with the for loop:
            if (endco.X == startco.X) { endco.X++; }
            if (endco.Y == startco.Y) { endco.Y++; }
            if (endco.Z == startco.Z) { endco.Z++; }
            int cutvoxels = 0;
            //Loop thru all the dimensions of our plane (one dimension should be 0 size)
            for (int xc = startco.X; xc < endco.X; xc++)
            {
                for (int yc = startco.Y; yc < endco.Y; yc++)
                {
                    for (int zc = startco.Z; zc < endco.Z; zc++)
                    {
                        BlockPos v = new BlockPos(xc, yc, zc);
                        if (v.X + writeoffset.X > 15 || v.X + writeoffset.X < 0) { continue; }
                        if (v.Y + writeoffset.Y > 15 || v.Y + writeoffset.Y < 0) { continue; }
                        if (v.Z + writeoffset.Z > 15 || v.Z + writeoffset.Z < 0) { continue; }

                        if (onlymatching)
                        {
                            foreach (CuboidWithMaterial cube in cuboidsInPlane)
                            {
                                if (!cube.Contains(v)) { continue; }
                                Vec3i changevoxel = new Vec3i(v.X + writeoffset.X, v.Y + writeoffset.Y, v.Z + writeoffset.Z);
                                bool isemptyvox = bmb.GetVoxelMaterialAt(changevoxel) == 0;
                                if (addmode && !isemptyvox) { break; }//Don't change the voxel unless it's empty if in match mode
                                bmb.SetVoxel(changevoxel, addmode, byPlayer, useindex);
                                cutvoxels++;
                                break;
                            }
                        }
                        else
                        {
                            if (addmode)
                            {
                                bmb.SetVoxel(new Vec3i(v.X + writeoffset.X, v.Y + writeoffset.Y, v.Z + writeoffset.Z), false, byPlayer, useindex);
                            }
                            bmb.SetVoxel(new Vec3i(v.X + writeoffset.X, v.Y + writeoffset.Y, v.Z + writeoffset.Z), addmode, byPlayer, useindex);
                            cutvoxels++;
                        }
                    }
                }
            }

            bmb.MarkDirty(true);
            return cutvoxels;
        }
        public static bool InsideBounds(Vec3i vec)
        {

            if (vec.X < 0 || vec.X > 15) { return false; }
            if (vec.Y < 0 || vec.Y > 15) { return false; }
            if (vec.Z < 0 || vec.Z > 15) { return false; }
            return true;
        }
        public virtual int MoveChiseledBlock(BlockSelection blockSel)
        {
            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bmb == null) { lastpos = null; return 0; }


            Vec3i writeoffset = new Vec3i(0, 0, 0);

            //adjust the plane based on facing
            if (blockSel.Face == BlockFacing.SOUTH) { writeoffset.Z = -1; }
            else if (blockSel.Face == BlockFacing.NORTH) { writeoffset.Z = 1; }
            else if (blockSel.Face == BlockFacing.EAST) { writeoffset.X = -1; }
            else if (blockSel.Face == BlockFacing.WEST) { writeoffset.X = 1; }
            else if (blockSel.Face == BlockFacing.UP) { writeoffset.Y = -1; }
            else if (blockSel.Face == BlockFacing.DOWN) { writeoffset.Y = 1; }

            List<uint> cuboids = new List<uint>();
            //Loop thru the cuboids and find the material we are looking at
            foreach (uint voxint in bmb.VoxelCuboids)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                //this static method converts the uint into a CubioidWithMaterial
                BlockEntityMicroBlock.FromUint(voxint, cwm);
                //Shift coordinates of every cuboid - possible issue - voxels stacking up at outside of object
                cwm.X1 += writeoffset.X; cwm.X1 = GameMath.Clamp(cwm.X1, 0, 16);
                cwm.X2 += writeoffset.X; cwm.X2 = GameMath.Clamp(cwm.X2, 0, 16);
                if (cwm.X2 <= cwm.X1) { continue; }
                cwm.Y1 += writeoffset.Y; cwm.Y1 = GameMath.Clamp(cwm.Y1, 0, 16);
                cwm.Y2 += writeoffset.Y; cwm.Y2 = GameMath.Clamp(cwm.Y2, 0, 16);
                if (cwm.Y2 <= cwm.Y1) { continue; }
                cwm.Z1 += writeoffset.Z; cwm.Z1 = GameMath.Clamp(cwm.Z1, 0, 16);
                cwm.Z2 += writeoffset.Z; cwm.Z2 = GameMath.Clamp(cwm.Z2, 0, 16);
                if (cwm.Z2 <= cwm.Z1) { continue; }
                uint voxelint = BlockEntityMicroBlock.ToUint(cwm.X1, cwm.Y1, cwm.Z1, cwm.X2, cwm.Y2, cwm.Z2, cwm.Material);
                cuboids.Add(voxelint);
            }
            if (cuboids == null || cuboids.Count == 0) { return 0; }
            ChiselToolLoader.UpdateChiseledBlock(bmb, null, cuboids);
            return 16 * 16; //todo calculate actual voxels based on the bounds of the cuboids
        }
        public virtual int MirrorBlock(BlockSelection blockSel)
        {
            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bmb == null) { lastpos = null; return 0; }
            if (bmb.VoxelCuboids == null || bmb.VoxelCuboids.Count == 0) { return 0; }
            List<uint> cuboids = new List<uint>();

            foreach (uint voxint in bmb.VoxelCuboids)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                //this static method converts the uint into a CubioidWithMaterial
                BlockEntityMicroBlock.FromUint(voxint, cwm);
                //x&z should stay the same
                if (blockSel.Face == BlockFacing.UP || blockSel.Face == BlockFacing.DOWN)
                {
                    int y1 = 16 - cwm.Y2;
                    int y2 = 16 - cwm.Y1;
                    cwm.Y1 = y1;
                    cwm.Y2 = y2;
                }
                else if (blockSel.Face == BlockFacing.EAST || blockSel.Face == BlockFacing.WEST)
                {
                    int z1 = 16 - cwm.Z2;
                    int z2 = 16 - cwm.Z1;
                    cwm.Z1 = z1;
                    cwm.Z2 = z2;
                }
                else if (blockSel.Face == BlockFacing.NORTH || blockSel.Face == BlockFacing.SOUTH)
                {
                    int x1 = 16 - cwm.X2;
                    int x2 = 16 - cwm.X1;
                    cwm.X1 = x1;
                    cwm.X2 = x2;
                }
                cuboids.Add(BlockEntityMicroBlock.ToUint(cwm.MinX, cwm.MinY, cwm.MinZ, cwm.MaxX, cwm.MaxY, cwm.MaxZ, cwm.Material));

            }


            if (cuboids == null || cuboids.Count == 0) { return 0; }
            ChiselToolLoader.UpdateChiseledBlock(bmb, null, cuboids);
            return 16 * 16; //todo calculate actual voxels based on the bounds of the cuboids
        }
        public virtual int RotateBlock(BlockSelection blockSel)
        {
            BlockEntityChisel bmb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bmb == null) { lastpos = null; return 0; }
            if (bmb.VoxelCuboids == null || bmb.VoxelCuboids.Count == 0) { return 0; }
            List<uint> cuboids = new List<uint>();
            double angle = 90;
            foreach (uint voxint in bmb.VoxelCuboids)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                //this static method converts the uint into a CubioidWithMaterial
                BlockEntityMicroBlock.FromUint(voxint, cwm);
                Cuboidi torotate = cwm as Cuboidi;
                if (blockSel.Face == BlockFacing.EAST || blockSel.Face == BlockFacing.WEST)
                {
                    torotate = torotate.RotatedCopy(new Vec3d(angle, 0, 0), new Vec3d(8, 8, 8));
                }
                else if (blockSel.Face == BlockFacing.NORTH || blockSel.Face == BlockFacing.SOUTH)
                {
                    torotate = torotate.RotatedCopy(new Vec3d(0, 0, angle), new Vec3d(8, 8, 8));
                }
                else
                {
                    torotate = torotate.RotatedCopy(new Vec3d(0, angle, 0), new Vec3d(8, 8, 8));
                }
                //x&z should stay the same

                cuboids.Add(BlockEntityMicroBlock.ToUint(torotate.MinX, torotate.MinY, torotate.MinZ, torotate.MaxX, torotate.MaxY, torotate.MaxZ, cwm.Material));

            }

            if (cuboids == null || cuboids.Count == 0) { return 0; }
            ChiselToolLoader.UpdateChiseledBlock(bmb, null, cuboids);
            return 16 * 16; //todo calculate actual voxels based on the bounds of the cuboids
        }
    }
}
