#ifndef UNIVERSAL_BRDF_INCLUDED
#define UNIVERSAL_BRDF_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/BSDF.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Deprecated.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ExposureFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDFData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.deprecated.hlsl"

#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)

half ReflectivitySpecular(half3 specular)
{
    return Max3(specular.r, specular.g, specular.b);
}

half OneMinusReflectivityMetallic(half metallic)
{
    // We'll need oneMinusReflectivity, so
    //   1-reflectivity = 1-lerp(dielectricSpec, 1, metallic) = lerp(1-dielectricSpec, 0, metallic)
    // store (1-dielectricSpec) in kDielectricSpec.a, then
    //   1-reflectivity = lerp(alpha, 0, metallic) = alpha + metallic*(0 - alpha) =
    //                  = alpha - metallic * alpha
    half oneMinusDielectricSpec = kDielectricSpec.a;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

half MetallicFromReflectivity(half reflectivity)
{
    half oneMinusDielectricSpec = kDielectricSpec.a;
    return (reflectivity - kDielectricSpec.r) / oneMinusDielectricSpec;
}

inline BRDFData InitializeBRDFDataDirect(half3 albedo, half3 diffuse, half3 specular, half reflectivity, half oneMinusReflectivity, half smoothness, half alpha, bool useAlphaPremultiply)
{
    BRDFData brdfData = (BRDFData)0;
    brdfData.albedo = albedo;
    brdfData.diffuse = diffuse;
    brdfData.specular = specular;
    brdfData.reflectivity = reflectivity;

    brdfData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    brdfData.roughness           = max(PerceptualRoughnessToRoughness(brdfData.perceptualRoughness), HALF_MIN_SQRT);
    brdfData.roughness2          = max(brdfData.roughness * brdfData.roughness, HALF_MIN);
    brdfData.grazingTerm         = saturate(smoothness + reflectivity);
    brdfData.normalizationTerm   = brdfData.roughness * half(4.0) + half(2.0);
    brdfData.roughness2MinusOne  = brdfData.roughness2 - half(1.0);

    // Input is expected to be non-alpha-premultiplied while ROP is set to pre-multiplied blend.
    // We use input color for specular, but (pre-)multiply the diffuse with alpha to complete the standard alpha blend equation.
    // In shader: Cs' = Cs * As, in ROP: Cs' + Cd(1-As);
    // i.e. we only alpha blend the diffuse part to background (transmittance).
    if (useAlphaPremultiply)
        brdfData.diffuse *= alpha; // TODO: would be clearer to multiply this once to accumulated diffuse lighting at end instead of the surface property.

    return brdfData;
}

// Initialize BRDFData for material, managing both specular and metallic setup using the isSpecularSetup flag (shader keyword _SPECULAR_SETUP).
inline BRDFData InitializeBRDFData(half3 albedo, half metallic, half3 specular, half smoothness, half alpha, bool isSpecularSetup, bool useAlphaPremultiply)
{
    half reflectivity;
    half oneMinusReflectivity;
    half3 brdfDiffuse;
    half3 brdfSpecular;
    if (isSpecularSetup)
    {
        reflectivity = ReflectivitySpecular(specular);
        oneMinusReflectivity = half(1.0) - reflectivity;
        brdfDiffuse = albedo * oneMinusReflectivity;
        brdfSpecular = specular;
    }
    else
    {
        oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
        reflectivity = half(1.0) - oneMinusReflectivity;
        brdfDiffuse = albedo * oneMinusReflectivity;
        brdfSpecular = lerp(kDielectricSpec.rgb, albedo, metallic);
    }

    return InitializeBRDFDataDirect(albedo, brdfDiffuse, brdfSpecular, reflectivity, oneMinusReflectivity, smoothness, alpha, useAlphaPremultiply);
}

inline BRDFData InitializeBRDFData(SurfaceData surfaceData, bool isSpecularSetup, bool useAlphaPremultiply)
{
    return InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, isSpecularSetup, useAlphaPremultiply);
}

half3 ConvertF0ForClearCoat15(half3 f0)
{
    return ConvertF0ForAirInterfaceToF0ForClearCoat15Fast(f0);
}

inline BRDFData InitializeBRDFDataClearCoat(half clearCoatMask, half clearCoatSmoothness, inout BRDFData baseBRDFData)
{
    BRDFData coatBRDFData = (BRDFData)0;
    coatBRDFData.albedo = half(1.0);

    // Calculate Roughness of Clear Coat layer
    coatBRDFData.diffuse             = kDielectricSpec.aaa; // 1 - kDielectricSpec
    coatBRDFData.specular            = kDielectricSpec.rgb;
    coatBRDFData.reflectivity        = kDielectricSpec.r;

    coatBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(clearCoatSmoothness);
    coatBRDFData.roughness           = max(PerceptualRoughnessToRoughness(coatBRDFData.perceptualRoughness), HALF_MIN_SQRT);
    coatBRDFData.roughness2          = max(coatBRDFData.roughness * coatBRDFData.roughness, HALF_MIN);
    coatBRDFData.normalizationTerm   = coatBRDFData.roughness * half(4.0) + half(2.0);
    coatBRDFData.roughness2MinusOne  = coatBRDFData.roughness2 - half(1.0);
    coatBRDFData.grazingTerm         = saturate(clearCoatSmoothness + kDielectricSpec.x);

    // Modify Roughness of base layer using coat IOR
    half ieta                        = lerp(1.0h, CLEAR_COAT_IETA, clearCoatMask);
    half coatRoughnessScale          = Sq(ieta);
    half sigma                       = RoughnessToVariance(PerceptualRoughnessToRoughness(baseBRDFData.perceptualRoughness));

    baseBRDFData.perceptualRoughness = RoughnessToPerceptualRoughness(VarianceToRoughness(sigma * coatRoughnessScale));

    // Recompute base material for new roughness, previous computation should be eliminated by the compiler (as it's unused)
    baseBRDFData.roughness          = max(PerceptualRoughnessToRoughness(baseBRDFData.perceptualRoughness), HALF_MIN_SQRT);
    baseBRDFData.roughness2         = max(baseBRDFData.roughness * baseBRDFData.roughness, HALF_MIN);
    baseBRDFData.normalizationTerm  = baseBRDFData.roughness * 4.0h + 2.0h;
    baseBRDFData.roughness2MinusOne = baseBRDFData.roughness2 - 1.0h;

    // Darken/saturate base layer using coat to surface reflectance (vs. air to surface)
    baseBRDFData.specular = lerp(baseBRDFData.specular, ConvertF0ForClearCoat15(baseBRDFData.specular), clearCoatMask);
    // TODO: what about diffuse? at least in specular workflow diffuse should be recalculated as it directly depends on it.

    return coatBRDFData;
}

// Placeholder for BRDFData that a disabled feature path leaves unused. The normalization term
// is the roughness-0 value (4 * roughness + 2) so a constant-folded disabled path cannot divide
// by zero in DirectBRDFSpecular, which the compiler reports as a warning.
BRDFData CreateEmptyBRDFData()
{
    BRDFData brdfData = (BRDFData)0;
    brdfData.normalizationTerm = half(2.0);
    return brdfData;
}

// Computes the specular term for EnvironmentBRDF
half3 EnvironmentBRDFSpecular(BRDFData brdfData, half fresnelTerm)
{
#if (UNITY_PLATFORM_META_QUEST) // This is platform specific change targeting performance only
    half surfaceReduction = half(1.0) / (brdfData.roughness2 + half(1.0));
#else
    float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0);
#endif
    return half3(surfaceReduction * lerp(brdfData.specular, brdfData.grazingTerm, fresnelTerm));
}

URP_LIGHT_ACCUM3 EnvironmentBRDF(BRDFData brdfData, URP_LIGHT_ACCUM3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm)
{
    URP_LIGHT_ACCUM3 c = indirectDiffuse * brdfData.diffuse;
    c += indirectSpecular * EnvironmentBRDFSpecular(brdfData, fresnelTerm);
    return c;
}

// Environment BRDF without diffuse for clear coat
half3 EnvironmentBRDFClearCoat(BRDFData brdfData, half clearCoatMask, half3 indirectSpecular, half fresnelTerm)
{
    return indirectSpecular * EnvironmentBRDFSpecular(brdfData, fresnelTerm) * clearCoatMask;
}

// Computes the scalar specular term for Minimalist CookTorrance BRDF
// NOTE: needs to be multiplied with reflectance f0, i.e. specular color to complete
float DirectBRDFSpecular(BRDFData brdfData, float3 normalWS, float3 lightDirectionWS, float3 viewDirectionWS)
{
    // Installed Standard BRDF2 GGX direct term. Genuine source roughness zero,
    // float d/lh, SafeNormalize .001 minimum, gamma branch, mobile clamp retained.
    float3 inVec=float3(lightDirectionWS)+float3(viewDirectionWS);
    float dp3=max(0.001f,dot(inVec,inVec));
    float3 halfDir=inVec*rsqrt(dp3);
    float nh=saturate(dot(float3(normalWS),halfDir));
    float lh=saturate(dot(float3(lightDirectionWS),halfDir));
    half perceptualRoughness=brdfData.perceptualRoughness;
    half roughness=perceptualRoughness*perceptualRoughness;
    float a=roughness;
    float a2=a*a;
    float d=nh*nh*(a2-1.f)+1.00001f;
#ifdef UNITY_COLORSPACE_GAMMA
    float specularTerm=a/(max(0.32f,lh)*(1.5f+roughness)*d);
#else
    float specularTerm=a2/(max(0.1f,lh*lh)*(roughness+0.5f)*(d*d)*4);
#endif
#if defined(SHADER_API_MOBILE)
    specularTerm=specularTerm-1e-4f;
    specularTerm=clamp(specularTerm,0.0,100.0);
#endif
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularTerm=0.0;
#endif
    return specularTerm;
}

// Based on Minimalist CookTorrance BRDF
// Implementation is slightly different from original derivation: http://www.thetenthplanet.de/archives/255
//
// * NDF [Modified] GGX
// * Modified Kelemen and Szirmay-Kalos for Visibility term
// * Fresnel approximated with 1/LdotH
half3 DirectBDRF(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS, bool specularHighlightsOff)
{
    // Can still do compile-time optimisation.
    // If no compile-time optimized, extra overhead if branch taken is around +2.5% on some untethered platforms, -10% if not taken.
    [branch] if (!specularHighlightsOff)
    {
        half specularTerm = DirectBRDFSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS);
        half3 color = brdfData.diffuse + specularTerm * brdfData.specular;
        return color;
    }
    else
        return brdfData.diffuse;
}

// Based on Minimalist CookTorrance BRDF
// Implementation is slightly different from original derivation: http://www.thetenthplanet.de/archives/255
//
// * NDF [Modified] GGX
// * Modified Kelemen and Szirmay-Kalos for Visibility term
// * Fresnel approximated with 1/LdotH
half3 DirectBRDF(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS, bool useSpecularHighlights)
{
    if (useSpecularHighlights)
        return brdfData.diffuse + DirectBRDFSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS) * brdfData.specular;
    return brdfData.diffuse;
}

#endif
