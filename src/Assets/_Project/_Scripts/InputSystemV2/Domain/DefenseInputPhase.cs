namespace Shield_Shot.InputSystemV2.Combat.Domain
{
    public enum DefenseInputPhase
    {
        Unknown = 0,
        Began = 1,
        DirectionChanged = 2,
        Released = 3,
        Canceled = 4
    }
}