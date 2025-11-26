Shader "Sprites/Default-Scrolling"
{
	Properties
	{
		_ScrollSpeed("Scroll Speed XY", Vector) = (1,1,0,0)
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_Color("Tint", Color) = (1,1,1,1)
		[MaterialToggle] PixelSnap("Pixel snap", Float) = 0
		[HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
		[HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
		[PerRendererData] _AlphaTex("External Alpha", 2D) = "white" {}
		[PerRendererData] _EnableExternalAlpha("Enable External Alpha", Float) = 0
	}

		SubShader
		{
			Tags
			{
				"Queue" = "Transparent"
				"IgnoreProjector" = "True"
				"RenderType" = "Transparent"
				"PreviewType" = "Plane"
				"CanUseSpriteAtlas" = "True"
			}

			Cull Off
			Lighting Off
			ZWrite Off
			Blend One OneMinusSrcAlpha
			Pass
			{
			CGPROGRAM
				#pragma vertex SpriteVert
				#pragma fragment Frag
				#pragma target 2.0
				#pragma multi_compile_instancing
				#pragma multi_compile_local _ PIXELSNAP_ON
				#pragma multi_compile _ ETC1_EXTERNAL_ALPHA
				#include "UnitySprites.cginc"

			float4 _ScrollSpeed;

			fixed4 Frag(v2f IN) : SV_Target
			{
				fixed2 uv = IN.texcoord;
				// No idea why, but the x speed seems to be 20x slower
				_ScrollSpeed.x *= 20.0f;
				uv += _ScrollSpeed.xy * _Time;
				fixed4 c = SampleSpriteTexture(uv) * IN.color;
				c.rgb *= c.a;
				return c;
			}

			ENDCG
			}
		}
}