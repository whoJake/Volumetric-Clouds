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
            //Steps are visible using current occlusion method
            //Fading result on edge of bounding box to avoid sharp cutoffs
            //Attempt to get working for point lights rather than just 1 directional light
            //Investigate fps problems at higher coverage values
            //Make further tweeks to variables and clean up files a bit
            //Add different kinds of wind movement such as swirling
            //See if adjustments can be made in regards to density as it seems a bit off atm


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

            float3 wind_speed;
            float3 disturbance_speed;

            float density_modifier;
            float coverage_modifier;
            float shape_modifier;
            float noise_to_drawn_blend;

            float3 cloud_detail_scale;
            float3 cloud_detail_offset;

            sampler2D _CloudInfoTexture;

            sampler2D _BlueNoise;
            int noise_size;
            float noise_strength;

            float in_scatter_g;
            float out_scatter_g;
            float scatter_blend;

            //Globals
            float depthTextureDistance;

            float remap(float val, float oa, float ob, float na, float nb){
                float a = val - oa;
                float b = nb - na;
                float c = ob - oa;
                return na + (a * b) / c;
            }

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

                return frac((pos - worldTexCornerMin) / (worldTexCornerMax - worldTexCornerMin));
            }

            float SampleDensity(float3 worldPos){
                float3 sampleWorldPos = (worldPos * cloud_scale) + cloud_offset;
                sampleWorldPos += _Time * wind_speed;
                float4 sampleTexPos = float4(WorldSpaceToSamplePos(sampleWorldPos), 0);

                //Coverage map samples
                float4 sampleCoveragePos = float4(sampleTexPos.xz, 0, 0);
                float3 coverageTexture = tex2Dlod(_CloudInfoTexture, sampleCoveragePos);
                float lowCoverage = coverageTexture.r;
                float highCoverage = coverageTexture.g;

                //Max height samples
                float maxHeight = coverageTexture.b;

                //Detail noise samples
                float3 sampleDetailWorldPos = (worldPos * cloud_detail_scale) + cloud_detail_offset;
                sampleDetailWorldPos += _Time * disturbance_speed;
                float4 sampleDetailTexPos = float4(WorldSpaceToSamplePos(sampleDetailWorldPos), 0);
                float4 shapeNoiseTexture = tex3Dlod(_CloudTexture, sampleDetailTexPos);

                //VIEW AS CARVING OUT CLOUDS RATHER THAN BUILDING THEM OUT

                //https://www.diva-portal.org/smash/get/diva2:1223894/FULLTEXT01.pdf
                //FUNCTION 11
                float shapeNoise = shapeNoiseTexture.r + shapeNoiseTexture.g * 0.625 + shapeNoiseTexture.b * 0.25 + shapeNoiseTexture.a * 0.125;
                shapeNoise *= 0.5;
                shapeNoise = 1 / shapeNoise;
                shapeNoise = saturate(shapeNoise - shape_modifier);
                //shapeNoise += 0.5;

                //Carves out noise from the coverage map to give the clouds a better shape than can be artisted
                float coverage = remap(lowCoverage, saturate(highCoverage - noise_to_drawn_blend), 1, 0, 1);

                //Height tapering towards the top
                float heightPercent = (worldPos.y - boxmin.y * cloud_scale.y) / (boxmax.y * cloud_scale.y - boxmin.y * cloud_scale.y);
                //Will return negative numbers if not in range 0-0.07 so the saturate ensures they stay at one
                float bottomTaper = saturate(remap(heightPercent, 0, 0.1, 1, 0));
                float topTaper = saturate(remap(heightPercent, maxHeight * 0.2, maxHeight, 0, 1));
                float heightModifier = bottomTaper * topTaper;

                //Density tapering towards top
                float densityTaperA = heightPercent * saturate(remap(heightPercent, 0, 0.05, 0, 1));
                float densityTaperB = saturate(remap(heightPercent, maxHeight * 0.75, maxHeight, 1, 0)); //If above % of maxheight then reverse lerp
                float densityModifier = densityTaperA * densityTaperB * density_modifier;

                //float value = saturate(remap(saturate(coverage - coverage_modifier), heightModifier, 1, 0, 1));
                float value = saturate(coverage - coverage_modifier);
                value = saturate(remap(value, heightModifier, 1, 0, 1));
                value *= densityModifier;
                value = saturate(value - shapeNoise);
                value = saturate(remap(value, shapeNoise, 1, 0, 1));

                //float value = saturate(coverage - coverage_modifier) * heightModifier * densityModifier;
                //value = saturate(remap(value, shapeNoise, 1, 0, 1));
                return value * 0.25;
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
                //return ((1 - g2) / pow(1 + g2 - 2 * g * cosA, 1.5)) / 4 * 3.1415;

                //Rearanged to have only one float division
                return ((1.0 - g2) * 3.1415) / (pow(1.0 + g2 - 2.0 * g * cosA, 1.5) * 4.0);
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

                    totalDensity += density * stepSize * light_strength;
                    currentStepPos += stepVec;
                }

                float energy = exp(-totalDensity);
                //Idea from
                //https://advances.realtimerendering.com/s2017/Nubis%20-%20Authoring%20Realtime%20Volumetric%20Cloudscapes%20with%20the%20Decima%20Engine%20-%20Final%20.pdf
                //Slide 85
                //float energy = max(exp(-totalDensity), (exp(-totalDensity * 0.25) * 0.7));
                return energy;
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

                float lightInfluence = remap(saturate(lightEnergy), 0, 1, shadow_cutoff, 1);

                //lightEnergy (and therefore lightEnergy * lightStrength) should be between 0-1 so it can be used to lerp between shadow and highlight colors
                //I think some improvements could be made to this lerp in order to improve stylisation
                fixed3 calculatedCloudColor = lerp(shadowColor, highlightColor, lightInfluence);

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
