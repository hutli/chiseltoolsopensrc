using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using ProtoBuf;
using System.Xml.Serialization;
using System.Reflection.PortableExecutable;
using Vintagestory.API.MathTools;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using chiseltools.src;
using Cairo;
using chiseltools;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Numerics;
using System.Diagnostics;
using Vintagestory.API.Common.Entities;
using HarmonyLib;
using static System.Net.Mime.MediaTypeNames;
using Newtonsoft.Json.Linq;
//TODO:
// - have a file delete function?
// - have folders?


namespace chisel.src
{
    class ChiselToolLoader : ModSystem
    {
        public static ChiselToolLoader loader;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        string serverconfigfile = "qpchiseltoolserversettings.json";
        public static ChiselToolServerData serverconfig;
        public INetworkChannel chiselnet; //used by true chisel and also door tool
        //public INetworkChannel pastenet; seemingly unused
        public INetworkChannel textnet; //used to pass around error messages
        
        public INetworkChannel portfolionet; //used to send portfolio shapes 
        public static string voxelsavek = "chiselportfolio";
        public static string voxeltestfilek = "test";
        GUIShapeBrowser gUIShapeBrowser;
        
        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            if (api is ICoreClientAPI)
            {
                capi = api as ICoreClientAPI;
                chiselnet = capi.Network.RegisterChannel("pantograph").RegisterMessageType(typeof(DoorData)).RegisterMessageType(typeof(PantographData));
                textnet = capi.Network.RegisterChannel("pantographtext").RegisterMessageType(typeof(string));
                portfolionet = capi.Network.RegisterChannel("portfolio").RegisterMessageType(typeof(PantographData));
                
                //pastenet = capi.Network.RegisterChannel("pastenet").RegisterMessageType(typeof(PantographData)).RegisterMessageType(typeof(BlockPos));
                ClientPreStart();
            }
            else if (api is ICoreServerAPI)
            {
                sapi = api as ICoreServerAPI;
                
                chiselnet = sapi.Network.RegisterChannel("pantograph")
                    .RegisterMessageType(typeof(DoorData)).
                    SetMessageHandler<DoorData>(DoorDataHandler)
                    .RegisterMessageType(typeof(PantographData))
                    .SetMessageHandler<PantographData>(PantographDataToBlockEntity);
                textnet = sapi.Network.RegisterChannel("pantographtext")
                    .RegisterMessageType((typeof(string))).
                    SetMessageHandler<string>(TextMessageHandler);
                portfolionet = sapi.Network.RegisterChannel("portfolio")
                    .RegisterMessageType(typeof(PantographData)).
                    SetMessageHandler<PantographData>(PantographWriter);

                //pastenet = sapi.Network.RegisterChannel("pastenet").RegisterMessageType(typeof(PantographData)).SetMessageHandler<PantographData>(PasteNetHandler)
                //    .RegisterMessageType(typeof(BlockPos)).SetMessageHandler<BlockPos>(RequestRelight);
                ServerPreStart();
            }
            loader = this;
        }


        void ClientPreStart()
        {

            var parsers = capi.ChatCommands.Parsers;
            capi.Input.RegisterHotKey("portfolio", Lang.Get("chiseltools:openportfolio"), GlKeys.P, HotkeyType.CharacterControls);
            capi.Input.SetHotKeyHandler("portfolio", OpenPortfolioKey);
            capi.ChatCommands.Create("pf").HandleWith(CmdPortfolio)
                .BeginSubCommand("list").WithDescription(Lang.Get("chiseltools:portfoliolist"))
                .WithArgs(parsers.Word("match"))
                .HandleWith(args =>
                {

                    var path = capi.GetOrCreateDataPath(voxelsavek);
                    string match = args.Parsers[0].GetValue().ToString();
                    string[] files = System.IO.Directory.GetFiles(path, match);
                    StringBuilder msg = new StringBuilder();
                    string evencolor = "<font color=\"#55ffff\">";
                    string oddcolor = "<font color=\"#00ffff\">";
                    msg.AppendLine("<font color=\"#00ff00\"><strong>" + Lang.Get("chiseltools:portfoliomatching") + "</font>" + evencolor + match + "</font></strong>");
                    int count = 0;

                    if (files.Length == 0) { msg.AppendLine(Lang.Get("chiseltools:nomatch")); }
                    else
                    {
                        foreach (string filename in files)
                        {
                            string usecolor = oddcolor;
                            if (count % 2 == 0) { usecolor = evencolor; }
                            msg.AppendLine(usecolor + System.IO.Path.GetFileName(filename) + "</font>");
                            count++;
                        }

                    }
                    capi.ShowChatMessage(msg.ToString());
                    return TextCommandResult.Success();
                })
                .EndSubCommand()
                .BeginSubCommand("save").WithDescription(Lang.Get("chiseltools:savetoportfolio"))
                .WithArgs(parsers.Word("filename"))
                .HandleWith(args =>
                {
                    if (args.Parsers[0].IsMissing)
                    {
                        return TextCommandResult.Error(Lang.Get("chiseltools:nofilename"));
                    }
                    return TextCommandResult.Success(PortfolioSave(args.Parsers[0].GetValue().ToString()));
                })
                .EndSubCommand()
                .BeginSubCommand("load").WithDescription(Lang.Get("chiseltools:loadfromportfolio"))
                .WithArgs(parsers.Word("filename"))
                .HandleWith(args =>
                {
                    if (args.Parsers[0].IsMissing)
                    {
                        return TextCommandResult.Error(Lang.Get("chiseltools:nofilename"));
                    }
                    return TextCommandResult.Success(PortfolioLoad(args.Parsers[0].GetValue().ToString()));
                })
                .EndSubCommand()
                .BeginSubCommand("doorsound").WithDescription(Lang.Get("chiseltools:setdoorsound"))
                .WithArgs(parsers.Word("filename"))
                .HandleWith(args =>
                {
                    if (args.Parsers[0].IsMissing)
                    {
                        return TextCommandResult.Error(Lang.Get("chiseltools:nofilename"));
                    }
                    return TextCommandResult.Success(SetDoorSound(args.Parsers[0].GetValue().ToString()));
                })
                .EndSubCommand()
                .BeginSubCommand("preview").WithDescription(Lang.Get("chiseltools:openbrowser")).WithArgs(parsers.Word("filename"))
                .HandleWith(args =>
                {
                    string filter = "";
                    if (!args.Parsers[0].IsMissing)
                    {
                        filter = args.Parsers[0].GetValue().ToString();
                    }
                    if (gUIShapeBrowser != null)
                    {
                        gUIShapeBrowser.TryClose();

                    }

                    //if (systeminventory == null || systeminventory.Empty) { return; }
                    BlockPos pos = capi.World.Player.CurrentBlockSelection.Position;
                    gUIShapeBrowser = new GUIShapeBrowser(filter, pos, "shapebrowser", capi);
                    gUIShapeBrowser.TryOpen();
                    return TextCommandResult.Success();
                })
                .EndSubCommand();
            ;



        }

        private bool OpenPortfolioKey(KeyCombination comb)
        {
            OpenPortfolioGUI("*");
            return true;
        }

        public void OpenPortfolioGUI(string filter)
        {
            if (capi == null) { return; }
            if (capi.World.Player == null) { return; }
            //if (capi.World.Player.CurrentBlockSelection == null) { capi.ShowChatMessage(ErrorWrap(Lang.Get("chiseltools:lookatchiseled"))); return; }

            if (gUIShapeBrowser != null)
            {
                gUIShapeBrowser.TryClose();

            }
            if (filter == null || filter == "") { filter = "*"; }
            //if (systeminventory == null || systeminventory.Empty) { return; }
            BlockPos pos = null;
            if (capi.World.Player.CurrentBlockSelection != null) { pos = capi.World.Player.CurrentBlockSelection.Position; }
            gUIShapeBrowser = new GUIShapeBrowser(filter, pos, "shapebrowser", capi);
            if (gUIShapeBrowser != null) { gUIShapeBrowser.TryOpen(); }
        }


        void ServerPreStart()
        {
            
            var parsers = sapi.ChatCommands.Parsers;

            serverconfig = null;
            try
            {
                serverconfig = sapi.LoadModConfig<ChiselToolServerData>(serverconfigfile);
            }
            catch
            {
                serverconfig = null;

            }
            if (serverconfig == null)
            {
                serverconfig = new ChiselToolServerData();
                sapi.StoreModConfig<ChiselToolServerData>(serverconfig, serverconfigfile);
            }
        }
        public override void AssetsFinalize(ICoreAPI api)
        {

            foreach (var entity in api.World.EntityTypes)
            {
                if (entity.Code.ToString() == "game:player")
                {

                    JToken token = JToken.Parse("{\"code\":\"EntityDecoBehavior\"}");
                    JsonObject jobj = new JsonObject(token);
                    if (entity.Client != null)
                    {
                        List<JsonObject> tempjson = entity.Client.BehaviorsAsJsonObj.ToList<JsonObject>();
                        tempjson.Add(jobj);
                        entity.Client.BehaviorsAsJsonObj = tempjson.ToArray();
                    }
                    if (entity.Server != null)
                    {
                        List<JsonObject> tempjson = entity.Server.BehaviorsAsJsonObj.ToList<JsonObject>();
                        tempjson.Add(jobj);
                        entity.Server.BehaviorsAsJsonObj = tempjson.ToArray();

                    }
                }
            }

        }
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("ItemHandPlaner", typeof(ItemHandPlaner));
            api.RegisterItemClass("ItemWedge", typeof(ItemWedge));
            api.RegisterItemClass("ItemPantograph", typeof(ItemPantograph));
            api.RegisterItemClass("ItemPaintBrush", typeof(ItemPaintBrush));
            api.RegisterItemClass("ItemBlockSwapper", typeof(ItemBlockSwapper));
            api.RegisterBlockEntityClass("BEFunctionChiseled", typeof(BEFunctionChiseled));
            api.RegisterBlockClass("BlockFunctionalChiseled", typeof(BlockFunctionalChiseled));
            api.RegisterItemClass("ItemChiselBlockController", typeof(ItemChiselBlockController));
            api.RegisterItemClass("ItemLadderMaker", typeof(ItemLadderMaker));
            api.RegisterItemClass("ItemDoorPart", typeof(ItemDoorPart));
            api.RegisterItemClass("TrueChisel", typeof(TrueChisel));
            api.RegisterItemClass("ItemJonasPaintBrush", typeof(ItemJonasPaintBrush));
            api.RegisterBlockBehaviorClass("BBChiseledCover", typeof(BBChiseledCover));
            api.RegisterBlockEntityBehaviorClass("BEBChiseledCover", typeof(BEBChiseledCover));
            api.RegisterBlockEntityClass("BETestDummy", typeof(BETestDummy));
            api.RegisterBlockClass("BlockDecoToolRack", typeof(BlockDecoToolRack));
            api.RegisterBlockEntityClass("BEDecoToolRack", typeof(BEDecoToolRack));
            api.RegisterBlockClass("BlockDecoShelf", typeof(BlockDecoShelf));
            api.RegisterBlockEntityClass("BEDecoShelf", typeof(BEDecoShelf));
            api.RegisterBlockClass("BlockDecoDisplayCase", typeof(BlockDecoDisplayCase));
            api.RegisterBlockEntityClass("BEDecoDisplayCase", typeof(BEDecoDisplayCase));
            api.RegisterBlockEntityClass("BEDecoBarrel", typeof(BEDecoBarrel));
            api.RegisterBlockBehaviorClass("BBBarrelCover", typeof(BBBarrelCover));
            api.RegisterBlockEntityBehaviorClass("BEBBarrelCover", typeof(BEBBarrelCover));
            api.RegisterBlockClass("BlockDecoOven", typeof(BlockDecoOven));
            api.RegisterBlockClass("BlockDecoPlantContainer", typeof(BlockDecoPlantContainer));
            api.RegisterBlockEntityClass("BEDecoPlantContainer", typeof(BEDecoPlantContainer));
            api.RegisterBlockClass("BlockDecoAnvil", typeof(BlockDecoAnvil));
            api.RegisterBlockEntityClass("BEDecoAnvil", typeof(BEDecoAnvil));
            api.RegisterBlockClass("BlockDecoForge", typeof(BlockDecoForge));
            api.RegisterBlockEntityClass("BEDecoForge", typeof(BEDecoForge));
            api.RegisterItemClass("ItemPathMaker", typeof(ItemPathMaker));
            api.RegisterItemClass("ItemPaintScraper", typeof(ItemPaintScraper));
            api.RegisterBlockClass("BlockDecoSign", typeof(BlockDecoSign));
            api.RegisterBlockEntityClass("BEDecoSign", typeof(BEDecoSign));
            api.RegisterItemClass("ItemPalette", typeof(ItemPalette));
            api.RegisterBlockEntityClass("BEDecoContainer", typeof(BEDecoContainer));
            api.RegisterBlockClass("BlockGenericDecoTypedContainer", typeof(BlockGenericDecoTypedContainer));
            api.RegisterBlockClass("BlockDeco", typeof(BlockDeco));
            api.RegisterBlockClass("BlockDecoBarrel", typeof(BlockDecoBarrel));
            api.RegisterBlockClass("BlockDecoBed", typeof(BlockDecoBed));
            api.RegisterBlockEntityClass("BEDecoFirepit", typeof(BEDecoFirepit));
            api.RegisterBlockClass("BlockDecoFirepit", typeof(BlockDecoFirepit));
            api.RegisterEntityBehaviorClass("EntityDecoBehavior", typeof(EntityDecoBehavior));
            api.RegisterBlockEntityClass("BELockedMicroblock", typeof(BELockedMicroblock));
            api.RegisterBlockEntityClass("BEChiselProjector", typeof(BEChiselProjector));
            api.RegisterBlockClass("BlockChiselProjector", typeof(BlockChiselProjector));
            api.RegisterBlockClass("BlockDecoBookshelf", typeof(BlockDecoBookshelf));
            api.RegisterBlockEntityClass("BEDecoBookShelf", typeof(BEDecoBookShelf));
            api.RegisterBlockClass("BlockDecoWaterSource", typeof(BlockDecoWaterSource));
            api.RegisterBlockEntityClass("BEDecoGeneric", typeof(BEDecoGeneric));
        }

        private TextCommandResult CmdPortfolio(TextCommandCallingArgs args)
        {

            capi.ShowChatMessage("Portfolio Commands:");
            capi.ShowChatMessage(".pf list * --lists files in portfolio you can use pattern matching - * for all or a* for files starting with a etc.");
            capi.ShowChatMessage(".pf load <filename> --will copy from the file into a pantograph in your hand");
            capi.ShowChatMessage(".pf save <filename> --will save copied pantograph data to the file");
            capi.ShowChatMessage(".pf preview <filename> --opens a UI and displays your shape portfolio (you must have a pantograph in hand and be targetting a chiselable block!)");

            return TextCommandResult.Success();

        }



        public string PortfolioSave(string filename)
        {

            PantographData data = PantographData.TryGrabFromSlot(capi, capi.World.Player.InventoryManager.ActiveHotbarSlot);

            if (data == null) { return Lang.Get("chiseltools:nopanto"); }
            data.name = filename;
            var path = capi.GetOrCreateDataPath(voxelsavek);  //APIExtensions.GetDataPath(api, filename);
            path = System.IO.Path.Combine(path, filename);
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(PantographData));
                writer = new StreamWriter(path, false);
                serializer.Serialize(writer, data);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }

            return Lang.Get("chiseltools:filesaved") + " " + filename;
        }
        public static string ErrorWrap(string msg)
        {
            return "<font color=\"#ff1111\">" + msg + "</font>";
        }

        

        public string PortfolioLoad(string filename, bool fullpath = false, bool reopen = false)
        {
            //will have to load client side then push result to server

            //check for Pantograph
            if (capi.World.Player.InventoryManager.ActiveHotbarSlot.Itemstack == null) { return Lang.Get("chiseltools:nullinventory"); }
            Item holditem = capi.World.Player.InventoryManager.ActiveHotbarSlot.Itemstack.Item;
            if (holditem == null) { return ErrorWrap(Lang.Get("chiseltools:nopanto")); }
            if (!(holditem is ItemPantograph)) { return ErrorWrap(Lang.Get("chiseltools:nopanto")); }
            var path = capi.GetOrCreateDataPath(voxelsavek);  //APIExtensions.GetDataPath(api, filename);
            path = System.IO.Path.Combine(path, filename);
            if (fullpath) { path = filename; }
            PantographData data = null;
            TextReader reader = null;
            if (path.EndsWith(".vox"))
            {

                bool ok = false;// PantographData.LoadVoxFile(path, out data);
            }
            else
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(PantographData));
                    try
                    {
                        reader = new StreamReader(path);
                        data = (PantographData)serializer.Deserialize(reader);

                        data.materialPasteOK = false;
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
            }
            if (data == null) { return ErrorWrap(Lang.Get("chiseltools:loaderror")); }
            else
            {
                //loader.chiselnet as IClientNetworkChannel).SendPacket<DoorData>(newdoor);
                ICoreAPI useapi = capi as ICoreAPI;
                if (sapi != null) { useapi = sapi as ICoreAPI; }

                data.materialPasteOK = false;
                if (data.blockcodes != null)
                {
                    int[] matref = SerializerUtil.Deserialize<int[]>(data.matdata);
                    if (matref != null && matref.Length > 0 && matref.Length == data.blockcodes.Length)
                    {
                        bool allgood = true;
                        for (int c = 0; c < data.blockcodes.Length; c++)
                        {
                            Block b = useapi.World.BlockAccessor.GetBlock(new AssetLocation(data.blockcodes[c]));
                            if (b == null || b.BlockId == 0) { allgood = false; continue; }
                            int newbid = b.BlockId;
                            if (newbid == 0) { allgood = false; continue; }
                            matref[c] = newbid;
                        }
                        data.matdata = SerializerUtil.Serialize<int[]>(matref);
                        data.materialPasteOK = allgood;
                    }
                }
                SetClientPantograph(capi,data);
                //(portfolionet as IClientNetworkChannel).SendPacket<PantographData>(data);
            }
            List<byte> materialcounter = new List<byte>();
            foreach (byte matref in data.matdata)
            {
                if (!materialcounter.Contains(matref)) { materialcounter.Add(matref); }
            }
            if (reopen)
            {
                OpenPortfolioGUI("*");
            }
            return Lang.Get("chiseltools:copied") + filename + Lang.Get("chiseltools:topantographfromportolio!uses") + (materialcounter.Count() - 2) + Lang.Get("chiseltools:materials");
        }
        public string SetDoorSound(string filename)
        {
            if (capi.World.Player.CurrentBlockSelection == null) { return Lang.Get("chiseltools:lookatdoor"); }
            if (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative &&  !capi.World.Player.HasPrivilege("adddoorsound"))
            //if (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative &&  !capi.World.Player.HasPrivilege("adddoorsound"))
            {
                return Lang.Get("chiseltools:needdoorsoundpermission");
            }
            if (filename == "list")
            {
                var soundlist = capi.Assets.AllAssets.ToList();
                soundlist = soundlist.Where(x => x.ToString().Contains(".ogg") && !x.ToString().Contains(":music")).ToList();

                //.Where(p => p.Key.Category == AssetCategory.sounds || p.Key.Category == AssetCategory.music);
                foreach (var sound in soundlist)
                {
                    capi.ShowChatMessage(sound.ToString());
                }

                return "";
            }
            BEFunctionChiseled be = capi.World.BlockAccessor.GetBlockEntity(capi.World.Player.CurrentBlockSelection.Position) as BEFunctionChiseled;
            if (be == null) { return Lang.Get("chiseltools:lookatdoor"); }
            be.SetSound(filename);
            return Lang.Get("chiseltools:doorsoundset"); ;
        }
        
        


        private TextCommandResult ClearChiselHead(TextCommandCallingArgs args)
        {
            args.Caller.Player.Entity.WatchedAttributes.RemoveAttribute("shapestack");
            sapi.Network.BroadcastEntityPacket(args.Caller.Player.Entity.EntityId, 87655);
            return TextCommandResult.Success("Head Shape Cleared");
        }

        private void CmdSetReset(IPlayer player, int groupid, CmdArgs args)
        {
            if (sapi == null) { return; }
            if (serverconfig == null) { sapi.BroadcastMessageToAllGroups("QP Chisel Server Config doesn't exist.", EnumChatType.Notification); return; }
            serverconfig.Reset();
            sapi.StoreModConfig<ChiselToolServerData>(serverconfig, serverconfigfile);
            sapi.SendMessage(player, groupid, "QP's Chisel Tools server config has been reset to defaults!", EnumChatType.CommandSuccess);
        }

        //Used by true chisel to copy client side data from a block entity
        private void PantographDataToBlockEntity(IPlayer fromplayer, PantographData networkmessage)
        {
            if (networkmessage == null) return;
            if (fromplayer == null || networkmessage.pos == null) { return; }
            if (!fromplayer.Entity.World.Claims.TryAccess(fromplayer, networkmessage.pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                sapi.Logger.Log(EnumLogType.Error, fromplayer.PlayerName + " (" + fromplayer.PlayerUID + ") has attempted a relight with Chisel Tools in a chunk they don't have access too at " + networkmessage.pos.ToLocalPosition(sapi).ToString() + ")");
                return;
            }
            BlockEntityChisel bec = sapi.World.BlockAccessor.GetBlockEntity(networkmessage.pos) as BlockEntityChisel;
            if (bec == null) return;
            int[] bids = SerializerUtil.Deserialize<int[]>(networkmessage.matdata);
            List<uint> voxc = SerializerUtil.Deserialize<List<uint>>(networkmessage.voxeldata);
            if (bids == null || voxc == null)
            {
                if (fromplayer != null) { sapi.SendMessage(fromplayer, 0, "True Chisel failure in PantographDataToBlockEntity", EnumChatType.CommandError); }
                return;
            }
            UpdateChiseledBlock(bec, bids, voxc, networkmessage.name);
        }

        //Used by pantograph to transfrom a chiseled block in to a function chiseled block
        private void DoorDataHandler(IPlayer fromplayer, DoorData networkMessage)
        {
            if (sapi == null||fromplayer==null) { return; }
            if (networkMessage== null) return;
            if (networkMessage.pos == null) return;
            if (!fromplayer.Entity.World.Claims.TryAccess(fromplayer, networkMessage.pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                sapi.Logger.Log(EnumLogType.Error, fromplayer.PlayerName + "(" + fromplayer.PlayerUID + ") tried to set door information where they don't have access at " + networkMessage.pos.ToLocalPosition(sapi).ToString());
                return;
            }

            if (networkMessage.makenewblock)
            {
                MakeNewChiseledBlock(networkMessage);
                return;
            }
            sapi.World.BlockAccessor.SetBlock(sapi.World.GetBlock(new AssetLocation("chiseltools:moveablechiseledblock")).BlockId, networkMessage.pos);
            BEFunctionChiseled bfc = sapi.World.BlockAccessor.GetBlockEntity(networkMessage.pos) as BEFunctionChiseled;

            if (bfc == null) { return; }

            bfc.AddState(networkMessage.state, networkMessage.voxeldata, networkMessage.matdata, networkMessage.passable, networkMessage.transparent);
            networkMessage.state = "ORIGINAL";
            bfc.AddState(BEFunctionChiseled.originalblockname, networkMessage.voxeldata, networkMessage.matdata, networkMessage.passable, networkMessage.transparent);
            bfc.SetState(BEFunctionChiseled.closename);

        }
        int chiselblockid;

        private void PantographWriter(IPlayer fromplayer, PantographData data)
        {
            IServerPlayer splr = fromplayer as IServerPlayer;
            if (splr.InventoryManager.ActiveHotbarSlot.Itemstack == null) { return; }
            Item holditem = splr.InventoryManager.ActiveHotbarSlot.Itemstack.Item;
            if (holditem == null) { return; }
            if (!(holditem is ItemPantograph)) { return; }
            splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetString(ItemPantograph.copiedblocknameK, data.name);
            splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetBytes(ItemPantograph.copiedblockvoxelsK, data.voxeldata);
            splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetBytes(ItemPantograph.copiedmaterialistK, data.matdata);
            /*if (data.blockcodes != null)
            {
                splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetBytes(ItemPantograph.copiedblocknameK, SerializerUtil.Serialize(data.blockcodes));
            }*/
            splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetBool(ItemPantograph.materialpasteokk, data.materialPasteOK);
            splr.InventoryManager.ActiveHotbarSlot.MarkDirty();
        }
        public static void SetClientPantograph(ICoreClientAPI capi, PantographData data)
        {
            //Attempting to send pantograph data to the server
            (loader.portfolionet as IClientNetworkChannel).SendPacket(data);
            
            
            IPlayer splr = capi.World.Player;
            Item holditem = splr.InventoryManager.ActiveHotbarSlot.Itemstack.Item;
            if (holditem == null) { return; }
            if (!(holditem is ItemPantograph)) { return; }
            if (data.name == null) { data.name = "Chiseled Block"; }
            splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetString(ItemPantograph.copiedblocknameK, data.name);
            splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetBytes(ItemPantograph.copiedblockvoxelsK, data.voxeldata);
            if (data.matdata != null)
            {
                splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetBytes(ItemPantograph.copiedmaterialistK, data.matdata);
            }
            if (data.blockcodes != null)
            {
                splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetBytes(ItemPantograph.copiedblocknameK, SerializerUtil.Serialize(data.blockcodes));
            }
            splr.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetBool(ItemPantograph.materialpasteokk, data.materialPasteOK);
            splr.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return;
        }
        private void TextMessageHandler(IPlayer fromplayer, string message)
        {
            IServerPlayer splr = fromplayer as IServerPlayer;
            if (splr == null || sapi == null) { return; }
            if (message.StartsWith("@"))
            {
                if (splr.Entity == null) { return; }
                string msg = splr.PlayerName + " is at ";
                msg += splr.Entity.Pos.AsBlockPos.ToLocalPosition(sapi).ToString();
                sapi.BroadcastMessageToAllGroups(msg, EnumChatType.OthersMessage);
                return;
            }
            splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(splr.LanguageCode, message), EnumChatType.Notification);
        }
        private void MakeNewChiseledBlock(DoorData data)
        {
            if (chiselblockid == 0)
            {
                chiselblockid = sapi.World.GetBlock(new AssetLocation("game:chiseledblock")).BlockId;
            }
            if (data.matdata == null || data.matdata.Count == 0 || data.voxeldata == null || data.voxeldata.Count == 0) { return; }
            sapi.World.BlockAccessor.SetBlock(chiselblockid, data.pos);
            BlockEntityChisel bec = sapi.World.BlockAccessor.GetBlockEntity(data.pos) as BlockEntityChisel;
            if (bec == null) { return; }
            UpdateChiseledBlock(bec, data.matdata.ToArray(), new List<uint>(data.voxeldata), data.name);
        }
        public static string copyprotectedblockname = "chiseltools:copyprotectedblock";

        
        
        
        
        /// <summary>
        /// So far only setup for sending a Mirror Paste from client
        /// </summary>
        /// <param name="fromplayer"></param>
        /// <param name="data"></param>
        private void PasteNetHandler(IPlayer fromplayer, PantographData data)
        {

            if (data == null || fromplayer == null) { return; }
            if (data.pos == null)
            {
                return;
            }
            if (!fromplayer.Entity.World.Claims.TryAccess(fromplayer, data.pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                sapi.Logger.Log(EnumLogType.Error, fromplayer.PlayerName + " (" + fromplayer.PlayerUID + ") has attempted to paste with Chisel Tools in a chunk they don't have access too at " + data.pos.ToLocalPosition(sapi).ToString() + ")");
                return;
            }
            BlockEntityMicroBlock bmb = sapi.World.BlockAccessor.GetBlockEntity(data.pos) as BlockEntityMicroBlock;
            if (bmb == null) { return; }
            List<uint> copiedvoxels = null;
            try { copiedvoxels = SerializerUtil.Deserialize<List<uint>>(data.voxeldata); }
            catch { return; }
            if (copiedvoxels == null) { return; }
            UpdateChiseledBlock(bmb, null, copiedvoxels);
            //HERE USE DATA TO PASTE
        }

        

        //find an empty hotslot and give an item stack (not really used rn, but might be useful in future)
        /*private void GiveStack(IPlayer fromplayer, ItemStack givestack)
        {

            if (fromplayer.InventoryManager == null) { return; }
            foreach (ItemSlot slot in fromplayer.InventoryManager.GetHotbarInventory())
            {
                if (slot != null && slot.Empty)
                {
                    slot.Itemstack = givestack;
                    slot.MarkDirty();
                    return;
                }
            }
        }*/

        public static List<int> GetCreativeBlockIDS(ICoreAPI api)
        {
            List<int> bids = new List<int>();
            for (int c = 0; c < 80; c++)
            {
                AssetLocation al = new AssetLocation("game:creativeblock-" + c);
                Block b = api.World.GetBlock(al);
                if (b != null)
                {
                    bids.Add(b.BlockId);
                }
            }
            if (bids.Count == 0)
            {
                return bids;
            }
            return bids;
        }

        public sealed class MicroBlockSnapshot
        {
            public int[] BlockIds;
            public uint[] Voxels;
            public ushort[] Quantities;
            public string Name;
            public int Absorption;
            public byte[] Hsv;
        }

        public static MicroBlockSnapshot Snapshot(BlockEntityMicroBlock bmb) => new MicroBlockSnapshot
        {
            BlockIds   = bmb.BlockIds?.ToArray(),
            Voxels     = bmb.VoxelCuboids?.ToArray(),
            Quantities = (bmb as BlockEntityChisel)?.AvailMaterialQuantities?.ToArray(),
            Name       = bmb.BlockName,
            Absorption = bmb.GetLightAbsorption(),
            Hsv        = bmb.GetLightHsv(bmb.Api.World.BlockAccessor),
        };

        private static bool Same<T>(T[] a, IEnumerable<T> b) => a == null ? b == null : b != null && a.SequenceEqual(b);

        public static bool UpdateBlock(BlockEntityMicroBlock bmb, MicroBlockSnapshot before)
        {
            IBlockAccessor ba = bmb.Api.World.BlockAccessor;
            void Log(string msg) => bmb.Api.Logger.Notification($"[lightfix] side={bmb.Api.Side} pos=({bmb.Pos}) {msg}");

            bool voxelsChanged     = !Same(before.Voxels, bmb.VoxelCuboids);
            bool materialsChanged  = !Same(before.BlockIds, bmb.BlockIds);
            bool quantitiesChanged = !Same(before.Quantities, (bmb as BlockEntityChisel)?.AvailMaterialQuantities);
            bool nameChanged       = before.Name != bmb.BlockName;

            if (!voxelsChanged && !materialsChanged)
            {
                // Data only: sync it, no redraw needed
                if (quantitiesChanged || nameChanged)
                {
                    Log($"MarkDirty(false) (quantitiesChanged={quantitiesChanged} | nameChanged={nameChanged})");
                    bmb.MarkDirty(false);
                }
                return quantitiesChanged || nameChanged;
            }

            // Read both before RebuildCuboidList, which reports its own absorption change if the absorbAnyLight flag flips
            int absorption = bmb.GetLightAbsorption();
            byte[] hsv = bmb.GetLightHsv(ba);
            bool lightChanged = !hsv.SequenceEqual(before.Hsv);
            string hsvChange = $"hsv {string.Join(",", before.Hsv)} -> {string.Join(",", hsv)}";

            // 1. Take the old light out while the block still has its old absorption
            if (lightChanged && before.Hsv[2] > 0)
            {
                Log($"RemoveBlockLight ({hsvChange})");
                ba.RemoveBlockLight(before.Hsv, bmb.Pos);
            }

            // 2. Then report the absorption change caused by the materials
            if (absorption != before.Absorption)
            {
                Log($"MarkAbsorptionChanged (absorption {before.Absorption} -> {absorption})");
                ba.MarkAbsorptionChanged(before.Absorption, absorption, bmb.Pos);
            }

            // 3. Side solidity/AO depend on voxels, emitSideAo also on brightness
            if (voxelsChanged || lightChanged)
            {
                Log($"RebuildCuboidList (voxelsChanged={voxelsChanged} | lightChanged={lightChanged})");
                bmb.RebuildCuboidList();
            }

            // 4. Put the new light in
            if (lightChanged && hsv[2] > 0)
            {
                Log($"ExchangeBlock ({hsvChange})");
                ba.ExchangeBlock(bmb.Block.Id, bmb.Pos);
            }

            if (!lightChanged) Log($"Light unchanged ({hsvChange})");

            Log("RegenSelectionBoxes");
            bmb.RegenSelectionBoxes(bmb.Api.World, null);

            if (bmb.Api.Side == EnumAppSide.Client)
            {
                Log("MarkMeshDirty");
                bmb.MarkMeshDirty();
            }

            Log("MarkDirty");
            bmb.MarkDirty(true);

            if (voxelsChanged || absorption != before.Absorption)
            {
                Log($"TriggerNeighbourBlockUpdate (voxelsChanged={voxelsChanged} | absorption {before.Absorption} -> {absorption})");
                ba.TriggerNeighbourBlockUpdate(bmb.Pos);
            }
            return true;
        }

        public static int AddMaterial(BlockEntityChisel bec, Block matblock, out bool isFull, bool compareToPickBlock = true)
        {
            ChiselToolLoader.MicroBlockSnapshot before = Snapshot(bec);
            int ret = bec.AddMaterial(matblock, out isFull, compareToPickBlock);
            UpdateBlock(bec, before);
            return ret;
        }

        public static int AddMaterial(BlockEntityChisel bec, Block block) => AddMaterial(bec, block, out _);

        public static bool UpdateChiseledBlock(BlockEntityMicroBlock bmb, int[] blockIds, List<uint> voxels, string blockName = null)
        {
            ChiselToolLoader.MicroBlockSnapshot before = Snapshot(bmb);
            if (voxels != null) bmb.VoxelCuboids = voxels;
            if (blockIds != null) bmb.BlockIds = blockIds;
            if (blockName != null) bmb.BlockName = blockName;
            return UpdateBlock(bmb, before);
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class ChiselToolServerData
        {
            public int handPlanerBaseDurabilityUse = 1;
            public float handPlanerBaseDurabilityMultiplier = handplanerDefault;
            public float pantographBaseDurabilityMultiplier = pantographDefault;
            public int pantographMinimumDamagePerOp = pantographMinimumDamagePerOpDefault;
            public bool enablePantographLeftClickCopy = true;
            public bool pantographInstantMode = true;
            public bool fixedToolWear = false;
            public int paintBrushUseRate = paintBrushUseRateDefault;
            public int paintBrushLiquidMultiplier = paintBrushLiquidMultiplierDefault;
            public static float handplanerDefault = 0.00625f;
            public static float pantographDefault = 0.001f;
            public static bool pantographInstantModeDefault = true;
            public static bool fixedToolWearDefault = false;
            public static int paintBrushUseRateDefault = 10;
            public static int pantographMinimumDamagePerOpDefault = 1;
            public static int paintBrushLiquidMultiplierDefault = 10;
            public static int minimumVoxelsForLadderDefault = 8;
            public int minimumVoxelsForLadder = minimumVoxelsForLadderDefault;
            public void Reset()
            {
                handPlanerBaseDurabilityMultiplier = handplanerDefault;

                pantographBaseDurabilityMultiplier = pantographDefault;
                pantographInstantMode = pantographInstantModeDefault;
                pantographMinimumDamagePerOp = pantographMinimumDamagePerOpDefault;
                paintBrushUseRate = paintBrushUseRateDefault;
                fixedToolWear = fixedToolWearDefault;
                minimumVoxelsForLadder = minimumVoxelsForLadderDefault;
            }
        }
        public static Shape GenShapeFromPantographData(ICoreAPI Api, PantographData pantodata)
        {
            if (pantodata==null) return null;
            Shape shape = new Shape();
            CuboidWithMaterial cwm = new CuboidWithMaterial();
            //voxel cubioids - list uint
            List<uint> VoxelCuboids = SerializerUtil.Deserialize<List<uint>>(pantodata.voxeldata);
            if (VoxelCuboids == null) { return null; }
            int[] BlockIds = SerializerUtil.Deserialize<int[]>(pantodata.matdata);
            if (BlockIds == null) { return null; }
            shape.Elements = new ShapeElement[VoxelCuboids.Count];
            shape.Textures = new Dictionary<string, AssetLocation>();
            shape.TextureWidth = 16;
            shape.TextureHeight = 16;

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                BlockEntityMicroBlock.FromUint(VoxelCuboids[i], cwm);
                var elem = new ShapeElement();
                elem.Name = "Cuboid" + i;
                elem.From = new double[] { cwm.X1, cwm.Y1, cwm.Z1 };
                elem.To = new double[] { cwm.X2, cwm.Y2, cwm.Z2 };
                //blockids is int[]
                var block = Api.World.Blocks[BlockIds[cwm.Material]];

                elem.Faces = new Dictionary<string, ShapeElementFace>();
                foreach (var facing in BlockFacing.ALLFACES)
                {
                    string texCode = block.Code.ToShortString();

                    CompositeTexture ctex;
                    if (!block.Textures.TryGetValue(facing.Code, out ctex))
                    {
                        block.Textures.TryGetValue("all", out ctex);
                    }
                    if (ctex == null) { return null; }
                    shape.Textures[texCode] = ctex.Base.Path.Replace("*", "1");

                    elem.Faces[facing.Code] = new ShapeElementFace() { Texture = texCode, Uv = new float[4] { 0, 0, 16, 16 } };
                }

                shape.Elements[i] = elem;
            }

            return shape;
        }
        public static Shape GenShapeFromMicroBlockEntity(ICoreAPI Api, BlockEntityMicroBlock fromblock)
        {
            Shape shape = new Shape();
            CuboidWithMaterial cwm = new CuboidWithMaterial();

            shape.Elements = new ShapeElement[fromblock.VoxelCuboids.Count];
            shape.Textures = new Dictionary<string, AssetLocation>();
            shape.TextureWidth = 16;
            shape.TextureHeight = 16;

            for (int i = 0; i < fromblock.VoxelCuboids.Count; i++)
            {
                BlockEntityMicroBlock.FromUint(fromblock.VoxelCuboids[i], cwm);
                var elem = new ShapeElement();
                elem.Name = "Cuboid" + i;
                elem.From = new double[] { cwm.X1, cwm.Y1, cwm.Z1 };
                elem.To = new double[] { cwm.X2, cwm.Y2, cwm.Z2 };

                var block = Api.World.Blocks[fromblock.BlockIds[cwm.Material]];

                elem.Faces = new Dictionary<string, ShapeElementFace>();
                foreach (var facing in BlockFacing.ALLFACES)
                {
                    string texCode = block.Code.ToShortString();

                    CompositeTexture ctex;
                    if (!block.Textures.TryGetValue(facing.Code, out ctex))
                    {
                        block.Textures.TryGetValue("all", out ctex);
                    }

                    shape.Textures[texCode] = ctex.Base.Path.Replace("*", "1");

                    elem.Faces[facing.Code] = new ShapeElementFace() { Texture = texCode, Uv = new float[4] { 0, 0, 16, 16 } };
                }

                shape.Elements[i] = elem;
            }

            return shape;
        }

    }

}
