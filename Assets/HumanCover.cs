using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// ART-H5f R3 - THE JOINT COVER (the foreshortened joint's covering surface).
//
// When -foreshorten shortens the thigh / the upper arm picture, the joint under it (the
// knee, the elbow, the hip) can lose paint: the shortened parent pulls its own paint back
// and the neighbour's skirt cannot reach across anymore, so a wedge of background shows
// between the two sprites and the limb reads as broken instead of gone into depth.
//
// For each joint Tools/art-h5f-cover-bake.py bakes ONE picture - the REST limb (parent +
// child canvases at v=0) as a single surface, toned by the child's own paint smeared above
// its edge and darkened toward the joint (the occlusion shadow) - plus the sidecar the
// runtime reads here: the crop in joint metres, Qp = the hinge in the parent's own box
// frame, blend=parent.
//
// THE COVER RIDES THE PARENT (sidecar blend=parent: world = Mparent * (p + Qp)): the crease
// the child vacates at a bent joint is exactly what has to stay painted, so the surface must
// NOT rotate with the child.  Each mesh node is a child of the parent segment's transform,
// so the ride costs no per-frame work at all - the transform does it.
//
// Picture only, the HumanSkeletonLines contract: built only when the owner's own gate asks
// for it (-foreshorten > 0, the flag the dress itself obeys, HumanArt.ForeshortenVisuals)
// and only when the capture draws; no collider, no rigidbody, no FixedUpdate work.  With
// the flag off no node exists and the player shows exactly the pictures it showed before.
public class HumanCover : MonoBehaviour
{
    const string KneeKey = "Art/Human/cover_knee";
    const string ElbowKey = "Art/Human/cover_elbow";
    const string HipNearKey = "Art/Human/cover_hip_near";
    const string HipFarKey = "Art/Human/cover_hip_far";

    // The dress's own far-side tone (HumanArt.FarTone), carried in the mesh's vertex colours.
    static readonly Color FarTone = new Color(0.70f, 0.70f, 0.70f, 1f);

    // Draw order: every cover sits UNDER BOTH of its joint's own sprites, so each sprite
    // keeps its own silhouette.  Near (right) limbs: knee -> 2 (under the near thigh 3 and
    // shin 4, over the torso/pelvis 0); elbow -> 5 (under the near upper arm 6 and lower arm
    // 7; the number is the near foot's own - the walk never brings the foot into the elbow's
    // disc); hip -> -7 (under the pelvis 0 and the near thigh 3 - and so also under the far
    // side's limb block, which the near leg already draws over).  Far (left) limbs: knee -> -7
    // (under the far thigh -6 and shin -5); elbow -> -4 (under the far upper arm -3 and lower
    // arm -2; again the far foot's number, same argument); hip -> -8 (under the pelvis and the
    // far thigh, and below the near hip's cover).
    const int KneeNearOrder = 2;
    const int KneeFarOrder = -7;
    const int ElbowNearOrder = 5;
    const int ElbowFarOrder = -4;
    const int HipNearOrder = -7;
    const int HipFarOrder = -8;

    const float Cell = 0.002f;      // metre: mesh cell

    Transform _root;
    readonly Dictionary<Texture2D, Material> _mats = new Dictionary<Texture2D, Material>();

    // Called from HumanArt.DressHuman - the same place, and the same gates, as the dress and
    // the Route-A skin: the cover exists only while the foreshortening is on.
    public static void EnsureOn(Transform root)
    {
        if (HeadlessTrial.Foreshorten <= 0f)
            return;
        if (!HeadlessTrial.CoverJoints)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (root == null)
            return;
        Human human = root.GetComponentInParent<Human>();
        if (human == null)
            return;
        if (human.GetComponent<HumanCover>() != null)
            return;
        HumanCover cover = human.gameObject.AddComponent<HumanCover>();
        cover._root = root;
        cover.Build();
    }

    void Build()
    {
        if (_root == null)
            _root = transform;
        int built = 0;
        built += BuildOne("KneeNear", KneeKey, "RightLegThigh", KneeNearOrder, Color.white);
        built += BuildOne("KneeFar", KneeKey, "LeftLegThigh", KneeFarOrder, FarTone);
        built += BuildOne("ElbowNear", ElbowKey, "RightArmUpper", ElbowNearOrder, Color.white);
        built += BuildOne("ElbowFar", ElbowKey, "LeftArmUpper", ElbowFarOrder, FarTone);
        built += BuildOne("HipNear", HipNearKey, "Pelvis", HipNearOrder, Color.white);
        built += BuildOne("HipFar", HipFarKey, "Pelvis", HipFarOrder, FarTone);
        Debug.Log("ART-H5f cover: " + built + "/6 surfaces built (knee elbow hip, both sides;"
            + " the shoulder's bay is the control art's own, out of scope)");
    }

    int BuildOne(string name, string key, string parentName, int order, Color tint)
    {
        Texture2D picture = Resources.Load<Texture2D>(key);
        TextAsset sidecar = Resources.Load<TextAsset>(key);
        if (picture == null || sidecar == null)
        {
            Debug.LogWarning("HumanCover: " + key + " missing (picture " + (picture != null)
                + ", sidecar " + (sidecar != null) + ") - that joint stays uncovered.");
            return 0;
        }
        Transform pt = transform.Find(parentName);
        if (pt == null)
        {
            Debug.LogWarning("HumanCover: no parent segment " + parentName + " for " + name);
            return 0;
        }
        HumanSegment parent = pt.GetComponent<HumanSegment>();
        if (parent == null)
        {
            Debug.LogWarning("HumanCover: " + parentName + " carries no HumanSegment for " + name);
            return 0;
        }

        float x0 = 0f, y0 = 0f, w = 0f, h = 0f, qpx = 0f, qpy = 0f;
        string[] lines = sidecar.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            int eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            string k = line.Substring(0, eq).Trim();
            float val;
            if (!float.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out val))
                continue;
            switch (k)
            {
                case "x0": x0 = val; break;
                case "y0": y0 = val; break;
                case "w": w = val; break;
                case "h": h = val; break;
                case "qpx": qpx = val; break;
                case "qpy": qpy = val; break;
            }
        }
        if (w <= 0f || h <= 0f)
        {
            Debug.LogWarning("HumanCover: " + key + " sidecar has no crop - skipped.");
            return 0;
        }

        int nx = Mathf.Max(2, Mathf.RoundToInt(w / Cell) + 1);
        int ny = Mathf.Max(2, Mathf.RoundToInt(h / Cell) + 1);
        float dx = w / (nx - 1);
        float dy = h / (ny - 1);
        int count = nx * ny;
        Vector3[] verts = new Vector3[count];
        Vector2[] uv = new Vector2[count];
        Vector3[] normals = new Vector3[count];
        Color[] colors = new Color[count];
        int v = 0;
        for (int j = 0; j < ny; j++)
        {
            float y = y0 + j * dy;
            for (int i = 0; i < nx; i++)
            {
                float x = x0 + i * dx;
                // the joint frame J -> the parent's own frame: point + Qp (the sidecar's
                // hinge).  The node below rides the parent's transform, so this is the
                // whole placement: world = Mparent * (p + Qp).
                verts[v] = new Vector3(x + qpx, y + qpy, 0f);
                uv[v] = new Vector2((x - x0) / w, (y - y0) / h);
                normals[v] = new Vector3(0f, 0f, -1f);
                colors[v] = tint;
                v++;
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

        Mesh mesh = new Mesh();
        mesh.name = "Cover" + name;
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.normals = normals;
        mesh.colors = colors;
        mesh.triangles = tris;
        // The node rides a moving body: keep the bounds at the crop, a margin wide, so the
        // culler never drops a cover that is plainly on screen.
        mesh.bounds = new Bounds(new Vector3(x0 + w * 0.5f + qpx, y0 + h * 0.5f + qpy, 0f),
            new Vector3(w + 0.4f, h + 0.4f, 1f));

        GameObject node = new GameObject("Cover" + name);
        node.transform.SetParent(pt, false);
        MeshFilter mf = node.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = node.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MaterialFor(picture);
        SpriteRenderer sr = parent.sprite;
        if (sr != null)
            mr.sortingLayerID = sr.sortingLayerID;
        mr.sortingOrder = order;
        return 1;
    }

    // One material per baked picture (the near and the far surface of one joint share it;
    // the side's tone rides the vertex colours).  Same shader family the dress's sprites
    // use under SceneLighting.
    Material MaterialFor(Texture2D picture)
    {
        Material mat;
        if (_mats.TryGetValue(picture, out mat))
            return mat;
        Shader shader = SceneLighting.Enabled
            ? Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")
            : Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("HumanCover: no sprite shader found, cover stays off.");
            return null;
        }
        mat = new Material(shader);
        mat.name = "HumanCover_" + picture.name;
        mat.mainTexture = picture;
        _mats[picture] = mat;
        return mat;
    }
}
