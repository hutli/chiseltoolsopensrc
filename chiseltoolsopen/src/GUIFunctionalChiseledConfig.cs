using chisel.src;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static HarmonyLib.Code;

namespace chiseltools.src
{
    public class GUIFunctionalChiseledConfig : GuiDialog
    {
        BEFunctionChiseled befc;
        bool updatetriggers = false;
        bool focusontimebox = false;
        bool focusonfilterbox = false;
        string soundfilter = "";
        string originalsound = "";
        int soundpagecounter = 0;
        static List<AssetLocation> soundlist;
        List<AssetLocation> subsoundlocations;
        public GUIFunctionalChiseledConfig(ICoreClientAPI capi,BEFunctionChiseled befc) : base(capi)
        {
            if (befc == null) { return; }
            this.befc = befc;
            originalsound=befc.UseSound;
            SetupDialog();
        }

        public override string ToggleKeyCombinationCode => "configurefunctionalchiseled";

        void SetupDialog()
        {
            ClearComposers();
            if (soundlist == null)
            {
                soundlist = capi.Assets.AllAssets.Keys.ToList();
                soundlist = soundlist.Where(x => x.ToString().Contains(".ogg") && !x.ToString().Contains(":music")).ToList();
            }
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            //This creates a fixed position rectangle with a size of 300x100 pixels, and 40 pixels from the top of the gui box.
            ElementBounds textBounds = ElementBounds.Fixed(0, 40, 1000, 700);
            
            ElementBounds itemBounds = ElementBounds.Fixed(10, 40, 200, 50);
            ElementBounds soundTextBounds = ElementBounds.Fixed(10, 40, 700, 50);
            ElementBounds rightColumn = itemBounds.RightCopy();
            rightColumn.fixedHeight *= 0.5;
            
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
                .AddDialogTitleBar("Configure Door", OnTitleBarCloseClicked)
                //and finally add the text in the text box.
                .AddStaticText("TRIGGER SETTINGS", CairoFont.SmallButtonText(), itemBounds);
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddToggleButton("Triggered by Right Click", CairoFont.SmallButtonText(), OnClicktriggerByInteraction, itemBounds, "triggerByInteraction");
            GuiElementToggleButton tb = SingleComposer.GetToggleButton("triggerByInteraction");tb.SetValue(befc.TriggerSettings.triggerByInteraction);

            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddToggleButton("Triggered by any Entity", CairoFont.SmallButtonText(), OnClicktriggerByAnyEntity, itemBounds, "triggerByAnyEntity");
            tb = SingleComposer.GetToggleButton("triggerByAnyEntity"); tb.SetValue(befc.TriggerSettings.triggerByAnyEntity);

            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddToggleButton("Triggered by any Player", CairoFont.SmallButtonText(), OnClicktriggerByPlayerEntity, itemBounds, "triggerByPlayerEntity");
            tb = SingleComposer.GetToggleButton("triggerByPlayerEntity"); tb.SetValue(befc.TriggerSettings.triggerByPlayerEntity);

            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddToggleButton("Triggered by allowed Players", CairoFont.SmallButtonText(), OnClicktriggerByAllowedPlayer, itemBounds, "triggerByAllowedPlayer");
            tb = SingleComposer.GetToggleButton("triggerByAllowedPlayer"); tb.SetValue(befc.TriggerSettings.triggerByAllowedPlayer);

            itemBounds = itemBounds.BelowCopy().BelowCopy();
            SingleComposer.AddStaticText("Minimum repeat time (1/100 sec)", CairoFont.SmallButtonText(), itemBounds);
            itemBounds = itemBounds.BelowCopy();
            SingleComposer.AddTextInput(itemBounds, OnChangeminTimeBetweenTrigger, CairoFont.SmallButtonText(), "minTimeBetweenTrigger");
            GuiElementTextInput ti = SingleComposer.GetTextInput("minTimeBetweenTrigger");
            ti?.SetValue(befc.TriggerSettings.minTimeBetweenTrigger);


            //Sound Page Buttons
            if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative || capi.World.Player.HasPrivilege("adddoorsound"))
            {
                itemBounds = itemBounds.BelowCopy();
                SingleComposer.AddButton("Prev Page", OnClickSoundPrevPageButton, itemBounds);
                itemBounds = itemBounds.BelowCopy();
                SingleComposer.AddButton("Next Page", OnClickSoundNextPageButton, itemBounds);
            }
            GuiElementTextInput sfti=null;
            if (befc.ControlBlockPos == befc.Pos && befc.ControlledBlocks?.Count>0)
            {
                SingleComposer.AddStaticText("CONTROLS BLOCKS", CairoFont.SmallButtonText(), rightColumn);
                
                rightColumn = rightColumn.BelowCopy();
                double startx = rightColumn.fixedX;
                int c = 0;
                foreach (var bp in befc.ControlledBlocks)
                {
                    if (rightColumn.absY + rightColumn.fixedHeight >= textBounds.absY + textBounds.fixedHeight) { continue; }
                    if (bp == null) continue;
                    SingleComposer.AddStaticText($"{bp.ToLocalPosition(capi).X},{bp.ToLocalPosition(capi).Y},{bp.ToLocalPosition(capi).Z}", CairoFont.SmallButtonText(), rightColumn);
                    
                    if (c % 2 == 0) { rightColumn = rightColumn.FlatCopy(); rightColumn.fixedX += 100;  }
                    else { rightColumn = rightColumn.FlatCopy(); rightColumn.fixedX -= 100; rightColumn.fixedY+=25;  }
                    c++;
                }
                
            }
            else if (befc.ControlBlockPos != befc.Pos)
            {
                itemBounds = itemBounds.BelowCopy().BelowCopy();
                Vec3i p = befc.ControlBlockPos.ToLocalPosition(capi);
                SingleComposer.AddStaticText($"Controlled by {p.X},{p.Y},{p.Z}", CairoFont.SmallButtonText(), itemBounds);
            }
            
            //sound stuff (if has permissions, or in creative)
            if (befc.ControlBlockPos==befc.Pos&&( capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative  || capi.World.Player.HasPrivilege("adddoorsound")))
            {
                double startx = rightColumn.fixedX;
                rightColumn = rightColumn.BelowCopy();
                rightColumn.fixedX = startx;
                SingleComposer.AddStaticText("SOUNDS", CairoFont.SmallButtonText(), rightColumn);
                ElementBounds defaultsoundbutton = rightColumn.RightCopy();
                defaultsoundbutton.fixedWidth *= 2;
                GUIElementClickableText gect = new GUIElementClickableText(capi, originalsound == "" ? "Click to Remove Sound" : "Click to reset sound to " + originalsound, EnumTextOrientation.Left, defaultsoundbutton, CairoFont.WhiteSmallText(), 0, OnResetSound);
                //SingleComposer.AddDynamicText(subsoundlocations[cc].GetName(), CairoFont.WhiteSmallText(), rightColumn, "sound" + cc);
                SingleComposer.AddInteractiveElement(gect);
                if (originalsound != "")
                {
                    defaultsoundbutton = defaultsoundbutton.RightCopy();
                    gect = new GUIElementClickableText(capi, "Click to Remove Sound", EnumTextOrientation.Left, defaultsoundbutton, CairoFont.WhiteSmallText(), -1, OnResetSound);
                    SingleComposer.AddInteractiveElement(gect);
                }
                //SingleComposer.AddButton("Reset Sound to " + originalsound == "" ? "None" : originalsound, OnResetSound, defaultsoundbutton);
                rightColumn = rightColumn.BelowCopy();
                SingleComposer.AddTextInput(rightColumn, OnEditSoundFilter, CairoFont.SmallButtonText(), "soundfilter");
                sfti = SingleComposer.GetTextInput("soundfilter");
                sfti.SetValue(soundfilter);
                
                if (soundfilter == "") { subsoundlocations = soundlist.OrderBy(x => x.GetName()).ToList(); }
                else { subsoundlocations = soundlist.Where(x => x.ToString().Contains(soundfilter)).OrderBy(x => x.GetName()).ToList(); }
                if (subsoundlocations.Count > 0)
                {

                    
                    int maxrows = 40;
                    int maxcolumns = 1;
                    int rowctr = 0;
                    int colctr = 0;
                    if (subsoundlocations.Count <= soundpagecounter * maxrows * maxcolumns) { soundpagecounter = 0; }
                    int startat = Math.Min(soundpagecounter*maxrows*maxcolumns,subsoundlocations.Count);
                    int endat = Math.Min((soundpagecounter+1) * maxrows * maxcolumns, subsoundlocations.Count);
                    ElementBounds soundbounds = rightColumn.BelowCopy();
                    soundbounds.fixedWidth *= 2;
                    for (int cc = startat; cc < endat; cc++)
                    {
                        if (rowctr == maxrows) { break; }

                        GUIElementClickableText defsoundbutton = new GUIElementClickableText(capi, subsoundlocations[cc].CodePartsAfterSecond().ToString().Replace("sounds/",""), EnumTextOrientation.Left, soundbounds, CairoFont.WhiteSmallText(), cc, OnClickSoundButton);
                        //SingleComposer.AddDynamicText(subsoundlocations[cc].GetName(), CairoFont.WhiteSmallText(), rightColumn, "sound" + cc);
                        SingleComposer.AddInteractiveElement(defsoundbutton);
                        if (colctr < maxcolumns)
                        {
                            soundbounds = soundbounds.FlatCopy();
                            soundbounds.fixedX += soundbounds.fixedWidth;
                            colctr++;
                        }
                        else
                        {
                            colctr = 0;
                            soundbounds = soundbounds.FlatCopy();
                            soundbounds.fixedX = rightColumn.fixedX;
                            soundbounds.fixedY += soundbounds.fixedHeight;
                            rowctr++;
                        }
                    }
                }

            }
            SingleComposer.Compose();
            if (focusonfilterbox && sfti != null) { SingleComposer.FocusElement(sfti.TabIndex); }
        }

        private bool OnResetSound(int index)
        {
            if (befc == null) { return true; }
            if (index == -1) { originalsound = ""; }
            befc.SetSound(originalsound);
            SetupDialog();
            return true;
        }

        private bool OnClickSoundButton(int index)
        {
            capi.World.PlaySoundFor(subsoundlocations[index], capi.World.Player);
            befc.SetSound(subsoundlocations[index].ToString());
            return true;
        }

        private bool OnClickSoundPrevPageButton()
        {
            soundpagecounter --;
            if (soundpagecounter < 0) { soundpagecounter = 0; }
            SetupDialog();
            return true;
        }
        private bool OnClickSoundNextPageButton()
        {
            soundpagecounter++;
            SetupDialog();
            return true;
        }

        private void OnEditSoundFilter(string obj)
        {
            
            GuiElementTextInput sfti = SingleComposer.GetTextInput("soundfilter");
            if (soundfilter == sfti.GetText()) { return; }
            focusonfilterbox = true;
            soundfilter = sfti.GetText();
            
            soundpagecounter = 0;
            
            SetupDialog();
            
        }

        private void OnChangeminTimeBetweenTrigger(string obj)
        {
            
            GuiElementTextInput ti = SingleComposer.GetTextInput("minTimeBetweenTrigger");
            int output=befc.TriggerSettings.minTimeBetweenTrigger;
            if (int.TryParse(ti.GetText(), out output)) { updatetriggers = true; }
            befc.TriggerSettings.minTimeBetweenTrigger =output;
            
        }

        private void OnClicktriggerByAllowedPlayer(bool obj)
        {
            GuiElementToggleButton tb = SingleComposer.GetToggleButton("triggerByAllowedPlayer");
            befc.TriggerSettings.triggerByAllowedPlayer = tb.On;
            updatetriggers = true;
        }

        private void OnClicktriggerByPlayerEntity(bool obj)
        {
            GuiElementToggleButton tb = SingleComposer.GetToggleButton("triggerByPlayerEntity");
            befc.TriggerSettings.triggerByPlayerEntity = tb.On;
            updatetriggers = true;
        }

        private void OnClicktriggerByAnyEntity(bool obj)
        {
            GuiElementToggleButton tb = SingleComposer.GetToggleButton("triggerByAnyEntity");
            befc.TriggerSettings.triggerByAnyEntity = tb.On;
            updatetriggers = true;
        }

        private void OnClicktriggerByInteraction(bool obj)
        {
            GuiElementToggleButton tb = SingleComposer.GetToggleButton("triggerByInteraction");
            befc.TriggerSettings.triggerByInteraction = tb.On;
            updatetriggers = true;
        }

        private void OnTitleBarCloseClicked()
        {
            
            TryClose();
        }

        public override bool TryClose()
        {
            if (updatetriggers)
            {
                befc?.UpdateTriggers(befc.TriggerSettings);
            }
            return base.TryClose();
        }
    }
}
