using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hlc.ChillPrototype
{
    public sealed class ChillStoryRenderFeature : ScriptableRendererFeature
    {
        public Material effectMaterial;
        Pass pass;
        public override void Create() { pass = new Pass { material = effectMaterial, renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing }; }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (data.cameraData.cameraType != CameraType.Game || !effectMaterial || !ChillStoryEffects.Active) return;
            pass.renderer = renderer; renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { pass?.Release(); }
        class Pass : ScriptableRenderPass
        {
            public ScriptableRenderer renderer;
            public Material material;
            RenderTexture snapshot;
            bool captured;
            int temporary = Shader.PropertyToID("_ChillStoryTemporary");
            public void Release() { if (snapshot) { snapshot.Release(); Object.Destroy(snapshot); snapshot = null; } captured = false; }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                var effect = ChillStoryEffects.Active;
                if (!effect || !effect.HasEffect) { Release(); return; }
                var descriptor = data.cameraData.cameraTargetDescriptor; descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1;
                var source = renderer.cameraColorTargetHandle.nameID;
                var command = CommandBufferPool.Get("Chill story effects");
                command.GetTemporaryRT(temporary, descriptor, FilterMode.Bilinear);
                if (effect.frozen)
                {
                    if (!snapshot || snapshot.width != descriptor.width || snapshot.height != descriptor.height)
                    { Release(); snapshot = new RenderTexture(descriptor); snapshot.Create(); }
                    if (!captured) { command.Blit(source, snapshot); captured = true; }
                    command.Blit(snapshot, temporary);
                }
                else { Release(); command.Blit(source, temporary); }
                material.SetVector("_ChillEffects", new Vector4(effect.glitch, effect.monochrome, effect.chromatic, effect.blackout));
                material.SetVector("_ChillExtra", new Vector4(effect.shake, effect.flash, Time.unscaledTime, 0));
                command.Blit(temporary, source, material);
                command.ReleaseTemporaryRT(temporary);
                context.ExecuteCommandBuffer(command); CommandBufferPool.Release(command);
            }
        }
    }
}
