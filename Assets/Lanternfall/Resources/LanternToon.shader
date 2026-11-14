// Lanternfall toon look (Built-in render pipeline).
// Flat cel colour with one hard shadow tone and a constant-width dark ink outline,
// modelled on the clean 2D-cartoon sports reel MAYA shared. Point lights (Lantern, braziers)
// add stepped light pools. No cast shadows, to keep the colours flat and readable.
Shader "Lanternfall/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _ShadeTint ("Shadow tone", Color) = (0.66,0.64,0.78,1)
        _ShadeStep ("Shadow threshold", Range(-1,1)) = 0.05
        _OutlineColor ("Outline", Color) = (0.106,0.106,0.133,1)
        _OutlineWidth ("Outline width (m)", Float) = 0.022
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "TOON"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color, _ShadeTint;
            sampler2D _MainTex; float4 _MainTex_ST;
            float _ShadeStep;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 n : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                float ndl = dot(normalize(i.n), normalize(_WorldSpaceLightPos0.xyz));
                float lit = smoothstep(_ShadeStep - 0.02, _ShadeStep + 0.02, ndl);
                // keep colours close to their true value; the sun only nudges the hue
                fixed3 sun = lerp(fixed3(1,1,1), _LightColor0.rgb / max(0.001, max(_LightColor0.r, max(_LightColor0.g, _LightColor0.b))), 0.2);
                fixed3 col = albedo.rgb * lerp(_ShadeTint.rgb, sun, lit);
                col += albedo.rgb * UNITY_LIGHTMODEL_AMBIENT.rgb * 0.25;
                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "TOON_ADD"
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            sampler2D _MainTex; float4 _MainTex_ST;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 n : TEXCOORD1;
                float3 wpos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                #ifdef USING_DIRECTIONAL_LIGHT
                    float3 l = normalize(_WorldSpaceLightPos0.xyz);
                #else
                    float3 l = normalize(_WorldSpaceLightPos0.xyz - i.wpos);
                #endif
                UNITY_LIGHT_ATTENUATION(atten, 0, i.wpos);
                float k = saturate(dot(normalize(i.n), l) * 0.5 + 0.5) * atten;
                k = floor(k * 3.0 + 0.35) / 3.0;   // stepped pools of light
                fixed4 c = fixed4(albedo.rgb * _LightColor0.rgb * k * 0.6, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0,0,0,0));
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct v2f { float4 pos : SV_POSITION; UNITY_FOG_COORDS(0) };

            v2f vert (appdata_full v)
            {
                v2f o;
                // Boxes carry their corner signs in the tangent (w = 2): push every face out by the same
                // world width along the box's own (unscaled) axes, so the hull stays closed at the corners
                // even on stretched cubes. Everything else pushes along the normal.
                float3 offset;
                if (v.tangent.w > 1.5)
                {
                    float3 ax = normalize(unity_ObjectToWorld._m00_m10_m20);
                    float3 ay = normalize(unity_ObjectToWorld._m01_m11_m21);
                    float3 az = normalize(unity_ObjectToWorld._m02_m12_m22);
                    offset = (ax * v.tangent.x + ay * v.tangent.y + az * v.tangent.z) * _OutlineWidth;
                }
                else offset = normalize(UnityObjectToWorldNormal(v.normal)) * _OutlineWidth;
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz + offset;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = _OutlineColor;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
