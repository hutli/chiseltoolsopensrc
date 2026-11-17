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
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common;

namespace chiseltools.src
{
    public class EntityDecoBehavior : EntityBehavior, IRenderer
    {
        public int RenderRange => 24;
        ICoreClientAPI capi;
        MeshRef meshRef = null;
        MultiTextureMeshRef mtmeshRef = null;
        ItemStack modelstack = null;
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            
            if (entity.Api is ICoreClientAPI)
            {
                capi=entity.Api as ICoreClientAPI;
                capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
                
            }
        }

        

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            if (capi != null)
            {
                if (modelstack != null && meshRef == null) { GenMesh(); }
                Vec3d camPos = capi.World.Player.Entity.CameraPos;
                translationtocamera = new Vec3d(entity.Pos.X - camPos.X, entity.Pos.Y - camPos.Y, entity.Pos.Z - camPos.Z);
                entityyaw=entity.Pos.Yaw;
            }
        }
        public Matrixf ModelMat = new Matrixf();
        Vec3d translationtocamera = Vec3d.Zero;
        float entityyaw = 0;
        public virtual void OnRenderFrame(float dt, EnumRenderStage stage) {
            if (capi == null||(capi.World.Player.CameraMode==EnumCameraMode.FirstPerson&&capi.World.Player.Entity==this.entity)) return;
            if (meshRef == null || meshRef.Disposed) return;
            //AttachmentPointAndPose apap = entity.AnimManager?.Animator?.GetAttachmentPointPose("Eyes");
            ElementPose epose =entity.AnimManager?.Animator?.GetPosebyName("Neck");
            if (epose == null)
            {
                return;
            }
            IRenderAPI rpi = capi.Render;

            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            //Vec3d translationtocamera = new Vec3d(entity.Pos.X - camPos.X, entity.Pos.Y - camPos.Y, entity.Pos.Z - camPos.Z);
            IStandardShaderProgram prog = null;
            prog = rpi.PreparedStandardShader(entity.Pos.AsBlockPos.X, entity.Pos.AsBlockPos.Y, entity.Pos.AsBlockPos.Z);
            prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
            Vec3f[] wts = epose.AnimModelMatrix.ToVec3fs();
            Vec3f wt = wts[4];
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(translationtocamera.X, translationtocamera.Y, translationtocamera.Z)
                        .RotateY(entityyaw)
                            //.RotateY(90 * GameMath.DEG2RAD)
                            .Translate(wt.X-0.95 , wt.Y-.1 , wt.Z-.9)
                            .RotateX((float)epose.degX * GameMath.DEG2RAD)
                            .RotateY((float)epose.degY * GameMath.DEG2RAD)
                            .RotateZ((float)epose.degZ * GameMath.DEG2RAD)
                            .Values
                 ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            rpi.RenderMesh(meshRef);
            prog.Stop();
        }

        //TODO
        // - add this behavior to player
        // - some way to transfer shape
        // - save the saved shape
        // - find the appropriate pose and offset
        // - render the shape
        public virtual void GenMesh(bool donotload = false)
        {
            if (capi == null) { return; }
            if (!donotload)
            {
                modelstack = entity.WatchedAttributes.GetItemstack("shapestack");
            }
            if (modelstack == null) {
                meshRef?.Dispose();
                mtmeshRef?.Dispose();
                return;
            }
            modelstack.ResolveBlockOrItem(capi.World);
            meshRef?.Dispose();
            mtmeshRef?.Dispose();
            MeshData mesh = null;
            //capi?.Tesselator.TesselateBlock(capi.World.GetBlock(new AssetLocation("chiseltools:deeps-880000")), out mesh);
            mesh = BEBChiseledCover.CreateModelFromChiseledItemStack(capi,modelstack);
            if (mesh==null)
            {
                return;
            }
            meshRef = capi.Render.UploadMesh(mesh);
        }

        public EntityDecoBehavior(Entity entity) : base(entity)
        {
        }
        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            modelstack = entity.WatchedAttributes?.GetItemstack("shapestack");
        }
        public void Dispose()
        {
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            meshRef?.Dispose();
            mtmeshRef?.Dispose();
        }
        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            if (packetid == 87654) {
                
                modelstack=new ItemStack(data) ;
                GenMesh(true);
                if (modelstack != null)
                {
                    entity.WatchedAttributes.SetItemstack("shapestack", modelstack);
                }
                handled = EnumHandling.Handled;
            }
            else if (packetid == 87655)
            {

                modelstack =null;
                GenMesh(true);
                if (modelstack != null)
                {
                    entity.WatchedAttributes.RemoveAttribute("shapestack");
                }
                handled = EnumHandling.Handled;
            }
            base.OnReceivedServerPacket(packetid, data, ref handled);
        }
        
        public double RenderOrder
        {
            get { return 0.5; }
        }
        public override string PropertyName()
        {
            return "entitydeco";
        }
    }
}
