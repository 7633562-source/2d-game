using System.Globalization;
using UnityEngine;

// ART-H5 R2 (Route A, the knee round): the joint patch skin.
//
// One small mesh spans the knee's overlap region, textured by the patch baked offline from
// the shipped canvases (Tools/art-h5-patch-bake.py).  Each vertex blends the two segment
// transforms with smoothstep skin weights across the overlap, so a bent knee reads as ONE
// continuous surface instead of two cut circles.  The patch texture, the crop frame, the
// weight zone and the segment offsets Qp/Qc all come from the sidecar the bake wrote
// (Assets/Resources/Art/Human/knee_patch.txt) - there is no second copy of the geometry.
//
// Picture only, the HumanSkeletonLines contract: built and ticked only under the trial's
// -visual capture and only when -skinJoints is on (default off, no existing flag moves);
// no FixedUpdate work, no physics contact, no colliders.  Off in every non-visual path,
// so the accepted physics rows cannot move.
[DefaultExecutionOrder(81)]
public class HumanSkin : MonoBehaviour
{
    // Draw order at the knee: above both of the side's own canvases (thigh/shin) and
    // below the side's foot; the torso/arm bands keep their own places relative to it.
    // The walk never brings the foot into the knee's disc, so the shared number is safe
    // (named in the report's draw-order note).
    const int LeftKneeOrder = -4;    // far side: between the far shin (-5) and the far foot (-4)
    const int RightKneeOrder = 5;    // near side: between the near shin (4) and the near foot (5)
    const float Cell = 0.002f;       // metre: mesh cell
    const string PatchKey = "Art/Human/knee_patch";

    class Knee
    {
        public HumanSegment parent;
        public HumanSegment child;
        public Mesh mesh;
        public Vector3[] restP;   // v + Qp (parent-local rest)
        public Vector3[] restC;   // v + Qc (child-local rest)
        public float[] w;         // blend weight (0 parent, 1 child)
        public Vector3[] verts;   // working array
    }

    Transform _root;
    Material _mat;
    Knee _left;
    Knee _right;
    bool _ready;

    // Called from HumanArt.DressHuman - the same place, and the same gates, as the
    // skeleton overlay: only when the trial asks for the skin and the capture draws.
    public static void EnsureOn(Transform root)
    {
        if (!HeadlessTrial.SkinJoints)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (root == null)
            return;
        Human human = root.GetComponentInParent<Human>();
        if (human == null)
            return;
        if (human.GetComponent<HumanSkin>() != null)
            return;
        human.gameObject.AddComponent<HumanSkin>();
    }

    void LateUpdate()
    {
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (!HeadlessTrial.SkinJoints)
            return;
        if (!_ready)
        {
            Bind();
            if (!_ready)
                return;
        }
        Tick(_left);
        Tick(_right);
    }

    void Bind()
    {
        if (_root == null)
            _root = transform;

        Texture2D patch = Resources.Load<Texture2D>(PatchKey);
        TextAsset sidecar = Resources.Load<TextAsset>(PatchKey);
        if (patch == null || sidecar == null)
        {
            Debug.LogWarning("HumanSkin: patch or sidecar missing, skin stays off.");
            return;
        }

        float x0 = 0f, y0 = 0f, wpx = 0f, hpx = 0f, upx = 0f, upy = 0f;
        float z0m = 0f, z1m = 0f, qpx = 0f, qpy = 0f, qcx = 0f, qcy = 0f;
        string[] lines = sidecar.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            int eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            string key = line.Substring(0, eq).Trim();
            float val;
            if (!float.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out val))
                continue;
            switch (key)
            {
                case "x0": x0 = val; break;
                case "y0": y0 = val; break;
                case "w": wpx = val; break;
                case "h": hpx = val; break;
                case "upx": upx = val; break;
                case "upy": upy = val; break;
                case "z0_m": z0m = val; break;
                case "z1_m": z1m = val; break;
                case "qpx": qpx = val; break;
                case "qpy": qpy = val; break;
                case "qcx": qcx = val; break;
                case "qcy": qcy = val; break;
            }
        }
        if (wpx <= 0f || hpx <= 0f)
        {
            Debug.LogWarning("HumanSkin: sidecar has no crop, skin stays off.");
            return;
        }

        Shader shader = SceneLighting.Enabled
            ? Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")
            : Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("HumanSkin: no sprite shader found, skin stays off.");
            return;
        }
        _mat = new Material(shader);
        _mat.name = "HumanSkinPatchShared";
        _mat.mainTexture = patch;

        _right = BuildKnee("RightLegThigh", "RightLegShin", "SkinKneeRight",
            RightKneeOrder, Color.white, x0, y0, wpx, hpx, upx, upy, z0m, z1m,
            qpx, qpy, qcx, qcy);
        _left = BuildKnee("LeftLegThigh", "LeftLegShin", "SkinKneeLeft",
            LeftKneeOrder, new Color(0.70f, 0.70f, 0.70f, 1f), x0, y0, wpx, hpx,
            upx, upy, z0m, z1m, qpx, qpy, qcx, qcy);

        if (_right == null && _left == null)
            return;
        _ready = true;
    }

    Knee BuildKnee(string parentName, string childName, string nodeName, int order,
        Color tint, float x0, float y0, float wpx, float hpx, float upx, float upy,
        float z0m, float z1m, float qpx, float qpy, float qcx, float qcy)
    {
        Transform pt = transform.Find(parentName);
        Transform ct = transform.Find(childName);
        if (pt == null || ct == null)
            return null;
        HumanSegment parent = pt.GetComponent<HumanSegment>();
        HumanSegment child = ct.GetComponent<HumanSegment>();
        if (parent == null || child == null)
            return null;

        int nx = Mathf.Max(2, Mathf.RoundToInt(wpx / Cell) + 1);
        int ny = Mathf.Max(2, Mathf.RoundToInt(hpx / Cell) + 1);
        float dx = wpx / (nx - 1);
        float dy = hpx / (ny - 1);
        int count = nx * ny;

        Knee knee = new Knee();
        knee.parent = parent;
        knee.child = child;
        knee.restP = new Vector3[count];
        knee.restC = new Vector3[count];
        knee.w = new float[count];
        knee.verts = new Vector3[count];

        Vector2[] uv = new Vector2[count];
        Vector3[] normals = new Vector3[count];
        Color[] colors = new Color[count];
        int k = 0;
        for (int j = 0; j < ny; j++)
        {
            float y = y0 + j * dy;
            for (int i = 0; i < nx; i++)
            {
                float x = x0 + i * dx;
                float du = upx * x + upy * y;
                float t = Mathf.Clamp01((z0m - du) / (z0m - z1m));
                knee.w[k] = t * t * (3f - 2f * t);
                knee.restP[k] = new Vector3(x + qpx, y + qpy, 0f);
                knee.restC[k] = new Vector3(x + qcx, y + qcy, 0f);
                uv[k] = new Vector2((x - x0) / wpx, (y - y0) / hpx);
                normals[k] = new Vector3(0f, 0f, -1f);
                colors[k] = tint;
                k++;
            }
        }
        int[] tris = new int[(nx - 1) * (ny - 1) * 6];
        int ti = 0;
        for (int j = 0; j < ny - 1; j++)
        {
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i;
                int b = a + 1;
                int c = a + nx;
                int d = c + 1;
                tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
            }
        }

        knee.mesh = new Mesh();
        knee.mesh.name = nodeName;
        knee.mesh.MarkDynamic();
        knee.mesh.vertices = knee.verts;
        knee.mesh.uv = uv;
        knee.mesh.normals = normals;
        knee.mesh.colors = colors;
        knee.mesh.triangles = tris;

        GameObject node = new GameObject(nodeName);
        node.transform.SetParent(transform, false);
        MeshFilter mf = node.AddComponent<MeshFilter>();
        mf.sharedMesh = knee.mesh;
        MeshRenderer mr = node.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _mat;
        SpriteRenderer sr = child.sprite;
        if (sr != null)
            mr.sortingLayerID = sr.sortingLayerID;
        mr.sortingOrder = order;

        // Deformation moves vertices far past their rest box; a fixed, generous bound in
        // the mesh's own (root-local) frame keeps the culler quiet.  Root-local coords of
        // a human stay within a couple of metres, so this bound cannot cull the patch.
        knee.mesh.bounds = new Bounds(Vector3.zero, new Vector3(6f, 6f, 1f));
        return knee;
    }

    void Tick(Knee knee)
    {
        if (knee == null)
            return;
        Transform pt = knee.parent.transform;
        Transform ct = knee.child.transform;
        Matrix4x4 toRoot = _root.worldToLocalMatrix;
        Matrix4x4 mp = toRoot * pt.localToWorldMatrix;
        Matrix4x4 mc = toRoot * ct.localToWorldMatrix;
        Vector3[] restP = knee.restP;
        Vector3[] restC = knee.restC;
        float[] w = knee.w;
        Vector3[] verts = knee.verts;
        for (int v = 0; v < verts.Length; v++)
        {
            Vector3 a = mp.MultiplyPoint3x4(restP[v]);
            Vector3 b = mc.MultiplyPoint3x4(restC[v]);
            float t = w[v];
            verts[v] = a + (b - a) * t;
        }
        knee.mesh.vertices = verts;
    }
}
