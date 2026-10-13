#ifndef UNIVERSAL_PARTICLES_FORWARD_LIT_PASS_INCLUDED
#define UNIVERSAL_PARTICLES_FORWARD_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesLitInput.hlsl"
#define SOURCE49_PARTICLE_NO_FORWARDADD 1
#include "Assets/Shaders/Source49/Particles/Source49ParticlePBRCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DistanceFog.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesLitFeatures.hlsl"

// Realtime shadows are never sampled when Receive Shadows is off at compile time;
// drop the vertex shadow-coord interpolator.
#if _RECEIVE_SHADOWS_OFF_STATICALLY_ENABLED
    #undef USE_VERTEX_SHADOW_COORD_INTERPOLATOR
    #define USE_VERTEX_SHADOW_COORD_INTERPOLATOR 0
#endif

void InitializeInputData(VaryingsParticle input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.preExposureMultiplier = GetPreExposureMultiplier();

    inputData.positionWS = input.positionWS.xyz;

#if FEATURES_NORMALMAP
    half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
    if (UseNormalMap())
    {
        inputData.tangentToWorld = half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
    }
    else
    {
        inputData.normalWS = input.normalWS.xyz;
    }
#else
    half3 viewDirWS = input.viewDirWS;
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS, UseNormalMap());

    viewDirWS = SafeNormalize(viewDirWS);

    inputData.viewDirectionWS = viewDirWS;

#if USE_VERTEX_SHADOW_COORD_INTERPOLATOR
    inputData.shadowCoord = ShadowCoordInterpolatorAvailable() ? input.shadowCoord : TransformWorldToShadowCoord(inputData.positionWS, IsSurfaceTypeTransparent());
#else
    inputData.shadowCoord = MainLightShadowsAvailable() ? TransformWorldToShadowCoord(inputData.positionWS, IsSurfaceTypeTransparent()) : float4(0, 0, 0, 0);
#endif

    inputData.vertexLighting = half3(0.0h, 0.0h, 0.0h);
#if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.clipPos.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SampleSHPixel(input.vertexSH, inputData.normalWS);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.clipPos);
    inputData.shadowMask = half4(1, 1, 1, 1);

#if defined(DEBUG_DISPLAY) && defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
#endif

    #if defined(DEBUG_DISPLAY) && !defined(PARTICLES_EDITOR_META_PASS)
    inputData.vertexSH = input.vertexSH;
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

VaryingsParticle ParticlesLitVertex(AttributesParticle input)
{
    VaryingsParticle output = (VaryingsParticle)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetParticleVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetParticleVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    URP_LIGHT_ACCUM3 vertexLight = VertexLighting(vertexInput.positionWS, half3(normalInput.normalWS));
    half fogFactor = 0.0;

#if FEATURES_NORMALMAP
    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
#else
    output.normalWS = half3(normalInput.normalWS);
    output.viewDirWS = viewDirWS;
#endif

    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

    output.vertexSH=SHEvalLinearL2(normalInput.normalWS,unity_SHBr,unity_SHBg,unity_SHBb,unity_SHC);
#if defined(_ADDITIONAL_LIGHTS)
    // Original Standard Surface is noforwardadd: additional point diffuse travels per vertex.
    uint pointCount=min(GetAdditionalLightsCount(),4u);
    for(uint pointIndex=0;pointIndex<pointCount;++pointIndex){
        Light lamp=GetAdditionalLight(pointIndex,vertexInput.positionWS);
        uint nativeIndex=GetPerObjectLightIndex(pointIndex);
        #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
        float3 delta=_AdditionalLightsBuffer[nativeIndex].position.xyz-vertexInput.positionWS;
        float inverseRangeSqr=_AdditionalLightsBuffer[nativeIndex].attenuation.x;
        #else
        float3 delta=_AdditionalLightsPosition[nativeIndex].xyz-vertexInput.positionWS;
        float inverseRangeSqr=_AdditionalLightsAttenuation[nativeIndex].x;
        #endif
        // Native49source uniform readback gives unity_4LightAtten0.x=.01 for actual range50:
        // 25/2500. Match Shade4PointLights reciprocal, without URP range-edge fade.
        float vertexAtten=rcp(1.0+max(dot(delta,delta),0.000001)*25.0*inverseRangeSqr);
        output.vertexSH+=lamp.color*vertexAtten*saturate(dot(normalInput.normalWS,lamp.direction));
    }
#endif
    output.positionWS.xyz = vertexInput.positionWS;
    output.positionWS.w = fogFactor;
    output.clipPos = vertexInput.positionCS;
    output.color = GetParticleColor(input.color);

#if defined(_FLIPBOOKBLENDING_ON)
#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
    GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords.xyxy, 0.0);
#else
    GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords, input.texcoordBlend);
#endif
#else
    GetParticleTexcoords(output.texcoord, input.texcoords.xy);
#endif

#if defined(_SOFTPARTICLES_ON) || defined(_FADING_ON) || defined(_DISTORTION_ON)
    output.projectedPosition = vertexInput.positionNDC;
#endif

#if USE_VERTEX_SHADOW_COORD_INTERPOLATOR
    output.shadowCoord = ShadowCoordInterpolatorAvailable() ? GetShadowCoord(vertexInput, IsSurfaceTypeTransparent()) : float4(0, 0, 0, 0);
#endif

    return output;
}

half4 ParticlesLitFragment(VaryingsParticle input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    ParticleParams particleParams;
    InitParticleParams(input, particleParams);

    SurfaceData surfaceData;
    InitializeParticleLitSurfaceData(particleParams, surfaceData,
        UseAlphaPremultiply(), UseMetallicSpecGlossMap(), UseEmission(),
        UseNormalMap(), UseAlphaModulate());

#if defined(_FADING_ON)
    surfaceData.emission *= CameraFade(CAMERA_NEAR_FADE,CAMERA_INV_FADE_DISTANCE,particleParams.projectedPosition);
#endif
#if defined(_SOFTPARTICLES_ON)
    surfaceData.emission *= SoftParticles(SOFT_PARTICLE_NEAR_FADE,SOFT_PARTICLE_INV_FADE_DISTANCE,particleParams);
#endif
    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA_FOR_TEX(inputData, input.texcoord, _BaseMap);

    Source49Surface source=(Source49Surface)0;
    source.Albedo=surfaceData.albedo;source.Normal=half3(0,0,1);source.Emission=surfaceData.emission;source.Metallic=surfaceData.metallic;source.Smoothness=surfaceData.smoothness;source.Occlusion=surfaceData.occlusion;source.Alpha=surfaceData.alpha;
    Source49Varyings native=(Source49Varyings)0;
    native.positionWS=inputData.positionWS;native.positionCS=input.clipPos;native.normalWS=inputData.normalWS;
    native.tangentWS=half4(normalize(cross(abs(inputData.normalWS.y)<0.99?half3(0,1,0):half3(1,0,0),inputData.normalWS)),1);
    native.eyeVec=half4(-inputData.viewDirectionWS,0);native.vertexSH=input.vertexSH;
    URP_LIGHT_ACCUM4 color=half4(Source49Evaluate(source,native),surfaceData.alpha);
    color.rgb = ClampExposed(inputData.preExposureMultiplier * BlendDistanceFog(color.rgb, input.clipPos));
#if defined(_TRANSPARENT_RECEIVE_FOG)
    if (IsSurfaceTypeTransparent())
        color.rgb = MixVolumetricFog(color.rgb, color.a, _Blend, UseAlphaPremultiply(), input.clipPos);
#endif
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());

    return color;
}

#endif // UNIVERSAL_PARTICLES_FORWARD_LIT_PASS_INCLUDED
