using UnityEngine;

// ─── GEOMETRIC SUPPORT STATE (A2, 19.09.2026) ───
//
// Instrumentation only: NOTHING in the simulation reads this file. It exists
// because the controller never knew it was falling: BalanceState stayed 0/1 in
// every measured run while the shared FallCriterion put the fall at 5.865 s on
// walk1_load4. A fall state a controller cannot act on is still the first half
// of the fix, so this is written as the state the controller WOULD publish.
//
// The state is pure GEOMETRY, evaluated fresh on every frame: no timer, no
// latch, no hysteresis, no filter.
//
//   0 Supported  the CoM and the capture point are both inside the polygon
//   1 Critical   the capture point is outside it: xi = x + v*sqrt(h/g) has left
//                the support, so no CoP inside the polygon can stop the mass -
//                only a step can. This arm is the one that fires first.
//   2 Fallen     the CoM itself is outside the polygon
//   3 NoSupport  no foot carries the body, so no polygon exists (flight)
//
// "The polygon" is whatever span the caller hands in: the controller passes the
// estimator's supportMinX/supportMaxX (the grounded collider span), the
// recorder also publishes the same ladder against its own measured loaded-plate
// hull, so the two are never confused in the CSV.
public static class BodySupportGeometry
{
    public const int Supported = 0;
    public const int Critical = 1;
    public const int Fallen = 2;
    public const int NoSupport = 3;

    // "On the edge" band, m. Smaller than the 5 mm contactOffset and much
    // smaller than any CoP the ankle can place (65 N*m / 687 N = 0.095 m), so
    // it cannot decide the state on its own.
    public const float EpsilonMeters = 0.005f;

    // Capture point (extrapolated CoM, "xi"), m in world x: x + v / omega with
    // omega = sqrt(g / h). h is the CoM height above the sole and is an INPUT,
    // measured per frame - a body that has sagged to 0.30 m has a different
    // omega than one standing at 0.90 m, and using a constant here is how a
    // fall meter goes blind during exactly the sag it is meant to catch.
    public static float CapturePoint(float comX, float comVelX, float comHeight)
    {
        float h = Mathf.Max(0.05f, comHeight);
        return comX + comVelX * Mathf.Sqrt(h / 9.81f);
    }

    // The two arms of the rule, published separately so a CSV reader can always
    // say WHICH one fired (level 1 = xi out, level 2 = CoM out).
    public static bool ComOutsideHull(float comX, float supportMinX, float supportMaxX)
    {
        return comX < supportMinX - EpsilonMeters || comX > supportMaxX + EpsilonMeters;
    }

    public static bool CaptureOutsideHull(float comX, float comVelX, float comHeight,
                                          float supportMinX, float supportMaxX)
    {
        float xi = CapturePoint(comX, comVelX, comHeight);
        return xi < supportMinX - EpsilonMeters || xi > supportMaxX + EpsilonMeters;
    }

    // One frame. hasSupport false means no foot is carrying the body: there is
    // no polygon to be inside or outside of, and that is its own code, never
    // counted as "up".
    public static int Level(bool hasSupport, float comX, float comVelX, float comHeight,
                            float supportMinX, float supportMaxX)
    {
        if (!hasSupport) return NoSupport;
        if (supportMaxX < supportMinX) return NoSupport;
        if (ComOutsideHull(comX, supportMinX, supportMaxX)) return Fallen;
        if (CaptureOutsideHull(comX, comVelX, comHeight, supportMinX, supportMaxX))
            return Critical;
        return Supported;
    }
}
