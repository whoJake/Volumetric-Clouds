Shader "Volumetric/Base"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Front
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                //Mesh vertex position
                float4 vertex : POSITION;
            };

            struct v2f
            {
                //World
                float4 clipVertex : SV_POSITION;
                float4 meshVertex : TEXCOORD0;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.clipVertex = UnityObjectToClipPos(v.vertex);
                o.meshVertex = v.vertex;
                return o;
            }

            //Returns (distToBox, distThroughBox) taken from https://github.com/SebLague/Clouds/blob/master/Assets/Scripts/Clouds/Shaders/Clouds.shader
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
            //uint divisions;
            
            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.clipVertex.xy / _ScreenParams.xy;

                //WorldSpaceViewDir points from object pixel towards camera
                float3 vecToPixel = -WorldSpaceViewDir(i.meshVertex); //tested correct

                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(vecToPixel);

                float2 boxinfo = RayBoxIntersect(boxmin, boxmax, rayOrigin, 1/rayDir);
                float maxDst = distance(boxmin, boxmax);

                float val = boxinfo.y / maxDst;

                fixed4 col = fixed4(1, 1, 1, val);
                return col;
            }
            ENDCG
        }
    }
}
