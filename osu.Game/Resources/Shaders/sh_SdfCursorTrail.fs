#ifndef SDF_CURSOR_TRAIL_FS
#define SDF_CURSOR_TRAIL_FS

layout(location = 0) in lowp vec4 v_Colour;
layout(location = 1) in highp vec2 v_TexCoord;
layout(location = 2) in highp float v_Time;
layout(location = 3) in highp float v_Extent;
layout(location = 4) in highp float v_OnePx;
layout(location = 5) in lowp float v_WhiteBlend;

layout(location = 0) out vec4 o_Colour;

// https://iquilezles.org/articles/distfunctions2d/
highp float sdSegment(highp vec2 p, highp vec2 a, highp vec2 b)
{
    highp vec2 pa = p - a;
    highp vec2 ba = b - a;
    highp float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-12), 0.0, 1.0);
    return length(pa - ba * h);
}

highp float linearstep(highp float edge0, highp float edge1, highp float x)
{
    return clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
}

void main(void)
{
    highp float dist = 1.0 - sdSegment(v_TexCoord, vec2(-v_Extent, 0.0), vec2(v_Extent, 0.0));
    highp float invTime = 1.0 - v_Time;
    highp float timeInterp = invTime * invTime * invTime;
    highp float alpha = v_Colour.a * linearstep(timeInterp, timeInterp + v_OnePx, dist) * v_Time;

    lowp vec3 col = v_Colour.rgb;

    // vertex colours are in gamma space, so the mix towards white is done in linear space.
    if (v_WhiteBlend > 0.5)
        col = pow(mix(pow(col, vec3(2.2)), vec3(1.0), linearstep(32.0 * timeInterp, 32.0 * timeInterp + 0.75, dist)), vec3(1.0 / 2.2));

    // premultiplied, with the real coverage in the alpha channel, so that the trail stays transparent when it's drawn into a buffer.
    alpha = max(alpha, 0.0);
    o_Colour = vec4(col * alpha, alpha);
}

#endif
