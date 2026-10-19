#ifndef SDF_CURSOR_TRAIL_VS
#define SDF_CURSOR_TRAIL_VS

layout(location = 0) in highp vec2 m_Position;
layout(location = 1) in lowp vec4 m_Colour;
layout(location = 2) in highp vec2 m_TexCoord;
layout(location = 3) in highp float m_Time;
layout(location = 4) in highp float m_Extent;
layout(location = 5) in highp float m_OnePx;
layout(location = 6) in lowp float m_WhiteBlend;

layout(location = 0) out lowp vec4 v_Colour;
layout(location = 1) out highp vec2 v_TexCoord;
layout(location = 2) out highp float v_Time;
layout(location = 3) out highp float v_Extent;
layout(location = 4) out highp float v_OnePx;
layout(location = 5) out lowp float v_WhiteBlend;

void main(void)
{
    gl_Position = g_ProjMatrix * vec4(m_Position, 1.0, 1.0);
    v_Colour = m_Colour;
    v_TexCoord = m_TexCoord;
    v_Time = m_Time;
    v_Extent = m_Extent;
    v_OnePx = m_OnePx;
    v_WhiteBlend = m_WhiteBlend;
}

#endif
