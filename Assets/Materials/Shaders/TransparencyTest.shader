Shader "Custom/TransparencyTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.position = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenPos = i.position.xy;

                fixed alpha = 1;
                screenPos.xy = floor(screenPos.xy * 0.25);
                if((screenPos.x + screenPos.y) % 2.0 == 0) alpha = 0;

                //Works the same but without need for alpha blending
                //float checker = -((screenPos.x + screenPos.y) % 2);
                //clip(checker);

                fixed4 col = fixed4(screenPos.xy / _ScreenParams.xy, 1, alpha);
                return col;
            }
            ENDCG
        }
    }
}
