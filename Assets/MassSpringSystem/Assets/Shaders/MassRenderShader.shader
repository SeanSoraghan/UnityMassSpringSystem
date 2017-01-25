/**
 * A very simple shader that can be used to render the positions, velocities, or some other debugging-speific properties, of data points.  
 */

Shader "Custom/MassRenderShader" 
{
	SubShader
    {
        Pass
        {
 
            CGPROGRAM
            #pragma target 5.0
 
            #pragma vertex vert
            #pragma fragment frag
 
            #include "UnityCG.cginc"
 
            //The buffer containing the points we want to draw.
            StructuredBuffer<float3> buf_Points;
            StructuredBuffer<float3> buf_Debug;
            StructuredBuffer<float3> buf_Vels;
            //A simple input struct for our pixel shader step containing a position.
            struct ps_input 
            {
                float4 pos : SV_POSITION;
                float4 vel : NORMAL;
            };
 
            //Our vertex function simply fetches a point from the buffer corresponding to the vertex index
            //which we transform with the view-projection matrix before passing to the pixel program.
            ps_input vert (uint id : SV_VertexID)
            {
                ps_input o;
                float3 worldPos = buf_Points[id];
                o.pos = mul (UNITY_MATRIX_VP, float4 (worldPos, 1.0f));
                o.vel = float4 (abs(buf_Vels[id]), 1.0f);
                return o;
            }
 
            //Pixel function returns a solid color for each point.
            //Currently we debug the velocity of each point using the r and g values of the colour.
            float4 frag (ps_input i) : COLOR
            {
                float4 debugCol  = float4 (1.0f - abs (i.vel.x), abs (i.vel.y), 0.0f, 1.0f);
                float4 renderCol = float4 (1.0f, 1.0f, 1.0f, 1.0f);
                return debugCol;
            }
 
            ENDCG
 
        }
    }
    Fallback Off
}
