using chisel.src;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;


namespace chiseltools.src
{
    public class BlockDecoBookshelf: BlockShapeMaterialFromAttributes
    {
        public Dictionary<string, int[]> UsableSlots = null!;

        public override string MeshKey { get; } = "BookshelfMeshes";

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            LoadTypes();
        }

        public override void LoadTypes()
        {
            base.LoadTypes();
            UsableSlots = Attributes["usableSlots"].AsObject<Dictionary<string, int[]>>();
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var beshelf = blockAccessor.GetBlockEntity(pos) as BEDecoBookShelf;

            if (beshelf?.UsableSlots != null)
            {
                List<Cuboidf> cubs = new List<Cuboidf>
                {
                    new Cuboidf(0, 0, 0, 1, 1, 0.1f),
                    new Cuboidf(0, 0, 0, 1, 1 / 16f, 0.5f),
                    new Cuboidf(0, 15 / 16f, 0, 1, 1, 0.5f),
                    new Cuboidf(0, 0, 0, 1 / 16f, 1, 0.5f),
                    new Cuboidf(15 / 16f, 0, 0, 1, 1, 0.5f)
                };

                for (int i = 0; i < 14; i++)
                {
                    if (!beshelf.UsableSlots.Contains(i))
                    {
                        cubs.Add(new Cuboidf());
                        continue;
                    }

                    float x = (i % 7) * 2f / 16f + 1.1f / 16f;
                    float y = (i / 7) * 7.5f / 16f;
                    float z = 6.5f / 16f;
                    var cub = new Cuboidf(x, y + 1f / 16f, 1 / 16f, x + 1.9f / 16f, y + 7 / 16f, z);



                    cubs.Add(cub);
                }

                for (int i = 0; i < cubs.Count; i++) cubs[i] = cubs[i].RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5));

                return cubs.ToArray();
            }

            return new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 1, 0.5f).RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var beshelf = blockAccessor.GetBlockEntity(pos) as BEDecoBookShelf;

            return new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 1, 0.5f).RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var beb = GetBlockEntity<BlockEntityBookshelf>(pos);
            if (beb?.Type != null && beb.Material != null)
            {
                var mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(beb.MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
                blockModelData = GetOrCreateMesh(beb.Type, beb.Material).Clone().MatrixTransform(mat);
                decalModelData = GetOrCreateMesh(beb.Type, beb.Material, null, decalTexSource).Clone().MatrixTransform(mat);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        //public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        //{
        //    return true;
        //}

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (TryDecoInteract(world, byPlayer, blockSel)) { return true; }
            /*Item hotitem = byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Item;
            if (hotitem != null && hotitem.GetType() == typeof(ItemPantograph))
            {
                return false;
            }*/
            var beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEDecoBookShelf;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        public bool TryDecoInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BBChiseledCover mybb = this.GetBehavior<BBChiseledCover>();
            if (mybb != null)
            {
                //handle clicks with chiseled block and wrench
                bool handled = mybb.TryAddCover(world, byPlayer, blockSel);
                if (handled) { return true; }
                if (mybb.DoWrenchClick(world, byPlayer, blockSel)) { return true; }

            }
            return false;
        }
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {

            if (world.Side is EnumAppSide.Server)
            {
                BlockEntity myrack = world.BlockAccessor.GetBlockEntity(pos);
                if (myrack != null)
                {
                    BEBChiseledCover cover = myrack.GetBehavior<BEBChiseledCover>();
                    if (cover != null) { cover.OnBlockBroken(byPlayer); }
                }
            }
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
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

    }
}

