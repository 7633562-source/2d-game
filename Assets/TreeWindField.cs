using UnityEngine;

// Advected gust field for the tree sail (req-287 round 14).
// Free stream at (x, y) and time t: uMean * shear(y) * (1 + g), where g is a
// fixed harmonic sum — the OU-equivalent of a slow (tau 3 s) and a fast
// (tau 0.5 s) component — advected downwind at uAdv (frozen-turbulence lag).
// Stateless and deterministic: no Random, no integration step, so the same
// (x, y, t) gives the same speed on every run and at any call order.
public class TreeWindField
{
    public float uMean = 2.6f;
    public float sigmaSlow = 1.4f;
    public float tauSlow = 3f;
    public float sigmaFast = 0.5f;
    public float tauFast = 0.5f;
    public float uAdv = 3f;
    public float xUpwind = -2f;
    // Shear exponent of the vertical profile, u(y) = uMean * ((y - rootY) / h)^alpha.
    public float alpha = 0.15f;
    // Ceiling of |g| / uMean: the free stream never leaves 1 +/- clamp of the mean.
    public float clamp = 0.2f;
    public float rootY;
    public float heightRef = 8f;

    // Band frequencies as multiples of the corner 1 / (2 pi tau), so tau moves
    // the whole spectrum and the sigmas set its height.
    private static readonly float[] SlowR = { 0.584f, 0.980f, 1.489f, 2.206f };
    private static readonly float[] SlowA = { 0.62f, 0.90f, 0.55f, 0.32f };
    private static readonly float[] SlowP = { 0.00f, 1.70f, 4.10f, 2.60f };
    private static readonly float[] FastR = { 0.660f, 1.037f, 1.477f, 1.979f };
    private static readonly float[] FastA = { 0.55f, 0.85f, 0.60f, 0.35f };
    private static readonly float[] FastP = { 0.90f, 3.30f, 5.20f, 1.40f };
    // 1 / sqrt(sum a^2 / 2): brings each band sum to unit RMS before the sigma.
    private const float SlowNorm = 1.1182f;
    private const float FastNorm = 1.1518f;

    public float Speed(float x, float y, float t)
    {
        // Taylor lag: the gust that is here now was upwind (x - xUpwind) / uAdv ago.
        float tp = t - (x - xUpwind) / Mathf.Max(0.1f, uAdv);
        float g = sigmaSlow * Band(SlowR, SlowA, SlowP, tp, tauSlow) * SlowNorm
                + sigmaFast * Band(FastR, FastA, FastP, tp, tauFast) * FastNorm;
        float frac = Mathf.Clamp(g / Mathf.Max(0.1f, uMean), -clamp, clamp);
        float h = Mathf.Clamp((y - rootY) / Mathf.Max(0.5f, heightRef), 0.02f, 2f);
        return uMean * Mathf.Pow(h, alpha) * (1f + frac);
    }

    private static float Band(float[] ratio, float[] amp, float[] phase, float t, float tau)
    {
        float corner = 1f / (2f * Mathf.PI * Mathf.Max(0.02f, tau));
        float sum = 0f;
        for (int i = 0; i < ratio.Length; i++)
            sum += amp[i] * Mathf.Sin(2f * Mathf.PI * corner * ratio[i] * t + phase[i]);
        return sum;
    }
}
