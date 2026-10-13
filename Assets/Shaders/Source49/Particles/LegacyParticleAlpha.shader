// Derived from installed Unity6.7 Particle Premultiply Blend.shader (MIT).
Shader "Liminal/URP/49/LegacyParticleAlpha" {
Properties { _TintColor("Tint Color",Color)=(0.5,0.5,0.5,0.5) _MainTex("Particle Texture",2D)="white" {} _InvFade("Soft Particles Factor",Range(0.01,3.0))=1.0 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
Blend SrcAlpha OneMinusSrcAlpha ColorMask RGB Cull Off ZWrite Off
Pass { Tags { "LightMode"="SRPDefaultUnlit" }
HLSLPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#pragma multi_compile _ SOFTPARTICLES_ON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;float _InvFade;half4 _TintColor;
CBUFFER_END
struct Attributes { float4 positionOS:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings { float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;float4 screenPos:TEXCOORD1;float eyeDepth:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO };
Varyings vert(Attributes v){Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color*_TintColor;o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.screenPos=ComputeScreenPos(o.positionCS);o.eyeDepth=-TransformWorldToView(TransformObjectToWorld(v.positionOS.xyz)).z;return o;}
half4 frag(Varyings i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#if defined(SOFTPARTICLES_ON)
float sceneZ=LinearEyeDepth(SampleSceneDepth(i.screenPos.xy/i.screenPos.w),_ZBufferParams);float fade=saturate(_InvFade*(sceneZ-i.eyeDepth));i.color.a*=fade;
#endif
half4 col=2.0*i.color*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);col.a=saturate(col.a);return col; }
ENDHLSL
} } Fallback Off }
