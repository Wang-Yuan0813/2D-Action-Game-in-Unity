Shader "Game2D/Sprites/Boss White Flash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashAmount ("White Flash Amount", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _FlashAmount;
            v2f vert(appdata input) { v2f output; output.vertex = UnityObjectToClipPos(input.vertex); output.uv = input.uv; output.color = input.color * _Color; return output; }
            fixed4 frag(v2f input) : SV_Target { fixed4 sprite = tex2D(_MainTex, input.uv) * input.color; sprite.rgb = lerp(sprite.rgb, fixed3(1, 1, 1), saturate(_FlashAmount)); sprite.rgb *= sprite.a; return sprite; }
            ENDCG
        }
    }
}
