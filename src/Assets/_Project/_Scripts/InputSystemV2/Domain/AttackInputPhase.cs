namespace Shield_Shot.InputSystemV2.Combat.Domain
{
    public enum AttackInputPhase
    {
        Unknown = 0,
        Began = 1,
        AimChanged = 2,
        ChargeChanged = 3,
        Released = 4,
        Canceled = 5
    }
}