
// Actual native CG source is full precision on D3D11; short GPU proof authenticates this adapter.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.deprecated.hlsl"
#include "Assets/Shaders/Source49/Stock/Source49NativeBRDF2.hlsl"
half3 Source49_UnpackScaleNormalRGorAG(half4 packednormal, half bumpScale)
{
    #if defined(UNITY_NO_DXT5nm)
        half3 normal = packednormal.xyz * 2 - 1;
        #if (SHADER_TARGET >= 30)
            // SM2.0: instruction count limitation
            // SM2.0: normal scaler is not supported
            normal.xy *= bumpScale;
        #endif
        return normal;
    #elif defined(UNITY_ASTC_NORMALMAP_ENCODING)
        half3 normal;
        normal.xy = (packednormal.wy * 2 - 1);
        normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
        normal.xy *= bumpScale;
        return normal;
    #else
        // This do the trick
        packednormal.x *= packednormal.w;

        half3 normal;
        normal.xy = (packednormal.xy * 2 - 1);
        #if (SHADER_TARGET >= 30)
            // SM2.0: instruction count limitation
            // SM2.0: normal scaler is not supported
            normal.xy *= bumpScale;
        #endif
        normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
        return normal;
    #endif
}
half3 Source49_UnpackScaleNormal(half4 packednormal, half bumpScale)
{
    return Source49_UnpackScaleNormalRGorAG(packednormal, bumpScale);
}
half3 Source49_BlendNormals(half3 n1, half3 n2)
{
    return normalize(half3(n1.xy + n2.xy, n1.z*n2.z));
}
float4 _EarthOrbitalSourceIndirectSpecColor;
struct Source49Surface {half3 Albedo,Normal,Emission,Specular;half Metallic,Smoothness,Occlusion,Alpha;};
struct Source49Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 uv1:TEXCOORD1;float2 uv2:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct Source49Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float4 uv:TEXCOORD3;half3 vertexSH:TEXCOORD4;float fog:TEXCOORD5;half4 eyeVec:TEXCOORD6;float4 lightmapUV:TEXCOORD7;half3 parallaxViewDirTS:TEXCOORD8;UNITY_VERTEX_OUTPUT_STEREO};
Source49Varyings Source49Vert(Source49Attributes v) {Source49Varyings o=(Source49Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.tangentWS=half4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());o.uv=float4(v.uv,v.uv1);o.lightmapUV.xy=v.uv1*unity_LightmapST.xy+unity_LightmapST.zw;o.lightmapUV.zw=v.uv2*unity_DynamicLightmapST.xy+unity_DynamicLightmapST.zw;o.vertexSH=SHEvalLinearL2(o.normalWS,unity_SHBr,unity_SHBg,unity_SHBb,unity_SHC);o.fog=ComputeFogFactor(o.positionCS.z);o.eyeVec=half4(o.positionWS-_WorldSpaceCameraPos.xyz,o.positionWS.y);half3 binormal=cross(v.normalOS,v.tangentOS.xyz)*v.tangentOS.w;half3x3 rotation=half3x3(v.tangentOS.xyz,binormal,v.normalOS);o.parallaxViewDirTS=mul(rotation,TransformWorldToObject(_WorldSpaceCameraPos.xyz)-v.positionOS.xyz);return o;}
half3 Source49Evaluate(Source49Surface s,Source49Varyings i) {
 half3 n=normalize(TransformTangentToWorld(s.Normal,half3x3(i.tangentWS.xyz,cross(i.normalWS,i.tangentWS.xyz)*i.tangentWS.w,i.normalWS)));
 half3 view=-normalize(i.eyeVec.xyz);half3 spec;half oneMinusReflectivity;half3 diff;
 #if defined(_SPECULAR_SETUP)
 spec=s.Specular;oneMinusReflectivity=1-max(max(spec.r,spec.g),spec.b);diff=s.Albedo*oneMinusReflectivity;
 #else
 diff=Source49_DiffuseAndSpecularFromMetallic(s.Albedo,s.Metallic,spec,oneMinusReflectivity);
 #endif
 #if defined(_ALPHAPREMULTIPLY_ON)
 diff*=s.Alpha;
 #endif
 Source49Indirect gi;// Installed source Standard high-LOD full ForwardBase uses L2 per vertex and L0/L1 per pixel.
 gi.diffuse=max(half3(0,0,0),i.vertexSH+SHEvalLinearL0L1(n,unity_SHAr,unity_SHAg,unity_SHAb));
 #if defined(UNITY_COLORSPACE_GAMMA)
 gi.diffuse=LinearToSRGB(gi.diffuse);
 #endif
 if(LightmapAvailable()||DynamicLightmapAvailable()){
  gi.diffuse=SampleLightmap(i.lightmapUV.xy,i.lightmapUV.zw,n);

 }
 gi.diffuse*=s.Occlusion;
 gi.specular=GlossyEnvironmentReflection(reflect(-view,n),i.positionWS,1-s.Smoothness,s.Occlusion,GetNormalizedScreenSpaceUV(i.positionCS),true);
 #if defined(_GLOSSYREFLECTIONS_OFF) || defined(_ENVIRONMENTREFLECTIONS_OFF)
 gi.specular=_EarthOrbitalSourceIndirectSpecColor.rgb*s.Occlusion;
 #endif
 Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));Source49Light nativeLight;nativeLight.dir=main.direction;nativeLight.color=main.color*(main.distanceAttenuation*main.shadowAttenuation);
 half3 color=Source49_BRDF2_Unity_PBS(diff,spec,oneMinusReflectivity,s.Smoothness,n,view,nativeLight,gi).rgb;
 #if defined(_ADDITIONAL_LIGHTS)
 Source49Indirect zeroGI;zeroGI.diffuse=0;zeroGI.specular=0;uint count=GetAdditionalLightsCount();LIGHT_LOOP_BEGIN(count)
 Light l=GetAdditionalLight(lightIndex,i.positionWS);nativeLight.dir=l.direction;nativeLight.color=l.color*(l.distanceAttenuation*l.shadowAttenuation);color+=Source49_BRDF2_Unity_PBS(diff,spec,oneMinusReflectivity,s.Smoothness,n,view,nativeLight,zeroGI).rgb;
 LIGHT_LOOP_END
 #endif
 return color+s.Emission;
}
