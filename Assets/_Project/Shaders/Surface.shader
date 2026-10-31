// The one surface shader of the game. Every solid material uses it, so the whole world shares one look:
// a texture and a normal map projected from three sides (no UVs needed, so it fits the generated meshes as well as the models),
// moss on upward-facing stone, wet darkening near the water line and a soft rim of light that lifts the silhouettes.
// With "Use mesh UV" it reads the model's own UVs instead, for the characters.
Shader "JumpGame/Surface"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "grey" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _Scale ("Tiles per metre (or per UV)", Float) = 0.4
        [Toggle] _UVMode ("Use mesh UV", Float) = 0
        _Detail ("Texture strength", Range(0, 1)) = 1
        _BumpScale ("Relief", Range(0, 2)) = 1
        _Glossiness ("Smoothness", Range(0, 1)) = 0.3
        _Metallic ("Metallic", Range(0, 1)) = 0
        _MossColor ("Moss", Color) = (0.30, 0.46, 0.16, 1)
        _MossAmount ("Moss amount", Range(0, 1)) = 0
        _WetHeight ("Wet below (m)", Float) = 0.6
        _WetAmount ("Wetness", Range(0, 1)) = 0
        _Rim ("Rim light", Range(0, 1)) = 0.25
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color, _MossColor, _EmissionColor;
        half _Scale, _UVMode, _Detail, _BumpScale, _Glossiness, _Metallic, _MossAmount, _WetHeight, _WetAmount, _Rim;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            float3 viewDir;
            INTERNAL_DATA
        };

        // Generated meshes carry no tangents; any orthonormal frame will do because the normal is built in world space.
        void vert(inout appdata_full v)
        {
            if (dot(v.tangent.xyz, v.tangent.xyz) < 0.01)
            {
                float3 n = v.normal;
                float3 t = abs(n.y) < 0.99 ? normalize(cross(float3(0, 1, 0), n)) : float3(1, 0, 0);
                v.tangent = float4(t, 1);
            }
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 geo = WorldNormalVector(IN, float3(0, 0, 1));   // the mesh's own normal in the world
            fixed3 tex;

            if (_UVMode > 0.5)
            {
                float2 uv = IN.uv_MainTex * _Scale;
                tex = tex2D(_MainTex, uv).rgb;
                o.Normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
            }
            else
            {
                float3 w = pow(abs(geo), 4);
                w /= (w.x + w.y + w.z);
                float2 uvX = IN.worldPos.zy * _Scale;
                float2 uvY = IN.worldPos.xz * _Scale;
                float2 uvZ = IN.worldPos.xy * _Scale;
                tex = tex2D(_MainTex, uvX).rgb * w.x + tex2D(_MainTex, uvY).rgb * w.y + tex2D(_MainTex, uvZ).rgb * w.z;

                // Whiteout blend of the three tangent-space normals, then back into the tangent frame of this surface.
                half3 tx = UnpackScaleNormal(tex2D(_BumpMap, uvX), _BumpScale);
                half3 ty = UnpackScaleNormal(tex2D(_BumpMap, uvY), _BumpScale);
                half3 tz = UnpackScaleNormal(tex2D(_BumpMap, uvZ), _BumpScale);
                tx = half3(tx.xy + geo.zy, abs(tx.z) * geo.x);
                ty = half3(ty.xy + geo.xz, abs(ty.z) * geo.y);
                tz = half3(tz.xy + geo.xy, abs(tz.z) * geo.z);
                float3 nw = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
                // The surface's tangent frame is orthonormal, so its inverse is its transpose.
                float3 tw = WorldNormalVector(IN, float3(1, 0, 0));
                float3 bw = WorldNormalVector(IN, float3(0, 1, 0));
                o.Normal = normalize(float3(dot(nw, tw), dot(nw, bw), dot(nw, geo)));
            }

            // The texture sits around mid grey, so doubling it leaves the tint unchanged on average.
            fixed3 col = _Color.rgb * lerp(1.0, saturate(tex * 2.0), _Detail);
            half gloss = _Glossiness;

            // Moss collects on the tops of the stones, more where the texture is bright.
            half moss = saturate((geo.y - 0.45) * 3.0 + (tex.g - 0.5) * 1.8) * _MossAmount;
            col = lerp(col, _MossColor.rgb * (0.6 + tex.g * 0.9), moss);
            gloss = lerp(gloss, 0.08, moss);

            // Things at the water line are wet: darker and shinier.
            half wet = saturate((_WetHeight - IN.worldPos.y) / 0.5) * _WetAmount;
            col *= 1.0 - 0.45 * wet;
            gloss = lerp(gloss, 0.85, wet);

            half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), geo)), 3.0);

            o.Albedo = col;
            o.Metallic = _Metallic;
            o.Smoothness = gloss;
            o.Emission = _EmissionColor.rgb + _Rim * rim * fixed3(0.16, 0.19, 0.25);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
