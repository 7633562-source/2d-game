using UnityEngine;

// LivingOrganism action: make a sound. The brain calls Cry.
// Posts a SoundEvent for hearing. Optional clip is picture for the player.
[DefaultExecutionOrder(-40)]
public class OrganismVoice : MonoBehaviour
{
    [Tooltip("Seconds between cries.")]
    public float cooldown = 0.8f;
    [Tooltip("Alarm fact loudness. Hearing range is about this many metres.")]
    public float alarmLoudness = 14f;
    [Tooltip("Player picture. Empty = fact only.")]
    public AudioClip alarmClip;

    public int SourceId { get; private set; }
    public int CryCount { get; private set; }
    public VoiceKind LastKind { get; private set; }
    public float LastCryTime { get; private set; } = -999f;

    private AudioSource speaker;
    private Transform body;

    void Awake()
    {
        SourceId = gameObject.GetHashCode();
        body = transform.Find("Body");
        if (alarmClip != null)
        {
            speaker = GetComponent<AudioSource>();
            if (speaker == null)
                speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0f;
        }
    }

    public void Cry(VoiceKind kind)
    {
        if (Time.time < LastCryTime + cooldown)
            return;

        LastCryTime = Time.time;
        LastKind = kind;
        CryCount++;

        Vector2 pos = transform.position;
        if (body != null)
            pos = body.position;

        float loud = kind == VoiceKind.Alarm ? alarmLoudness : alarmLoudness * 0.5f;
        SoundBus.Post(kind, pos, loud, SourceId);

        if (speaker != null && kind == VoiceKind.Alarm && alarmClip != null)
            speaker.PlayOneShot(alarmClip);
    }
}
