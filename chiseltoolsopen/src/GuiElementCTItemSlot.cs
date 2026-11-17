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
using chisel.src;
using System.IO;
using System.Xml.Serialization;
using System.Runtime.Intrinsics.X86;

using static System.Runtime.InteropServices.JavaScript.JSType;
//using VoxReader.Interfaces;
//using VoxReader;

namespace chiseltools.src
{
    internal class GuiElementCTItemSlot : GuiElementPassiveItemSlot
    {
        float myScale;
        ItemSlot mySlot;
        bool myShouldRotate = false;
        public bool isVisible = true;
        public bool isEnabled = true;
        bool isOver;
        int slotno = -1;
        ElementBounds myBounds;
        ActionConsumable<int> onClick;
        bool currentlyMouseDownOnElement = false;
        public GuiElementCTItemSlot(ICoreClientAPI capi, ActionConsumable<int> onClick, int slotno,ElementBounds bounds,  IInventory inventory, ItemSlot slot, bool drawBackground = true,float scale=1,bool shouldrotate=false) : base(capi, bounds, inventory, slot, drawBackground)
        {
            myScale = scale;
            mySlot = slot;
            myShouldRotate = shouldrotate;
            myBounds = bounds;
            myBounds.fixedHeight = myScale * unscaledSlotSize;
            myBounds.fixedWidth = myScale * unscaledSlotSize;
            this.onClick = onClick;
            this.slotno= slotno; 
        }
        public override void RenderInteractiveElements(float deltaTime)
        {
           
            if (mySlot.Itemstack != null && myBounds.PartiallyInside(myBounds.ParentBounds))
            {
                
                double num = GuiElement.scaled(unscaledSlotSize*myScale) /2;
                api.Render.PushScissor(myBounds, stacking: true);
                try
                {
                    api.Render.RenderItemstackToGui(mySlot, myBounds.renderX + num, myBounds.renderY + num, 450.0, (float)GuiElement.scaled(unscaledItemSize * myScale), -1, false, isOver || myShouldRotate);
                }
                catch { }
                api.Render.PopScissor();
            }
        }

        protected void setIsOver()
        {
            isOver = isVisible && myBounds.PointInside(api.Input.MouseX, api.Input.MouseY);

        }
        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            bool num = isOver;
            setIsOver();
            
        }
        public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (isEnabled && currentlyMouseDownOnElement && Bounds.PointInside(args.X, args.Y) && (args.Button == EnumMouseButton.Left || args.Button == EnumMouseButton.Right))
            {
                args.Handled = onClick(slotno);
            }

            currentlyMouseDownOnElement = false;
        }
        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            if (isVisible)
            {

                //if (currentlyMouseDownOnElement) { OnMouseDownOnElement(api, args); }
                base.OnMouseUp(api, args);
                currentlyMouseDownOnElement = false;
            }
        }
        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (isVisible && isEnabled)
            {
                base.OnMouseDownOnElement(api, args);
                currentlyMouseDownOnElement = true;


                setIsOver();
            }
        }
    }
}
