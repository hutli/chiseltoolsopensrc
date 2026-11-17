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
    public class BEDecoShelf: BlockEntityShelf
    {
        //private InventoryGeneric inv;

        private Block block;

        //private static int slotCount = 8;

        //public override InventoryBase Inventory => inv;

        //public override string InventoryClassName => "shelf";

        //public override string AttributeTransformCode => "onshelfTransform";

        public BEDecoShelf()
        {
            //inv = new InventoryGeneric(slotCount, "shelf-0", null);
            inv = new InventoryGeneric(slotCount, "shelf-0", null, (id, inv) => new ItemSlotDisplay(inv, GetSlotType(id)));
        }

        public override void Initialize(ICoreAPI api)
        {
            block = api.World.BlockAccessor.GetBlock(Pos);
            
            base.Initialize(api);
        }
       
        

        new public bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (TryUse(byPlayer, blockSel)) return true;
            else if (slot.Empty) return TryTake(byPlayer, blockSel);
            else if (GetShelvableLayout(slot.Itemstack) != null) return TryPut(byPlayer, blockSel);

            return false;
        }

        private bool TryUse(IPlayer player, BlockSelection blockSel)
        {
            bool up = blockSel.SelectionBoxIndex > 1;
            bool left = (blockSel.SelectionBoxIndex % 2) == 0;
            var shelvableLayout = GetShelvableLayout(inv[up ? 4 : 0].Itemstack);
            if (shelvableLayout is not EnumShelvableLayout.SingleCenter)
            {
                if (!left) shelvableLayout = GetShelvableLayout(inv[up ? 6 : 2].Itemstack);
            }

            int start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
            int end = start + (shelvableLayout is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter ? 1 : 2);

            if (player.Entity.Controls.ShiftKey) return false;

            for (int i = end - 1; i >= start; i--)
            {
                var collIci = inv[i].Itemstack?.Collectible.GetCollectibleInterface<IContainedInteractable>();
                if (collIci != null)
                {
                    if (collIci.OnContainedInteractStart(this, inv[i], player, blockSel))
                    {
                        MarkDirty();
                        return true;
                    }
                }
            }

            return false;
        }

        
        private bool TryPut(IPlayer byPlayer, BlockSelection blockSel)
        {
            var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            bool up = blockSel.SelectionBoxIndex > 1;
            bool left = (blockSel.SelectionBoxIndex % 2) == 0;

            int filledSlots = 0;
            var shelvableLayout = GetShelvableLayout(heldSlot.Itemstack);

            int start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
            int end = start + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 4 : 2);

            if (shelvableLayout is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter)
            {
                for (int i = start; i < end; i++)
                {
                    if (!inv[i].Empty)
                    {
                        var layout = GetShelvableLayout(inv[i].Itemstack);
                        filledSlots += layout is EnumShelvableLayout.SingleCenter ? 4 : layout is EnumShelvableLayout.Halves ? 2 : 1;
                    }
                }
            }

            if (filledSlots > 0 && filledSlots < (shelvableLayout is EnumShelvableLayout.SingleCenter ? 4 : 2))
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "needsmorespace", Lang.Get("shelfhelp-needsmorespace-error"));
                return false;
            }

            if (shelvableLayout is not EnumShelvableLayout.SingleCenter) shelvableLayout = GetShelvableLayout(inv[up ? 4 : 0].Itemstack);
            if (shelvableLayout is not EnumShelvableLayout.SingleCenter && !left) shelvableLayout = GetShelvableLayout(inv[up ? 6 : 2].Itemstack);

            start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
            end = start + (shelvableLayout is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter ? 1 : 2);

            for (int i = start; i < end; i++)
            {
                if (!inv[i].Empty) continue;

                int moved = heldSlot.TryPutInto(Api.World, inv[i]);
                MarkDirty();
                (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                if (moved > 0)
                {
                    Api.World.PlaySoundAt(inv[i].Itemstack?.Block?.Sounds?.Place ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
                    Api.World.Logger.Audit("{0} Put 1x{1} into Shelf at {2}.",
                        byPlayer.PlayerName,
                        inv[i].Itemstack?.Collectible.Code,
                        Pos
                    );
                    return true;
                }

                return false;
            }

            (Api as ICoreClientAPI)?.TriggerIngameError(this, "shelffull", Lang.Get("shelfhelp-shelffull-error"));
            return false;
        }


        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            bool num = blockSel.SelectionBoxIndex > 1;
            bool flag = blockSel.SelectionBoxIndex % 2 == 0;
            int num2 = (num ? 4 : 0) + ((!flag) ? 2 : 0);
            for (int num3 = num2 + 2 - 1; num3 >= num2; num3--)
            {
                if (!inv[num3].Empty)
                {
                    ItemStack itemStack = inv[num3].TakeOut(1);
                    if (byPlayer.InventoryManager.TryGiveItemstack(itemStack))
                    {
                        //AssetLocation assetLocation = itemStack.Block?.Sounds?.Place;
                       //Api.World.PlaySoundAt((assetLocation != null) ? assetLocation : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, randomizePitch: true, 16f);
                    }

                    if (itemStack.StackSize > 0)
                    {
                        Api.World.SpawnItemEntity(itemStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }

                    (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    MarkDirty();
                    return true;
                }
            }

            return false;
        }

        /*protected override float[][] genTransformationMatrices()
        {
            float[][] array = new float[slotCount][];
            for (int i = 0; i < slotCount; i++)
            {
                float num = ((i % 4 >= 2) ? 0.75f : 0.25f);
                float y = ((i >= 4) ? 0.625f : 0.125f);
                float num2 = ((i % 2 == 0) ? 0.25f : 0.625f);
                array[i] = new Matrixf().Translate(0.5f, 0f, 0.5f).RotateYDeg(block.Shape.rotateY).Translate(num - 0.5f, y, num2 - 0.5f)
                    .Translate(-0.5f, 0f, -0.5f)
                    .Values;
            }

            return array;
        }*/

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            RedrawAfterReceivingTreeAttributes(worldForResolving);
        }

        /*
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);


            float ripenRate = GameMath.Clamp(((1 - container.GetPerishRate()) - 0.5f) * 3, 0, 1);
            if (ripenRate > 0)
            {
                sb.Append(Lang.Get("Suitable spot for food ripening."));
            }

            sb.AppendLine();

            bool up = forPlayer.CurrentBlockSelection != null && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;

            for (int j = 3; j >= 0; j--)
            {
                int i = j + (up ? 4 : 0);
                i ^= 2;   //Display shelf contents text for items from left-to-right, not right-to-left

                if (inv[i].Empty) continue;

                ItemStack stack = inv[i].Itemstack;

                if (stack.Collectible is BlockCrock)
                {
                    sb.Append(CrockInfoCompact(inv[i]));
                }
                else
                {
                    if (stack.Collectible.TransitionableProps != null && stack.Collectible.TransitionableProps.Length > 0)
                    {
                        sb.Append(PerishableInfoCompact(Api, inv[i], ripenRate));
                    }
                    else
                    {
                        sb.AppendLine(stack.GetName());
                    }
                }
            }
        }*/

        /*public static string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            if (contentSlot.Empty) return "";

            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

            bool nowSpoiling = false;

            if (transitionStates != null)
            {
                bool appendLine = false;
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                nowSpoiling = true;
                                dsc.Append(", " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Ripen:
                            if (nowSpoiling) break;

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(", " + Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }

                if (appendLine) dsc.AppendLine();
            }

            return dsc.ToString();
        }*/
        /*
        public string CrockInfoCompact(ItemSlot inSlot)
        {
            BlockMeal mealblock = Api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            BlockCrock crock = inSlot.Itemstack.Collectible as BlockCrock;
            IWorldAccessor world = Api.World;

            CookingRecipe recipe = crock.GetCookingRecipe(world, inSlot.Itemstack);
            ItemStack[] stacks = crock.GetNonEmptyContents(world, inSlot.Itemstack);

            if (stacks == null || stacks.Length == 0)
            {
                return Lang.Get("Empty Crock") + "\n";
            }

            StringBuilder dsc = new StringBuilder();

            if (recipe != null)
            {
                double servings = inSlot.Itemstack.Attributes.GetDecimal("quantityServings");

                if (recipe != null)
                {
                    if (servings == 1)
                    {
                        dsc.Append(Lang.Get("{0:0.#}x {1}.", servings, recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.Append(Lang.Get("{0:0.#}x {1}.", servings, recipe.GetOutputName(world, stacks)));
                    }
                }
            }
            else
            {
                int i = 0;
                foreach (var stack in stacks)
                {
                    if (stack == null) continue;
                    if (i++ > 0) dsc.Append(", ");
                    dsc.Append(stack.StackSize + "x " + stack.GetName());
                }

                dsc.Append(".");
            }

            DummyInventory dummyInv = new DummyInventory(Api);

            ItemSlot contentSlot = BlockCrock.GetDummySlotForFirstPerishableStack(Api.World, stacks, null, dummyInv);
            dummyInv.OnAcquireTransitionSpeed += (transType, stack, mul) =>
            {
                return mul * crock.GetContainingTransitionModifierContained(world, inSlot, transType) * inv.GetTransitionSpeedMul(transType, stack);
            };

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);
            bool addNewLine = true;
            if (transitionStates != null)
            {
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(world, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    addNewLine = false;
                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:
                            if (transitionLevel > 0)
                            {
                                dsc.AppendLine(" " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.AppendLine(" " + Lang.Get("Fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                /*else if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerMonth)  - confusing. 12 days per months and stuff..
                                {
                                    dsc.AppendLine(Lang.Get("<font color=\"orange\">Perishable.</font> Fresh for {0} months", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerMonth, 1)));
                                }*//*
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.AppendLine(" " + Lang.Get("Fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.AppendLine(" " + Lang.Get("Fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }
            }

            if (addNewLine)
            {
                dsc.AppendLine("");
            }

            return dsc.ToString();
        }*/

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            for (int index = 0; index < DisplayedItems; index++)
            {
                ItemSlot slot = Inventory[index];
                if (slot.Empty || tfMatrices == null)
                {
                    continue;
                }
                mesher.AddMeshData(getMesh(slot), tfMatrices[index]);
            }
            
            return base.OnTesselation(mesher, tessThreadTesselator);
        }
    }
}
