// Shared life of every LivingOrganism. Not a body pose and not a force.
// Sleep and Wake belong to all species. Wake kinds: Forage, Flee, Pursue.
public enum LifeState
{
    Sleep = 0,
    Forage = 1,
    Flee = 2,
    Pursue = 3
}

public static class OrganismLife
{
    // Ordinary waking is Forage. Stronger hostile or alarm → Flee.
    // Weaker hostile → Pursue. Flee wins if both are true.
    public static LifeState ChooseWake(
        bool strongerHostile, bool weakerHostile, bool heardAlarm, bool alertHeld)
    {
        if (strongerHostile || heardAlarm || alertHeld)
            return LifeState.Flee;
        if (weakerHostile)
            return LifeState.Pursue;
        return LifeState.Forage;
    }

    public static bool HostileIsStronger(float selfStrength, float otherStrength)
    {
        return otherStrength > selfStrength;
    }
}
