Shader "Sprites/ShaderAnimated"
{
    Properties
    {
        [PerRendererData] _MainTex ("Atlas Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _FrameWidth ("Frame Width (UV)", Float) = 0.25
        _FrameHeight ("Frame Height (UV)", Float) = 1.0
        _StartU ("Start U", Float) = 0.0
        _StartV ("Start V", Float) = 0.0
        _TotalFrames ("Total Frames", Float) = 4
        _FrameColumns ("Frame Columns", Float) = 4
        _FrameRate ("Frame Rate", Float) = 4
        _TimeOffset ("Time Offset", Float) = 0
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
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            fixed4 _Color;
            
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _FrameWidth)
                UNITY_DEFINE_INSTANCED_PROP(float, _FrameHeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _StartU)
                UNITY_DEFINE_INSTANCED_PROP(float, _StartV)
                UNITY_DEFINE_INSTANCED_PROP(float, _TotalFrames)
                UNITY_DEFINE_INSTANCED_PROP(float, _FrameColumns)
                UNITY_DEFINE_INSTANCED_PROP(float, _FrameRate)
                UNITY_DEFINE_INSTANCED_PROP(float, _TimeOffset)
            UNITY_INSTANCING_BUFFER_END(Props)
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                
                return OUT;
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                
                float frameRate = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameRate);
                float2 uv = IN.texcoord;
                
                if (frameRate > 0)
                {
                    float totalFrames = UNITY_ACCESS_INSTANCED_PROP(Props, _TotalFrames);
                    float frameColumns = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameColumns);
                    float frameWidth = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameWidth);
                    float frameHeight = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameHeight);
                    float timeOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _TimeOffset);
                    
                    float time = _Time.y + timeOffset;
                    float frame = floor(time * frameRate);
                    frame = frame - floor(frame / totalFrames) * totalFrames;
                    
                    float col = frame - floor(frame / frameColumns) * frameColumns;
                    float row = floor(frame / frameColumns);
                    
                    uv = float2(
                        IN.texcoord.x + col * frameWidth,
                        IN.texcoord.y - row * frameHeight
                    );
                }
                
                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}