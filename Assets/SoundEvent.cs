using UnityEngine;

// Short fact: who made a sound. Not AudioSource and not a force.
public enum VoiceKind
{
    Alarm = 0,
    Call = 1
}

public struct SoundEvent
{
    public VoiceKind kind;
    public Vector2 position;
    public float loudness;
    public float time;
    public int sourceId;
}

public static class SoundBus
{
    private const int Cap = 32;
    private static readonly SoundEvent[] ring = new SoundEvent[Cap];
    private static int write;
    private static int count;

    public static void Post(VoiceKind kind, Vector2 position, float loudness, int sourceId)
    {
        ring[write] = new SoundEvent
        {
            kind = kind,
            position = position,
            loudness = loudness,
            time = Time.time,
            sourceId = sourceId
        };
        write = (write + 1) % Cap;
        if (count < Cap)
            count++;
    }

    public static int CollectRecent(float now, float window, SoundEvent[] dest)
    {
        if (dest == null || dest.Length == 0 || count == 0)
            return 0;

        int n = 0;
        int start = write - count;
        if (start < 0)
            start += Cap;
        for (int i = 0; i < count && n < dest.Length; i++)
        {
            SoundEvent ev = ring[(start + i) % Cap];
            if (now - ev.time <= window)
                dest[n++] = ev;
        }

        return n;
    }

    public static void Clear()
    {
        write = 0;
        count = 0;
    }
}

// Cheap v1 look/listen. Not Vision / Hearing components.
public static class OrganismAlert
{
    public static bool SeesAhead(
        float selfX, float facing, float targetX, float range, float minAhead, out float dx)
    {
        dx = targetX - selfX;
        float dist = Mathf.Abs(dx);
        if (dist > range || dist < minAhead)
            return false;
        return dx * facing > minAhead;
    }

    public static bool HearsForeignAlarm(
        int selfId, Vector2 hearer, float now, float window, SoundEvent[] buf)
    {
        if (buf == null || buf.Length == 0)
            return false;

        int n = SoundBus.CollectRecent(now, window, buf);
        return HearsForeignAlarm(selfId, hearer, buf, n);
    }

    public static bool HearsForeignAlarm(
        int selfId, Vector2 hearer, SoundEvent[] events, int count)
    {
        if (events == null || count <= 0)
            return false;

        int n = Mathf.Min(count, events.Length);
        for (int i = 0; i < n; i++)
        {
            if (events[i].kind != VoiceKind.Alarm)
                continue;
            if (events[i].sourceId == selfId)
                continue;
            float reach = events[i].loudness;
            if (Mathf.Abs(events[i].position.x - hearer.x) > reach)
                continue;
            return true;
        }

        return false;
    }
}
