Shader "Volumetric/Base"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
            Tags { "RenderType"="Transparent" "Queue"="Transparent+1" LightMode=ForwardBase }

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
            #include "Lighting.cginc"

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

            //TO DO LIST
            //Add perlin ontop
            //Make texture more advanced to help with tiling being visible at lower scales
            //Blue noise on starting position of raymarch
            //Henyey-Greenstein scattering
            //Add brighter highlights around sun
            //Steps are visible using current occlusion method
            //Fading result on edge of bounding box to avoid sharp cutoffs
            //Look at converting to while loop so that it can be broken out of and improve performance (performance is always same for same number of pixels, no matter the density of cloud)
            //Variable steplength to improve performance

            //Offload texture creation to GPU (VERY SLOW ATM)


            //Uniforms
            float3 boxmin;
            float3 boxmax;

            #define MAX_STEPS 64
            int view_steps;
            int light_steps;
            fixed4 _ShadowColor;
            float light_strength;
            float shadow_cutoff;

            sampler3D _CloudTexture;
            float world_tex_size;
            float3 cloud_scale;
            float3 cloud_offset;
            float cloud_coverage_threshold;

            //Globals
            float depthTextureDistance;

            void CalculateDepthTextureDistance(float2 uv){
                float3 viewPixelPos = viewSpacePosAtScreenUV(uv);
                float3 worldPixelPos = mul(unity_CameraToWorld, float4(viewPixelPos.xy, -viewPixelPos.z, 1.0)).xyz;
                depthTextureDistance = length(worldPixelPos - _WorldSpaceCameraPos);
            }

            bool OccludedByDepthTexture(float3 position){
                float distance = length(position - _WorldSpaceCameraPos);
                return (distance > depthTextureDistance);
            }

            //Manual ZTest since back faces are rendered but still want objects infront of the cube to block it
            void ManualZTestBox(float boxdst){
                clip(depthTextureDistance - boxdst);
            }

            float3 WorldSpaceToSamplePos(float3 pos){
                //centres texture on middle of volume
                float3 boxCentre = boxmin + ((boxmax - boxmin) / 2);
                float3 worldTexCornerMin = boxCentre - (float3(world_tex_size, world_tex_size, world_tex_size) / 2);
                float3 worldTexCornerMax = worldTexCornerMin + float3(world_tex_size, world_tex_size, world_tex_size);

                return (pos - worldTexCornerMin) / (worldTexCornerMax - worldTexCornerMin);
            }

            float SampleTexture(float3 worldPos){
                float3 sampleWorldPos = (worldPos * cloud_scale) + cloud_offset;
                float3 sampleTexPos = WorldSpaceToSamplePos(sampleWorldPos);
                float value = tex3D(_CloudTexture, frac(sampleTexPos));

                return max(0, value - cloud_coverage_threshold);
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

            float TakeLightSteps(float3 origin, float3 dir, float stepSize, int steps){
                float3 currentStepPos = origin;
                float3 stepVec = dir * stepSize;

                float totalDensity = 0;

                for(int i = 0; i < steps; i++){
                    float density  = SampleTexture(currentStepPos);
                    totalDensity += density * stepSize;

                    currentStepPos += stepVec;
                }

                float transmittance = exp(-totalDensity);
                return transmittance;
            }

            //Returns float2.x transmittance, float2.y lightEnergy
            //Transmittance is used for cloud opacity
            //LightEnergy is used in cloud color
            float2 TakeViewSteps(float3 origin, float3 dir, float stepSize, int steps){
                float3 currentStepPos = origin;
                float3 stepVec = dir * stepSize;

                float transmittance = 1;
                float lightEnergy = 0;

                //I can't break out of the loop since I get errors to do with texture sampling inside varying size for loop
                //This kind of works around it by just nullifying the results if they are being occluded, but they are still calculated...
                float isOccludedMultiplier = 1;

                for(int i = 0; i < steps; i++){
                    
                    isOccludedMultiplier = OccludedByDepthTexture(currentStepPos) ? 0 : 1;
                    //Checks the position to see if it is being occluded by depth texture 
                    
                    float density = SampleTexture(currentStepPos);

                    float3 lightDir = _WorldSpaceLightPos0.xyz;
                    float2 lightBoxInfo = RayBoxIntersect(boxmin, boxmax, currentStepPos, 1/lightDir);
                    float lightTransmittance = TakeLightSteps(currentStepPos, lightDir, lightBoxInfo.y / light_steps, light_steps);

                    //Was having trouble with this since its a +=
                    //Equation taken from https://github.com/SebLague/Clouds/blob/master/Assets/Scripts/Clouds/Shaders/Clouds.shader
                    lightEnergy += density * stepSize * transmittance * lightTransmittance * isOccludedMultiplier;

                    //*stepSize makes sure the density is normalized for steps
                    transmittance *= exp(-density * stepSize * isOccludedMultiplier);
                    
                    currentStepPos += stepVec;
                }

                return float2(transmittance, lightEnergy);
            }

            fixed4 frag (v2f i, UNITY_VPOS_TYPE vpos : VPOS) : SV_Target
            {
                float2 screenUV = vpos.xy / _ScreenParams.xy;

                //WorldSpaceViewDir points from object pixel towards camera
                float3 vecToPixel = -WorldSpaceViewDir(i.meshVertex);

                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(vecToPixel);

                float2 boxinfo = RayBoxIntersect(boxmin, boxmax, rayOrigin, 1/rayDir);

                CalculateDepthTextureDistance(screenUV);

                //Does initial clipping of pixel if the bounding box is occluded by opaque objects
                //Calls clip() so will avoid taking steps if it doesnt have to
                ManualZTestBox(boxinfo.x);

                float2 cloudInfo = TakeViewSteps(rayOrigin + rayDir * boxinfo.x, rayDir, boxinfo.y / view_steps, view_steps);
                float lightEnergy = cloudInfo.y;

                fixed3 highlightColor = _LightColor0.xyz;
                fixed3 shadowColor = _ShadowColor.xyz;

                //lightEnergy (and therefore lightEnergy * lightStrength) should be between 0-1 so it can be used to lerp between shadow and highlight colors
                //I think some improvements could be made to this lerp in order to improve stylisation

                fixed3 calculatedCloudColor = lerp(shadowColor, highlightColor, min(1, (lightEnergy * light_strength) + shadow_cutoff));

                //Basic highlight-black color
                //fixed3 lightedColor = highlightColor * lightEnergy * light_strength;

                //Opacity of cloud is soely dependant on the original rays density
                //https://www.diva-portal.org/smash/get/diva2:1223894/FULLTEXT01.pdf Section 3.2
                fixed opacity =  1 - cloudInfo.x;

                fixed4 col = fixed4(calculatedCloudColor, opacity);
                return col;
            }
            ENDCG
        }
    }
}
