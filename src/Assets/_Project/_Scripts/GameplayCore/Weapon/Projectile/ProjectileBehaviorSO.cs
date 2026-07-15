using UnityEngine;

namespace Shield_Shot.GameplayCore.Weapon.Projectile
{
    public abstract class ProjectileBehaviorSO : ScriptableObject
    {
        [Header("Behavior Base Info")]
        public string BehaviorID;
        public string BehaviorName;
        public Sprite Icon;

        [Header("Network")]
        [SerializeField] private int networkCode;

        public int NetworkCode => networkCode;

        [TextArea(2, 5)]
        public string Description;

        [SerializeField, Min(1)] private int maxLevel = 1;

        [Tooltip("Lower values run earlier. Example: Split=10, Reflect=50")]
        public int Priority = 100;

        public int MaxLevel => Mathf.Max(1, maxLevel);


        public virtual bool CanInject(ProjectileBase projectile)
        {
            return projectile != null;
        }

        public abstract void InjectBehavior(ProjectileBase projectile, int currentLevel);

        public virtual string GetDynamicDescription(int currentLevel)
        {
            int nextLevel = Mathf.Clamp(currentLevel + 1, 1, MaxLevel);

            if (string.IsNullOrWhiteSpace(Description))
            {
                return $"Lv. {nextLevel}/{MaxLevel}";
            }

            try
            {
                return string.Format(Description, currentLevel, nextLevel, MaxLevel);
            }
            catch (System.FormatException)
            {
                return Description;
            }
        }
    }
}
