Shader "Sprites/MonsterFogVisible"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            sampler2D _FogTex;
            float4 _FogCameraParams;  // x, y = 카메라 위치, z = width, w = height
            fixed4 _Color;
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };
            
            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                // Fog 텍스처에서 시야 값 샘플링
                float2 fogUV = (IN.worldPos - _FogCameraParams.xy) / _FogCameraParams.zw + 0.5;
                fixed4 fog = tex2D(_FogTex, fogUV);
                
                // Fog 텍스처에서 시야 안 = 검정(0), 검정일때 보여야 하니까 1에서 빼기
                float visibility = 1.0 - fog.r;
                
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                c.a *= visibility;
                c.rgb *= c.a;
                
                return c;
            }
            ENDCG
        }
    }
}