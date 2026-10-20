// Owner's camera rule (Yury, 25.09): in the test scenes the frame's edges are set by the
// OBJECTS' extent - everything tracked stays in the picture, and the lens zooms in and out
// as the bodies move. Picture only: no forces, no Rigidbody2D, and no physics row reads a
// camera (-nographics builds none). Runs last (order 10000) so it owns the final frame
// after CameraFollow.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class CameraAutoFit : MonoBehaviour
{
    public readonly List<Transform> targets = new List<Transform>();

    [Tooltip("Extra air around the objects' combined bounds, meters.")]
    public float margin = 0.5f;

    [Tooltip("The frame never goes tighter than this half-height (a lone tiny bird still reads).")]
    public float minHalfHeight = 1.2f;

    private Camera cam;

    public void Bind(Transform t)
    {
        if (t != null && !targets.Contains(t))
            targets.Add(t);
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (cam == null || targets.Count == 0)
            return;

        bool any = false;
        Bounds b = default;
        for (int t = 0; t < targets.Count; t++)
        {
            Transform tr = targets[t];
            if (tr == null)
                continue;
            Renderer[] rs = tr.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null || !rs[i].enabled || !rs[i].gameObject.activeInHierarchy)
                    continue;
                if (!any) { b = rs[i].bounds; any = true; }
                else b.Encapsulate(rs[i].bounds);
            }
        }
        if (!any)
            return;

        float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
        float halfH = Mathf.Max(
            b.extents.y + margin,
            b.extents.x / aspect + margin,
            minHalfHeight);
        cam.orthographicSize = halfH;
        cam.transform.position = new Vector3(b.center.x, b.center.y, cam.transform.position.z);
    }
}
