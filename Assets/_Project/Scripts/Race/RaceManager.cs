using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RallyAyiti.Race
{
    public enum RacePhase { Countdown, Running, Finished }

    /// <summary>Décompte, tours, classement en direct et fin de course.</summary>
    public sealed class RaceManager : MonoBehaviour
    {
        [Tooltip("Parent des checkpoints, dans l'ordre du tour. Le premier enfant est la ligne d'arrivée.")]
        [SerializeField] private Transform checkpointsRoot;
        [SerializeField, Min(1)] private int totalLaps = 3;
        [SerializeField, Min(1)] private int countdownSeconds = 3;
        [Tooltip("Survol cinématique joué avant le compte à rebours (optionnel).")]
        [SerializeField] private RaceIntro intro;

        public int TotalLaps => totalLaps;

        /// <summary>Nombre de tours imposé par l'épreuve de carrière (appelé par RaceSetup avant le départ).</summary>
        public void SetTotalLaps(int laps) => totalLaps = Mathf.Max(1, laps);
        public int CheckpointCount => _checkpoints.Length;
        public float RaceTime { get; private set; }
        public RacePhase Phase { get; private set; } = RacePhase.Countdown;
        public IReadOnlyList<LapTracker> Standings => _standings;
        public LapTracker Player
        {
            get
            {
                if (_player == null || !_player.IsPlayer) _player = _standings.Find(r => r.IsPlayer);
                return _player;
            }
        }

        private LapTracker _player;

        /// <summary>3, 2, 1 puis 0 au départ.</summary>
        public event Action<int> CountdownTick;
        public event Action RaceStarted;
        public event Action<LapTracker> RacerFinished;
        public event Action RaceEnded;

        private Checkpoint[] _checkpoints = Array.Empty<Checkpoint>();
        private readonly List<LapTracker> _standings = new();
        private Comparison<LapTracker> _byProgress;
        private int _finishedCount;

        /// <summary>Pièces ramassées par le joueur pendant cette course.</summary>
        public int PlayerCoins { get; private set; }
        /// <summary>Gourdes gagnées à l'arrivée (place + pièces), déjà ajoutées à la cagnotte.</summary>
        public int Reward { get; private set; }
        public event Action<int> CoinsChanged;

        private static readonly int[] PlaceRewards = { 300, 200, 150, 80, 80, 80 };
        public const int CoinValue = 5;

        public void AddCoin()
        {
            PlayerCoins++;
            CoinsChanged?.Invoke(PlayerCoins);
        }

        public Checkpoint GetCheckpoint(int index) => _checkpoints[index];
        public int PositionOf(LapTracker racer) => _standings.IndexOf(racer) + 1;

        /// <summary>Course en cours (une seule par scène).</summary>
        public static RaceManager Current { get; private set; }

        private void Awake()
        {
            Current = this;
            var list = new List<Checkpoint>(checkpointsRoot.childCount);
            for (int i = 0; i < checkpointsRoot.childCount; i++)
            {
                if (!checkpointsRoot.GetChild(i).TryGetComponent(out Checkpoint cp)) continue;
                cp.Index = list.Count;
                list.Add(cp);
            }
            _checkpoints = list.ToArray();
            if (_checkpoints.Length < 2)
                Debug.LogError("RaceManager : il faut au moins 2 checkpoints sous checkpointsRoot.", this);

            foreach (LapTracker racer in FindObjectsByType<LapTracker>(FindObjectsSortMode.None))
            {
                racer.Init(this);
                racer.LapCompleted += OnLapCompleted;
                _standings.Add(racer);
            }

            _byProgress = (a, b) => b.Progress.CompareTo(a.Progress);
        }

        private void OnDestroy()
        {
            foreach (LapTracker racer in _standings)
                if (racer != null) racer.LapCompleted -= OnLapCompleted;
        }

        private IEnumerator Start()
        {
            SetControl(false);
            yield return null; // RaceSetup a choisi le pilote du joueur.
            if (intro != null && Player != null) yield return intro.Play(Player.transform);
            var second = new WaitForSeconds(1f);
            for (int i = countdownSeconds; i > 0; i--)
            {
                CountdownTick?.Invoke(i);
                yield return second;
            }
            CountdownTick?.Invoke(0);
            Phase = RacePhase.Running;
            SetControl(true);
            RaceStarted?.Invoke();
        }

        private void Update()
        {
            if (Phase != RacePhase.Running) return;
            RaceTime += Time.deltaTime;
            _standings.Sort(_byProgress);
        }

        private void SetControl(bool enabled)
        {
            foreach (LapTracker racer in _standings) racer.Kart.ControlEnabled = enabled;
        }

        private void OnLapCompleted(LapTracker racer)
        {
            if (Phase != RacePhase.Running || racer.Finished || racer.Lap < totalLaps) return;

            racer.MarkFinished(RaceTime);
            _finishedCount++;
            if (racer.IsPlayer) racer.Kart.ControlEnabled = false;
            RacerFinished?.Invoke(racer);

            if (racer.IsPlayer || _finishedCount == _standings.Count) EndRace();
        }

        private void EndRace()
        {
            if (Phase == RacePhase.Finished) return;
            _standings.Sort(_byProgress);
            Phase = RacePhase.Finished;
            if (Player != null)
            {
                int place = PositionOf(Player);
                Reward = PlaceRewards[Mathf.Clamp(place - 1, 0, PlaceRewards.Length - 1)] + PlayerCoins * CoinValue;
                Core.GameSettings.Gourdes += Reward;
            }
            RaceEnded?.Invoke();
        }
    }
}
