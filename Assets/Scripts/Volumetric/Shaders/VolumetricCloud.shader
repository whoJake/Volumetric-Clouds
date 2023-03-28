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

            //Variables
            float3 boxmin;
            float3 boxmax;

            #define MAX_STEPS 64
            int steps;

            sampler3D cloudTexture;
            float3 cloud_scale;
            float3 cloud_offset;


            //Manual ZTest since back faces are rendered but still want objects infront of the cube to block it
            void ManualZTest(float2 uv, float boxdst){
                float3 viewPixelPos = viewSpacePosAtScreenUV(uv);
                float3 worldPixelPos = mul(unity_CameraToWorld, float4(viewPixelPos.xy, -viewPixelPos.z, 1.0)).xyz;
                float worldPixelDist = length(worldPixelPos - _WorldSpaceCameraPos);

                clip(worldPixelDist - boxdst);
            }

            float3 WorldSpaceToSamplePos(float3 pos){
                return (pos - boxmin) / (boxmax - boxmin);
            }

            float SampleTexture(float3 worldPos){
                float3 sampleWorldPos = (worldPos * cloud_scale) + cloud_offset;
                float3 sampleTexPos = WorldSpaceToSamplePos(sampleWorldPos);
                float value = tex3D(cloudTexture, frac(sampleTexPos));

                return value;
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


            float TakeSteps(float3 origin, float3 dir, float stepSize, int steps){
                float3 currentStepPos = origin;
                float3 stepVec = dir * stepSize;

                float totalDensity = 0;

                for(int i = 0; i < steps; i++){
                    float density = SampleTexture(currentStepPos);
                    density = max(0, density - 0.25);

                    //*stepSize makes sure the density is normalized for steps
                    totalDensity += density * stepSize;

                    currentStepPos += stepVec;
                }

                return totalDensity;
            }

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
                //Ensure sample points are always the same even if inside the cube DONE
                //See if average opacity or additive (probably additive) DONE -> EXP
                //Add sun color
                //Add perlin ontop
                //Blue noise on starting position of raymarch
                //Lighting
                //Henyey-Greenstein scattering
                //Move this outside of the fragment function (ease to read) DONE

                float val = TakeSteps(rayOrigin + rayDir * boxinfo.x, rayDir, boxinfo.y / (float)steps, steps);

                //e^(-x) transformation
                float transmittance = exp(-val);
                
                fixed4 col = fixed4(1, 1, 1, 1 - transmittance);
                return col;
            }
            ENDCG
        }
    }
}
