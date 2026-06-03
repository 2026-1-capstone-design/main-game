Shader "UI/BattleUnitOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.2, 1, 0.2, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
        }

        Pass
        {
            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata input)
            {
                v2f output;
                float3 expandedPosition = input.vertex.xyz + input.normal * _OutlineWidth;
                output.vertex = UnityObjectToClipPos(float4(expandedPosition, 1));
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}