Shader "Volumetric/Cloud"
{
    Properties
    {
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

            //Fixed distorted clipping issues thanks to this code
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
            //Henyey-Greenstein scattering
            //Add brighter highlights around sun
            //Steps are visible using current occlusion method
            //Fading result on edge of bounding box to avoid sharp cutoffs
            //Remap when changing shadows instead of cutoff (may improve clarity when using shadow cutoff)
            //Dynamic stepSize with step_inc needs to have a max implemented as perhaps backstep once it reaches a density != 0
            //Look more into blue noise offsets
            //Attempt to get working for point lights rather than just 1 directional light


            //Uniforms
            float3 boxmin;
            float3 boxmax;

            #define MAX_STEPS 64
            int view_steps;
            int light_steps;
            float step_inc;
            fixed4 _ShadowColor;
            float light_strength;
            float shadow_cutoff;

            sampler3D _CloudTexture;
            float world_tex_size;
            float3 cloud_scale;
            float3 cloud_offset;
            float cloud_coverage_threshold;

            sampler2D _CloudInfoTexture;

            sampler2D _BlueNoise;
            int noise_size;
            float noise_strength;

            float in_scatter_g;
            float out_scatter_g;
            float scatter_blend;

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

            float SampleDensity(float3 worldPos){
                //Textures needed
                //Coverage probability map
                //Height density probability
                //3D cloud textures for detail (combination of worley and perlin)

                //Sample coverage map
                //Combine with height density probability to get cloud probability map
                //Add perlin-worley ontop to form cloud shapes around density map

                float3 sampleWorldPos = (worldPos * cloud_scale) + cloud_offset;
                float4 sampleTexPos = float4(WorldSpaceToSamplePos(sampleWorldPos), 0);
                float4 wrappedSampleTexPos = frac(sampleTexPos);

                float cloudCoverageValue = max(0.5, tex2Dlod(_CloudInfoTexture, float4(wrappedSampleTexPos.xz, 0, 0)).r);
                float cloudHeightDensityValue = tex2Dlod(_CloudInfoTexture, float4(wrappedSampleTexPos.xy, 0, 0)).g;
                float cloudShapeValue = tex3Dlod(_CloudTexture, wrappedSampleTexPos).r;

                float value = cloudShapeValue * cloudCoverageValue * cloudHeightDensityValue;

                return saturate(value - cloud_coverage_threshold);
            }

            float SampleNoise(float2 screenUV){
                float2 uv = frac(screenUV / noise_size);
                float4 uv4 = float4(uv, 0, 0);

                float noise = tex2Dlod(_BlueNoise, uv4);
                return noise;
            }

            //Equation from
            //https://omlc.org/classroom/ece532/class3/hg.html
            float hgScatter(float cosA, float g){
                float g2 = g * g;
                return (0.5) * ((1 - g2) / pow(1 + g2 - 2 * g * cosA, 1.5));
            }
            
            //Only doing one hgScatter makes everything darker so we also apply an 
            //outscatter towards the sun in order to brighten up facing away from sun areas
            //Idea also used in https://www.diva-portal.org/smash/get/diva2:1223894/FULLTEXT01.pdf
            float phase(float cosA){
                float inScatterHG = hgScatter(cosA, in_scatter_g);
                float outScatterHG = hgScatter(cosA, -out_scatter_g);

                return lerp(inScatterHG, outScatterHG, scatter_blend);
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

            float TakeLightSteps(float3 origin, float3 dir, float maxRayLength, int steps){
                float3 currentStepPos = origin;
                float stepSize = maxRayLength / steps;
                float3 stepVec = dir * stepSize;

                float totalDensity = 0;

                for(int i = 0; i < steps; i++){
                    float density  = SampleDensity(currentStepPos);

                    totalDensity += density * stepSize;
                    currentStepPos += stepVec;
                }

                float transmittance = exp(-totalDensity);
                return transmittance;
            }

            //Returns float2.x transmittance, float2.y lightEnergy
            //Transmittance is used for cloud opacity
            //LightEnergy is used in cloud color
            float2 TakeViewSteps(float3 origin, float3 dir, float initialDst, float maxRayLength, int steps){
                float transmittance = 1;
                float lightEnergy = 0;

                float initialStepSize = maxRayLength / steps;

                float currentStepDst = initialDst;
                float currentStepSize = initialStepSize;

                // <= doesnt work here for some reason, while loop never finishes have no idea why currentStepDst would get stuck at maxRayLength
                while(currentStepDst < maxRayLength){
                    if(transmittance <= 0.005) break;

                    float3 densitySamplePos = origin + (dir * currentStepDst);

                    if(OccludedByDepthTexture(densitySamplePos)) break;

                    float density = SampleDensity(densitySamplePos);

                    //No cloud to sample so can skip light calculations and increase stepSize so that a density with clouds is hit sooner
                    if(density == 0){
                        currentStepSize += step_inc;
                        currentStepDst += currentStepSize;
                        continue;
                    }
                    
                    float3 lightDir = _WorldSpaceLightPos0.xyz;
                    float2 lightBoxInfo = RayBoxIntersect(boxmin, boxmax, densitySamplePos, 1/lightDir);
                    float lightTransmittance = TakeLightSteps(densitySamplePos, lightDir, lightBoxInfo.y, light_steps);

                    //Was having trouble with this since its rearranged from exp(-totalDensity)
                    //Equation taken from https://github.com/SebLague/Clouds/blob/master/Assets/Scripts/Clouds/Shaders/Clouds.shader
                    lightEnergy += density * currentStepSize * transmittance * lightTransmittance;

                    //stepSize makes sure the density is normalized for steps
                    transmittance *= exp(-density * currentStepSize);

                    currentStepSize = initialStepSize;
                    currentStepDst += currentStepSize;
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

                //Trying this, I don't think the results were really acceptable. It got rid of the visible layering you get from having a low sample rate but it was so overpowering.
                //I think there may be a way to afterwards, use the same noise to denoise the image but as is, the results are bad
                //float3 blueNoiseOffset = SampleBlue(vpos.xy * boxinfo.y);

                float cosA = dot(rayDir, _WorldSpaceLightPos0.xyz);
                float phaseValue = phase(cosA);

                //applying blue noise offset makes really large step sizes look noisy rather than distorted
                //noise is * by steplength so is more noticable on larger step lengths but that is where it helps the most
                float blueNoise = SampleNoise(vpos.xy) * 2 - 1;
                float stepSize = boxinfo.y / view_steps;
                float noiseOffset = stepSize * blueNoise * noise_strength;


                float2 cloudInfo = TakeViewSteps(rayOrigin + rayDir * boxinfo.x, rayDir, noiseOffset, boxinfo.y, view_steps);
                float lightEnergy = cloudInfo.y * phaseValue;

                fixed3 highlightColor = _LightColor0.xyz;
                fixed3 shadowColor = _ShadowColor.xyz;

                //lightEnergy (and therefore lightEnergy * lightStrength) should be between 0-1 so it can be used to lerp between shadow and highlight colors
                //I think some improvements could be made to this lerp in order to improve stylisation
                fixed3 calculatedCloudColor = lerp(shadowColor, highlightColor, min(1, (lightEnergy * light_strength) + shadow_cutoff));

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
