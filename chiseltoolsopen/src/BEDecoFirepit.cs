using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace chiseltools.src
{
    public class BEDecoFirepit: BlockEntityFirepit
    {
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (base.Block == null)
            {
                return false;
            }

            bool skipdefault = false;
            BEBChiseledCover cover = GetBehavior<BEBChiseledCover>();
            if (cover != null)
            {
                MeshData covershape = cover.GetChiseledMesh();
                if (covershape != null)
                {
                    mesher.AddMeshData(covershape);
                    skipdefault = true;
                    //return true;
                }
            }
            
            //think this adds the rendered item - not sure why crucible not working
            ItemStack contentStack = inputStack == null ? outputStack : inputStack;
            
            

            MethodInfo getContentMeshMethod = typeof(BlockEntityFirepit).GetMethod("getContentMesh",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);
            if (getContentMeshMethod != null)
            {
                
                MeshData contentmesh=(MeshData)getContentMeshMethod.Invoke(this, new object[] { contentStack,tesselator });
                //MeshData contentmesh = getContentMeshModded(contentStack, tesselator);
                if (contentmesh != null)
                {
                    mesher.AddMeshData(contentmesh);
                }
            }
            
            /*string burnState = Block.Variant["burnstate"];
            string contentState = CurrentModel.ToString().ToLowerInvariant();
            if (burnState == "cold" && fuelSlot.Empty) burnState = "extinct";

            mesher.AddMeshData(getOrCreateMesh(burnState, contentState));*/
            

            return  skipdefault;

        }
       
        private InFirePitProps GetRenderPropsModded(ItemStack contentStack)
        {
            if (contentStack != null && (contentStack.ItemAttributes?.KeyExists("inFirePitProps")).GetValueOrDefault())
            {
                InFirePitProps inFirePitProps = contentStack.ItemAttributes["inFirePitProps"].AsObject<InFirePitProps>();
                inFirePitProps.Transform.EnsureDefaultValues();
                return inFirePitProps;
            }

            return null;
        }
    }
}
