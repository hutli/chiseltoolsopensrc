using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

using Vintagestory.API.Util;
using chisel.src;

namespace chiseltools.src
{
    public class BlockGenericDecoTypedContainer: BlockGenericTypedContainer,IAttachableToEntity, IWearableShapeSupplier
    {
        override public bool IsAttachable(Entity toEntity, ItemStack itemStack)
        {
            return false;
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be != null)
            {
                BEBChiseledCover beb = be.Block.GetBEBehavior<BEBChiseledCover>(pos);
                if (beb != null)
                {
                    string chiselname = beb.GetChiseledName();
                    if (chiselname != "") { return chiselname; }
                }
            }
            return base.GetPlacedBlockName(world, pos);
        }
        Shape IWearableShapeSupplier.GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            AssetLocation shapeloc = new AssetLocation("game:shapes/block/wood/chest/normal.json");
            Shape shape = api.Assets.TryGet(shapeloc).ToObject<Shape>();
            //**** Not sure if this is the issue with crashing on boats?
            shape.SubclassForStepParenting("game:chest-normalgeneric-", 0);
            //shape.SubclassForStepParenting(texturePrefixCode, 0);


            return shape;
        }
        public new void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {

            string type = stack.Attributes.GetString("type");
            if (type == null)
            {

            }
            else
            {
                foreach (var key in shape.Textures.Keys)
                {
                    intoDict[texturePrefixCode + key] = this.Textures[type + "-" + key];
                }
            }
        }
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            Item hotitem = byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Item;
            if (hotitem != null && hotitem.GetType() == typeof(ItemPantograph))
            {
                return false;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        /*
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            MicroBlockModelCache cache = capi.ModLoader.GetModSystem<MicroBlockModelCache>();
            renderinfo.ModelRef = cache.GetOrCreateMeshRef(itemstack);
        }*/
    }
}
