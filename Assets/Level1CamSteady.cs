using System.Globalization;
using System.Text;
using UnityEngine;

// req-265 (LEVEL 1 storyteller camera, round 1): draw the story camera every frame.
// The state (Level1StoryCam) is ticked at the fixed step; until now the camera TRANSFORM
// was written only at the shots, so in the owner window the frame jumped once a second.
// This component copies the fixed-step state onto the camera every LateUpdate, with the
// step capped per second (not per row) so no frame-to-frame jump leaves the bar.
// Picture only: it reads the state, never writes a body. Armed by -level1CamSteady 1.
public class Level1CamSteady : MonoBehaviour
{
    public Level1StoryCam State;
    public float MaxSpeed = 2.85f;      // centre ease, m/s (the state's own cap)
    public float MaxOrthoRate = 0.5f;   // lens ease, units/s
    public float MaxFrameStep = 0.12f;  // per-frame ceiling, m (the bar is 0.15)

    private Camera cam;
    private int lastFrame = -1;
    private bool snapped;
    private int rows;
    private readonly StringBuilder log = new StringBuilder(1 << 14);

    public void Bind(Level1StoryCam state)
    {
        State = state;
    }

    // One drawn frame: what the camera shows between shots is the same pose the
    // shot PNG and the shot CSV read.
    private void LateUpdate()
    {
        if (State == null)
            return;
        if (cam == null)
            cam = GetComponent<Camera>();
        if (cam == null)
            return;
        int frame = Time.frameCount;
        if (frame == lastFrame)
            return;
        lastFrame = frame;

        Vector3 want = new Vector3(State.CamX, State.CamY, -10f);
        float ortho = State.Ortho;
        if (!snapped)
        {
            transform.position = want;
            cam.orthographicSize = ortho;
            cam.orthographic = true;
            snapped = true;
        }
        else
        {
            float dt = Time.deltaTime;
            if (dt < 0f)
                dt = 0f;
            Vector3 cur = transform.position;
            Vector3 dp = want - cur;
            float len = dp.magnitude;
            float maxd = Mathf.Min(MaxFrameStep, MaxSpeed * dt);
            if (len > maxd && len > 0.000001f)
                dp *= maxd / len;
            transform.position = cur + dp;
            cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, ortho,
                Mathf.Min(MaxFrameStep, MaxOrthoRate * dt));
        }
        Vector3 p = transform.position;
        log.Append(Time.time.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
            .Append(p.x.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
            .Append(p.y.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
            .Append(cam.orthographicSize.ToString("F4", CultureInfo.InvariantCulture)).Append('\n');
        rows++;
    }

    public string DrawLog()
    {
        return log.ToString();
    }

    public int DrawRows
    {
        get { return rows; }
    }
}
