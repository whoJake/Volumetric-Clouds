Shader "Volumetric/Base"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
            Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            //Fixed clipping issues thanks to this code
            //I dont really understand it but thank god it exists
            //https://forum.unity.com/threads/understanding-worldspaceviewdir-incorrect-weird-values.1272374/
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize;
            float getRawDepth(float2 uv) { return SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(uv, 0.0, 0.0)); }
     
            // inspired by keijiro's depth inverse projection
            // https://github.com/keijiro/DepthInverseProjection
            // constructs view space ray at the far clip plane from the screen uv
            // then multiplies that ray by the linear 01 depth
            float3 viewSpacePosAtScreenUV(float2 uv)
            {
                float3 viewSpaceRay = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 1.0, 1.0) * _ProjectionParams.z);
                float rawDepth = getRawDepth(uv);
                return viewSpaceRay * Linear01Depth(rawDepth);
            }
            //-------------------------------------------------------------------
     

            struct v2f
            {
                //World
                float4 clipVertex : TEXCOORD0;
                float4 meshVertex : TEXCOORD1;
            };


            v2f vert (float4 vertex : POSITION, out float4 outpos : SV_POSITION)
            {
                v2f o;
                o.clipVertex = UnityObjectToClipPos(vertex);
                outpos = o.clipVertex;
                o.meshVertex = vertex;
                return o;
            }

            //Manual ZTest since back faces are rendered but still want objects infront of the cube to block it
            void ManualZTest(float2 uv, float boxdst){
                float3 viewPixelPos = viewSpacePosAtScreenUV(uv);
                float3 worldPixelPos = mul(unity_CameraToWorld, float4(viewPixelPos.xy, -viewPixelPos.z, 1.0)).xyz;
                float worldPixelDist = length(worldPixelPos - _WorldSpaceCameraPos);

                clip(worldPixelDist - boxdst);
            }

            //Returns (distToBox, dstThroughBox) taken from https://github.com/SebLague/Clouds/blob/master/Assets/Scripts/Clouds/Shaders/Clouds.shader
            //Which was adapted from http://jcgt.org/published/0007/03/04/
            float2 RayBoxIntersect(float3 boxmin, float3 boxmax, float3 rayOrigin, float3 invRayDir){
                float3 t0 = (boxmin - rayOrigin) * invRayDir;
                float3 t1 = (boxmax - rayOrigin) * invRayDir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                //ray miss : dstA > dstB
                //inside box : distA < 0 and distB > 0
                //outside box : distA > 0 and distB > 0

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

            
            
            float3 boxmin;
            float3 boxmax;
            #define MAX_STEPS 64
            int steps;

            float3 WorldSpaceToSamplePos(float3 pos){
                return (pos - boxmin) / (boxmax - boxmin);
            }

            sampler3D cloudTexture;

            fixed4 frag (v2f i, UNITY_VPOS_TYPE vpos : VPOS) : SV_Target
            {
                float2 screenUV = vpos.xy / _ScreenParams.xy;

                //WorldSpaceViewDir points from object pixel towards camera
                float3 vecToPixel = -WorldSpaceViewDir(i.meshVertex);

                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(vecToPixel);

                float2 boxinfo = RayBoxIntersect(boxmin, boxmax, rayOrigin, 1/rayDir);
                ManualZTest(screenUV, boxinfo.x);

                //TODO
                //Ensure sample points are always the same even if inside the cube
                //See if average opacity or additive (probably additive)
                //Add perlin ontop
                //Blue noise
                //Lighting
                //Refraction
                //Move this outside of the fragment function (ease to read)
                float3 sampleWorldPos = _WorldSpaceCameraPos + (rayDir * boxinfo.x);
                steps = min(min(2, steps), MAX_STEPS);

                float stepLength = boxinfo.y / steps;
                float opacity = 0;

                for(int step = 0; step < steps + 1; step++){
                    float3 samplePos = WorldSpaceToSamplePos(sampleWorldPos);
                    float sampleDensity = tex3D(cloudTexture, samplePos);
                    sampleDensity = max(0, sampleDensity - 0.2f);
                    opacity += sampleDensity;

                    sampleWorldPos += rayDir * stepLength;
                }

                float maxDst = length(boxmax - boxmin);

                float val = boxinfo.y / maxDst;
                
                fixed4 col = fixed4(1, 1, 1, opacity / steps);
                return col;
            }
            ENDCG
        }
    }
}
