// Water: two ripple normal maps drifting in different directions, a tint that goes from shallow to deep with the viewing angle,
// strong highlights, and rings of foam and ripple that spread from every splash (the game feeds the splashes in as _Impacts).
Shader "JumpGame/Water"
{
    Properties
    {
        _Color ("Shallow colour", Color) = (0.2, 0.7, 0.75, 0.85)
        _DeepColor ("Deep colour", Color) = (0.05, 0.3, 0.45, 0.95)
        [Normal] _BumpMap ("Ripples A", 2D) = "bump" {}
        [Normal] _BumpMap2 ("Ripples B", 2D) = "bump" {}
        _FoamTex ("Foam", 2D) = "white" {}
        _Caustics ("Caustics", 2D) = "black" {}
        _Scale ("Ripple tiles per metre", Float) = 0.12
        _Flow ("Flow speed", Float) = 0.035
        _BumpScale ("Ripple strength", Range(0, 2)) = 0.6
        _Glossiness ("Smoothness", Range(0, 1)) = 0.92
        _Emission ("Glow", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert fullforwardshadows nolightmap
        #pragma target 3.0

        sampler2D _BumpMap, _BumpMap2, _FoamTex, _Caustics;
        fixed4 _Color, _DeepColor;
        half _Scale, _Flow, _BumpScale, _Glossiness, _Emission;

        // up to four splashes at once: x, y = world position on the surface, z = seconds since, w = strength (0 = unused)
        float4 _Impacts[4];

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void vert(inout appdata_full v)
        {
            if (dot(v.tangent.xyz, v.tangent.xyz) < 0.01) v.tangent = float4(1, 0, 0, 1);
        }

        // Ring-shaped swell and foam that travels outwards from a splash and fades.
        void impact(float2 p, out float2 push, out half foam)
        {
            push = 0;
            foam = 0;
            for (int i = 0; i < 4; i++)
            {
                float4 s = _Impacts[i];
                if (s.w <= 0) continue;
                float2 d = p - s.xy;
                float r = length(d) + 0.0001;
                float front = s.z * 2.6;
                float x = (r - front) / (0.35 + s.z * 0.12);
                float life = exp(-s.z * 0.9) * s.w;
                float ring = exp(-x * x) * life;
                push += d / r * ring * cos(x * 1.6) * 1.4;
                foam += ring * 0.9 + exp(-r * 1.6) * exp(-s.z * 1.8) * s.w * 0.9;   // a patch of white where the body went in
            }
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float t = _Time.y * _Flow;
            float2 p = IN.worldPos.xz;
            float2 uvA = p * _Scale + float2(t, t * 0.6);
            float2 uvB = p * _Scale * 1.7 + float2(-t * 0.8, t * 1.1) + 0.37;
            half3 nA = UnpackScaleNormal(tex2D(_BumpMap, uvA), _BumpScale);
            half3 nB = UnpackScaleNormal(tex2D(_BumpMap2, uvB), _BumpScale * 0.7);
            half3 n = normalize(half3(nA.xy + nB.xy, nA.z * nB.z));

            float2 push;
            half foam;
            impact(p, push, foam);
            n = normalize(half3(n.xy + push * 0.5, n.z));
            // The glass sides of the water block are plain tinted panes: no ripples, no foam.
            half top = saturate((WorldNormalVector(IN, float3(0, 0, 1)).y - 0.6) * 5.0);
            n = normalize(lerp(half3(0, 0, 1), n, top));
            foam *= top;
            o.Normal = n;

            // Seen from above the water shows its floor (lighter, more transparent); seen at a slant it turns to deep colour.
            half facing = saturate(dot(normalize(IN.viewDir), float3(0, 1, 0)));
            half depthTint = pow(1.0 - facing, 1.3);
            fixed4 col = lerp(_Color, _DeepColor, depthTint);

            // Shifting light on the floor, strongest in the clear shallow colour.
            half caustic = tex2D(_Caustics, p * 0.07 + float2(t * 2.0, -t * 1.4)).r * tex2D(_Caustics, p * 0.05 - float2(t * 1.3, t)).r;
            col.rgb += caustic * 0.55 * (1.0 - depthTint) * fixed3(0.8, 1.0, 1.0);

            half foamTex = tex2D(_FoamTex, p * 0.35 + float2(t, 0)).r;
            half f = saturate(foam * (0.35 + foamTex));
            col.rgb = lerp(col.rgb, fixed3(0.95, 0.99, 1.0), f);

            o.Albedo = col.rgb;
            o.Metallic = 0;
            o.Smoothness = lerp(_Glossiness, 0.3, f);
            o.Emission = col.rgb * _Emission;
            o.Alpha = saturate(lerp(col.a, 1.0, f)) * lerp(0.45, 1.0, top);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
