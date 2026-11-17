using chisel.src;
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

namespace chiseltools.src
{
    public class BlockDecoBed:BlockDeco
    {
        public static IMountableSeat GetMountable(IWorldAccessor world, TreeAttribute tree)
        {
            BlockPos pos = new BlockPos(tree.GetInt("posx"), tree.GetInt("posy"), tree.GetInt("posz"));
            Block block = world.BlockAccessor.GetBlock(pos);

            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            BlockEntityBed beBed = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBed;

            return beBed;
        }

        

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            Item hotitem = byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Item;
            if (hotitem != null && hotitem.GetType() == typeof(ItemPantograph))
            {
                return false;
            }
            if (TryDecoInteract(world, byPlayer, blockSel)) { return true; }
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            BlockFacing facing = BlockFacing.FromCode(LastCodePart()).Opposite;
            BlockEntityBed beBed = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBed;

            if (beBed == null) return false;
            if (beBed.MountedBy != null) return false;

            EntityBehaviorTiredness ebt = byPlayer.Entity.GetBehavior("tiredness") as EntityBehaviorTiredness;
            if (ebt != null && ebt.Tiredness <= 8)
            {
                if (world.Side == EnumAppSide.Client) (api as ICoreClientAPI).TriggerIngameError(this, "nottiredenough", Lang.Get("not-tired-enough"));
                else byPlayer.Entity.TryUnmount();
                return false;
            }

            int tempStormSleep = world.Config.GetString("temporalStormSleeping", "0").ToInt();
            if (tempStormSleep == 0 && api.ModLoader.GetModSystem<SystemTemporalStability>().StormStrength > 0)
            {
                if (world.Side == EnumAppSide.Client) (api as ICoreClientAPI).TriggerIngameError(this, "cantsleep-tempstorm", Lang.Get("cantsleep-tempstorm"));
                else byPlayer.Entity.TryUnmount();

                return false;
            }



            return byPlayer.Entity.TryMount(beBed);
        }

        

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing beforeFacing = BlockFacing.FromCode(LastCodePart());
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return CodeWithParts(nowFacing.Code);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("head", "north")));
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.Opposite.Code);
            }
            return Code;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            double eff = inSlot.Itemstack.Collectible.Attributes["sleepEfficiency"].AsDouble();
            double sleephours = eff * world.Calendar.HoursPerDay / 2;

            dsc.AppendLine("\n" + Lang.Get("Lets you sleep for {0} hours a day", sleephours.ToString("#.#")));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            float sleepEfficiency = 0.5f;
            if (Attributes?["sleepEfficiency"] != null) sleepEfficiency = Attributes["sleepEfficiency"].AsFloat(0.5f);

            return base.GetPlacedBlockInfo(world, pos, forPlayer) + Lang.Get("Lets you sleep for up to {0} hours", System.Math.Round(sleepEfficiency * world.Calendar.HoursPerDay / 2, 2));
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-bed-sleep",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (isImpact && facing.Axis == EnumAxis.Y)
            {
                if (Sounds?.Break != null && System.Math.Abs(collideSpeed.Y) > 0.2)
                {
                    world.PlaySoundAt(Sounds.Break, entity);
                }
                entity.Pos.Motion.Y = GameMath.Clamp(-entity.Pos.Motion.Y * 0.8, -0.5, 0.5);
            }
        }
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            
            BlockEntity myBE = blockAccessor.GetBlockEntity(pos);
            if (myBE == null) return base.GetSelectionBoxes(blockAccessor,pos);
            BEBChiseledCover mycover = myBE.GetBehavior<BEBChiseledCover>();
            if (mycover != null)
            {
                Cuboidf[] selectionBoxes = mycover.GetSelectionBoxes(blockAccessor, pos);
                if (selectionBoxes==null) return base.GetSelectionBoxes(blockAccessor, pos);
                return selectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity myBE = blockAccessor.GetBlockEntity(pos);
            if (myBE == null) return base.GetCollisionBoxes(blockAccessor, pos);
            BEBChiseledCover mycover = myBE.GetBehavior<BEBChiseledCover>();
            if (mycover != null)
            {
                Cuboidf[] selectionBoxes = mycover.GetCollisionBoxes(blockAccessor, pos);
                if (selectionBoxes == null) return base.GetCollisionBoxes(blockAccessor, pos);
                return selectionBoxes;
            }

            return base.GetCollisionBoxes(blockAccessor, pos);
        }
    }
}
