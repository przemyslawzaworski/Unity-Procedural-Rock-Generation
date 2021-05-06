Shader "TriplanarMapping"
{
	Properties
	{
		_Color("Main Color", Color) = (1, 1, 1, 1)
		_MainTex("Albedo Map", 2D) = "white" {}
		_Metallic("Metallic", Range(0, 1)) = 0.0
		_Smoothness("Smoothness", Range(0, 1)) = 0.0
		_Scale("Scale", Float) = 1
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		Cull Off
		CGPROGRAM
		#pragma surface SurfaceShaderProgram Standard vertex:VertexShaderProgram fullforwardshadows addshadow
		#pragma target 5.0

		sampler2D _MainTex;
		float4 _Color;
		float _Metallic, _Smoothness;

		float _Scale;

		struct Input
		{
			float3 localCoord;
			float3 localNormal;
		};

		void VertexShaderProgram(inout appdata_full v, out Input data)
		{
			UNITY_INITIALIZE_OUTPUT(Input, data);
			data.localCoord = v.vertex.xyz;
			data.localNormal = v.normal.xyz;
		}

		void SurfaceShaderProgram(Input IN, inout SurfaceOutputStandard o)
		{
			float3 weight = normalize(abs(IN.localNormal));
			weight /= (weight.x + weight.y + weight.z); 
			float4 px = tex2D(_MainTex, IN.localCoord.yz * _Scale) * weight.x;
			float4 py = tex2D(_MainTex, IN.localCoord.zx * _Scale) * weight.y;
			float4 pz = tex2D(_MainTex, IN.localCoord.xy * _Scale) * weight.z;
			float4 color = (px + py + pz) * _Color;
			o.Albedo = color.rgb;
			o.Normal = float3(0,0,1);
			o.Alpha = color.a;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
		}
		ENDCG
	}
	FallBack "Diffuse"
}