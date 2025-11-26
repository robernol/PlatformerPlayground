Shader "Graphy/Graph Standard"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_Color("Tint", Color) = (1,1,1,1)
		[MaterialToggle] PixelSnap("Pixel snap", Float) = 0

		_GoodColor("Good Color", Color) = (1,1,1,1)
		_CautionColor("Caution Color", Color) = (1,1,1,1)
		_CriticalColor("Critical Color", Color) = (1,1,1,1)

		_GoodThreshold("Good Threshold", Float) = 0.5
		_CautionThreshold("Caution Threshold", Float) = 0.25
		_HighValue("High Value", Float) = 0.00208
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
			ZTest Off
			Blend One OneMinusSrcAlpha

			Pass
			{
				Name "Default"
				CGPROGRAM

				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile _ PIXELSNAP_ON
				
				#include "UnityCG.cginc"

				struct appdata_t
				{
					float4 vertex    : POSITION;
					float4 color     : COLOR;
					float2 texcoord  : TEXCOORD0;
				};

				struct v2f
				{
					float4 vertex    : SV_POSITION;
					fixed4 color	 : COLOR;
					float2 texcoord  : TEXCOORD0;
				};

				fixed4 _Color;

				v2f vert(appdata_t IN)
				{
					v2f OUT;
					OUT.vertex = UnityObjectToClipPos(IN.vertex);
					OUT.texcoord = IN.texcoord;
					OUT.color = IN.color * _Color;
				#ifdef PIXELSNAP_ON
					OUT.vertex = UnityPixelSnap(OUT.vertex);
				#endif

					return OUT;
				}

				sampler2D _MainTex;
				sampler2D _AlphaTex;
				float _AlphaSplitEnabled;

				fixed4 SampleSpriteTexture(float2 uv)
				{
					fixed4 color = tex2D(_MainTex, uv);

				#if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
					if (_AlphaSplitEnabled)
						color.a = tex2D(_AlphaTex, uv).r;
				#endif //UNITY_TEXTURE_ALPHASPLIT_ALLOWED

					return color;
				}

				fixed4 _GoodColor;
				fixed4 _CautionColor;
				fixed4 _CriticalColor;

				fixed  _GoodThreshold;
				fixed  _CautionThreshold;
				fixed  _HighValue;

				// NOTE: The size of this array can break compatibility with some older GPUs
				// If you see a pink box or that the graphs are not working, try lowering this value
				// or using the GraphMobile.shader
				
				uniform float GraphValues[512];

				uniform float GraphValues_Length;

				fixed4 frag(v2f IN) : SV_Target
				{
					fixed xCoord = IN.texcoord.x;
					fixed yCoord = IN.texcoord.y;

					float highValue = max(_HighValue, _GoodThreshold);

					float graphValue = GraphValues[floor(xCoord * GraphValues_Length)];
					float graphValue_normalized = graphValue / highValue;
					float goodValue_normalized = _GoodThreshold / highValue;

					// Define the width of each element of the graph
					float increment = 1.0f / (GraphValues_Length - 1);

					float fuzzyRange = highValue / 20.0f;
					// start with good color
					fixed4 valueColor = _GoodColor;
					// override with caution color
					valueColor = lerp(valueColor, _CautionColor, smoothstep(_GoodThreshold, _GoodThreshold + fuzzyRange, graphValue));
					// override with critical color
					valueColor = lerp(valueColor, _CriticalColor, smoothstep(_CautionThreshold, _CautionThreshold + fuzzyRange, graphValue));

					float halfTipHeight = .025f;
					float halfTipHeight_thresholdBar = .02f;
					

					float alpha = 0;
					// draw in color for height of bar
					float tipAlpha = ((1.0f - smoothstep(graphValue_normalized, graphValue_normalized, yCoord)) * (smoothstep(graphValue_normalized - halfTipHeight, graphValue_normalized - halfTipHeight, yCoord)));
					float barAlpha = min(smoothstep(0, graphValue_normalized * 3.0f, yCoord), smoothstep(yCoord, yCoord, graphValue_normalized));
					fixed4 color = valueColor;

					color *= max(tipAlpha,barAlpha);
					// override with good bar
					//color = lerp(color, _GoodColor, ((1.0f - smoothstep(goodValue_normalized, goodValue_normalized, yCoord)) * (smoothstep(goodValue_normalized - halfTipHeight_thresholdBar, goodValue_normalized - halfTipHeight_thresholdBar, yCoord))));
					return color;
				}

				ENDCG
			}
		}
}