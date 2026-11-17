using chisel.src;
using chiseltools.src;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
//using VoxReader;
//using VoxReader.Interfaces;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace chisel.src
{
    internal class GUIShapeBrowser:GuiDialogGeneric
    {
        ICoreClientAPI clientAPI;
        InventoryBase virtualinventory;
        InventoryBase Inventory=>virtualinventory;
        DummyInventory blockInventory; //inventory of materials
        BlockPos pos;
        BlockEntityMicroBlock targetblock;
        string filter = "";
        List<PantographData> shapes;
        InventoryBase pantographInventory;
        PantographData currentPantographData;
        List<string> loadedfilenames;
        int lastClickedMaterial = -1; //for material swapping
        public GUIShapeBrowser(string filter,BlockPos pos,string dialogtitle,ICoreClientAPI capi) : base(dialogtitle,capi)
        {
            this.pos= pos;
            if (filter!= null && filter != "") { filter = filter.Replace("\\", "");filter = filter.Replace("/",""); this.filter = filter; }
            clientAPI = capi;
            if (capi == null ) { return; }
            targetblock = null;
            if (pos != null) { targetblock = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityChisel; }
            int lastClickedMaterial = -1;

            //if (targetblock == null ) { capi.ShowChatMessage(ChiselToolLoader.ErrorWrap(Lang.Get("chiseltools:lookatchiseled"))); ; return; }

            SetupInventory(filter);
            //capi.World.Player.InventoryManager.OpenInventory((IInventory)inventory);
            SetupDialog();
        }

        //build a standard list of blocks for colouring

        public List<int>GetBlockIndex(ICoreAPI uapi)
        {
            /*string[] blocktextlist = {"game:creativeblock-79",
"game:creativeblock-64",
"game:creativeblock-32",
"game:creativeblock-35",
"game:creativeblock-37",
"game:creativeblock-19",
"game:creativeblock-9",
"game:creativeblock-58",
"game:creativeblock-73",
"game:creativeblock-70",
"chiseltools:pastel-ff0000",
"chiseltools:pastel-ff8800",
"chiseltools:pastel-ffff00",
"chiseltools:pastel-0088ff",
"chiseltools:pastel-00ff00",
"chiseltools:pastel-8800ff",
"chiseltools:deeps-880000",
"chiseltools:deeps-888800",
"chiseltools:deeps-008800",
"chiseltools:deeps-000088" };*/
            string[] blocktextlist = {"game:creativeblock-79" };
            //List<int> allcreative = ChiselToolLoader.GetCreativeBlockIDS(capi);
            //allcreative = allcreative.Where((x, i) => i % 3 == 0).ToList<int>();
            List<int> allcreative = new List<int>();
            foreach (string s in blocktextlist)
            {
                Block b=capi.World.GetBlock(new AssetLocation(s));
                if (b != null) { allcreative.Add(b.BlockId); }
                
            }
            return allcreative;
        }

        /// <summary>
        /// Steps-
        /// - load in shapes
        ///   - get filtered list of files (if no files then escape, send message to player)
        ///   - load each file in add to list
        /// - create inventory same size as shapes
        /// - cycle thru shapes create microblock matching source block, then update shape (might be an issue?)
        ///     - yeah need to figure out how to create microblock without the BlockEntity
        /// - then goes to SetupDialog similar to storage controller but if you click it should run the pantograph command
        /// </summary>

        public void SetupInventory(string matchcode)
        {

            ItemSlot currenthotbar = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            pantographInventory = null;
            currentPantographData = null;
            List<int> copiedmaterials = null;
            Block chiselblock = capi.World.GetBlock(new AssetLocation("game:chiseledblock"));
            if (currenthotbar != null && !(currenthotbar.Empty) && currenthotbar.Itemstack.Item != null && currenthotbar.Itemstack.Collectible.Code.ToString().Contains("pantograph"))
            {
                pantographInventory = new DummyInventory(capi, 1);
                currentPantographData = PantographData.TryGrabFromSlot(capi, currenthotbar);
                if (currentPantographData == null)
                {
                    capi.World.PlaySound(new SoundAttributes(new AssetLocation("sounds/comperr"), true));
                    capi.World.Player.ShowChatNotification(Lang.Get("chiseltools:portfoliorequirespantograph"));
                    return;
                }
                if (currenthotbar != null && currentPantographData != null)
                {


                    ItemStack[] stacks = { new ItemStack(chiselblock, 1) };// targetblock.Block.GetDrops(capi.World, pos, null);
                    if (stacks == null || stacks.Count() == 0) { capi.ShowChatMessage(ChiselToolLoader.ErrorWrap(Lang.Get("No shapes found - GUIShapeBrowser (setup inventory)"))); return; }
                    copiedmaterials = SerializerUtil.Deserialize<List<int>>(currentPantographData.matdata);
                    
                    //if (copiedmaterials.Count >= maxmats) { currentPantographData = PantographData.ReduceMaterialCount(currentPantographData, maxmats); }

                    stacks[0].Attributes["cuboids"] = new IntArrayAttribute(SerializerUtil.Deserialize<List<uint>>(currentPantographData.voxeldata).ToArray());
                    stacks[0].Attributes["materials"] = new IntArrayAttribute(copiedmaterials.ToArray());
                    stacks[0].Attributes["name"] = new StringAttribute( currentPantographData.name);
                    //stacks[0].Attributes.SetBytes("cuboids",shapes[c].voxeldata);
                    //stacks[0].Attributes.SetBytes("materials", shapes[c].matdata);
                    stacks[0].ResolveBlockOrItem(capi.World); //we want the "cuboids" attribute as uint array and "materials"

                    pantographInventory[0].Itemstack = stacks[0];
                }
            }
            if (copiedmaterials == null) { TryClose(); return; }
            shapes = new List<PantographData>();
            //get the default materials
            List<int> defaultblocks = GetBlockIndex(capi);
            List<int> mergedblocks = new List<int>();
            
            foreach (int b in copiedmaterials)
            {
                mergedblocks.Add(b);
            }

            if (mergedblocks.Count == 0)
            {
                foreach (int b in defaultblocks)
                {
                    mergedblocks.Add(b);
                }
            }

            int[] mats = mergedblocks.ToArray();
            if (copiedmaterials != null && copiedmaterials.Count > 0)
            {
                for (int mc = 0; mc < copiedmaterials.Count; mc++)
                {
                    mats[mc] = copiedmaterials[mc];
                }
            }
            blockInventory = new DummyInventory(capi,mats.Length);
            if (blockInventory == null) { return; }
            for (int mc=0;mc<mats.Length; mc++)
            {
                blockInventory[mc].Itemstack = new ItemStack(capi.World.GetBlock(mats[mc]), 1);
            }
            IntArrayAttribute matat = new IntArrayAttribute(mats);
            int maxmats = mats.Length;// targetblock.BlockIds.Count(); //if there are too many materials in the loaded shapes we have work to do!
            //to do add the virtual item for the pantograph - have to check for a pantograph with valid data
            
            //load list of files
            var dirpath = capi.GetOrCreateDataPath(ChiselToolLoader.voxelsavek);
            matchcode=matchcode.Replace("\\","");
            string[] rawfiles = System.IO.Directory.GetFiles(dirpath, matchcode);
            List<string> files = new List<string>(rawfiles);
            files.Sort();
            if (files == null || files.Count() == 0) { return; } //TODO ADD ERROR MESSAGE
            loadedfilenames = new List<string>();
            //cycle thru files
            foreach (string filename in files)
            {

                var path = filename;  //APIExtensions.GetDataPath(api, filename);
                PantographData data = null;
                TextReader reader = null;
                if (filename.Contains(".vox"))
                {
                    PantographData voxdata = new PantographData();
                    //bool ok = PantographData.LoadVoxFile(path, out voxdata);
                    bool ok = false;
                    if (ok)
                    {
                        voxdata.name = Path.GetFileName(path);
                        shapes.Add(voxdata);

                        loadedfilenames.Add(path);
                        continue;
                    }
                }
                try
                {
                    var serializer = new XmlSerializer(typeof(PantographData));
                    try
                    {
                        reader = new StreamReader(path);
                        data = (PantographData)serializer.Deserialize(reader);
                    }
                    catch
                    {
                        
                    }

                }
                finally
                {
                    if (reader != null)
                        reader.Close();
                }
                if (data == null) { continue; } //this item couldnt' be loaded
                shapes.Add(data);
                loadedfilenames.Add(path);
            }
            //now we need to create an inventory with these shapes some how
            if (shapes.Count == 0) { capi.ShowChatMessage(ChiselToolLoader.ErrorWrap(Lang.Get("chiseltools:nofiles"))); return; } //TODO: this should trigger an error message
            virtualinventory = new DummyInventory(capi, shapes.Count);
            

            for (int c = 0; c < shapes.Count; c++)
            {
                
                
                ItemStack[] stacks = { new ItemStack(chiselblock, 1) };// targetblock.Block.GetDrops(capi.World, pos, null);
                if (stacks == null || stacks.Count() == 0) { capi.ShowChatMessage(ChiselToolLoader.ErrorWrap(Lang.Get("No shapes found - GUIShapeBrowser (setup inventory)"))); return; }
                if (stacks == null || stacks.Count() == 0 || stacks[0]==null) { return; }
                
                stacks[0].Attributes["cuboids"] = new IntArrayAttribute(SerializerUtil.Deserialize<List<uint>>(shapes[c].voxeldata).ToArray());

                //select saved materials or pantograph materials
                bool usedSavedMaterials = true;
                copiedmaterials = SerializerUtil.Deserialize<List<int>>(shapes[c].matdata);
                if (copiedmaterials == null || copiedmaterials.Count() == 0 || shapes[c].blockcodes == null || shapes[c].blockcodes.Length==0) { usedSavedMaterials = false; }
                if (!usedSavedMaterials)
                {
                    if (copiedmaterials.Count >= maxmats) { shapes[c] = PantographData.ReduceMaterialCount(shapes[c], maxmats); }
                    stacks[0].Attributes["materials"] = matat;
                }
                else
                {
                    for (int bmc = 0; bmc < shapes[c].blockcodes.Length; bmc++)
                    {
                        AssetLocation al = new AssetLocation(shapes[c].blockcodes[bmc]);
                        Block thisblock = capi.World.GetBlock(al);
                        if (thisblock!=null&&thisblock.BlockId != 0)
                        {
                            copiedmaterials[bmc] = thisblock.BlockId;
                        }
                    }
                    stacks[0].Attributes["materials"] = new IntArrayAttribute(copiedmaterials.ToArray());
                }
                 
                stacks[0].ResolveBlockOrItem(capi.World); //we want the "cuboids" attribute as uint array and "materials"

                Inventory[c].Itemstack = stacks[0];
                //in theory this will just make a big list of the same item, need to figure out how to manipulate shape data
            }
            
        }
        
        string mainDialogRef = "maindialog";
        bool ignorenextfilter = false;
        protected virtual void SetupDialog()
        {
            ClearComposers();

  
            int itemsincolumn = 15;
            int columns = 1;
            if (virtualinventory != null)
            {
                columns = (this.Inventory.Count - 2) / itemsincolumn;
            }
            double inventoryWindowWidth = 1200;
            double inventoryWindowHeight = 850;

            ElementBounds dialogBounds =
                ElementBounds.Fixed(EnumDialogArea.LeftTop, 30, 0, inventoryWindowWidth, inventoryWindowHeight)
                
            ;

            Composers[mainDialogRef] =
                capi.Gui.CreateCompo("inventory-backpack", dialogBounds).AddShadedDialogBG(ElementBounds.Fill).AddDialogTitleBar(Lang.Get("Portfolio"), CloseIconPressed)
            ;

            GuiComposer main = Composers[mainDialogRef];
            main.AddStaticText(Lang.Get("chiseltools:yourpantograph"),CairoFont.WhiteDetailText(),ElementBounds.Fixed(30,47,190,10));
            main.AddStaticText(Lang.Get("chiseltools:searchshapes"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(195, 47,150,10));
            
            //pantograph preview area and save button
            if (currentPantographData != null && pantographInventory != null && !(pantographInventory.Empty))
            {
                main.AddTextInput(ElementBounds.Fixed(24,225,150,24), onSaveTextChange, CairoFont.WhiteSmallText(), "savefilename");
                main.GetTextInput("savefilename").SetValue(currentPantographData.name);
                main.AddAutoSizeHoverText(Lang.Get("chiseltools:savefilename"), CairoFont.WhiteSmallText(), 300, ElementBounds.Fixed(24, 225, 150, 24));
                main.AddButton(Lang.Get("chiseltools:saveshape"), OnClickSave, ElementBounds.Fixed(24,250,150,25), CairoFont.WhiteDetailText(), EnumButtonStyle.Small, "savefilebutton");
                main.AddAutoSizeHoverText(Lang.Get("chiseltools:saveshapemouseover"), CairoFont.WhiteSmallText(), 300, ElementBounds.Fixed(50, 250, 85, 25));
                main.AddContainer(ElementBounds.Fixed(24,67,150,150), "previewshape").AddInset(ElementBounds.Fixed(24, 67, 150, 150));
                var container = main.GetContainer("previewshape");
                GuiElementCTItemSlot previewslot = new GuiElementCTItemSlot(capi, GetClick,-1, ElementBounds.Fixed(0, 0, 200, 200), pantographInventory, pantographInventory[0], false, 3, true);
                container.Add(previewslot);
               
                
            }
            main.AddTextInput(ElementBounds.Fixed(315, 39, 216, 24), onTextChanged, CairoFont.WhiteSmallText(), "filterbox");
            GuiElementTextInput filterbox = main.GetTextInput("filterbox");
            ignorenextfilter = true;
            filterbox.SetValue(filter);
            
            //shape preview area
            if (virtualinventory != null && virtualinventory.Count > 0 && shapes!=null&&shapes.Count>0)
            {
                int browserColumns = 5;
                double itemSlotScale = 2.9;
                double itemSlotSize = 50*itemSlotScale;
                double elementToDialogPadding = GuiStyle.ElementToDialogPadding;
                double unscaledSlotPadding = GuiElementItemSlotGridBase.unscaledSlotPadding;
                int browserFullHeight = virtualinventory.Count / browserColumns *(int) itemSlotSize;
                double height = 765;
                //This is the area on the screen we want the browser area to be
                ElementBounds browserAreaSize = ElementBounds.Fixed(195, 67, 985, height).WithParent(dialogBounds);//full area for the shape browser
                
                //FULL BOUNDS, including hte full height if the area wasn't clipped
                ElementBounds browserContainingBounds = ElementBounds.Fixed(browserAreaSize.fixedX, browserAreaSize.fixedY, browserAreaSize.fixedWidth, browserFullHeight);//full grid area if not clipped
                
                
                //This is the part that will get scrolled (I think)
                ElementBounds browserScrollContainerBounds = browserContainingBounds.ForkBoundingParent(0, 0, 0, 0);

                //This should be the same as browserAreaSize (copied because I'm copying some other code)
                ElementBounds browserClippingAreaBounds = browserContainingBounds.CopyOffsetedSibling();
                browserClippingAreaBounds.fixedHeight = height;

                //This is the bounds for the actual slots
                ElementBounds browserItemSlotBounds = ElementBounds.Fixed(browserContainingBounds.fixedX, browserContainingBounds.fixedY, itemSlotSize, itemSlotSize);
                ElementBounds browserItemNextRowBounds = browserItemSlotBounds.BelowCopy(); //for grid tracking

                ElementBounds browserScrollBarBounds = ElementStdBounds.VerticalScrollbar(browserScrollContainerBounds).WithParent(dialogBounds);
                browserScrollBarBounds.fixedOffsetX -= 2.0;
                browserScrollBarBounds.fixedWidth = 15;
                browserScrollBarBounds.fixedHeight = height;
                ElementBounds insetbounds = browserScrollContainerBounds;
                insetbounds.fixedHeight = height;
                main.AddInset(insetbounds); //the darkened box
                
                main.BeginClip(browserClippingAreaBounds); //start the clipping area so the list doesn't go off the screen)
                
                main.AddContainer(browserContainingBounds.ForkContainingChild(0, 0, 0, -3), "browsergrid"); //a container that will hold all the itemslots
                var slotcontainer = main.GetContainer("browsergrid");
                int colctr = 0;
                //now go thru the actual inventory and make the slots
                for (int c = 0; c < Math.Min(virtualinventory.Count,shapes.Count); c++)
                {
                    if (virtualinventory[c] == null || virtualinventory[c].Empty) { continue; }//skip bad entries
                    
                    GuiElementCTItemSlot slot = new GuiElementCTItemSlot(capi, GetClick,c ,browserItemSlotBounds, virtualinventory, virtualinventory[c], false,(float)itemSlotScale,false);
                    slotcontainer.Add(slot);
                    //TODO NEED TO ADD CLICK HANDLING TO SLOTS
                    //TODO ROTATE - SHOULD ADD ROTATION WHEN MOUSE OVER!
                    
                        GuiElementHoverText text = new GuiElementHoverText(capi, shapes[c].name, CairoFont.WhiteSmallText(), 200, browserItemSlotBounds);
                        text.ZPosition = 1000;
                        slotcontainer.Add(text);
                    
                    browserItemSlotBounds=browserItemSlotBounds.RightCopy(itemSlotSize/2,0,0,0);
                    colctr++;
                    if (colctr == browserColumns)
                    {
                        browserItemSlotBounds = browserItemNextRowBounds;
                        browserItemNextRowBounds=browserItemNextRowBounds.BelowCopy();
                        colctr = 0;
                    }
                }
                
                main.EndClip();
                
                main.AddVerticalScrollbar(onNewScrollbarValue, browserScrollBarBounds, "browserscrollbar");
                GuiElementScrollbar scrollbar = main.GetScrollbar("browserscrollbar");
                scrollbar.SetHeights((float)height, browserFullHeight);

            }

            //material index area
            {
                int browserColumns = 3;
                double itemSlotScale = 0.75;
                double itemSlotSize = 50 * itemSlotScale;
                double elementToDialogPadding = GuiStyle.ElementToDialogPadding;
                double unscaledSlotPadding = GuiElementItemSlotGridBase.unscaledSlotPadding;
                if (blockInventory == null) {
                    capi.World.PlaySound(new SoundAttributes(new AssetLocation("sounds/comperr"), true));
                    capi.World.Player.ShowChatNotification(Lang.Get("chiseltools:portfoliorequirespantograph"));
                    return;
                }
                int browserFullHeight = blockInventory.Count / browserColumns * (int)itemSlotSize;
                double height = 530;
                //This is the area on the screen we want the browser area to be
                ElementBounds browserAreaSize = ElementBounds.Fixed(24, 300, 150 , height).WithParent(dialogBounds);//full area for the shape browser

                //FULL BOUNDS, including hte full height if the area wasn't clipped
                ElementBounds browserContainingBounds = ElementBounds.Fixed(browserAreaSize.fixedX, browserAreaSize.fixedY, browserAreaSize.fixedWidth, browserFullHeight);//full grid area if not clipped


                //This is the part that will get scrolled (I think)
                ElementBounds browserScrollContainerBounds = browserContainingBounds.ForkBoundingParent(0, 0, 0, 0);

                //This should be the same as browserAreaSize (copied because I'm copying some other code)
                ElementBounds browserClippingAreaBounds = browserContainingBounds.CopyOffsetedSibling();
                browserClippingAreaBounds.fixedHeight = height;

                //This is the bounds for the actual slots
                ElementBounds browserItemSlotBounds = ElementBounds.Fixed(browserContainingBounds.fixedX, browserContainingBounds.fixedY, itemSlotSize, itemSlotSize);
                ElementBounds browserItemNextRowBounds = browserItemSlotBounds.BelowCopy(); //for grid tracking

                ElementBounds browserScrollBarBounds = ElementStdBounds.VerticalScrollbar(browserScrollContainerBounds).WithParent(dialogBounds);
                browserScrollBarBounds.fixedOffsetX -= 2.0;
                browserScrollBarBounds.fixedWidth = 15;
                browserScrollBarBounds.fixedHeight = height;
                ElementBounds insetbounds = browserScrollContainerBounds;
                insetbounds.fixedHeight = height;
                main.AddInset(insetbounds); //the darkened box

                main.BeginClip(browserClippingAreaBounds); //start the clipping area so the list doesn't go off the screen)

                main.AddContainer(browserContainingBounds.ForkContainingChild(0, 0, 0, -3), "blockgrid"); //a container that will hold all the itemslots
                var slotcontainer = main.GetContainer("blockgrid");
                int colctr = 0;
                //now go thru the actual inventory and make the slots
                for (int c = 0; c < blockInventory.Count; c++)
                {
                    if (blockInventory[c] == null || blockInventory[c].Empty) { continue; }//skip bad entries

                    GuiElementCTItemSlot slot = new GuiElementCTItemSlot(capi, GetMaterialListBlockClick, c, browserItemSlotBounds, blockInventory, blockInventory[c], false, (float)itemSlotScale, false);
                    slotcontainer.Add(slot);
                    //TODO NEED TO ADD CLICK HANDLING TO SLOTS
                    //TODO ROTATE - SHOULD ADD ROTATION WHEN MOUSE OVER!

                    GuiElementHoverText text = new GuiElementHoverText(capi, blockInventory[c].Itemstack.GetName(), CairoFont.WhiteSmallText(), 200, browserItemSlotBounds);
                    text.ZPosition = 1000;
                    slotcontainer.Add(text);

                    browserItemSlotBounds = browserItemSlotBounds.RightCopy(itemSlotSize / 2, 0, 0, 0);
                    colctr++;
                    if (colctr == browserColumns)
                    {
                        browserItemSlotBounds = browserItemNextRowBounds;
                        browserItemNextRowBounds = browserItemNextRowBounds.BelowCopy();
                        colctr = 0;
                    }
                }

                main.EndClip();

                main.AddVerticalScrollbar(onNewScrollbarValue, browserScrollBarBounds, "blockbrowserscrollbar");
                GuiElementScrollbar scrollbar = main.GetScrollbar("blockbrowserscrollbar");
                scrollbar.SetHeights((float)height, browserFullHeight);
            }
            
            main.Compose();
            if (filterfocus)
            {
                main.FocusElement(filterbox.TabIndex);
                filterfocus = false;
            }
            //main.UnfocusOwnElements();
            return;
           
        }

        private void onNewScrollbarValue(float value)
        {
            if (IsOpened())
            {
               
                if (Composers[mainDialogRef] != null)
                {
                    var slotcontainer = Composers[mainDialogRef].GetContainer("browsergrid");
                    if (slotcontainer != null)
                    {
                        ElementBounds bounds = slotcontainer.Bounds;
                        bounds.fixedY = 0 - value;
                        bounds.CalcWorldBounds();
                    }
                }
            }
        }
        
        protected void CloseIconPressed()
        {
            TryClose();
        }
        private bool GetClick(int slotno)
        {
            if (slotno == -1) { return true; }
            if (slotno >= virtualinventory.Count) { return true; }
            //TryClose();

            string result = ChiselToolLoader.loader.PortfolioLoad(loadedfilenames[slotno], true,false);
            capi.ShowChatMessage(result);
            SetupInventory(filter);
            SetupDialog();
            return true;
        }

        //Receive Clicks from material list
        private bool GetMaterialListBlockClick(int slotno)
        {
            //** We need to only grab the unpadded material list!!
            //** Also don't process if they've clicked on a padded material

            //** maybe first refatch currentPantograph data?

            if (lastClickedMaterial == slotno) { return true; }

            if (lastClickedMaterial == -1 )
            {
                lastClickedMaterial = slotno;
                capi.World.PlaySound(new SoundAttributes(new AssetLocation("sounds/compblip"), true));
                return true;
                
            }
            capi.World.PlaySound(new SoundAttributes(new AssetLocation("sounds/compcuteblip"),true));
            PantographData workingdata = new PantographData(PantographData.TryGrabFromSlot(capi,capi.World.Player.InventoryManager.ActiveHotbarSlot));
            if (workingdata==null) { return true; }
            
            int[] materials = SerializerUtil.Deserialize<int[]>(workingdata.matdata);
            if (materials==null||materials.Length==0||slotno>= materials.Length||lastClickedMaterial>=materials.Length) { return true; }
            int a = materials[slotno];
            int b = materials[lastClickedMaterial];
            materials[slotno] = b;
            materials[lastClickedMaterial] = a;
            lastClickedMaterial = -1; //reset
            workingdata.matdata = SerializerUtil.Serialize<int[]>(materials);
            //workingdata.blockcodes[0] = targetDesc;
            //need to update the active pantograph
            ChiselToolLoader.SetClientPantograph(capi, workingdata);
            SetupInventory(filter);
            SetupDialog();
            return true;
        }
        private bool OnClickSave()
        {
            GuiElementTextInput i = Composers[mainDialogRef].GetTextInput("savefilename");
            string filename= i.Text;
            if (filename == "") { return false; }
            if (ChiselToolLoader.loader == null) { return false; }
            capi.ShowChatMessage(ChiselToolLoader.loader.PortfolioSave(filename));
            SetupInventory("*" + filename + "*");
            
            return true;
        }

        private void onSaveTextChange(string newtext)
        {
            Composers[mainDialogRef].GetTextInput("savefilename").Text=newtext;
        }
        static bool filterfocus = false;
        private void onTextChanged(string newfilter)
        {
            if (ignorenextfilter)
            {
                ignorenextfilter = false;
                return;
            }
            if (newfilter == "") { return; }
            filter =newfilter;
            filter = filter.Replace("*", "");
            SetupInventory("*" + newfilter + "*");
            filterfocus = true; 
            SetupDialog();
            
        }

    }
}
