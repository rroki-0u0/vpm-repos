// 色相回転 (グレー軸 (1,1,1)/√3 まわりの回転行列)。標準的な公開数式によるクリーンルーム実装。
// ColorAdjust / Emission / MatCapsExtra と同一のガード名で共有する (どのモジュールが有効でも一度だけ定義)。
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

// マットキャップ UV。ワールド上方向で安定化したビュー基底に法線を投影する標準的な手法
// (NonToon 標準 MatCaps / MatCapsExtra と同じ投影)。クリーンルーム実装。
#ifndef RROKI_NT_MATCAPUV_INCLUDED
#define RROKI_NT_MATCAPUV_INCLUDED
half2 RrokiNTMatCapUV(half3 headDir, half3 N)
{
    half3 mcN = headDir;
    half3 mcB = normalize(half3(0, 1, 0) - mcN * mcN.y * 0.9);
    half3 mcT = cross(mcN, mcB);
    half3x3 mcTBN = half3x3(mcT, mcB, mcN);
    return mul(mcTBN, N).xy * 0.5 + 0.5;
}
#endif
