using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace chiseltools
{
    public class GUIElementClickableText : GuiElementStaticText
    {
        int index=-1;
        public bool isVisible = true;
        public bool isEnabled = true;
        bool currentlyMouseDownOnElement = false;
        ElementBounds myBounds;
        bool isOver;
        
        ActionConsumable<int> onClick;
        private int cc;
        private System.Func<int, bool> onClickSoundButton;

        public GUIElementClickableText(ICoreClientAPI capi, string text, EnumTextOrientation orientation, ElementBounds bounds, CairoFont font, int index,ActionConsumable<int> onClick) : base(capi, text, orientation, bounds, font)
        {
            this.index= index;
            this.onClick= onClick;
            myBounds = bounds;
        }

        

        protected void setIsOver()
        {
            isOver = isVisible && myBounds.PointInside(api.Input.MouseX, api.Input.MouseY);
            if (isOver == true)
            {
                int ok = 1;
            }
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
                args.Handled = onClick(index);
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
