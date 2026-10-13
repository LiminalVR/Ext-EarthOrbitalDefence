
#include "Assets/Shaders/Source49/Stock/Source49PBRCommon.hlsl"
half4 Source49StockFrag(Source49Varyings i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 float2 uv=i.uv.xy*_BaseMap_ST.xy+_BaseMap_ST.zw;

 #if defined(_PARALLAXMAP)
 // Installed source UnityStandardInput.Parallax samples green then the source one-step offset.
 half h=SAMPLE_TEXTURE2D(_ParallaxMap,sampler_ParallaxMap,uv).g;
 h=h*_Parallax-_Parallax/2.0;
 half3 v=normalize(i.parallaxViewDirTS);v.z+=0.42;
 uv+=h*(v.xy/v.z);
 #endif
 SurfaceData surface;InitializeStandardLitSurfaceData(uv,surface);
 Source49Surface source=(Source49Surface)0;source.Albedo=surface.albedo;source.Normal=surface.normalTS;source.Emission=surface.emission;source.Specular=surface.specular;source.Metallic=surface.metallic;source.Smoothness=surface.smoothness;source.Occlusion=surface.occlusion;source.Alpha=surface.alpha;
 #if defined(_NORMALMAP)
 source.Normal=Source49_UnpackScaleNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale);
 #endif
 half3 color=Source49Evaluate(source,i);
 half alpha=1;
 #if defined(_SURFACE_TYPE_TRANSPARENT)
 alpha=source.Alpha;
 #if defined(_ALPHAPREMULTIPLY_ON)
 half oneMinusReflectivity=Source49_OneMinusReflectivityFromMetallic(source.Metallic);
 #if defined(_SPECULAR_SETUP)
 oneMinusReflectivity=1-max(max(source.Specular.r,source.Specular.g),source.Specular.b);
 #endif
 alpha=source.Alpha*oneMinusReflectivity+(1-oneMinusReflectivity);
 #endif
 #endif
 return half4(MixFog(color,i.fog),alpha);
}
