using UnityEngine;

// Written at spawn. Not a force and not a brain.
// 1 Player. 2 Wildlife (Wolf, Bird). 3 Insects (always Hostile to birds).
public enum FactionId
{
    Player = 1,
    Wildlife = 2,
    Insects = 3
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
        if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
            return FactionStance.Neutral;
        if (ReferenceEquals(a, b))
            return FactionStance.Ally;
        return RelIds(a.faction, a.subfaction, b.faction, b.subfaction);
    }

    // Table without a scene scan. Recorder and tests use this; Rel still
    // needs stamped members at runtime.
    public static FactionStance RelIds(
        FactionId a, SubfactionId aSub, FactionId b, SubfactionId bSub)
    {
        if (a == FactionId.Player && b == FactionId.Player)
            return FactionStance.Ally;
        if (a == FactionId.Wildlife && b == FactionId.Wildlife)
        {
            if (aSub == bSub)
                return FactionStance.Ally;
            if (WolfBirdPair(aSub, bSub))
                return FactionStance.Hostile;
            return FactionStance.Neutral;
        }

        if (BirdInsectPair(a, aSub, b, bSub))
            return FactionStance.Hostile;

        if (a == FactionId.Insects && b == FactionId.Insects)
            return FactionStance.Ally;

        // Player / Wolf / unlabeled Wildlife vs Insects: Neutral until a card.
        return FactionStance.Neutral;
    }

    public static bool IsHostile(FactionMember a, FactionMember b)
    {
        return Rel(a, b) == FactionStance.Hostile;
    }

    // Insects are food for Wildlife.Bird. Not a strength compare, not Wolf,
    // not Player. Rel already returns Hostile for a stamped pair; this is
    // the hunt gate when the brain already holds an Insect.
    public static bool BirdHuntsInsect(FactionMember hunter, FactionMember prey)
    {
        if (ReferenceEquals(prey, null) || prey.faction != FactionId.Insects)
            return false;
        // Missing stamp is not food. BeakStrike / Drive stamp Wildlife.Bird
        // first. A null hunter used to hunt as Player/Wolf too.
        if (ReferenceEquals(hunter, null))
            return false;
        return RelIds(
            hunter.faction, hunter.subfaction,
            prey.faction, prey.subfaction) == FactionStance.Hostile;
    }

    private static bool BirdInsectPair(
        FactionId a, SubfactionId aSub, FactionId b, SubfactionId bSub)
    {
        bool birdA = a == FactionId.Wildlife && aSub == SubfactionId.Bird;
        bool birdB = b == FactionId.Wildlife && bSub == SubfactionId.Bird;
        return (birdA && b == FactionId.Insects)
            || (birdB && a == FactionId.Insects);
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

    public static FactionMember Insect(GameObject go)
    {
        return Mark(go, FactionId.Insects, SubfactionId.None);
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
