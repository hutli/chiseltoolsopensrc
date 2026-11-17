using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace chiseltools.src
{
    public class GUIChiselProjector : GuiDialog
    {
        BEChiselProjector myprojector;

        public GUIChiselProjector(ICoreClientAPI capi, BEChiselProjector myprojector) : base(capi)
        {
            this.myprojector = myprojector;
            SetupDialog();
        }

        public override string ToggleKeyCombinationCode => "chiselprojector";

        void SetupDialog()
        {
            ClearComposers();
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            //This creates a fixed position rectangle with a size of 300x100 pixels, and 40 pixels from the top of the gui box.
            ElementBounds textBounds = ElementBounds.Fixed(0, 40, 1000, 700);

            ElementBounds itemBounds = ElementBounds.Fixed(10, 40, 300, 60);
            ElementBounds buttonPosition = ElementBounds.Fixed(10, 40, 300, 60);
            //buttonPosition.fixedHeight *= 0.5;

            //Background boundaries. Again, just make it fit it's child elements, then add the text as a child element
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(textBounds);
            textBounds.fixedHeight = 700;

            //Using the composer will actually create the dialog itself using the bounds supplied.
            SingleComposer = capi.Gui.CreateCompo("CenteredTextBox", dialogBounds)
                //add the background...
                .AddShadedDialogBG(bgBounds)
                //now the title bar... We also need to register an event for when the X is clicked on the UI menu.
                .AddDialogTitleBar("Projector Controls", OnTitleBarCloseClicked);
            //and finally add the text in the text box.
            //.AddStaticText("TRIGGER SETTINGS", CairoFont.SmallButtonText(), itemBounds);
            SingleComposer.AddButton("SCALE DOWN",OnScaleDown, buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddButton("SCALE UP", OnScaleUp, buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddButton("RESET SCALE", OnScaleReset, buttonPosition); buttonPosition = buttonPosition.RightCopy();
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddButton("MOVE UP", ()=>OnMove(new Vec3f(0,1,0)), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddButton("MOVE DOWN", () => OnMove(new Vec3f(0, -1, 0)), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddButton("MOVE NORTH", () => OnMove(new Vec3f(0, 0, -1)), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddButton("MOVE SOUTH", () => OnMove(new Vec3f(0, 0, 1)), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddButton("MOVE EAST", () => OnMove(new Vec3f(1, 0, 0)), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddButton("MOVE WEST", () => OnMove(new Vec3f(-1, 0, 0)), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddButton("RESET POSITION", () => OnMove(null), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddButton("ROTATE CLOCKWISE", () => OnRotate(new Vec3f(0, -1, 0)), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddButton("ROTATE CCW", () => OnRotate(new Vec3f(0, 1, 0)), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddButton("RESET ROTATION", () => OnRotate(null), buttonPosition); buttonPosition = buttonPosition.RightCopy();

            //red slider
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddStaticText("Red", CairoFont.SmallButtonText(), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddSlider(OnSetRed, buttonPosition, "rslider");
            GuiElementSlider rslider = SingleComposer.GetSlider("rslider");
            rslider.SetValues((int)(myprojector.RGBA.R * 100), 0, 100, 1, "%");

            //green slider
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddStaticText("Green", CairoFont.SmallButtonText(), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddSlider(OnSetGreen, buttonPosition, "gslider");
            GuiElementSlider gslider = SingleComposer.GetSlider("gslider");
            gslider.SetValues((int)(myprojector.RGBA.G * 100), 0, 100, 1, "%");

            //blue slider
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddStaticText("Blue", CairoFont.SmallButtonText(), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddSlider(OnSetBlue, buttonPosition, "bslider");
            GuiElementSlider bslider = SingleComposer.GetSlider("bslider");
            bslider.SetValues((int)(myprojector.RGBA.B * 100), 0, 100, 1, "%");

            //alpha slider
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddStaticText("Transparency",CairoFont.SmallButtonText(),buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddSlider(OnSetAlpha, buttonPosition,"aslider");
            GuiElementSlider aslider = SingleComposer.GetSlider("aslider");
            aslider.SetValues((int)(myprojector.RGBA.A*100), 0, 100, 1,"%");

            //glow slider
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddStaticText("Glow", CairoFont.SmallButtonText(), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddSlider(OnSetGlow, buttonPosition, "glowslider");
            GuiElementSlider glowslider = SingleComposer.GetSlider("glowslider");
            glowslider.SetValues((int)(myprojector.Glow), 1, 256, 1);
            
            //on off
            buttonPosition = itemBounds.BelowCopy();
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddButton("OFF", () => OnShouldRender(false), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.AddButton("ON", () => OnShouldRender(true), buttonPosition); buttonPosition = buttonPosition.RightCopy();
            SingleComposer.Compose();
        }

        void PlayButtonSound()
        {
            capi.World.PlaySoundAt(new AssetLocation("sounds/comperr"), myprojector.Pos.X, myprojector.Pos.Y, myprojector.Pos.Z);
        }

        private bool OnSetAlpha(int alpha)
        {
            myprojector.RGBA.A = (float)alpha / 100f;
            myprojector.UpdateProjectorData();
            return true;
        }
        private bool OnSetRed(int alpha)
        {
            myprojector.RGBA.R = (float)alpha / 100f;
            myprojector.UpdateProjectorData();
            return true;
        }
        private bool OnSetGreen(int alpha)
        {
            myprojector.RGBA.G = (float)alpha / 100f;
            myprojector.UpdateProjectorData();
            return true;
        }
        private bool OnSetBlue(int alpha)
        {
            myprojector.RGBA.B = (float)alpha / 100f;
            myprojector.UpdateProjectorData();
            return true;
        }

        private bool OnSetGlow(int glow)
        {
            myprojector.SetGlow(glow);
            return true;

        }

        private bool OnShouldRender(bool onoff)
        {
            PlayButtonSound();
            myprojector.SetBigRenderer(onoff);
            myprojector.UpdateProjectorData();
            return true;
        }

        private bool OnMove(Vec3f movevec)
        {
            
            if (movevec == null) { myprojector.ResetOffset(); PlayButtonSound(); return true; }
            myprojector.AddOffset(movevec);
            return true;
        }

        private bool OnRotate(Vec3f rotvec)
        {

            
            if (rotvec == null) { PlayButtonSound(); myprojector.ResetRotation(); return true; }
            
            myprojector.AddRotation(rotvec);
            return true;
        }

        private bool OnScaleDown()
        {
            myprojector.AddScale(-1);
            
            return true;
        }

        private bool OnScaleUp()
        {
            myprojector.AddScale(1);
            
            return true;
        }
        private bool OnScaleReset()
        {
            myprojector.ResetScale();
            PlayButtonSound();
            return true;
        }
        private void OnTitleBarCloseClicked()
        {
            
            TryClose();
            
        }
    }
}
