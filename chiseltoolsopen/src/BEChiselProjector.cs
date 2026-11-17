using Cairo;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace chiseltools.src
{
    public class BEChiselProjector:BlockEntity,IReceiveCoverNotification
    {
        public static int UPDATEPACKETID = 2038450;
        BEBChiseledCover mycover;
        MeshData chiselmesh;
        ICoreClientAPI capi;
        float scale = 16; public float Scale => scale;
        Vec3f offset = Vec3f.Zero; public Vec3f Offset => offset;
        Vec3f rotation = Vec3f.Zero; public Vec3f Rotation=> rotation;
        Vec3f rotationOrigin = new Vec3f(8f, 0.8f, 8f);
        Vec4f rgba = new Vec4f(1, 1, 1, 0.5f); public Vec4f RGBA=>rgba;
        int glow = 64; public int Glow => glow;

        public bool ShouldRenderBig = true;
        public bool ShouldRenderSmall = true;

        GUIChiselProjector gui;
        float scaleMin => 8;
        float scaleMax => 32;
        float scaleStep => 8;
        float scaleDefault => 16;
        float rotationStep => GameMath.DEG2RAD * 15;
        float offsetStep => 1;
       
        ChiselProjectorRenderer renderer;
        

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api is ICoreClientAPI)
            {
                capi= api as ICoreClientAPI;
                renderer = new ChiselProjectorRenderer();
                capi.Event.RegisterRenderer(renderer,EnumRenderStage.Opaque);
                SetupRenderer();
            }
        }

        public void SetBigRenderer(bool shouldrenderbig)
        {
            ShouldRenderBig = shouldrenderbig;
        }

        public override void OnBlockRemoved()
        {
            renderer?.Dispose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            renderer?.Dispose();
            base.OnBlockBroken(byPlayer);
        }

        void SetupRenderer()
        {
            if (capi == null) { return; }
            if (renderer == null) { renderer = new ChiselProjectorRenderer(); }
            if (mycover == null) { mycover = this.GetBehavior<BEBChiseledCover>(); }
            if (mycover != null && mycover.ChiseledItemStack != null
                && mycover.ChiseledItemStack.Block != null && mycover.ChiseledItemStack.Block.Code.ToString() == "game:chiseledblock")
            {
                renderer.Setup(capi, this, mycover.ChiseledItemStack);
                renderer.ShouldRender = true;
            }
            else { renderer.ShouldRender = false; }
        }

        public void OpenGUI()
        {
            if (capi!=null)
            {
                gui?.Dispose();
                gui = null;
                gui = new GUIChiselProjector(capi, this); 
                gui.TryOpen();
                capi.World.PlaySoundAt(new AssetLocation("sounds/compcuteblip"), Pos.X, Pos.Y, Pos.Z);
            }
            SetupRenderer();
        }

        

        public void ResetRotation()
        {
            rotation = Vec3f.Zero;
            SetupRenderer();
            UpdateProjectorData();

        }
        


        public void AddRotation(Vec3f adjustment)
        {
            adjustment.X=Math.Sign(adjustment.X)*rotationStep;
            adjustment.Y = Math.Sign(adjustment.Y) * rotationStep;
            adjustment.Z = Math.Sign(adjustment.Z) * rotationStep;
            rotation += adjustment;
            SetupRenderer(); UpdateProjectorData();
        }

        public void SetRotation(Vec3f newrotation)
        {
            rotation = newrotation;
            SetupRenderer(); UpdateProjectorData();
        }

        public void AddOffset(Vec3f adjustment)
        {
            adjustment.X = Math.Sign(adjustment.X) * offsetStep;
            adjustment.Y = Math.Sign(adjustment.Y) * offsetStep;
            adjustment.Z = Math.Sign(adjustment.Z) * offsetStep;
            offset += adjustment;
            SetupRenderer(); UpdateProjectorData();
        }
        
        public void ResetOffset()
        {
            offset=Vec3f.Zero;
            SetupRenderer(); UpdateProjectorData();
        }

        public void SetOffset(Vec3f newoffset)
        {
            offset= newoffset;
            SetupRenderer(); UpdateProjectorData();
        }


        public void AddScale(float adjustment)
        {
            adjustment = Math.Sign(adjustment) * scaleStep;
            scale += adjustment;
            scale=Math.Clamp(scale, scaleMin, scaleMax);
            SetupRenderer(); UpdateProjectorData();
        }

        public void ResetScale()
        {
            scale = scaleDefault;
            SetupRenderer(); UpdateProjectorData();
        }

        public void SetScale(float newscale)
        {
            scale = newscale;
            SetupRenderer(); UpdateProjectorData();
        }

        public void CoverChanged()
        {
            SetupRenderer(); UpdateProjectorData();

        }

        public void SetGlow(int newglow)
        {
            glow = newglow;
            SetupRenderer(); UpdateProjectorData();
        }
        
        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid == UPDATEPACKETID)
            {
                ChiselProjectorData cpd = SerializerUtil.Deserialize<ChiselProjectorData>(data);
                if (cpd != null) {
                    SetProjectorData(cpd);
                    MarkDirty(true);
                    return;
                }
            }
            base.OnReceivedClientPacket(fromPlayer, packetid, data);
        }
        public void UpdateProjectorData()
        {
            if (capi == null) { return; }
            byte[] data = SerializerUtil.Serialize<ChiselProjectorData>(GetProjectorData());
            capi.Network.SendBlockEntityPacket(Pos, UPDATEPACKETID, data);
        }

        public void SetProjectorData(ChiselProjectorData cpd)
        {
            scale = cpd.scale;
            offset = cpd.offset;
            rotation = cpd.rotation;
            rotationOrigin = cpd.rotationOrigin;
            ShouldRenderBig = cpd.ShouldRenderBig;
            ShouldRenderSmall = cpd.ShouldRenderSmall;
            rgba = cpd.GetRGBA();
            glow = cpd.glow;
        }

        public ChiselProjectorData GetProjectorData()
        {
            ChiselProjectorData cpd = new ChiselProjectorData();
            cpd.scale = scale;
            cpd.offset = offset;
            cpd.rotation = rotation;
            cpd.ShouldRenderBig = ShouldRenderBig;
            cpd.ShouldRenderSmall= ShouldRenderSmall;
            cpd.r=rgba.R; cpd.g=rgba.G; cpd.b=rgba.B; cpd.a=rgba.A;
            cpd.glow = Glow;
            return cpd;
        }
        public bool SkipShapeRendering => true;
        public bool SkipCollisionBoxGeneration => true;

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            //tree.SetBool("triggerByAnyEntity", triggerByAnyEntity);
            byte[] data = SerializerUtil.Serialize(GetProjectorData());
            tree.SetBytes("projectordata", data);
            base.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            byte[] data = tree.GetBytes("projectordata", null);
            if (data != null)
            {
                try
                {
                    ChiselProjectorData cpd = SerializerUtil.Deserialize<ChiselProjectorData>(data);
                    if (cpd != null)
                    {
                        SetProjectorData(cpd);
                    }
                }
                catch { }
            }
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [Serializable]
    public class ChiselProjectorData
    {
        public float scale = 16;
        public Vec3f offset = Vec3f.Zero;
        public Vec3f rotation = Vec3f.Zero;
        public Vec3f rotationOrigin = Vec3f.Zero;
        public float r = 1;
        public float g = 1;
        public float b = 1;
        public float a = 0.5f;
        public int glow = 64;
        public Vec4f GetRGBA() {return new Vec4f(r, g, b, a); }
        public bool ShouldRenderBig = true;
        public bool ShouldRenderSmall = true;
    }
}
