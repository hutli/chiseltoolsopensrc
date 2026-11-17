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
    public class BEDecoBarrel:BlockEntityBarrel
    {
        
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            BEBChiseledCover cover=GetBehavior<BEBChiseledCover>();
            
            if (cover != null)
            {
                MeshData covermesh = cover.GetChiseledMesh();
                if (covermesh != null)
                {
                    mesher.AddMeshData(covermesh);
                    return true;
                }
            }
            return base.OnTesselation(mesher, tesselator);
        }
    }
}
