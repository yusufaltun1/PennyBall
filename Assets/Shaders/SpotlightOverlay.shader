Shader "PennyBall/UI/SpotlightOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 0, 0, 0.72)
        _HoleCenter ("Hole Center", Vector) = (0.5, 0.5, 0, 0)
        _HoleRadius ("Hole Radius", Float) = 0.14
        _HoleSoftness ("Hole Softness", Float) = 0.035
        _HoleAspect ("Hole Aspect", Float) = 1
        _WedgeEnabled ("Wedge Enabled", Float) = 0
        _WedgeApex ("Wedge Apex", Vector) = (0.5, 0.5, 0, 0)
        _WedgePointLeft ("Wedge Point Left", Vector) = (0.4, 0.7, 0, 0)
        _WedgePointRight ("Wedge Point Right", Vector) = (0.6, 0.7, 0, 0)
        _WedgeSoftness ("Wedge Softness", Float) = 0.02
        _GateEnabled ("Gate Enabled", Float) = 0
        _GatePointA ("Gate Point A", Vector) = (0.4, 0.7, 0, 0)
        _GatePointB ("Gate Point B", Vector) = (0.6, 0.7, 0, 0)
        _WedgeNearColor ("Wedge Near Color", Color) = (1, 0, 0, 0.235)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _HoleCenter;
            float _HoleRadius;
            float _HoleSoftness;
            float _HoleAspect;
            float _WedgeEnabled;
            float4 _WedgeApex;
            float4 _WedgePointLeft;
            float4 _WedgePointRight;
            float _WedgeSoftness;
            float _GateEnabled;
            float4 _GatePointA;
            float4 _GatePointB;
            fixed4 _WedgeNearColor;

            // Apex'ten boundaryPoint'e giden ışının iç (otherPoint) tarafına olan
            // işaretli dik mesafe; pozitif değerler wedge'in içini gösterir.
            float SignedEdgeDistance(float2 apex, float2 boundaryPoint, float2 otherPoint, float2 p)
            {
                float2 edge = boundaryPoint - apex;
                float edgeLength = max(length(edge), 0.0001);
                float side = sign(edge.x * (otherPoint.y - apex.y) - edge.y * (otherPoint.x - apex.x));
                float crossValue = edge.x * (p.y - apex.y) - edge.y * (p.x - apex.x);
                return side * crossValue / edgeLength;
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half4 color = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);

                if (_WedgeEnabled > 0.5)
                {
                    float2 aspectScale = float2(_HoleAspect, 1.0);
                    float2 p = input.texcoord * aspectScale;
                    float2 apex = _WedgeApex.xy * aspectScale;
                    float2 leftPoint = _WedgePointLeft.xy * aspectScale;
                    float2 rightPoint = _WedgePointRight.xy * aspectScale;

                    float leftDistance = SignedEdgeDistance(apex, leftPoint, rightPoint, p);
                    float rightDistance = SignedEdgeDistance(apex, rightPoint, leftPoint, p);
                    float inside = smoothstep(0.0, _WedgeSoftness, leftDistance)
                        * smoothstep(0.0, _WedgeSoftness, rightDistance);

                    // Koridor içinde, kapı çizgisinin coin tarafında kalan bölge kırmızı tonlanır.
                    float nearGateMask = 0.0;
                    if (_GateEnabled > 0.5)
                    {
                        float2 gateA = _GatePointA.xy * aspectScale;
                        float2 gateB = _GatePointB.xy * aspectScale;
                        float gateDistance = SignedEdgeDistance(gateA, gateB, apex, p);
                        nearGateMask = inside * smoothstep(0.0, _WedgeSoftness, gateDistance);
                    }

                    float darkMask = 1.0 - inside;
                    fixed4 result;
                    result.rgb = color.rgb * darkMask + _WedgeNearColor.rgb * nearGateMask;
                    result.a = color.a * darkMask + _WedgeNearColor.a * nearGateMask;

                    return result;
                }

                float2 delta = input.texcoord - _HoleCenter.xy;
                delta.x *= _HoleAspect;
                float dist = length(delta);
                float alpha = smoothstep(_HoleRadius, _HoleRadius + _HoleSoftness, dist);
                color.a *= alpha;

                return color;
            }
            ENDCG
        }
    }
}
