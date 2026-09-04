using UnityEngine;

// Written at spawn. Not a force and not a brain.
// Faction 1 is the player (one member). Wildlife has Wolf and Bird.
public enum FactionId
{
    Player = 1,
    Wildlife = 2
}

public enum SubfactionId
{
    None = 0,
    Wolf = 1,
    Bird = 2
}

public enum FactionStance
{
    Ally = 0,
    Neutral = 1,
    Hostile = 2
}

public class FactionMember : MonoBehaviour
{
    public FactionId faction = FactionId.Wildlife;
    public SubfactionId subfaction = SubfactionId.None;
}

public static class FactionTable
{
    public static FactionStance Rel(FactionMember a, FactionMember b)
    {
        if (a == null || b == null)
            return FactionStance.Neutral;
        if (a == b)
            return FactionStance.Ally;
        if (a.faction == FactionId.Player && b.faction == FactionId.Player)
            return FactionStance.Ally;
        if (a.faction == FactionId.Wildlife && b.faction == FactionId.Wildlife)
        {
            if (a.subfaction == b.subfaction)
                return FactionStance.Ally;
            if (WolfBirdPair(a.subfaction, b.subfaction))
                return FactionStance.Hostile;
            return FactionStance.Neutral;
        }

        return FactionStance.Neutral;
    }

    private static bool WolfBirdPair(SubfactionId a, SubfactionId b)
    {
        return (a == SubfactionId.Wolf && b == SubfactionId.Bird)
            || (a == SubfactionId.Bird && b == SubfactionId.Wolf);
    }
}

public static class FactionStamp
{
    public static FactionMember Player(GameObject go)
    {
        return Mark(go, FactionId.Player, SubfactionId.None);
    }

    public static FactionMember Bird(GameObject go)
    {
        return Mark(go, FactionId.Wildlife, SubfactionId.Bird);
    }

    // The Dog body is the wolf subfaction until a separate wolf ragdoll exists.
    public static FactionMember Wolf(GameObject go)
    {
        return Mark(go, FactionId.Wildlife, SubfactionId.Wolf);
    }

    public static FactionMember Mark(GameObject go, FactionId faction, SubfactionId sub)
    {
        if (go == null)
            return null;
        FactionMember member = go.GetComponent<FactionMember>();
        if (member == null)
            member = go.AddComponent<FactionMember>();
        member.faction = faction;
        member.subfaction = sub;
        return member;
    }
}
