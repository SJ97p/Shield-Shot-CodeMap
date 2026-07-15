using System;
using Fusion;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [Serializable]
    public struct PvpProjectileAugmentPayload : INetworkStruct
    {
        public const int MaxEntries = 8;

        public PvpProjectileAugmentEntry Entry0;
        public PvpProjectileAugmentEntry Entry1;
        public PvpProjectileAugmentEntry Entry2;
        public PvpProjectileAugmentEntry Entry3;
        public PvpProjectileAugmentEntry Entry4;
        public PvpProjectileAugmentEntry Entry5;
        public PvpProjectileAugmentEntry Entry6;
        public PvpProjectileAugmentEntry Entry7;

        public bool HasAnyAugment =>
            Entry0.IsValid ||
            Entry1.IsValid ||
            Entry2.IsValid ||
            Entry3.IsValid ||
            Entry4.IsValid ||
            Entry5.IsValid ||
            Entry6.IsValid ||
            Entry7.IsValid;

        public bool TryAdd(PvpProjectileAugmentEntry entry)
        {
            if (!entry.IsValid)
            {
                return false;
            }

            if (!Entry0.IsValid) { Entry0 = entry; return true; }
            if (!Entry1.IsValid) { Entry1 = entry; return true; }
            if (!Entry2.IsValid) { Entry2 = entry; return true; }
            if (!Entry3.IsValid) { Entry3 = entry; return true; }
            if (!Entry4.IsValid) { Entry4 = entry; return true; }
            if (!Entry5.IsValid) { Entry5 = entry; return true; }
            if (!Entry6.IsValid) { Entry6 = entry; return true; }
            if (!Entry7.IsValid) { Entry7 = entry; return true; }

            return false;
        }

        public PvpProjectileAugmentEntry GetEntry(int index)
        {
            return index switch
            {
                0 => Entry0,
                1 => Entry1,
                2 => Entry2,
                3 => Entry3,
                4 => Entry4,
                5 => Entry5,
                6 => Entry6,
                7 => Entry7,
                _ => default
            };
        }

        public void ForEach(Action<PvpProjectileAugmentEntry> action)
        {
            if (action == null)
            {
                return;
            }

            for (int i = 0; i < MaxEntries; i++)
            {
                PvpProjectileAugmentEntry entry = GetEntry(i);
                if (entry.IsValid)
                {
                    action.Invoke(entry);
                }
            }
        }

        public static PvpProjectileAugmentPayload Empty => default;
    }
}
