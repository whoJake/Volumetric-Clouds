Shader "Volumetric/Base"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

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

            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.clipVertex.xy / _ScreenParams.xy;


                float3 vecToPixel = WorldSpaceViewDir(i.meshVertex);
                float3 viewDirection = normalize(vecToPixel);
                fixed4 col = fixed4(viewDirection, 1);
                return col;
            }
            ENDCG
        }
    }
}
