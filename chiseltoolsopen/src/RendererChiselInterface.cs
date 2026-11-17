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
    public class RendererChiselInterface:IRenderer
    {
        public double RenderOrder => 1;

        public int RenderRange => 24;
        public bool ShouldRender = true;
        ICoreClientAPI capi;

        MultiTextureMeshRef meshref;
        BEChiselProjector projector;
        int textureId;
        ModelTransform transform;
        ModelTransform defaultTransform;
        ItemStack chiseleditemstack;
        Matrixf ModelMat = new Matrixf();

        float modelrotation = 0;
        float modelrotationrate = GameMath.DEG2RAD * 5;
        int flickercounter;
        int flickermod = 5;

        public void Setup(ICoreClientAPI capi, BEChiselProjector projector, ItemStack chiseleditemstack)
        {
            meshref?.Dispose();
            meshref = null;
            if (capi == null || projector == null || chiseleditemstack == null) { return; }
            this.capi = capi;
            this.projector = projector;
            this.chiseleditemstack = chiseleditemstack;


            transform = new ModelTransform().EnsureDefaultValues();
            defaultTransform = transform;
            MeshData meshdata = BEBChiseledCover.CreateModelFromChiseledItemStack(capi, chiseleditemstack);
            //capi.Tesselator.TesselateBlock(chiseleditemstack.Block, out meshdata);

            textureId = capi.ItemTextureAtlas.Positions[chiseleditemstack.Block.Textures.FirstOrDefault().Value.Baked.TextureSubId].atlasTextureId;
            meshref = capi.Render.UploadMultiTextureMesh(meshdata);
        }



        public void Dispose()
        {
            meshref?.Dispose();
            meshref = null;


        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshref == null || projector == null || !ShouldRender) { return; }
            if (!meshref.Initialized) { return; }
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            BlockPos pos = this.projector.Pos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);
            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = projector.RGBA;
            prog.ExtraGlow = projector.Glow;
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X + projector.Scale * 0.5f, pos.Y - camPos.Y, pos.Z - camPos.Z + projector.Scale * 0.5f)
                .RotateY(projector.Rotation.Y)
                .Translate(-projector.Scale * 0.5f, 0, -projector.Scale * 0.5f)
                .Scale(projector.Scale, projector.Scale, projector.Scale)

                .Values
            ;

            Matrixf adjustment = new Matrixf(rpi.CameraMatrixOriginf);
            adjustment.Translate(-projector.Scale / 2, 0, -projector.Scale - 1);
            adjustment.Translate(projector.Offset);
            prog.ViewMatrix = adjustment.Values;

            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            if (projector.ShouldRenderBig)
            {
                rpi.RenderMultiTextureMesh(meshref, "tex");
            }
            prog.ViewMatrix = rpi.CameraMatrixOriginf;

            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.RgbaTint = new Vec4f(0.25f, 1f, 0.25F, 0.5f);
            prog.ExtraGlow = 64;
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X + 0.5f, pos.Y - camPos.Y + 0.75f, pos.Z - camPos.Z - 0.5f)
                .RotateY(modelrotation)
                .Translate(-0.5f, 0, -0.5f)
                .Values
            ;
            if (projector.ShouldRenderSmall)
            {
                rpi.RenderMultiTextureMesh(meshref, "tex");
            }

            modelrotation += modelrotationrate * deltaTime;

            prog.Stop();
        }
    }
}
