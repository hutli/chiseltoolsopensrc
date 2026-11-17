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
using System.Reflection;
namespace chiseltools.src
{
    public class BEDecoSign:BlockEntitySign
    {
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {

            //FieldInfo fi=base.GetType().GetField("currentMesh");
            //MeshData parentmesh=fi.GetValue(this as BlockEntityAnvil) as MeshData;

            //mesher.AddMeshData(parentmesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
            BEBChiseledCover cover = GetBehavior<BEBChiseledCover>();
            if (cover != null)
            {
                MeshData covershape = cover.GetChiseledMesh();
                if (covershape != null)
                {
                    mesher.AddMeshData(covershape);
                    return true;
                }
            }
            return false;
        }
    }
}
