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
using ProtoBuf;
using Microsoft.VisualBasic;
using Vintagestory.Server;
using chiseltools.src;
namespace chisel.src
{
    
    
    public class BEFunctionChiseled : BlockEntityMicroBlock
    {
        public enum enPacketCode { SETSTATE=999001, ADDSTATE=999002, SETCONTROLLED=999003, SETSOUND=999004, TOGGLESTATE=999005,UPDATETRIGGERS=999006,SETOPEN=999007,SETCLOSE=999008 };
        public static string openname = "OPEN";
        public static string closename = "CLOSE";
        public static string originalblockname = "ORIGINAL";
        string currentstate = closename;
        string usesound = "";
        public string UseSound => usesound;
        GUIFunctionalChiseledConfig gui;
        Dictionary<string, List<uint>> statevoxels;
        
        Dictionary<string, List<int>> statematerials;
        ICoreServerAPI sapi;
        //features for functional blocks
        TriggerSettings triggersettings= new TriggerSettings();
        public TriggerSettings TriggerSettings => triggersettings;
        Int64 triggerCooldown = 0;
        //Passable needs a way to set things passable (ie no collision)
        Dictionary<string, bool> statepassable;
        List<BlockPos> controlledblocks;
        public List<BlockPos> ControlledBlocks => controlledblocks;
        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
           
            //ItemStack dropstack = new ItemStack(sapi.World.GetItem(new AssetLocation("chiseltools:doorpart")), 1);
            //sapi.World.SpawnItemEntity(dropstack, Pos.ToVec3d());
            controlblockpos = null;
            if (controlledblocks != null && controlledblocks.Count > 0)
            {
                foreach (BlockPos pos in controlledblocks)
                {
                    if (pos == null) {  continue; }
                    BEFunctionChiseled bfc = Api.World.BlockAccessor.GetBlockEntity(pos) as BEFunctionChiseled;
                    if (bfc == null) { continue; }
                    bfc.SetControlledBlocks(new List<BlockPos>());
                }
                controlledblocks = new List<BlockPos>();
            }
            //now we need to make a new chiseled block where the old door block was and copy back original state
            if (statevoxels==null|| !statevoxels.ContainsKey(originalblockname)) { return; }
            //package up a special door data flagged to create original and let server make the block next tick
            
            if (Api is ICoreClientAPI &&statematerials!=null&&statematerials!=null&&Pos!=null)
            {
                DoorData reborn = new DoorData();
                reborn.voxeldata = new List<uint>(statevoxels[originalblockname]);
                reborn.matdata = new List<int>(statematerials[originalblockname]);
                reborn.pos = Pos;
                reborn.makenewblock = true;
                if (ChiselToolLoader.loader == null) { return; }
                (ChiselToolLoader.loader.chiselnet as IClientNetworkChannel).SendPacket<DoorData>(reborn);
            }
        }

        public virtual bool Passable {
            get
            {
                if (statepassable == null || !statepassable.ContainsKey(currentstate)) { return false; }
                return statepassable[currentstate];
            }
        }

        public void SetSound(string tosound)
        {
            usesound = tosound;
            if (Api is ICoreClientAPI)
            {
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, (int)enPacketCode.SETSOUND, SerializerUtil.Serialize<string>(usesound));
            }
            else { MarkDirty(); }
        }
       

        


        BlockPos controlblockpos;
        public BlockPos ControlBlockPos => controlblockpos;
        
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            //just a note - when this is placed it's already a door block so we can't read the old block here
            controlblockpos = Pos;
            SetupDictionaries();
            triggersettings = new TriggerSettings();
            triggersettings.triggerByInteraction = true;

            List<int> newmat = new List<int>();
            Block blankblock = Api.World.GetBlock(new AssetLocation("chiseltools:techblank"));
            newmat.Add(blankblock.BlockId);

            List<uint> newvox = new List<uint>();
            CuboidWithMaterial cwms = new CuboidWithMaterial();
            cwms.Set(0,0,0,16,16,16);
            cwms.Material = 0;

            newvox.Add(ToUint(cwms));
            //add a (ugly) block as closed state
            AddState(closename, newvox, newmat, false, false);
            //add a default open/empty state
            AddState(openname, newvox, newmat, true, true);
            MarkDirty(true);
            
            base.OnBlockPlaced(byItemStack);
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            if (api is ICoreServerAPI) { sapi = api as ICoreServerAPI; }
            if (triggersettings == null) { triggersettings = new TriggerSettings();triggersettings.triggerByInteraction = true; }
            SetState(currentstate);
        }

        //Add set of voxel data to the library
        public virtual void AddState(string key, List<uint> voxels, List<int> materials, bool passable = false, bool blank = false)
        {
            if (sapi == null) { return; }

            SetupDictionaries();
            if (blank)
            {
                CuboidWithMaterial cwms = new CuboidWithMaterial();
                cwms.Set(1, 1, 1, 14, 14, 14);
                cwms.Material = 0;
                materials = new List<int>();
                Block blankblock = Api.World.GetBlock(new AssetLocation("chiseltools:blankblock"));
                materials.Add(blankblock.BlockId);

                voxels = new List<uint>();
                voxels.Add(ToUint(cwms));
            }
            //check for closed state if doing open first
            if (key== BEFunctionChiseled.openname && !statevoxels.ContainsKey(BEFunctionChiseled.closename))
            {
                statevoxels[BEFunctionChiseled.closename] = voxels;
                statematerials[BEFunctionChiseled.closename] = materials;
                statepassable[BEFunctionChiseled.closename] = !passable;
            }
            statevoxels[key] = voxels;
            statematerials[key] = materials;
            statepassable[key] = passable;
            MarkDirty(true);
            
        }
        

        //will cycle between open and closed, or set to closed if in a different state
        public virtual void ToggleOpenClosed()
        {
            if (Api is ICoreClientAPI)
            {
                //(Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos,(int)enPacketCode.TOGGLESTATE);
                return;
            }
            
            //first if we are controlled by a block we will just let the block know and wait for it to tell us to open/close
            if (controlblockpos != Pos)
            {
                BEFunctionChiseled bfc = Api.World.BlockAccessor.GetBlockEntity(controlblockpos) as BEFunctionChiseled;
                if (bfc != null)
                {
                    bfc.ToggleOpenClosed();
                    
                    return;
                }
                else
                {
                    controlblockpos = Pos;
                }
            }
            AssetLocation soundal = usesound;
            if (Api is ICoreServerAPI) { Api.World.PlaySoundAt(soundal, Pos, 0); }
            string targetstate = currentstate;
            if (currentstate == closename) { SetState(openname); targetstate = openname; }
            else { SetState(closename); targetstate = closename; }
            if (controlledblocks != null && controlledblocks.Count > 0)
            {
                foreach (BlockPos p in controlledblocks)
                {
                    BEFunctionChiseled bfc = Api.World.BlockAccessor.GetBlockEntity(p) as BEFunctionChiseled;
                    if (bfc == null) { continue; }
                    bfc.ControlBlockSignal(Pos, targetstate);
                    bfc.MarkDirty(true);
                    
                }
            }
        }

        //change state to match door control signal (if possible)
        //also will record a given door as controlling it so it can send future door toggle requests
        //to the controller
        //also if a door control signal is recieved it will cease controlling other doors
        public virtual bool ControlBlockSignal(BlockPos fromblock, string tostate)
        {
            controlblockpos = fromblock;
            controlledblocks = new List<BlockPos>();
            SetState(tostate);
            MarkDirty(true);
            return true;
        }

        public virtual void SetStateFromServer(string tostate)
        {
            SetState(tostate);
            MarkDirty(true);
        }

        public virtual void InitControlBlockSignal(BlockPos fromblock)
        {
            controlblockpos = fromblock;
            controlledblocks = new List<BlockPos>();
            MarkDirty(true);
            SetState(closename);
        }

        void SetupDictionaries() {
            if (statevoxels == null) { statevoxels = new Dictionary<string, List<uint>>(); }
            if (statematerials == null) { statematerials = new Dictionary<string, List<int>>(); }

            if (statepassable == null) { statepassable = new Dictionary<string, bool>(); }
            if (controlledblocks == null) { controlledblocks = new List<BlockPos>(); }
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {

            //Mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rotationspeed * GameMath.DEG2RAD, 0);
            //rotationtracker += rotationspeed;
            return base.OnTesselation(mesher, tesselator);
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            
            if (packetid == (int)enPacketCode.ADDSTATE && data!=null)
            {
                DoorData dat = SerializerUtil.Deserialize<DoorData>(data);
                if (dat == null) { return; }
                AddState(dat.state, dat.voxeldata, dat.matdata, dat.passable, dat.transparent);
            }
            else if (packetid == (int)enPacketCode.SETCONTROLLED)
            {
                if (data == null) { controlblockpos = Pos;controlledblocks = new List<BlockPos>();MarkDirty(true); }
                else
                {
                    controlledblocks = SerializerUtil.Deserialize<List<BlockPos>>(data);
                    controlblockpos = Pos;
                    MarkDirty(true);
                }
            }
            else if (packetid == (int)enPacketCode.SETSOUND)
            {
                if (data == null) { usesound = ""; }
                else
                {
                    usesound=SerializerUtil.Deserialize<string>(data,"");
                }
                MarkDirty(true);
            }
            else if (packetid == (int)enPacketCode.TOGGLESTATE)
            {
                if (DateAndTime.Now.Ticks > triggerCooldown)
                {
                    ToggleOpenClosed();
                    triggerCooldown=DateAndTime.Now.Ticks+triggersettings.minTimeBetweenTrigger*100000;
                }
            }
            else if (packetid == (int)enPacketCode.UPDATETRIGGERS)
            {
                TriggerSettings newtriggersettings=SerializerUtil.Deserialize<TriggerSettings>(data);
                if (newtriggersettings != null)
                {
                    triggersettings=new TriggerSettings(newtriggersettings);
                    MarkDirty();
                }
            }
            base.OnReceivedClientPacket(fromPlayer, packetid, data);
        }
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)enPacketCode.ADDSTATE && data != null)
            {
                DoorData dat = SerializerUtil.Deserialize<DoorData>(data);
                if (dat == null) { return; }
                AddState(dat.state, dat.voxeldata, dat.matdata, dat.passable, dat.transparent);
            }
            else if (packetid == (int)enPacketCode.SETOPEN)
            {
                SetStateFromServer(openname);
            }
            else if (packetid == (int)(int)enPacketCode.SETCLOSE)
            {
                SetStateFromServer(closename);
            }
            base.OnReceivedServerPacket(packetid, data);
        }
        //Set the object to a particular state
        
        public virtual void SetState(string newstate)
        {
            if (Api is ICoreClientAPI) { return; }
            if (statepassable == null || !statepassable.ContainsKey(newstate)) { return; }

            //1.19
            this.BlockIds = statematerials[newstate].ToArray();
            
            this.VoxelCuboids = statevoxels[newstate];

            currentstate = newstate;
           
            MarkDirty(true);
            if (newstate == openname)
            {
                sapi.Network.BroadcastBlockEntityPacket(Pos, (int)enPacketCode.SETOPEN);
                
            }
            else if (newstate == closename)
            {
                sapi.Network.BroadcastBlockEntityPacket(Pos, (int)enPacketCode.SETCLOSE);
                
            }
            this.MarkMeshDirty();
            Api.World.BlockAccessor.SetBlock(this.Block.BlockId, Pos);
            Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);



        }

        public virtual void SetControlledBlocks(List<BlockPos> newlist)
        {
            controlblockpos = Pos;
            controlledblocks = new List<BlockPos>(newlist);

            foreach (BlockPos p in newlist)
            {
                if (p == Pos)
                {
                    break;
                }
                BEFunctionChiseled bfc = Api.World.BlockAccessor.GetBlockEntity(p) as BEFunctionChiseled;
                if (p == null) { continue; }
                bfc.InitControlBlockSignal(Pos);
            }

            SetState(closename);
        }



        public void OpenGUI()
        {
            if (gui != null) { gui.TryClose(); }
            gui = new GUIFunctionalChiseledConfig(Api as ICoreClientAPI, this);
            gui.TryOpen();
        }

        public void CloseGUI()
        {
            if (gui != null) { gui.TryClose(); }
        }

        public void UpdateTriggers(TriggerSettings newtriggersettings)
        {
            ICoreClientAPI capi= Api as ICoreClientAPI;
            if (capi == null) { return; }
            triggersettings = new TriggerSettings(newtriggersettings);
            byte[] data = SerializerUtil.Serialize(triggersettings);
            capi.Network.SendBlockEntityPacket(Pos, (int)enPacketCode.UPDATETRIGGERS, data);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            try
            {
                base.ToTreeAttributes(tree);
            }
            catch
            {
                return;
            }
            tree.SetString("currentstate", currentstate);

            //Serialize &  Save all the dictionaries
            SetupDictionaries();
            byte[] bvox = SerializerUtil.Serialize<Dictionary<string, List<uint>>>(statevoxels);
            byte[] bmat = SerializerUtil.Serialize<Dictionary<string, List<int>>>(statematerials);
            byte[] bpass = SerializerUtil.Serialize<Dictionary<string, bool>>(statepassable);
            byte[] bcontrol = SerializerUtil.Serialize<List<BlockPos>>(controlledblocks);
            tree.SetBytes("statevoxels", bvox);
            tree.SetBytes("statematerials", bmat);
            tree.SetBytes("statepassable", bpass);
            tree.SetBytes("controlledblocks", bcontrol);
            if (controlblockpos == null) { controlblockpos = Pos; }
            tree.SetBlockPos("controlblockpos", controlblockpos);
            tree.SetString("sound", usesound);
            triggersettings.ToTreeAttributes(ref tree);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            currentstate = tree.GetString("currentstate", currentstate);
            controlblockpos = tree.GetBlockPos("controlblockpos", controlblockpos);
            triggersettings = new TriggerSettings(tree);
            SetupDictionaries();
            try
            {
                byte[] voxdat = tree.GetBytes("statevoxels", null);
                
                if (voxdat == null) { return; }
                byte[] matdat = tree.GetBytes("statematerials", null);
                if (matdat == null) { return; }
                byte[] passdat = tree.GetBytes("statepassable", null);
                if (passdat == null) { return; }

                statevoxels = SerializerUtil.Deserialize<Dictionary<string, List<uint>>>(voxdat);
                statematerials = SerializerUtil.Deserialize<Dictionary<string, List<int>>>(matdat);
                statepassable = SerializerUtil.Deserialize<Dictionary<string, bool>>(passdat);
                byte[] controldat = tree.GetBytes("controlledblocks", null);
                if (controldat != null)
                {
                    controlledblocks = SerializerUtil.Deserialize<List<BlockPos>>(controldat);
                }
                usesound = tree.GetString("sound", "");
            }
            catch
            {
                return;
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            //dsc.AppendLine(currentstate);
            if (controlblockpos == null) { controlblockpos = Pos; }
            //if (controlblockpos != Pos) { dsc.AppendLine(Lang.Get("chiseltools:controlledby") + controlblockpos.ToLocalPosition(Api).ToString()); }
            
            if (controlledblocks != null && controlledblocks.Count > 0)
            {
                dsc.Append("*");
                
            }
            //if (usesound != "") { dsc.AppendLine("Sound set to "+usesound); }
            if (VoxelCuboids == null || VoxelCuboids.Count == 0) { return; }
            base.GetBlockInfo(forPlayer, dsc);

        }
        
        public virtual void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (Api is ICoreServerAPI) { return; }
            if ((Api as ICoreClientAPI).World.Player.Entity.EntityId!=entity.EntityId) { return; }
            if (Api is ICoreClientAPI && triggersettings.ShouldEntityTrigger(entity,pos)) //for chiseled blocks client side doesn't trigger
            {
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos,(int) enPacketCode.TOGGLESTATE);
            }
        }


    }
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class DoorData
    {
        public BlockPos pos;
        public DoorData() { }
        public string state;
        public List<uint> voxeldata;
        public List<int> matdata;
        public bool passable=false;
        public bool transparent = false;
        public bool makenewblock = false;
        public string name="Door";
        
    }
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TriggerSettings
    {
        public TriggerSettings() { }
        public TriggerSettings(TriggerSettings other) {
            triggerByAnyEntity = other.triggerByAnyEntity;
            triggerByPlayerEntity = other.triggerByPlayerEntity;
            triggerByAllowedPlayer = other.triggerByAllowedPlayer;
            triggerByAnimals = other.triggerByAnimals;
            triggerByHostiles = other.triggerByHostiles;
            triggerByInteraction = other.triggerByInteraction;
            minTimeBetweenTrigger= other.minTimeBetweenTrigger;
            interactionSet=other.interactionSet;
        }
        public TriggerSettings(ITreeAttribute tree)
        {
            triggerByAnyEntity = tree.GetAsBool("triggerByAnyEntity", triggerByAnyEntity);
            triggerByPlayerEntity = tree.GetAsBool("triggerByPlayerEntity", triggerByPlayerEntity);
            triggerByAllowedPlayer = tree.GetAsBool("triggerByAllowedPlayer", triggerByAllowedPlayer);
            triggerByAnimals = tree.GetAsBool("triggerByAnimals", triggerByAnimals);
            triggerByHostiles = tree.GetAsBool("triggerByHostiles", triggerByHostiles);
            triggerByInteraction = tree.GetAsBool("triggerByInteraction", triggerByInteraction);
            minTimeBetweenTrigger = tree.GetInt("minTimeBetweenTrigger", minTimeBetweenTrigger);
            interactionSet = tree.GetBool("interactionSet", interactionSet);
            if (!interactionSet) { interactionSet = true; triggerByInteraction = true; }
        }
        public void ToTreeAttributes(ref ITreeAttribute tree)
        {
            tree.SetBool("triggerByAnyEntity", triggerByAnyEntity);
            tree.SetBool("triggerByPlayerEntity", triggerByPlayerEntity);
            tree.SetBool("triggerByAllowedPlayer", triggerByAllowedPlayer);
            tree.SetBool("triggerByAnimals", triggerByAnimals);
            tree.SetBool("triggerByHostiles", triggerByHostiles);
            tree.SetBool("triggerByInteraction", triggerByInteraction);
            tree.SetInt("minTimeBetweenTrigger", minTimeBetweenTrigger);
            tree.SetBool("interactionSet", interactionSet);
        }
        public bool ShouldEntityTrigger(Entity e, BlockPos pos)
        {
            if (e == null|| pos==null) return false;
            

            if (triggerByAnyEntity) return true;
            EntityPlayer eplr = e as EntityPlayer;
            if (eplr == null) return false;
            
            if (triggerByAllowedPlayer && eplr!=null && eplr.World!=null &&eplr.World.Claims!=null)
            {
                EnumWorldAccessResponse wwar = eplr.World.Claims.TestAccess(eplr.Player, pos, EnumBlockAccessFlags.Traverse);

                if (wwar==EnumWorldAccessResponse.Granted)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            if (triggerByPlayerEntity && eplr != null) return true;

            return false;
        }
        public bool triggerByAnyEntity;// = false;
        public bool triggerByPlayerEntity;// = false;
        public bool triggerByAllowedPlayer;// = false;
        public bool triggerByAnimals;// = false;
        public bool triggerByHostiles;// = false;
        public bool triggerByInteraction;// = true;
        public bool interactionSet;
        public int minTimeBetweenTrigger = 100;

    }
}
