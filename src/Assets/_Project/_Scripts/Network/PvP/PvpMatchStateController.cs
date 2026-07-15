using Fusion;
using Shield_Shot.GameplayCore.Network.Match;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PvpMatchStateController : NetworkBehaviour
    {
        [Header("Match Rule")]
        [SerializeField] private int _targetScore = 2;

        [Header("Countdown")]
        [SerializeField] private float _countdownSeconds = 3f;

        [Header("Round Flow")]
        [SerializeField] private float _roundEndDelaySeconds = 1.5f;

        [Header("Arena Seed")]
        [SerializeField] private int _defaultArenaSeed = 12345;

        [Networked]
        public PvpMatchState CurrentState { get; private set; }

        [Networked]
        public int BottomScore { get; private set; }

        [Networked]
        public int TopScore { get; private set; }

        [Networked]
        private TickTimer CountdownTimer { get; set; }

        [Networked]
        private TickTimer RoundEndTimer { get; set; }

        [Networked]
        public NetworkBool BottomAugmentReady { get; private set; }

        [Networked]
        public NetworkBool TopAugmentReady { get; private set; }

        [Networked]
        public int ArenaSeed { get; private set; }

        [Networked]
        public NetworkBool ArenaSeedInitialized { get; private set; }

        public int TargetScore => _targetScore;

        public bool IsFighting => CurrentState == PvpMatchState.Fighting;
        public bool IsCountdown => CurrentState == PvpMatchState.Countdown;
        public bool IsMatchEnded => CurrentState == PvpMatchState.MatchEnded;

        public float CountdownRemaining
        {
            get
            {
                if (Runner == null || !CountdownTimer.IsRunning)
                {
                    return 0f;
                }

                return Mathf.Max(0f, CountdownTimer.RemainingTime(Runner) ?? 0f);
            }
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                BottomScore = 0;
                TopScore = 0;
                EnsureArenaSeedInitialized();
                SetState(PvpMatchState.WaitingForPlayers);
            }
            BottomAugmentReady = false;
            TopAugmentReady = false;
            Debug.Log($"[PvpMatchStateController] Spawned. StateAuthority: {Object.HasStateAuthority}");
        }

        public void EnsureArenaSeedInitialized()
        {
            if (!Object.HasStateAuthority || ArenaSeedInitialized)
            {
                return;
            }

            int tickSeed = Runner != null ? Runner.Tick.Raw : 0;
            ArenaSeed = _defaultArenaSeed ^ tickSeed ^ System.Environment.TickCount;
            ArenaSeedInitialized = true;

            Debug.Log($"[PvpMatchStateController] Arena seed initialized: {ArenaSeed}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (CurrentState == PvpMatchState.Countdown &&
                CountdownTimer.Expired(Runner))
            {
                StartFight();
                return;
            }

            if (CurrentState == PvpMatchState.RoundEnded &&
                RoundEndTimer.Expired(Runner))
            {
                StartAugmentSelection();
            }
        }

        public void StartCountdown()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (CurrentState == PvpMatchState.MatchEnded ||
                CurrentState == PvpMatchState.ReturningToLobby)
            {
                return;
            }

            CountdownTimer = TickTimer.CreateFromSeconds(Runner, _countdownSeconds);
            SetState(PvpMatchState.Countdown);

            Debug.Log($"[PvpMatchStateController] Countdown started: {_countdownSeconds}s");
        }

        public void StartFight()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            CountdownTimer = TickTimer.None;
            SetState(PvpMatchState.Fighting);

            Debug.Log("[PvpMatchStateController] Fight started.");
        }

        private void StartRoundEndDelay()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            RoundEndTimer = TickTimer.CreateFromSeconds(Runner, _roundEndDelaySeconds);
            SetState(PvpMatchState.RoundEnded);

            Debug.Log($"[PvpMatchStateController] Round end delay started: {_roundEndDelaySeconds}s");
        }

        private void StartAugmentSelection()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }
            BottomAugmentReady = false;
            TopAugmentReady = false;
            RoundEndTimer = TickTimer.None;
            SetState(PvpMatchState.AugmentSelection);

            Debug.Log("[PvpMatchStateController] Augment selection started.");
        }

        public void SetState(PvpMatchState nextState)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (CurrentState == nextState)
            {
                return;
            }

            PvpMatchState previousState = CurrentState;
            CurrentState = nextState;

            Debug.Log($"[PvpMatchStateController] State changed: {previousState} -> {CurrentState}");
        }

        public void AddScore(PlayerSide scoringSide)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (CurrentState == PvpMatchState.MatchEnded ||
                CurrentState == PvpMatchState.ReturningToLobby)
            {
                return;
            }

            if (scoringSide == PlayerSide.Bottom)
            {
                BottomScore++;
                Debug.Log($"[PvpMatchStateController] Bottom scored. Score: {BottomScore}/{_targetScore}");
            }
            else
            {
                TopScore++;
                Debug.Log($"[PvpMatchStateController] Top scored. Score: {TopScore}/{_targetScore}");
            }

            if (BottomScore >= _targetScore || TopScore >= _targetScore)
            {
                SetState(PvpMatchState.MatchEnded);
                return;
            }

            StartRoundEndDelay();
        }

        public void ResetScores()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            BottomScore = 0;
            TopScore = 0;
            Debug.Log("[PvpMatchStateController] Scores reset.");
        }

        public void NotifyLocalAugmentSelectionCompleted(PlayerSide playerSide)
        {
            if (CurrentState != PvpMatchState.AugmentSelection)
            {
                Debug.LogWarning($"[PvpMatchStateController] Ignore augment ready. CurrentState: {CurrentState}");
                return;
            }

            if (Object.HasStateAuthority)
            {
                SetAugmentReady(playerSide);
                return;
            }

            RPC_SetAugmentReady(playerSide);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SetAugmentReady(PlayerSide playerSide)
        {
            SetAugmentReady(playerSide);
        }

        private void SetAugmentReady(PlayerSide playerSide)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (CurrentState != PvpMatchState.AugmentSelection)
            {
                return;
            }

            if (playerSide == PlayerSide.Bottom)
            {
                BottomAugmentReady = true;
                Debug.Log("[PvpMatchStateController] Bottom augment ready.");
            }
            else
            {
                TopAugmentReady = true;
                Debug.Log("[PvpMatchStateController] Top augment ready.");
            }

            if (BottomAugmentReady && TopAugmentReady)
            {
                PrepareNextRound();
            }
        }

        private void PrepareNextRound()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            ResetAllWeaponHealth();

            BottomAugmentReady = false;
            TopAugmentReady = false;

            StartCountdown();

            Debug.Log("[PvpMatchStateController] Next round prepared.");
        }

        private void ResetAllWeaponHealth()
        {
            PvpWeaponHealth[] healths = FindObjectsByType<PvpWeaponHealth>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < healths.Length; i++)
            {
                if (healths[i] == null)
                {
                    continue;
                }

                healths[i].ResetHealth();
            }
        }
    }
}
