using UnityEngine;

// ART-H1 - the mannequin's real textures are a LEVEL 1 thing (Yury's sheet: "реальные
// текстуры грузить только в сцену N1 Level 1"; every other scene keeps the simple look,
// the semi-transparent rectangles).
//
// This component is that rule made visible: it sits on Level 1's level object, and it is
// the only thing in the project that dresses the mannequin with the painted canvases. Open
// any other scene and no mannequin art is loaded at all - the rectangles stay.
//
// The human is built by GameProcess at run time, and its segments are rebuilt there, so the
// dress waits for the rig to appear (Unity runs every Start before the first Update) and
// then lands once.
public class HumanArtSwitch : MonoBehaviour
{
    [Tooltip("Yury's sheet: real segment textures only in scene N1 \"Level 1\". Off = the " +
             "semi-transparent rectangles, the look every other scene keeps.")]
    public bool realTextures = true;

    [Tooltip("Seconds to wait for the mannequin to be built before giving up (0 = forever).")]
    public float waitSeconds = 30f;

    private float startedAt;
    private int dressed;
    private bool reported;

    void Start()
    {
        startedAt = Time.realtimeSinceStartup;
    }

    void Update()
    {
        if (!realTextures || dressed > 0)
            return;
        if (waitSeconds > 0f && Time.realtimeSinceStartup - startedAt > waitSeconds)
        {
            Report("no mannequin after " + waitSeconds.ToString("0") + " s");
            enabled = false;
            return;
        }

        Human[] humans = Object.FindObjectsByType<Human>(FindObjectsSortMode.None);
        for (int i = 0; i < humans.Length; i++)
        {
            if (humans[i] == null)
                continue;
            string firstMissing;
            if (dressed == 0 && HumanArt.MissingCanvases(out firstMissing) > 0)
            {
                Report("missing canvas " + firstMissing);
                enabled = false;
                return;
            }
            dressed += HumanArt.DressHuman(humans[i].transform, true);
        }
        if (dressed > 0)
            Report("dressed=" + dressed);
    }

    private void Report(string what)
    {
        if (reported)
            return;
        reported = true;
        Debug.Log("ART-HUMAN scene=" + gameObject.scene.name + " " + what);
    }
}
