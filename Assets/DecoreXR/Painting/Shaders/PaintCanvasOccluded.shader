// The paint canvas shader: an unlit, transparent quad that is occluded by the real world.
//
// Structurally this is Meta's own EnvironmentDepth/URP/OcclusionUnlit reference
// (com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/OcclusionUnlit.shader), kept deliberately
// close to it because that shader is the version Meta validates on Quest and the META_DEPTH_*
// macros are documented as order- and name-sensitive. What differs here, and only this:
//   - transparent render state, because an unpainted texel is alpha 0 and the real wall has to
//     show through it (ADR 0003: the canvas is a texture of what has been painted, not a skin
//     over the whole wall);
//   - an inspector-exposed _OcclusionBias, since ADR 0005 pairs the quad's ~5mm z-offset with a
//     depth bias and M4-T2 has to tune that number on device (ADR 0011).
//
// When neither occlusion keyword is set — which is what QualityBudgetConfig.occlusionEnabled
// being false leaves us with (ADR 0004/0012) — Meta's macros compile down to a no-op and this
// draws as a plain unlit transparent quad. Occlusion off is therefore a real config state, not a
// broken shader.
Shader "DecoreXR/Paint Canvas Occluded"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Paint Canvas", 2D) = "black" {}

        // Positive values pull the paint towards the viewer before it is tested against the
        // environment depth map, i.e. make it harder for the wall to occlude its own paint.
        //
        // Some bias is not optional. The quad's 5mm offset stops it z-fighting the wall, but the
        // depth map's own error at wall distances is far larger than 5mm, so with zero bias a wall
        // routinely measures nearer than the paint sitting on it and the paint flickers out
        // against the very surface it belongs to.
        //
        // 0.1 is where Meta's own depth-mask bias sits in this SDK, used here as a starting point
        // rather than a measured answer: the value that survives on hardware is M4-T2's to find
        // (ADR 0005, ADR 0011).
        _OcclusionBias("Occlusion Bias", Range(0, 1)) = 0.1
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }

        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Meta's blend, kept verbatim: the second (alpha) term is what makes this composite
        // correctly against the transparent passthrough background rather than over black.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        // Transparent paint contributes no depth. Occlusion here is decided in the fragment shader
        // from the environment depth map, not from the depth buffer, so writing depth would only
        // let one canvas wrongly reject another where two walls meet at a corner.
        ZWrite Off

        // The quad is built facing +Z, i.e. out of the wall (see PaintCanvas.BuildQuad), so the
        // back face is inside the wall and there is nothing to draw there.
        Cull Back

        Pass
        {
            Name "PaintCanvasForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // The keywords EnvironmentDepthManager sets globally once depth textures arrive. Both
            // off is the valid "occlusion disabled" variant.
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                // Adds the world position the occlusion maths needs, at the next free TEXCOORD.
                META_DEPTH_VERTEX_OUTPUT(1)

                UNITY_VERTEX_INPUT_INSTANCE_ID
                // Which eye is being rendered, so the fragment shader samples the right depth
                // texture. Wrong eye means paint that occludes correctly in one eye only.
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _OcclusionBias;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.vertex.xyz);

                META_DEPTH_INITIALIZE_VERTEX_OUTPUT(output, input.vertex);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 finalColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Multiplies finalColor by the environment's visibility at this pixel — alpha
                // included, which is why this works on a transparent surface: a pixel the real
                // world covers ends up fully transparent rather than blended black.
                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY(input, finalColor, _OcclusionBias);

                return finalColor;
            }

            ENDHLSL
        }
    }

    // No fallback. Falling back to a lit or opaque shader on a device that cannot compile this
    // would paint every wall a flat unoccluded colour and look like a working feature, which is
    // worse than a magenta quad that says the shader failed (architecture §8.5).
}
