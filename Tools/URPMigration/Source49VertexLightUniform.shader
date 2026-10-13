Shader "Hidden/49/SourceVertexLightUniform" {
Properties { _Probe("Readback selector",Float)=0 }
SubShader { Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fwdbase nolightmap
#include "C:/Program Files/Unity/Hub/Editor/6000.7.0b2/Editor/Data/Resources/CGIncludes/UnityCG.cginc"
float _Probe;
float4 vert(float4 v:POSITION):SV_POSITION{return UnityObjectToClipPos(v);}
float4 frag():SV_Target {
 if(_Probe<0.5)return unity_4LightAtten0;
 if(_Probe<1.5)return unity_4LightPosX0;
 if(_Probe<2.5)return unity_4LightPosY0;
 if(_Probe<3.5)return unity_4LightPosZ0;
 return float4(unity_LightColor[0].rgb,1);
}
ENDCG } } }
