// 色相回転 (グレー軸 (1,1,1)/√3 まわりの回転行列)。標準的な公開数式によるクリーンルーム実装。
// ColorAdjust / Emission / MatCapsExtra / InternalParallax と同一のガード名で共有する。
#ifndef RROKI_NT_HUEROTATE_INCLUDED
#define RROKI_NT_HUEROTATE_INCLUDED
half3 RrokiNTHueRotate(half3 color, half shift)
{
    half angle = shift * 6.2831853;
    half c = cos(angle);
    half s = sin(angle);
    half3x3 rot = half3x3(
        0.333333 + c * 0.666667,                0.333333 - c * 0.333333 - s * 0.57735,  0.333333 - c * 0.333333 + s * 0.57735,
        0.333333 - c * 0.333333 + s * 0.57735,  0.333333 + c * 0.666667,                0.333333 - c * 0.333333 - s * 0.57735,
        0.333333 - c * 0.333333 - s * 0.57735,  0.333333 - c * 0.333333 + s * 0.57735,  0.333333 + c * 0.666667);
    return mul(rot, color);
}
#endif
