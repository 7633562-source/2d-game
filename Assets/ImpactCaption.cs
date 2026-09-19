using UnityEngine;

// ─── BALANCE-ARENA-1: THE CAPTION ON THE FRAME ───
//
// The impact video is the deliverable Yury judges the thrower by, and a frame of a body
// being hit says nothing about WHAT hit it: the object is a placeholder rectangle and the
// numbers live in a JSON nobody sees. This draws them on the frame.
//
//   header  the parameter set of the run, fixed for its whole length
//   banner  the throw that is currently being measured, filled in as its window fills:
//           contact part and time, reference impulse, delivered impulse, centre-of-mass
//           excursion, support margin, steps taken after the impact, and the outcome
//   cross   a small cross on the contact point, for one second after the hit
//
// It is created by ImpactRecorder - the same object that measures a throw prints it - and
// both entry points (the editor scene `Assets/Scenes/BalanceArena.unity` and the headless
// `-impact` harness) construct an ImpactRecorder, so the caption cannot drift from the
// numbers and no caller has to remember to add it.
//
// Picture only: TextMesh and SpriteRenderer, no collider and no Rigidbody2D, so the
// physics of a run cannot depend on whether the caption is there. Text is sized from the
// live lens, so it reads the same at any -impactCamera.
public class ImpactCaption
{
    private const int SortingMarker = 880;
    private const int SortingPlate = 900;
    private const int SortingText = 910;

    private const float TextFractionOfLens = 0.045f;
    private const float MinTextMeters = 0.06f;
    private const float MaxTextMeters = 0.16f;
    private const float LinePitchFactor = 1.25f;
    private const int HeaderLines = 3;
    private const int BannerLines = 4;
    private const float MarkerSeconds = 1.0f;

    private GameObject root;
    private GameObject headerGroup;
    private GameObject bannerGroup;
    private GameObject markerGroup;
    private SpriteRenderer headerPlate;
    private SpriteRenderer bannerPlate;
    private SpriteRenderer markerHorizontal;
    private SpriteRenderer markerVertical;
    private TextMesh[] texts;
    private Camera camera;
    private string shownBanner = string.Empty;
    private float textHeight;
    private float linePitch;
    private float lastLens = -1f;
    private bool failed;

    public static ImpactCaption Create(string headerText, string logContext)
    {
        ImpactCaption caption = new ImpactCaption();
        caption.Build(headerText, logContext);
        return caption;
    }

    private void Build(string headerText, string logContext)
    {
        camera = ResolveCamera();
        if (camera == null)
        {
            // A picture needs something to be a picture of: with no camera the run is
            // unaffected, so this is a warning and not an error.
            failed = true;
            Debug.LogWarning("IMPACT-CAPTION skipped: no camera (" + logContext + ")");
            return;
        }

        textHeight = Mathf.Clamp(camera.orthographicSize * TextFractionOfLens,
            MinTextMeters, MaxTextMeters);
        linePitch = textHeight * LinePitchFactor;
        lastLens = camera.orthographicSize;

        root = new GameObject("ImpactCaption");

        headerGroup = new GameObject("Header");
        headerGroup.transform.SetParent(root.transform, false);
        headerPlate = GalleryLabels.AddPlate(headerGroup.transform, "HeaderPlate",
            new Vector2(4f, linePitch * HeaderLines), new Color(0f, 0f, 0f, 0.45f),
            SortingPlate);
        texts = new TextMesh[4];
        texts[0] = GalleryLabels.Label(headerGroup.transform, headerText,
            new Vector3(textHeight * 0.06f, -textHeight * 0.08f, 0.01f), textHeight,
            new Color(0f, 0f, 0f, 0.85f), SortingText);
        texts[1] = GalleryLabels.Label(headerGroup.transform, headerText, Vector3.zero,
            textHeight, Color.white, SortingText + 1);

        bannerGroup = new GameObject("Banner");
        bannerGroup.transform.SetParent(root.transform, false);
        bannerPlate = GalleryLabels.AddPlate(bannerGroup.transform, "BannerPlate",
            new Vector2(4f, linePitch * BannerLines), new Color(0f, 0f, 0f, 0.45f),
            SortingPlate);
        texts[2] = GalleryLabels.Label(bannerGroup.transform, string.Empty,
            new Vector3(textHeight * 0.06f, -textHeight * 0.08f, 0.01f), textHeight,
            new Color(0f, 0f, 0f, 0.85f), SortingText);
        texts[3] = GalleryLabels.Label(bannerGroup.transform, string.Empty, Vector3.zero,
            textHeight, new Color(1f, 0.86f, 0.35f, 1f), SortingText + 1);

        markerGroup = new GameObject("ContactMarker");
        markerGroup.transform.SetParent(root.transform, false);
        markerHorizontal = GalleryLabels.AddPlate(markerGroup.transform, "MarkerH",
            new Vector2(textHeight * 0.9f, textHeight * 0.12f), new Color(1f, 1f, 1f, 0.9f),
            SortingMarker, new Vector3(0f, 0f, 0.5f));
        markerVertical = GalleryLabels.AddPlate(markerGroup.transform, "MarkerV",
            new Vector2(textHeight * 0.12f, textHeight * 0.9f), new Color(1f, 1f, 1f, 0.9f),
            SortingMarker + 1, new Vector3(0f, 0f, 0.5f));
        markerGroup.SetActive(false);

        bannerGroup.SetActive(false);
        Update(string.Empty, -1f, Vector2.zero);
        Debug.Log("IMPACT-CAPTION built: " + logContext
            + " text=" + textHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " m lens=" + camera.orthographicSize.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }

    // Once per fixed step. `liveSeconds` is the age of the throw being measured, or -1
    // when none has landed yet; `banner` is the text ImpactRecorder composed for it.
    public void Update(string banner, float liveSeconds, Vector2 contactPoint)
    {
        if (failed || root == null) return;
        Camera cam = ResolveCamera();
        if (cam == null) return;

        float lens = cam.orthographicSize > 0f ? cam.orthographicSize : 2.4f;
        float top = cam.transform.position.y + lens;
        float halfWidth = Mathf.Max(1f, lens * cam.aspect);

        if (Mathf.Abs(lens - lastLens) > 0.0005f)
        {
            lastLens = lens;
            textHeight = Mathf.Clamp(lens * TextFractionOfLens, MinTextMeters, MaxTextMeters);
            linePitch = textHeight * LinePitchFactor;
            for (int i = 0; i < texts.Length; i++)
                texts[i].characterSize = textHeight / 7.2f;
            ResizeMarker();
        }

        float plateWidth = Mathf.Max(1f, halfWidth * 1.76f);
        float headerTall = linePitch * HeaderLines + textHeight * 0.8f;
        float bannerTall = linePitch * BannerLines + textHeight * 0.8f;
        float headerDrop = textHeight * 0.6f + headerTall * 0.5f;
        float bannerDrop = headerDrop + headerTall * 0.5f + textHeight * 0.8f + bannerTall * 0.5f;

        headerGroup.transform.localPosition = new Vector3(0f, top - headerDrop, 0f);
        bannerGroup.transform.localPosition = new Vector3(0f, top - bannerDrop, 0f);
        headerPlate.size = new Vector2(plateWidth, headerTall);
        bannerPlate.size = new Vector2(plateWidth, bannerTall);

        if (!string.Equals(banner, shownBanner, System.StringComparison.Ordinal))
        {
            shownBanner = banner;
            texts[2].text = banner;
            texts[3].text = banner;
            bannerGroup.SetActive(!string.IsNullOrEmpty(banner));
        }

        bool markerOn = liveSeconds >= 0f && liveSeconds <= MarkerSeconds;
        if (markerGroup.activeSelf != markerOn)
            markerGroup.SetActive(markerOn);
        if (markerOn)
            markerGroup.transform.position = new Vector3(contactPoint.x, contactPoint.y, -0.5f);
    }

    private void ResizeMarker()
    {
        markerHorizontal.size = new Vector2(textHeight * 0.9f, textHeight * 0.12f);
        markerVertical.size = new Vector2(textHeight * 0.12f, textHeight * 0.9f);
    }

    private static Camera ResolveCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();
        return cam;
    }
}
