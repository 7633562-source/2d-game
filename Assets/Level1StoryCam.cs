using UnityEngine;

// req-246 (LEVEL 1 N3, round 1): the storyteller camera - state only, picture only.
// The frame is aimed between the hero CoM and the level's points of interest - the
// apple crown top while the hero is near the apple, the flock cloud while the flock
// is airborne, the dog's chest while the dog trails - so one shot tells the story
// (crows on the crown, the flush, the dog). The lens starts at 5 and widens to at
// most 6.5 while the hero stays inside a 10 % safe margin; the centre and the lens
// both ease (centre <= 3 m/s, lens <= 0.5 /s). Pure reads: no body is ever written.
public class Level1StoryCam
{
    public float MinOrtho = 5f;      // the film's lens (scene camera size)
    public float MaxOrtho = 6.5f;    // the widest the story lens may go
    public float OrthoRate = 0.5f;   // lens ease, units/s
    // 2.85, not the 3.0 bar itself: the row-to-row read is a nominal 0.5 s over the
    // real capture delta, so a cap set exactly on 3.0 measured 3.0009. 2.85 clears it.
    public float MaxSpeed = 2.85f;   // centre ease, m/s
    public float SmoothTime = 0.30f; // seconds to the target
    public float Margin = 0.10f;     // the hero's safe margin, share of the half frame
    public float Pad = 0.40f;        // slack around the anchor box, m
    public float Aspect = 16f / 9f;

    public float CamX { get; private set; }
    public float CamY { get; private set; }
    public float Ortho { get; private set; }
    public float HalfW => Ortho * Aspect;
    public float HalfH => Ortho;

    private bool snapped;

    // One fixed step. Every input is a read-only world point of the same step;
    // the flags say which interest points are armed at this instant.
    public void Tick(float dt, Vector2 hero, bool crownOn, Vector2 crown,
                     bool flockOn, Vector2 flock, bool dogOn, Vector2 dog)
    {
        float minX = hero.x, maxX = hero.x, minY = hero.y, maxY = hero.y;
        if (crownOn) Expand(crown, ref minX, ref maxX, ref minY, ref maxY);
        if (flockOn) Expand(flock, ref minX, ref maxX, ref minY, ref maxY);
        if (dogOn) Expand(dog, ref minX, ref maxX, ref minY, ref maxY);

        // The blend: the anchor box centre. The lens fits the box AND the hero's
        // 10 % margin, so at the target the hero is always inside its safe rect.
        float cx = (minX + maxX) * 0.5f;
        float cy = (minY + maxY) * 0.5f;
        float needY = Mathf.Max((maxY - minY) * 0.5f + Pad, Mathf.Abs(hero.y - cy) / (1f - Margin));
        float needX = Mathf.Max((maxX - minX) * 0.5f + Pad, Mathf.Abs(hero.x - cx) / (1f - Margin));
        float want = Mathf.Clamp(Mathf.Max(needY, needX / Aspect), MinOrtho, MaxOrtho);

        if (!snapped)
        {
            // First step: no travel from the scene default, the frame starts on target.
            CamX = cx;
            CamY = cy;
            Ortho = want;
            snapped = true;
            return;
        }

        float k = 1f - Mathf.Exp(-Mathf.Max(0.005f, dt) / Mathf.Max(0.04f, SmoothTime));
        float stepX = (cx - CamX) * k;
        float stepY = (cy - CamY) * k;
        Cap(ref stepX, ref stepY, MaxSpeed * Mathf.Max(0.005f, dt));
        CamX += stepX;
        CamY += stepY;
        Ortho = Mathf.MoveTowards(Ortho, want, OrthoRate * Mathf.Max(0.005f, dt));

        // Safety trim: if the ease lagged the hero out of its safe rect, spend the
        // remaining speed budget pulling the centre back to the hero's edge.
        float sw = HalfW * (1f - Margin);
        float sh = HalfH * (1f - Margin);
        float dx = hero.x - CamX;
        float dy = hero.y - CamY;
        float over = Mathf.Max(Mathf.Abs(dx) - sw, Mathf.Abs(dy) - sh);
        if (over > 0f)
        {
            float pullX = Mathf.Clamp(dx, -1f, 1f);
            float pullY = Mathf.Clamp(dy, -1f, 1f);
            float len = Mathf.Sqrt(pullX * pullX + pullY * pullY);
            if (len > 0.001f)
            {
                float give = Mathf.Min(over, MaxSpeed * Mathf.Max(0.005f, dt));
                CamX += pullX / len * give;
                CamY += pullY / len * give;
            }
        }
    }

    private static void Expand(Vector2 p, ref float minX, ref float maxX, ref float minY, ref float maxY)
    {
        if (p.x < minX) minX = p.x;
        if (p.x > maxX) maxX = p.x;
        if (p.y < minY) minY = p.y;
        if (p.y > maxY) maxY = p.y;
    }

    private static void Cap(ref float x, ref float y, float maxLen)
    {
        float len = Mathf.Sqrt(x * x + y * y);
        if (len <= maxLen || len < 0.000001f)
            return;
        float s = maxLen / len;
        x *= s;
        y *= s;
    }
}
