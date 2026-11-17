using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
//using VoxReader.Interfaces;
//using VoxReader;
using Vintagestory.GameContent;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace chisel.src
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [Serializable]
    public class PantographData
    {

        public string name; //name of the piece
        public byte[] voxeldata; //raw voxel data
        public byte[] matdata; //Serialized int[] raw material reference array (material indexes, doesn't know what the actual material is)
        public string[] blockcodes;
        public bool materialPasteOK;
        public BlockPos pos;
        public PantographData() { }
        public PantographData(string name, byte[] voxeldata, byte[] matdata, string[] blockcodes, bool oktopastemat)
        {
            this.name = name;
            this.voxeldata = voxeldata;
            this.matdata = matdata;
            this.materialPasteOK = oktopastemat;
            this.blockcodes = blockcodes;
        }
        public PantographData(string name, List<uint> voxeldata, int[]materials,string[] blockcodes, bool oktopastemat = true)
        {
            this.name = name;
            this.voxeldata=SerializerUtil.Serialize(voxeldata);
            this.matdata=SerializerUtil.Serialize(matdata);
            this.materialPasteOK=oktopastemat;
            this.blockcodes = blockcodes;
        }
        public PantographData(PantographData data)
        {
            this.name = data.name;
            this.voxeldata = (byte[])data.voxeldata.Clone();
            this.matdata=(byte[])data.matdata.Clone();
            this.materialPasteOK = data.materialPasteOK;
            this.blockcodes = (string[]) data.blockcodes.Clone();
        }

        public PantographData(ICoreAPI api, BlockEntityChisel bec,bool oktopastemat = true)
        {
            this.materialPasteOK =(oktopastemat);
            if (bec != null)
            {
                this.voxeldata = SerializerUtil.Serialize(bec.VoxelCuboids);
                this.matdata = SerializerUtil.Serialize(bec.BlockIds);
                this.blockcodes = new string[matdata.Length];
                for (int i = 0; i < matdata.Length; i++)
                {
                    this.blockcodes[i]= api.World.GetBlock(matdata[i]).Code;
                }
                this.name = bec.BlockName;
                this.pos = bec.Pos;
            }
        }

        /// <summary>
        /// Attempt to create pantograph data from a pantograph in a supplied slot, returns null if nothing there
        /// </summary>
        /// <param name="api"></param>
        /// <param name="fromslot"></param>
        /// <returns></returns>
        public static PantographData TryGrabFromSlot(ICoreAPI api, ItemSlot fromslot)
        {
            if (api==null||fromslot==null) return null;
            return TryGrabFromStack(api,fromslot.Itemstack);
        }

        /// <summary>
        /// Attempt to create pantograph data from a pantograph in a supplied slot, returns null if nothing there
        /// </summary>
        /// <param name="api"></param>
        /// <param name="fromslot"></param>
        /// <returns></returns>
        public static PantographData TryGrabFromStack(ICoreAPI api, ItemStack fromstack)
        {
            //check for valid pantograph
            if (api == null || fromstack == null)
            {
                return null;
            }
            
            Item holditem = fromstack.Item;
            if (holditem == null || !(holditem is ItemPantograph)) { return null; }
            string copiedblockname = fromstack.Attributes.GetString(ItemPantograph.copiedblocknameK, "");
            byte[] voxdat = fromstack.Attributes.GetBytes(ItemPantograph.copiedblockvoxelsK, null);
            byte[] copmat = fromstack.Attributes.GetBytes(ItemPantograph.copiedmaterialistK, null);
            byte[] blockcodedata = fromstack.Attributes.GetBytes(ItemPantograph.copiedblockcodesK, null);
            bool oktopastemat = fromstack.Attributes.GetBool(ItemPantograph.materialpasteokk, true);
            if (voxdat == null) { return null; }
            if (copmat == null) { return null; }
            string[] blockcodes = null;
            if (blockcodedata != null)
            {
                blockcodes = SerializerUtil.Deserialize<string[]>(blockcodedata);
            }
            PantographData data = new PantographData(copiedblockname, voxdat, copmat, blockcodes, oktopastemat);

            return data;
        }


        /// <summary>
        /// Helper function to remove materials out of range for application to another block.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="maxmaterials"></param>
        /// <returns></returns>
        public static PantographData ReduceMaterialCount(PantographData data,int maxmaterials)
        {
            List<uint> voxels = SerializerUtil.Deserialize<List<uint>>(data.voxeldata);
            List<uint> newvoxeldata = new List<uint>();
            foreach (uint v in voxels)
            {
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                BlockEntityMicroBlock.FromUint(v, cwm);
                if (cwm.Material >= maxmaterials) { cwm.Material = 0; }
                newvoxeldata.Add(BlockEntityMicroBlock.ToUint(cwm));

            }
            data.voxeldata=SerializerUtil.Serialize(newvoxeldata);
            return data;
        }

        //Quick test to see if a vec3i is in a valid position
        public static bool VoxelInRange(Vec3i testvoxel)
        {
            if (testvoxel.X < 0 || testvoxel.X > 15)  return false; 
            if (testvoxel.Y<0||testvoxel.Y> 15) return false;
            if (testvoxel.Z<0||testvoxel.Z> 15) return false;
            return true;
        }

        /*public static bool LoadVoxFile(string filename, out PantographData outvox)
        {
            
            PantographData voxdata=new PantographData();
            IVoxFile voxfile = VoxReader.VoxReader.Read(filename); 
            
            //stage one build a list of materials
            List<VoxReader.Color>colorlist = new List<VoxReader.Color>();
            List<uint>cuboids= new List<uint>();
            //loop thru all the voxels and grab what we can, also setup colours
            foreach (VoxReader.Voxel v in voxfile.Models[0].Voxels)
            {
                Vec3i vec3I = new Vec3i(v.Position.X,v.Position.Y,v.Position.Z);
                        
                if (!PantographData.VoxelInRange(vec3I)) { continue; }//this voxel is out of range do nothing
                int colorindex = 0;
                if (!colorlist.Contains(v.Color)) {
                    colorlist.Add(v.Color);
                    colorindex = colorlist.Count() - 1;
                }
                else
                {
                    colorindex= colorlist.IndexOf(v.Color);
                }
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                cwm.Material = (byte)colorindex;
                //cwm.Material = 0; //temporarily set material to 0 for testing!
                cwm.X1=vec3I.X; cwm.X2 =vec3I.X+1;
                cwm.Y1 = vec3I.Z;cwm.Y2=vec3I.Z+1;
                cwm.Z1 = vec3I.Y; cwm.Z2 =vec3I.Y+1;
                cuboids.Add(BlockEntityMicroBlock.ToUint(cwm));
                        
            }
            if (cuboids.Count() == 0) { outvox = null; return false; }//no valid cuboids return
                    
            voxdata.voxeldata = SerializerUtil.Serialize(cuboids);
            //just going to use blockids starting at 1, as they will not be used anyways
            List<int>tempmat= new List<int>();
            for (int c=0; c<colorlist.Count(); c++)
            {
                tempmat.Add (c+1);
            }
            voxdata.matdata = SerializerUtil.Serialize(tempmat);
            outvox = voxdata;
            return true;
        }*/
    }
}
