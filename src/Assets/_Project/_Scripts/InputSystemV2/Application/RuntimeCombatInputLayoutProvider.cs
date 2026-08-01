using Shield_Shot.InputSystemV2.Combat.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public sealed class RuntimeCombatInputLayoutProvider
        : ICombatInputLayoutProvider
    {
        private CombatInputLayout currentLayout;

        public CombatInputLayout CurrentLayout =>
            currentLayout;

        public RuntimeCombatInputLayoutProvider(
            in CombatInputLayout initialLayout)
        {
            currentLayout =
                CopyValidated(in initialLayout);
        }

        public void Apply(
            in CombatInputLayout layout)
        {
            currentLayout =
                CopyValidated(in layout);
        }

        private static CombatInputLayout CopyValidated(
            in CombatInputLayout source)
        {
            /*
             * default(CombatInputLayout)처럼 유효하지 않은 값이
             * 들어오는 것을 막기 위해 생성자를 다시 통과시킨다.
             */
            return new CombatInputLayout(
                source.SplitDirection,
                source.SplitRatio,
                source.IsInverted);
        }
    }
}