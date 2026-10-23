Shader "LTC/Card Space Rend"
{
 Properties { _MainTex ("Frozen screen", 2D) = "white" {} _Progress ("Progress", Range(0,1)) = 0 }
 SubShader
 {
  Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off ZWrite Off ZTest Always
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   sampler2D _MainTex;
   float4 _MainTex_TexelSize;
   float _Progress;
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
   float hash(float x) { return frac(sin(x * 127.1 + 311.7) * 43758.5453); }
   float4 frag(v2f i) : SV_Target
   {
    float t = _Progress;
    float aspect = _MainTex_TexelSize.z / max(1, _MainTex_TexelSize.w);
    float2 p = (i.uv - .5) * float2(aspect, 1);
    float2 offset = 0;
    float core = 0, glow = 0;
    float release = 1 - smoothstep(.84, 1, t);
    float burst = exp(-max(0, t - .75) * 32) * step(.75, t);
    [unroll] for (int k=0; k<12; k++)
    {
     float id = (float)k;
     float angle = hash(id + 2) * 3.14159265;
     float2 n = float2(cos(angle), sin(angle));
     float d = dot(p, n) - (hash(id + 45) - .5) * .66;
     float start = .14 + id * .043;
     float age = t - start;
     float visible = smoothstep(0, .012, age) * release;
     float ripple = exp(-max(0, age) * 4.5);
     offset += n * sign(d) * .006 * visible * (ripple + burst * 1.4);
     float width = .0008 + .0018 * ripple;
     float distance = abs(d);
     core += (1 - smoothstep(width, width + .0012, distance)) * visible;
     glow += exp(-distance * 120) * visible * (.3 + ripple * .7);
    }
    offset += float2(sin(t * 170), cos(t * 143)) * burst * .008;
    float2 uv = saturate(i.uv + offset / float2(aspect, 1));
    float3 col = tex2D(_MainTex, uv).rgb;
    float grey = dot(col, float3(.299, .587, .114));
    float dark = smoothstep(0, .10, t) * release;
    col = lerp(col, grey * float3(.40, .48, .64), dark * .91);
    float chromatic = .0018 * release * smoothstep(.15, .6, t);
    col.r += tex2D(_MainTex, saturate(uv + float2(chromatic, 0))).r * chromatic * 85;
    col.b += tex2D(_MainTex, saturate(uv - float2(chromatic, 0))).b * chromatic * 110;
    float vignette = saturate(1 - dot(p * .55, p * .55));
    col *= lerp(1, vignette, dark);
    col += float3(.20, .58, 1) * glow * .65;
    col += float3(.72, .91, 1) * saturate(core);
    // One brief impact, followed by the fragments rejoining the board.
    col = lerp(col, float3(.82, .94, 1), burst * .58);
    return float4(saturate(col), 1) * i.color;
   }
   ENDCG
  }
 }
}
