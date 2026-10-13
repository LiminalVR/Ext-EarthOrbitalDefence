// Extracted from installed Unity6000.7 native Standard source; provenance.json is authoritative.
// Only symbols/types are source-prefixed. Lighting/SH/reflection inputs must be authenticated separately.
// BRDF2 was proved for this project's actual current Editor source shader variants; device selection remains separate.
#ifndef SOURCE166_NATIVE_BRDF_INCLUDED
#define SOURCE166_NATIVE_BRDF_INCLUDED
#ifndef SOURCE166_BRDF_GGX
    #ifdef UNITY_BRDF_GGX
        #define SOURCE166_BRDF_GGX UNITY_BRDF_GGX
    #else
        #define SOURCE166_BRDF_GGX 1
    #endif
#endif
#ifdef UNITY_COLORSPACE_GAMMA
    #define Source49_DielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)
#else
    #define Source49_DielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)
#endif

struct Source49Light
{
    half3 color;
    half3 dir;
    half  ndotl; // Deprecated: Ndotl is now calculated on the fly and is no longer stored. Do not used it.
};

struct Source49Indirect
{
    half3 diffuse;
    half3 specular;
};

float Source49_PerceptualRoughnessToRoughness(float perceptualRoughness)
{
    return perceptualRoughness * perceptualRoughness;
}

float Source49_SmoothnessToPerceptualRoughness(float smoothness)
{
    return (1 - smoothness);
}

inline half Source49_Pow4 (half x)
{
    return x*x*x*x;
}

inline half3 Source49_FresnelLerpFast (half3 F0, half3 F90, half cosA)
{
    half t = Source49_Pow4 (1 - cosA);
    return lerp (F0, F90, t);
}

inline half Source49_PerceptualRoughnessToSpecPower (half perceptualRoughness)
{
    half m = Source49_PerceptualRoughnessToRoughness(perceptualRoughness);   // m is the true academic roughness.
    half sq = max(1e-4f, m*m);
    half n = (2.0 / sq) - 2.0;                          // https://dl.dropboxusercontent.com/u/55891920/papers/mm_brdf.pdf
    n = max(n, 1e-4f);                                  // prevent possible cases of pow(0,0), which could happen when roughness is 1.0 and NdotH is zero
    return n;
}

inline float3 Source49_Unity_SafeNormalize(float3 inVec)
{
    float dp3 = max(0.001f, dot(inVec, inVec));
    return inVec * rsqrt(dp3);
}

inline half Source49_OneMinusReflectivityFromMetallic(half metallic)
{
    // We'll need oneMinusReflectivity, so
    //   1-reflectivity = 1-lerp(dielectricSpec, 1, metallic) = lerp(1-dielectricSpec, 0, metallic)
    // store (1-dielectricSpec) in Source49_DielectricSpec.a, then
    //   1-reflectivity = lerp(alpha, 0, metallic) = alpha + metallic*(0 - alpha) =
    //                  = alpha - metallic * alpha
    half oneMinusDielectricSpec = Source49_DielectricSpec.a;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

inline half3 Source49_DiffuseAndSpecularFromMetallic (half3 albedo, half metallic, out half3 specColor, out half oneMinusReflectivity)
{
    specColor = lerp (Source49_DielectricSpec.rgb, albedo, metallic);
    oneMinusReflectivity = Source49_OneMinusReflectivityFromMetallic(metallic);
    return albedo * oneMinusReflectivity;
}

half4 Source49_BRDF2_Unity_PBS (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
    float3 normal, float3 viewDir,
    Source49Light light, Source49Indirect gi)
{
    float3 halfDir = Source49_Unity_SafeNormalize (float3(light.dir) + viewDir);

    half nl = saturate(dot(normal, light.dir));
    float nh = saturate(dot(normal, halfDir));
    half nv = saturate(dot(normal, viewDir));
    float lh = saturate(dot(light.dir, halfDir));

    // Specular term
    half perceptualRoughness = Source49_SmoothnessToPerceptualRoughness (smoothness);
    half roughness = Source49_PerceptualRoughnessToRoughness(perceptualRoughness);

#if SOURCE166_BRDF_GGX

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155
    float a = roughness;
    float a2 = a*a;

    float d = nh * nh * (a2 - 1.f) + 1.00001f;
#ifdef UNITY_COLORSPACE_GAMMA
    // Tighter approximation for Gamma only rendering mode!
    // DVF = sqrt(DVF);
    // DVF = (a * sqrt(.25)) / (max(sqrt(0.1), lh)*sqrt(roughness + .5) * d);
    float specularTerm = a / (max(0.32f, lh) * (1.5f + roughness) * d);
#else
    float specularTerm = a2 / (max(0.1f, lh*lh) * (roughness + 0.5f) * (d * d) * 4);
#endif

    // on mobiles (where half actually means something) denominator have risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE)
    specularTerm = specularTerm - 1e-4f;
#endif

#else

    // Legacy
    half specularPower = Source49_PerceptualRoughnessToSpecPower(perceptualRoughness);
    // Modified with approximate Visibility function that takes roughness into account
    // Original ((n+1)*N.H^n) / (8*Pi * L.H^3) didn't take into account roughness
    // and produced extremely bright specular at grazing angles

    half invV = lh * lh * smoothness + perceptualRoughness * perceptualRoughness; // approx ModifiedKelemenVisibilityTerm(lh, perceptualRoughness);
    half invF = lh;

    half specularTerm = ((specularPower + 1) * pow (nh, specularPower)) / (8 * invV * invF + 1e-4h);

#ifdef UNITY_COLORSPACE_GAMMA
    specularTerm = sqrt(max(1e-4f, specularTerm));
#endif

#endif

#if defined (SHADER_API_MOBILE)
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularTerm = 0.0;
#endif

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(realRoughness^2+1)

    // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
    // 1-x^3*(0.6-0.08*x)   approximation for 1/(x^4+1)
#ifdef UNITY_COLORSPACE_GAMMA
    half surfaceReduction = 0.28;
#else
    half surfaceReduction = (0.6-0.08*perceptualRoughness);
#endif

    surfaceReduction = 1.0 - roughness*perceptualRoughness*surfaceReduction;

    half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
    half3 color =   (diffColor + specularTerm * specColor) * light.color * nl
                    + gi.diffuse * diffColor
                    + surfaceReduction * gi.specular * Source49_FresnelLerpFast (specColor, grazingTerm, nv);

    return half4(color, 1);
}

#endif
