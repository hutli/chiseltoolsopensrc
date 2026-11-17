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
    public class BEDecoContainer: BlockEntityGenericTypedContainer
    {

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            /*
            if (Block.Attributes?["spoilSpeedMulByFoodCat"].Exists == true&& Block.Attributes?["spoilSpeedMulByFoodCat"]!=null)
            {
                
                
                Type mytype = this.GetType();
                FieldInfo[] members = mytype.GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy);
                FieldInfo fi= members.First(x=>x.Name=="inventory") as FieldInfo;
                InventoryGeneric actualinventory = fi.GetValue(this) as InventoryGeneric;
                Dictionary<EnumFoodCategory,float> foodspoil= new Dictionary<EnumFoodCategory, float>();
                foodspoil[EnumFoodCategory.Grain] = 0.01f;
                foodspoil[EnumFoodCategory.Vegetable] = 0.01f;
                foodspoil[EnumFoodCategory.Protein] = 0.01f;
                actualinventory.PerishableFactorByFoodCategory = foodspoil; //Block.Attributes["spoilSpeedMulByFoodCat"].AsObject<Dictionary<EnumFoodCategory, float>>();
                int b = 1;
                //MeshData parentmesh=fi.GetValue(this as BlockEntityAnvil) as MeshData;
            }
            */
        }

        
        
    }
}
