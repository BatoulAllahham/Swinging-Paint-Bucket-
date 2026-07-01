// Canvas shader that reads PaintTexture (RGB=color, A=thickness/height)
// and generates surface normals from the height gradient so thick paint looks raised.
Shader "Custom/CanvasPaint"
{
    Properties
    {
        _MainTex        ("Paint Texture (RGB=color, A=thickness)", 2D) = "white" {}
        _CanvasColor    ("Canvas Base Color", Color) = (0.95, 0.92, 0.85, 1)
        _BumpStrength   ("Bump Strength", Range(0, 20)) = 6.0
        _WetnessGloss   ("Wetness / Gloss from Thickness", Range(0, 1)) = 0.20
        _CanvasSmoothness ("Canvas Surface Smoothness", Range(0, 1)) = 0.08
        // THICKNESS (POM disabled — looked stretched): _ParallaxDepth ("Parallax Depth", Range(0, 0.08)) = 0.025
        // THICKNESS: tessellation — subdivides mesh so paint is geometrically raised
        _TessellationFactor ("Tessellation Factor", Range(1, 64)) = 32
        _DisplacementScale  ("Displacement Scale (world units)", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 200

        // ── Forward Lit ──────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            // OLD (no tessellation): #pragma vertex vert
            // THICKNESS: tessellation stages added
            #pragma vertex   vert_tess
            #pragma hull     hull_main
            #pragma domain   domain_main
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize; // auto-populated: (1/w, 1/h, w, h)
                float4 _CanvasColor;
                float  _BumpStrength;
                float  _WetnessGloss;
                float  _CanvasSmoothness;
                // THICKNESS (POM disabled): float _ParallaxDepth;
                // THICKNESS
                float _TessellationFactor;
                float _DisplacementScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS    = normInputs.normalWS;
                OUT.tangentWS   = normInputs.tangentWS;
                OUT.bitangentWS = normInputs.bitangentWS;
                OUT.shadowCoord = GetShadowCoord(posInputs);
                return OUT;
            }

            // ── THICKNESS: Tessellation ──────────────────────────────────────────────
            // vert_tess: pass-through to hull shader (no world-space transform yet)
            struct ControlPoint
            {
                float4 positionOS : INTERNALTESSPOS;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct TessFactors
            {
                float edge[3] : SV_TessFactor;
                float inside  : SV_InsideTessFactor;
            };

            ControlPoint vert_tess(Attributes IN)
            {
                ControlPoint OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionOS = IN.positionOS;
                OUT.normalOS   = IN.normalOS;
                OUT.tangentOS  = IN.tangentOS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            TessFactors hull_const(InputPatch<ControlPoint, 3> patch)
            {
                TessFactors f;
                f.edge[0] = _TessellationFactor;
                f.edge[1] = _TessellationFactor;
                f.edge[2] = _TessellationFactor;
                f.inside  = _TessellationFactor;
                return f;
            }

            [domain("tri")]
            [partitioning("fractional_odd")]
            [outputtopology("triangle_cw")]
            [outputcontrolpoints(3)]
            [patchconstantfunc("hull_const")]
            ControlPoint hull_main(InputPatch<ControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            // domain_main: interpolates tessellated vertex, displaces it upward by paint height
            [domain("tri")]
            Varyings domain_main(TessFactors factors, OutputPatch<ControlPoint, 3> patch, float3 bary : SV_DomainLocation)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(patch[0]);

                // Barycentric interpolation
                float4 posOS  = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
                float3 normOS = patch[0].normalOS   * bary.x + patch[1].normalOS   * bary.y + patch[2].normalOS   * bary.z;
                float4 tangOS = patch[0].tangentOS  * bary.x + patch[1].tangentOS  * bary.y + patch[2].tangentOS  * bary.z;
                float2 uv     = patch[0].uv          * bary.x + patch[1].uv          * bary.y + patch[2].uv          * bary.z;

                // Sample paint height and push vertex up along world-space normal
                float  height = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
                float3 posWS  = TransformObjectToWorld(posOS.xyz);
                float3 normWS = normalize(TransformObjectToWorldNormal(normOS));
                posWS += normWS * height * _DisplacementScale;

                // Full vertex output (mirrors vert(), but from displaced world position)
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(patch[0], OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexNormalInputs normInputs = GetVertexNormalInputs(normOS, tangOS);
                OUT.positionCS  = TransformWorldToHClip(posWS);
                OUT.positionWS  = posWS;
                OUT.uv          = uv;
                OUT.normalWS    = normInputs.normalWS;
                OUT.tangentWS   = normInputs.tangentWS;
                OUT.bitangentWS = normInputs.bitangentWS;

                VertexPositionInputs posInputs = (VertexPositionInputs)0;
                posInputs.positionWS = posWS;
                posInputs.positionCS = OUT.positionCS;
                OUT.shadowCoord = GetShadowCoord(posInputs);

                return OUT;
            }
            // ── end tessellation ─────────────────────────────────────────────────────

            // ── Paint micro-texture helpers ──────────────────────────────────────────
            // Gradient noise: looks organic (dried paint), not like regular trig waves.
            float2 _PaintHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453) * 2.0 - 1.0;
            }
            float _GradNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f); // smoothstep
                return lerp(
                    lerp(dot(_PaintHash(i),                f),
                         dot(_PaintHash(i + float2(1, 0)), f - float2(1, 0)), u.x),
                    lerp(dot(_PaintHash(i + float2(0, 1)), f - float2(0, 1)),
                         dot(_PaintHash(i + float2(1, 1)), f - float2(1, 1)), u.x), u.y);
            }
            // ── end helpers ──────────────────────────────────────────────────────────

            half4 frag(Varyings IN) : SV_Target
            {
                // THICKNESS (POM disabled — looked stretched on the canvas mesh):
                // float3 T = normalize(IN.tangentWS); float3 B = normalize(IN.bitangentWS); float3 N = normalize(IN.normalWS);
                // float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                // float3 eyeTS = float3(dot(viewDirWS,T), dot(viewDirWS,B), dot(viewDirWS,N));
                // if (eyeTS.z > 0.01 && _ParallaxDepth > 0.001) { ... ray-march uv shift ... }

                float2 uv = IN.uv;

                // Sample center + 4 neighbours for height gradient (Sobel-lite)
                float4 c  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float tx  = _MainTex_TexelSize.x;
                float ty  = _MainTex_TexelSize.y;
                float hL  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-tx,  0)).a;
                float hR  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( tx,  0)).a;
                float hD  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(  0, -ty)).a;
                float hU  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(  0,  ty)).a;

                float dhdx = (hR - hL) * _BumpStrength;
                float dhdy = (hU - hD) * _BumpStrength;

                // Tangent-space normal from height gradient
                float3 normalTS = normalize(float3(-dhdx, -dhdy, 1.0));

                // THICKNESS: when alpha is saturated the Sobel gradient goes to zero → flat look.
                // Blend in gradient noise to simulate dried paint surface texture in those areas.
                // sobelMag≈0 means flat saturated region; sobelMag≈1 means real edge → use real edge.
                float sobelMag = saturate(abs(dhdx) + abs(dhdy));
                float flatness = saturate(c.a * 8.0) * (1.0 - sobelMag);
                float nX = _GradNoise(uv * 40.0);
                float nY = _GradNoise(uv * 40.0 + float2(31.4, 17.9));
                normalTS = normalize(normalTS + float3(nX, nY, 0.0) * 0.25 * flatness);

                // Convert tangent-space → world-space
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );
                float3 normalWS = normalize(mul(normalTS, TBN));

                // Color: blend from canvas base to paint color
                // sqrt(height*500): watercolor center (height≈0.002) → hasPaint≈1.0, full color.
                // Displacement still uses raw height so WallPaint is physically thicker than watercolor.
                float height   = c.a;
                // OLD: float hasPaint = saturate(height * 25.0);
                float hasPaint = saturate(sqrt(height * 500.0));
                float3 albedo  = lerp(_CanvasColor.rgb, c.rgb, hasPaint);

                // Thick paint slightly glossier but never mirror-like — squared curve keeps it matte
                // OLD: float smoothness = lerp(_CanvasSmoothness, _WetnessGloss, height);
                float smoothness = lerp(_CanvasSmoothness, _WetnessGloss, height * height);

                // URP PBR lighting
                InputData inputData = (InputData)0;
                inputData.positionWS            = IN.positionWS;
                inputData.normalWS              = normalWS;
                inputData.viewDirectionWS       = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord           = IN.shadowCoord;
                inputData.fogCoord              = 0;
                inputData.vertexLighting        = 0;
                inputData.bakedGI               = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask            = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = albedo;
                surfaceData.metallic    = 0;
                surfaceData.smoothness  = smoothness;
                surfaceData.normalTS    = normalTS;
                surfaceData.occlusion   = 1;
                surfaceData.alpha       = 1;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        // ── Shadow Caster ────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert_shadow
            #pragma fragment frag_shadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes_S { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 vert_shadow(Attributes_S v) : SV_POSITION
            {
                float3 posWS  = TransformObjectToWorld(v.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                return TransformWorldToHClip(ApplyShadowBias(posWS, normWS, lightDir));
            }

            half4 frag_shadow() : SV_Target { return 0; }
            ENDHLSL
        }

        // ── Depth Only ───────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert_depth
            #pragma fragment frag_depth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes_D { float4 positionOS : POSITION; };

            float4 vert_depth(Attributes_D v) : SV_POSITION
            {
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half4 frag_depth() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
