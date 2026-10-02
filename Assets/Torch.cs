using UnityEngine;
using UnityEngine.Rendering.Universal;

// Trial source: pole picture + flame (Unlit) and Point Light2D.
// No physics: no Rigidbody2D, no collider. Flicker in Update.
public class Torch : MonoBehaviour
{
    [Header("Light")]
    public float baseIntensity = 0.028f;
    public float baseOuterRadius = 2.2f;
    public float innerRadius = 0.08f;

    [Header("Flicker")]
    public float flickerSpeed = 6.4f;
    public float intensityJitter = 0.12f;
    public float radiusJitter = 0.06f;

    public const float CullX = 18f;

    private Light2D flameLight;
    private Transform flameVisual;
    private Camera followCamera;
    private float noiseOffset;
    private bool emitting = true;

    public static Torch Spawn(Vector3 worldPosition)
    {
        GameObject root = new GameObject("Torch");
        root.transform.position = worldPosition;
        Torch torch = root.AddComponent<Torch>();
        torch.Build();
        return torch;
    }

    private void Build()
    {
        noiseOffset = Random.Range(0f, 32f);

        // Pole stands on the ground: center is 0.32 m above the base.
        SpriteRenderer pole = MakeBox("Pole", new Vector2(0.07f, 0.64f), new Color(0.50f, 0.25f, 0f, 0.5f), 12);
        pole.transform.localPosition = new Vector3(0f, 0.32f, 0f);

        SpriteRenderer cup = MakeBox("Cup", new Vector2(0.14f, 0.10f), new Color(0.80f, 0.10f, 0.10f, 0.5f), 13);
        cup.transform.localPosition = new Vector3(0f, 0.66f, 0f);

        SpriteRenderer flame = MakeBox("Flame", new Vector2(0.13f, 0.26f), new Color(1f, 1f, 0f, 0.5f), 20);
        flame.transform.localPosition = new Vector3(0f, 0.86f, 0f);
        flameVisual = flame.transform;

        GameObject lightObject = new GameObject("FlameLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.86f, 0f);

        flameLight = lightObject.AddComponent<Light2D>();
        flameLight.lightType = Light2D.LightType.Point;
        flameLight.color = new Color(1f, 0.55f, 0.22f, 1f);
        flameLight.intensity = baseIntensity;
        flameLight.pointLightInnerRadius = innerRadius;
        flameLight.pointLightOuterRadius = baseOuterRadius;
        flameLight.pointLightInnerAngle = 360f;
        flameLight.pointLightOuterAngle = 360f;
        flameLight.falloffIntensity = 0.72f;
        flameLight.blendStyleIndex = 1;
        flameLight.overlapOperation = Light2D.OverlapOperation.Additive;
        flameLight.shadowsEnabled = false;
        // Volumetric light is off to save the 2D renderer frame budget (not from physics).
        flameLight.volumetricEnabled = false;
        flameLight.volumeIntensity = 0f;
    }

    void Update()
    {
        TickCulling();
        if (flameLight == null || !emitting)
            return;

        float n = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);
        float n2 = Mathf.PerlinNoise(noiseOffset, Time.time * flickerSpeed * 1.37f);
        float pulse = 1f + (n - 0.5f) * 2f * intensityJitter;
        flameLight.intensity = baseIntensity * pulse;
        flameLight.pointLightOuterRadius = baseOuterRadius * (1f + (n2 - 0.5f) * 2f * radiusJitter);

        if (flameVisual != null)
        {
            float s = 0.88f + n * 0.28f;
            flameVisual.localScale = new Vector3(s, 0.9f + n2 * 0.25f, 1f);
        }
    }

    private void TickCulling()
    {
        if (followCamera == null)
        {
            followCamera = Camera.main;
            if (followCamera == null)
                return;
        }

        float dx = transform.position.x - followCamera.transform.position.x;
        bool near = dx * dx < CullX * CullX;
        if (near == emitting)
            return;

        emitting = near;
        if (flameLight != null)
            flameLight.enabled = near;
        if (flameVisual != null)
            flameVisual.gameObject.SetActive(near);
    }

    private SpriteRenderer MakeBox(string name, Vector2 size, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, color, sortingOrder);
        return sr;
    }
}
