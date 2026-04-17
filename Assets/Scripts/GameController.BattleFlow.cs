using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Last d6 roll shown in battle HUD (one step at a time).</summary>
    public readonly struct BattleUiDiceRoll
    {
        public readonly UnitType UnitType;
        public readonly bool AttackerRolling;
        public readonly int Dice;
        public readonly int Need;
        public readonly bool Impossible;
        public readonly int Hits;
        public readonly int[] Rolls;

        public BattleUiDiceRoll(UnitType unitType, bool attackerRolling, int dice, int need, bool impossible, int hits,
            int[] rolls)
        {
            UnitType = unitType;
            AttackerRolling = attackerRolling;
            Dice = dice;
            Need = need;
            Impossible = impossible;
            Hits = hits;
            Rolls = rolls ?? Array.Empty<int>();
        }
    }

    public partial class GameController
    {
        [Header("Cards & full battle")]
        [Tooltip("Interactive battle order, Energize, casualty pick, secrets. Off = legacy auto-resolve.")]
        public bool UseFullBattleFlow = true;

        [Tooltip("Skip arrangement/Energize/casualty UI; weakest-first casualties, auto-pass Energize.")]
        public bool AutoResolveBattlesQuick = false;

        Queue<UnifiedEnergizeDraw> _unifiedEnergizeDeck;
        Queue<SecretMissionInHand> _secretDeck;
        System.Random _cardRng;
        int _nextSecretInstanceId = 1;
        /// <summary>Round index: 1 until play returns to the first player, then 2, etc. (not per seat turn).</summary>
        int _turnNumber = 1;

        public string LastDrawPhaseLog { get; private set; } = "";
        public int EnergizeDeckCount => _unifiedEnergizeDeck != null ? _unifiedEnergizeDeck.Count : 0;
        public int SecretDeckCount => _secretDeck != null ? _secretDeck.Count : 0;
        /// <summary>Round number (increments when turn order returns to player 1 / index 0).</summary>
        public int TurnNumber => _turnNumber;

        public bool BattlePhaseBlockingPlay { get; private set; }
        public List<PlannedBattleEntry> BattlePlan { get; private set; } = new List<PlannedBattleEntry>();
        public bool PendingBattleArrangement { get; private set; }

        public PlayerState EnergizePromptPlayer { get; private set; }
        public string EnergizeBattleContext { get; private set; }
        bool _energizeRoundActive;
        Coroutine _battleCoroutine;

        public CasualtyPickState CasualtyPick { get; private set; }

        public PlayerState FocusFirePicker { get; private set; }
        public bool FocusFireForAttackerSide { get; private set; }
        BoardTile _focusFireHex;
        public BoardTile FocusFireBattleHex => _focusFireHex;

        public SecretMissionOfferState SecretMissionOffer { get; private set; }

        public DragonPhaseState DragonPhase { get; private set; }

        PlayerState _battleAttacker;
        PlayerState _battleDefender;
        BoardTile _battleHex;
        public BoardTile ActiveBattleHex => _battleHex;
        public PlayerState ActiveBattleAttacker => _battleAttacker;
        public PlayerState ActiveBattleDefender => _battleDefender;
        public bool HasActiveBattleStep { get; private set; }
        public UnitType ActiveBattleStepUnitType { get; private set; }
        public int ActiveBattleHitsOnAttacker { get; private set; }
        public int ActiveBattleHitsOnDefender { get; private set; }
        BattleEnergizeModifiers _mods;
        System.Random _battleRng;
        List<string> _liveBattleLines;

        BattleUiDiceRoll? _lastBattleUiDiceRoll;
        bool _battleClashIntroActive;

        /// <summary>Shown between battle confirmation and Energize (placeholder until sword art exists).</summary>
        public bool BattleClashIntroActive => _battleClashIntroActive;

        public BattleUiDiceRoll? LastBattleUiDiceRoll => _lastBattleUiDiceRoll;

        /// <summary>Battle HUD cycles random pip faces for this long before showing the real roll.</summary>
        public const float BattleDiceRollSpinSeconds = 0.48f;

        /// <summary>After dice are revealed, wait this long before resolving the next unit roll in the coroutine.</summary>
        public const float BattleDiceRollHoldSeconds = 0.5f;

        const float BattleClashIntroSeconds = 0.55f;

        void SetBattleUiDiceRoll(BattleResolver.DiceRollResult roll, UnitType unitType, bool attackerRolling)
        {
            int[] copy = roll.Rolls != null && roll.Rolls.Count > 0
                ? roll.Rolls.ToArray()
                : Array.Empty<int>();
            _lastBattleUiDiceRoll = new BattleUiDiceRoll(unitType, attackerRolling, roll.Dice, roll.Need,
                roll.ImpossibleToHit, roll.Hits, copy);
        }

        public string LiveBattlePhaseLog
        {
            get
            {
                if (_liveBattleLines == null || _liveBattleLines.Count == 0)
                    return "";

                // Keep this intentionally short so it fits the HUD box.
                const int maxLines = 16;
                int start = Mathf.Max(0, _liveBattleLines.Count - maxLines);

                var sb = new StringBuilder();
                for (int i = start; i < _liveBattleLines.Count; i++)
                    sb.AppendLine(_liveBattleLines[i]);

                return sb.ToString().TrimEnd();
            }
        }

        public void AppendBattleLog(string line)
        {
            _liveBattleLines?.Add(line);
            Debug.Log("[Battle] " + line);
            NexusBattleDebug.LogBattle(line);
        }

        void InitCardDecks()
        {
            _cardRng = new System.Random(Environment.TickCount ^ 0x5EED);
            _unifiedEnergizeDeck = CardDecks.BuildUnifiedEnergizeDeck(_cardRng);
            _secretDeck = CardDecks.BuildSecretDeck(_cardRng, ref _nextSecretInstanceId);
        }

        /// <summary>Stop battle coroutine and clear interactive battle / dragon UI state (new match).</summary>
        public void HardResetFlowState()
        {
            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }

            BattlePhaseBlockingPlay = false;
            PendingBattleArrangement = false;
            BattlePlan.Clear();
            EnergizePromptPlayer = null;
            EnergizeBattleContext = null;
            HasActiveBattleStep = false;
            ActiveBattleHitsOnAttacker = 0;
            ActiveBattleHitsOnDefender = 0;
            _energizeRoundActive = false;
            CasualtyPick = null;
            FocusFirePicker = null;
            SecretMissionOffer = null;
            DragonPhase = null;
            _liveBattleLines = null;
            _lastEnergizePlayed = EnergizeBattleId.None;
            _pendingFocusFireCard = false;
            _lastBattleUiDiceRoll = null;
            _battleClashIntroActive = false;
            _turnNumber = 1;
            _nextSecretInstanceId = 1;
            LastDrawPhaseLog = "";
            LastBattlePhaseLog = "";
            _miningIncomeFlightsForHud = null;
            _victoryPointFlightsForHud = null;
        }

        void RunDrawPhase(PlayerState player)
        {
            if (player == null)
                return;

            int secretBefore = player.SecretMissions.Count;
            int battleBefore = player.BattleEnergize.Count;
            int deployBefore = player.DeployEnergize.Count;
            DrawSecretMission(player, 1);
            bool monolith = false;
            if (PlayerControlsMonolithAlone(player))
            {
                DrawEnergizeCards(player, 2);
                monolith = true;
            }

            int secretGained = player.SecretMissions.Count - secretBefore;
            int battleGained = player.BattleEnergize.Count - battleBefore;
            int deployGained = player.DeployEnergize.Count - deployBefore;

            LastDrawPhaseLog =
                $"Turn {_turnNumber} Draw: P{player.PlayerIndex + 1} +{secretGained} Secret" +
                (monolith ? $" | Monolith +{battleGained + deployGained} Energize ({battleGained} battle, {deployGained} deploy)" : "") +
                $" | Decks: Secret {SecretDeckCount}, Energize {EnergizeDeckCount}";
        }

        bool PlayerControlsMonolithAlone(PlayerState player)
        {
            foreach (var tile in Board.AllTiles)
            {
                if (tile.Type != TileType.Monolith)
                    continue;

                PlayerState sole = null;
                foreach (var u in FindObjectsOfType<UnitInstance>())
                {
                    if (u.Tile != tile)
                        continue;
                    if (sole == null)
                        sole = u.Owner;
                    else if (sole != u.Owner)
                        return false;
                }

                return sole == player;
            }

            return false;
        }

        void DrawSecretMission(PlayerState p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (_secretDeck.Count == 0)
                    _secretDeck = CardDecks.BuildSecretDeck(_cardRng, ref _nextSecretInstanceId);
                if (_secretDeck.Count > 0)
                    p.SecretMissions.Add(_secretDeck.Dequeue());
            }
        }

        void DrawEnergizeCards(PlayerState p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (_unifiedEnergizeDeck.Count == 0)
                    _unifiedEnergizeDeck = CardDecks.BuildUnifiedEnergizeDeck(_cardRng);
                if (_unifiedEnergizeDeck.Count == 0)
                    break;
                var c = _unifiedEnergizeDeck.Dequeue();
                if (c.IsDeployment)
                    p.DeployEnergize.Add(c.Deploy);
                else
                    p.BattleEnergize.Add(c.Battle);
            }
        }

        /// <summary>Play a Deployment-phase Energize. FreeHuman needs a home-base tile owned by current player.</summary>
        public bool TryPlayDeploymentEnergize(EnergizeDeploymentId id, BoardTile selectedHomeHex)
        {
            if (IsGameOver || BattlePhaseBlockingPlay || DragonPhase != null)
                return false;
            if (AnyMovementOccurredThisTurn)
                return false;

            var p = CurrentPlayer;
            if (p == null || !p.DeployEnergize.Contains(id))
                return false;

            switch (id)
            {
                case EnergizeDeploymentId.StripMine:
                    p.Rubium += 2;
                    break;
                case EnergizeDeploymentId.Convoy:
                    DrawEnergizeCards(p, 1);
                    break;
                case EnergizeDeploymentId.RushOrder:
                    p.DeploymentPurchaseDiscountRubium += 2;
                    break;
                case EnergizeDeploymentId.FreeHuman:
                    if (!CanDeployToStartingHomeTile(p, selectedHomeHex))
                        return false;
                    SpawnUnit(p, UnitType.Human, selectedHomeHex);
                    break;
                case EnergizeDeploymentId.SupplyRun:
                    p.Rubium += 1;
                    DrawEnergizeCards(p, 1);
                    break;
                default:
                    return false;
            }

            p.DeployEnergize.Remove(id);
            return true;
        }

        void BeginBattleArrangement(PlayerState attacker)
        {
            if (IsGameOver)
                return;

            BattlePlan.Clear();
            var hexes = BattleResolver.FindContestedHexesForAttacker(attacker);
            hexes.Sort((a, b) =>
            {
                int c = a.Q.CompareTo(b.Q);
                return c != 0 ? c : a.R.CompareTo(b.R);
            });

            foreach (var hex in hexes)
            {
                var opps = BattleResolver.OpponentsOnHex(hex, attacker);
                if (opps.Count == 0)
                    continue;
                BattlePlan.Add(new PlannedBattleEntry
                {
                    Hex = hex,
                    DefenderPlayerIndex = opps[0].PlayerIndex
                });
            }

            if (BattlePlan.Count == 0)
                return;

            var planMsg = new StringBuilder();
            planMsg.Append($"Contested hexes ({BattlePlan.Count}) for P{attacker.PlayerIndex + 1}: ");
            planMsg.Append(string.Join(" | ",
                BattlePlan.Select(e => $"({e.Hex.Q},{e.Hex.R}) vs P{e.DefenderPlayerIndex + 1}")));
            Debug.Log("[Battle] " + planMsg);

            if (!UseFullBattleFlow)
            {
                RunLegacyAutoBattle(attacker);
                PendingBattleArrangement = false;
                BattlePhaseBlockingPlay = false;
                return;
            }

            if (AutoResolveBattlesQuick)
            {
                PendingBattleArrangement = false;
                StartBattleCoroutine(attacker);
                return;
            }

            // If there is only one battle and only one valid defender on that hex,
            // skip arrangement UI and start immediately.
            if (BattlePlan.Count == 1)
            {
                var only = BattlePlan[0];
                var opps = BattleResolver.OpponentsOnHex(only.Hex, attacker);
                if (opps.Count <= 1)
                {
                    PendingBattleArrangement = false;
                    StartBattleCoroutine(attacker);
                    return;
                }
            }

            PendingBattleArrangement = true;
            BattlePhaseBlockingPlay = true;
        }

        public void MoveBattlePlanEntry(int index, int delta)
        {
            int ni = index + delta;
            if (index < 0 || index >= BattlePlan.Count || ni < 0 || ni >= BattlePlan.Count)
                return;
            var t = BattlePlan[index];
            BattlePlan[index] = BattlePlan[ni];
            BattlePlan[ni] = t;
        }

        public void SetBattleDefenderForEntry(int planIndex, int defenderPlayerIndex)
        {
            if (planIndex < 0 || planIndex >= BattlePlan.Count)
                return;
            var e = BattlePlan[planIndex];
            var opps = BattleResolver.OpponentsOnHex(e.Hex, Players[_currentPlayerIndex]);
            foreach (var o in opps)
            {
                if (o.PlayerIndex == defenderPlayerIndex)
                {
                    e.DefenderPlayerIndex = defenderPlayerIndex;
                    break;
                }
            }
        }

        public void ConfirmBattleArrangement()
        {
            if (!PendingBattleArrangement)
                return;
            PendingBattleArrangement = false;
            Debug.Log("[Battle] Battle order confirmed — resolving");
            StartBattleCoroutine(CurrentPlayer);
        }

        void StartBattleCoroutine(PlayerState attacker)
        {
            if (_battleCoroutine != null)
                StopCoroutine(_battleCoroutine);
            _battleCoroutine = StartCoroutine(BattlePhaseCoroutine(attacker));
        }

        IEnumerator BattlePhaseCoroutine(PlayerState attacker)
        {
            BattlePhaseBlockingPlay = true;
            var log = new StringBuilder();
            _battleRng = new System.Random(Environment.TickCount ^ (attacker.PlayerIndex << 8));

            if (BattlePlan.Count == 0)
            {
                BattlePhaseBlockingPlay = false;
                _battleCoroutine = null;
                yield break;
            }

            Debug.Log(
                $"[Battle] --- Phase start: P{attacker.PlayerIndex + 1} attacker, {BattlePlan.Count} battle(s) ---");

            foreach (var entry in BattlePlan)
            {
                if (IsGameOver)
                    yield break;

                var hex = entry.Hex;
                var defender = Players.Find(p => p.PlayerIndex == entry.DefenderPlayerIndex);
                if (defender == null || hex == null)
                    continue;

                Debug.Log(
                    $"[Battle] >>> Hex ({hex.Q},{hex.R}): P{attacker.PlayerIndex + 1} (attacker) vs P{defender.PlayerIndex + 1} (defender)");

                _mods = new BattleEnergizeModifiers();
                _battleAttacker = attacker;
                _battleDefender = defender;
                _battleHex = hex;
                _lastBattleUiDiceRoll = null;

                if (!AutoResolveBattlesQuick)
                {
                    _battleClashIntroActive = true;
                    yield return new WaitForSeconds(BattleClashIntroSeconds);
                    _battleClashIntroActive = false;
                }

                if (!AutoResolveBattlesQuick)
                    yield return StartCoroutine(EnergizePassCoroutine(attacker, defender, hex));

                int defStart = CountParticipants(hex, defender);
                bool defLostDragon = false;

                var battleLines = new List<string>();
                _liveBattleLines = battleLines;

                yield return StartCoroutine(RunBattleStepsCoroutine(
                    hex, attacker, defender, _battleRng,
                    (a, d, line) => AppendBattleLog(line),
                    _ => { },
                    () => { defLostDragon = true; }));

                foreach (var l in battleLines)
                    log.AppendLine(l);

                _liveBattleLines = null;

                RefreshPoolsLocal(hex, attacker, defender, out _, out var dLeft);
                int defEnd = dLeft.Count;
                int defCasualties = defStart - defEnd;
                bool attackerWin = defEnd == 0;

                if (attackerWin)
                {
                    attacker.VictoryPoints += 1;
                    QueueVictoryPointHudFlight(attacker, 1);
                    log.AppendLine($"P{attacker.PlayerIndex + 1} wins battle (+1 VP).");
                    if (MetaProgression.Instance != null)
                        MetaProgression.Instance.OnBattleWinReward();
                    if (CheckGameEndAfterVpChange())
                    {
                        log.AppendLine("Game over.");
                        log.AppendLine("---");
                        break;
                    }

                    DrawEnergizeCards(defender, 1);
                    if (!AutoResolveBattlesQuick)
                    {
                        SecretMissionOffer = BuildSecretOffer(attacker, defCasualties, defStart, defLostDragon);
                        while (SecretMissionOffer != null && SecretMissionOffer.Waiting)
                            yield return null;
                        SecretMissionOffer = null;
                        if (IsGameOver)
                        {
                            log.AppendLine("---");
                            break;
                        }
                    }
                }
                else
                    log.AppendLine("Defender holds the hex.");

                log.AppendLine("---");
            }

            LastBattlePhaseLog = log.ToString().TrimEnd();
            BattlePlan.Clear();
            BattlePhaseBlockingPlay = false;
            _battleHex = null;
            HasActiveBattleStep = false;
            ActiveBattleHitsOnAttacker = 0;
            ActiveBattleHitsOnDefender = 0;
            _battleCoroutine = null;
            Debug.Log("[Battle] --- Phase complete ---");
        }

        SecretMissionOfferState BuildSecretOffer(PlayerState attacker, int casualtiesInflicted, int defStartCount, bool dragonKilled)
        {
            var eligible = new List<int>();
            for (int i = 0; i < attacker.SecretMissions.Count; i++)
            {
                var s = attacker.SecretMissions[i];
                if (s.Kind != SecretMissionKind.Battle)
                    continue;
                if (MeetsBattleMission(s, casualtiesInflicted, defStartCount, dragonKilled))
                    eligible.Add(i);
            }

            if (eligible.Count == 0)
                return null;

            return new SecretMissionOfferState
            {
                Attacker = attacker,
                EligibleIndices = eligible,
                Waiting = true
            };
        }

        static bool MeetsBattleMission(SecretMissionInHand s, int defenderCasualties, int _, bool dragonKilled)
        {
            switch (s.MissionTypeId)
            {
                case SecretMissionTypes.WinAnyBattle:
                    return true;
                case SecretMissionTypes.WinBattleKillTwoPlus:
                    return defenderCasualties >= 2;
                case SecretMissionTypes.WinBattleEnemyLostDragon:
                    return dragonKilled;
                default:
                    return false;
            }
        }

        public void PlaySecretMissionAtIndex(int indexInHand)
        {
            if (SecretMissionOffer == null || !SecretMissionOffer.Waiting)
                return;
            if (!SecretMissionOffer.EligibleIndices.Contains(indexInHand))
                return;

            var p = SecretMissionOffer.Attacker;
            var s = p.SecretMissions[indexInHand];
            p.VictoryPoints += s.VictoryPoints;
            QueueVictoryPointHudFlight(p, s.VictoryPoints);
            p.SecretMissions.RemoveAt(indexInHand);
            SecretMissionOffer.Waiting = false;
            Debug.Log($"[Battle] Secret mission played: P{p.PlayerIndex + 1} +{s.VictoryPoints} VP (index {indexInHand})");
            CheckGameEndAfterVpChange();
        }

        public void SkipSecretMissionPlay()
        {
            if (SecretMissionOffer != null)
            {
                Debug.Log(
                    $"[Battle] Secret mission: P{SecretMissionOffer.Attacker.PlayerIndex + 1} skipped optional play");
                SecretMissionOffer.Waiting = false;
            }
        }

        IEnumerator EnergizePassCoroutine(PlayerState attacker, PlayerState defender, BoardTile hex)
        {
            EnergizeBattleContext = $"⬡({hex.Q},{hex.R})  P{attacker.PlayerIndex + 1}⚔P{defender.PlayerIndex + 1}";
            Debug.Log($"[Battle] Energize window: {EnergizeBattleContext}");
            bool played;
            do
            {
                played = false;
                foreach (var p in EnergizePlayerOrder(attacker, defender))
                {
                    EnergizePromptPlayer = p;
                    _energizeRoundActive = true;
                    while (_energizeRoundActive)
                        yield return null;

                    if (_lastEnergizePlayed != EnergizeBattleId.None)
                    {
                        played = true;
                        var id = _lastEnergizePlayed;
                        ApplyEnergizeCard(id, p, attacker, defender);
                        Debug.Log(
                            $"[Battle] Energize: P{p.PlayerIndex + 1} played {EnergizeBattleCatalog.GetName(id)}");
                        _lastEnergizePlayed = EnergizeBattleId.None;
                    }
                }
            } while (played);

            Debug.Log("[Battle] Energize: both sides done (pass chain complete)");
            EnergizePromptPlayer = null;
            EnergizeBattleContext = null;
        }

        EnergizeBattleId _lastEnergizePlayed = EnergizeBattleId.None;

        public void SubmitEnergizePass()
        {
            if (EnergizePromptPlayer == null)
                return;
            Debug.Log($"[Battle] Energize: P{EnergizePromptPlayer.PlayerIndex + 1} pass");
            _lastEnergizePlayed = EnergizeBattleId.None;
            _energizeRoundActive = false;
        }

        public void SubmitEnergizePlay(EnergizeBattleId id)
        {
            if (EnergizePromptPlayer == null || id == EnergizeBattleId.None)
                return;
            if (!EnergizePromptPlayer.BattleEnergize.Contains(id))
                return;

            EnergizePromptPlayer.BattleEnergize.Remove(id);
            if (id == EnergizeBattleId.BattleCache)
                DrawEnergizeCards(EnergizePromptPlayer, 1);

            if (id == EnergizeBattleId.FocusFire)
            {
                _lastEnergizePlayed = id;
                FocusFirePicker = EnergizePromptPlayer;
                FocusFireForAttackerSide = EnergizePromptPlayer == _battleAttacker;
                _focusFireHex = _battleHex;
                _pendingFocusFireCard = true;
                return;
            }

            _lastEnergizePlayed = id;
            _energizeRoundActive = false;
        }

        bool _pendingFocusFireCard;

        public void SubmitFocusFireUnitType(UnitType type)
        {
            if (FocusFirePicker == null || !_pendingFocusFireCard)
                return;

            if (FocusFireForAttackerSide)
            {
                _mods.AttackerFocusFireType = type;
                _mods.AttackerFocusFireExtraDice = 2;
            }
            else
            {
                _mods.DefenderFocusFireType = type;
                _mods.DefenderFocusFireExtraDice = 2;
            }

            Debug.Log(
                $"[Battle] Focus Fire: P{FocusFirePicker.PlayerIndex + 1} → +2 dice on {type} ({(FocusFireForAttackerSide ? "attacker" : "defender")} side)");
            FocusFirePicker = null;
            _pendingFocusFireCard = false;
            _energizeRoundActive = false;
        }

        public void CancelFocusFireRefund()
        {
            if (FocusFirePicker == null || !_pendingFocusFireCard)
                return;
            FocusFirePicker.BattleEnergize.Add(EnergizeBattleId.FocusFire);
            FocusFirePicker = null;
            _pendingFocusFireCard = false;
            _lastEnergizePlayed = EnergizeBattleId.None;
            _energizeRoundActive = false;
        }

        IEnumerable<PlayerState> EnergizePlayerOrder(PlayerState attacker, PlayerState defender)
        {
            yield return attacker;
            yield return defender;
            int n = Players.Count;
            int start = (defender.PlayerIndex + 1) % n;
            for (int k = 0; k < n; k++)
            {
                var p = Players[(start + k) % n];
                if (p == attacker || p == defender)
                    continue;
                yield return p;
            }
        }

        void ApplyEnergizeCard(EnergizeBattleId id, PlayerState who, PlayerState attacker, PlayerState defender)
        {
            bool isAtt = who == attacker;
            switch (id)
            {
                case EnergizeBattleId.BattleFury:
                    if (isAtt) _mods.AttackerDiceBonus++;
                    else _mods.DefenderDiceBonus++;
                    break;
                case EnergizeBattleId.Elusive:
                    if (isAtt) _mods.HitThresholdBonusWhenAttackingAttacker++;
                    else _mods.HitThresholdBonusWhenAttackingDefender++;
                    break;
                case EnergizeBattleId.DeadlyAim:
                    if (isAtt) _mods.AttackerHitThresholdReduction++;
                    else _mods.DefenderHitThresholdReduction++;
                    break;
                case EnergizeBattleId.Aegis:
                    if (isAtt) _mods.AttackerIgnoresNextHit = true;
                    else _mods.DefenderIgnoresNextHit = true;
                    break;
                case EnergizeBattleId.BattleCache:
                    break;
                case EnergizeBattleId.FocusFire:
                    break;
            }
        }

        IEnumerator RunBattleStepsCoroutine(
            BoardTile hex,
            PlayerState attacker,
            PlayerState defender,
            System.Random rng,
            Action<PlayerState, PlayerState, string> logLine,
            Action<int> onDefenderCasualty,
            Action onDefenderDragonKilled)
        {
            void Log(string s)
            {
                string ts = DateTime.Now.ToString("HH:mm:ss");
                string step = HasActiveBattleStep ? $"[{ActiveBattleStepUnitType}]" : "[Battle]";
                logLine(attacker, defender, $"[{ts}] {step} {s}");
            }

            Log($"Battle at ({hex.Q},{hex.R}): P{attacker.PlayerIndex + 1} vs P{defender.PlayerIndex + 1}");

            bool aegisAtt = _mods.AttackerIgnoresNextHit;
            bool aegisDef = _mods.DefenderIgnoresNextHit;

            foreach (var unitType in BattleResolver.BattleOrder)
            {
                RefreshPoolsLocal(hex, attacker, defender, out var aliveAtt, out var aliveDef);
                if (aliveDef.Count == 0)
                {
                    HasActiveBattleStep = false;
                    ActiveBattleHitsOnAttacker = 0;
                    ActiveBattleHitsOnDefender = 0;
                    Log("Defender eliminated.");
                    yield break;
                }

                if (aliveAtt.Count == 0)
                {
                    HasActiveBattleStep = false;
                    ActiveBattleHitsOnAttacker = 0;
                    ActiveBattleHitsOnDefender = 0;
                    Log("Attacker eliminated from hex.");
                    yield break;
                }

                var attOfType = aliveAtt.FindAll(u => u.Definition.Type == unitType);
                var defOfType = aliveDef.FindAll(u => u.Definition.Type == unitType);
                attOfType.RemoveAll(u => u == null || u.Tile != hex || u.Owner != attacker);
                defOfType.RemoveAll(u => u == null || u.Tile != hex || u.Owner != defender);
                if (attOfType.Count == 0 && defOfType.Count == 0)
                    continue;
                HasActiveBattleStep = true;
                ActiveBattleStepUnitType = unitType;

                int hitsOnAttacker = 0;
                foreach (var u in defOfType)
                {
                    int extra = _mods.DefenderDiceBonus;
                    if (_mods.DefenderFocusFireType == unitType)
                        extra += _mods.DefenderFocusFireExtraDice;
                    int shift = _mods.HitThresholdBonusWhenAttackingAttacker - _mods.DefenderHitThresholdReduction;
                    var roll = BattleResolver.RollDiceForUnit(u.Definition, rng, extra, shift);
                    hitsOnAttacker += roll.Hits;
                    SetBattleUiDiceRoll(roll, unitType, false);
                    if (!AutoResolveBattlesQuick)
                        yield return new WaitForSeconds(BattleDiceRollSpinSeconds + BattleDiceRollHoldSeconds);
                    if (roll.Dice > 0 && roll.Rolls != null && roll.Rolls.Count > 0)
                    {
                        Log($"  {unitType} (def): rolled {roll.Dice}d6 [{string.Join(",", roll.Rolls)}], need >= {roll.Need} => {roll.Hits} hit(s)");
                    }
                    else if (roll.Dice > 0 && roll.ImpossibleToHit)
                    {
                        Log($"  {unitType} (def): {roll.Dice}d6, need >= {roll.Need} (impossible) => 0 hit(s)");
                    }
                    else
                    {
                        Log($"  {unitType} (def): {roll.Dice} dice => 0 hit(s)");
                    }
                }

                int hitsOnDefender = 0;
                foreach (var u in attOfType)
                {
                    int extra = _mods.AttackerDiceBonus;
                    if (_mods.AttackerFocusFireType == unitType)
                        extra += _mods.AttackerFocusFireExtraDice;
                    int shift = _mods.HitThresholdBonusWhenAttackingDefender - _mods.AttackerHitThresholdReduction;
                    var roll = BattleResolver.RollDiceForUnit(u.Definition, rng, extra, shift);
                    hitsOnDefender += roll.Hits;
                    SetBattleUiDiceRoll(roll, unitType, true);
                    if (!AutoResolveBattlesQuick)
                        yield return new WaitForSeconds(BattleDiceRollSpinSeconds + BattleDiceRollHoldSeconds);
                    if (roll.Dice > 0 && roll.Rolls != null && roll.Rolls.Count > 0)
                    {
                        Log($"  {unitType} (atk): rolled {roll.Dice}d6 [{string.Join(",", roll.Rolls)}], need >= {roll.Need} => {roll.Hits} hit(s)");
                    }
                    else if (roll.Dice > 0 && roll.ImpossibleToHit)
                    {
                        Log($"  {unitType} (atk): {roll.Dice}d6, need >= {roll.Need} (impossible) => 0 hit(s)");
                    }
                    else
                    {
                        Log($"  {unitType} (atk): {roll.Dice} dice => 0 hit(s)");
                    }
                }

                RefreshPoolsLocal(hex, attacker, defender, out aliveAtt, out aliveDef);

                int capAtt = Mathf.Min(hitsOnAttacker, aliveAtt.Count);
                if (aegisAtt && capAtt > 0)
                {
                    capAtt--;
                    aegisAtt = false;
                    Log("    Aegis: first hit vs attacker ignored.");
                }
                ActiveBattleHitsOnAttacker = capAtt;

                if (capAtt > 0)
                {
                    if (AutoResolveBattlesQuick || !UseFullBattleFlow)
                    {
                        foreach (var v in BattleResolver.PickCasualtiesWeakestFirst(aliveAtt, capAtt))
                        {
                            Log($"    → P{attacker.PlayerIndex + 1} dies: {v.Definition.Type}");
                            RemoveUnit(v);
                        }
                    }
                    else
                    {
                        var validPool = new List<UnitInstance>();
                        foreach (var u in aliveAtt)
                        {
                            if (u != null)
                                validPool.Add(u);
                        }
                        int required = Mathf.Min(capAtt, validPool.Count);
                        if (required <= 0)
                            continue;

                        CasualtyPick = new CasualtyPickState
                        {
                            Owner = attacker,
                            Pool = validPool,
                            Required = required,
                            Selected = new List<UnitInstance>()
                        };
                        while (CasualtyPick != null)
                            yield return null;
                    }
                }

                RefreshPoolsLocal(hex, attacker, defender, out aliveAtt, out aliveDef);
                int capDef = Mathf.Min(hitsOnDefender, aliveDef.Count);
                if (aegisDef && capDef > 0)
                {
                    capDef--;
                    aegisDef = false;
                    Log("    Aegis: first hit vs defender ignored.");
                }
                ActiveBattleHitsOnDefender = capDef;

                if (capDef > 0)
                {
                    if (AutoResolveBattlesQuick || !UseFullBattleFlow)
                    {
                        foreach (var v in BattleResolver.PickCasualtiesWeakestFirst(aliveDef, capDef))
                        {
                            Log($"    → P{defender.PlayerIndex + 1} dies: {v.Definition.Type}");
                            onDefenderCasualty(1);
                            if (v.Definition.Type == UnitType.RubiumDragon)
                                onDefenderDragonKilled();
                            RemoveUnit(v);
                        }
                    }
                    else
                    {
                        var validPool = new List<UnitInstance>();
                        foreach (var u in aliveDef)
                        {
                            if (u != null)
                                validPool.Add(u);
                        }
                        int required = Mathf.Min(capDef, validPool.Count);
                        if (required <= 0)
                            continue;

                        CasualtyPick = new CasualtyPickState
                        {
                            Owner = defender,
                            Pool = validPool,
                            Required = required,
                            Selected = new List<UnitInstance>(),
                            OnEachRemove = u =>
                            {
                                onDefenderCasualty(1);
                                if (u.Definition.Type == UnitType.RubiumDragon)
                                    onDefenderDragonKilled();
                            }
                        };
                        while (CasualtyPick != null)
                            yield return null;
                    }
                }
            }

            HasActiveBattleStep = false;
            ActiveBattleHitsOnAttacker = 0;
            ActiveBattleHitsOnDefender = 0;
            _lastBattleUiDiceRoll = null;
        }

        public void SubmitCasualtyPick()
        {
            if (CasualtyPick == null)
                return;
            CasualtyPick.Pool.RemoveAll(u => u == null);
            CasualtyPick.Selected.RemoveAll(u => u == null || !CasualtyPick.Pool.Contains(u));
            CasualtyPick.Required = Mathf.Clamp(CasualtyPick.Required, 0, CasualtyPick.Pool.Count);
            if (CasualtyPick.Required == 0)
            {
                CasualtyPick = null;
                return;
            }
            if (CasualtyPick.Selected.Count != CasualtyPick.Required)
                return;

            foreach (var v in CasualtyPick.Selected)
            {
                AppendBattleLog(
                    $"    → P{CasualtyPick.Owner.PlayerIndex + 1} dies: {v.Definition.Type}");
                CasualtyPick.OnEachRemove?.Invoke(v);
                RemoveUnit(v);
            }

            CasualtyPick = null;
        }

        public void ToggleCasualtyUnit(UnitInstance u)
        {
            if (CasualtyPick == null || u == null || !CasualtyPick.Pool.Contains(u))
                return;
            if (CasualtyPick.Selected.Contains(u))
                CasualtyPick.Selected.Remove(u);
            else if (CasualtyPick.Selected.Count < CasualtyPick.Required)
                CasualtyPick.Selected.Add(u);
        }

        static void RefreshPoolsLocal(BoardTile hex, PlayerState attacker, PlayerState defender,
            out List<UnitInstance> aliveAtt, out List<UnitInstance> aliveDef)
        {
            aliveAtt = new List<UnitInstance>();
            aliveDef = new List<UnitInstance>();
            foreach (var u in UnityEngine.Object.FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Tile != hex)
                    continue;
                if (u.Owner == attacker)
                    aliveAtt.Add(u);
                else if (u.Owner == defender)
                    aliveDef.Add(u);
            }
        }

        static int CountParticipants(BoardTile hex, PlayerState p)
        {
            int n = 0;
            foreach (var u in UnityEngine.Object.FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile == hex && u.Owner == p)
                    n++;
            }

            return n;
        }

        static int CountTypeOnHex(BoardTile hex, PlayerState p, UnitType t)
        {
            int n = 0;
            foreach (var u in UnityEngine.Object.FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile == hex && u.Owner == p && u.Definition.Type == t)
                    n++;
            }

            return n;
        }

        /// <summary>Start or advance dragon strike phase before ending turn.</summary>
        public void BeginDragonPhaseIfNeeded(Action onComplete)
        {
            if (IsGameOver)
            {
                onComplete?.Invoke();
                return;
            }

            DragonPhase = BuildDragonPhase(CurrentPlayer);
            if (DragonPhase == null || DragonPhase.Options.Count == 0)
            {
                DragonPhase = null;
                onComplete?.Invoke();
                return;
            }

            DragonPhase.OnComplete = onComplete;
        }

        DragonPhaseState BuildDragonPhase(PlayerState player)
        {
            var options = new List<DragonStrikeOption>();
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u.Owner != player || u.Definition.Type != UnitType.RubiumDragon)
                    continue;
                if (!IsHexControlledByPlayer(u.Tile, player))
                    continue;
                foreach (var n in Board.GetNeighbors(u.Tile))
                {
                    if (IsTileContested(n))
                        continue;

                    bool enemyHere = false;
                    foreach (var o in FindObjectsOfType<UnitInstance>())
                    {
                        if (o.Tile == n && o.Owner != player)
                        {
                            enemyHere = true;
                            break;
                        }
                    }

                    if (enemyHere)
                        options.Add(new DragonStrikeOption { Dragon = u, TargetHex = n });
                }
            }

            if (options.Count == 0)
                return null;

            return new DragonPhaseState
            {
                Player = player,
                Options = options,
                Rng = new System.Random(Environment.TickCount)
            };
        }

        static bool IsHexControlledByPlayer(BoardTile hex, PlayerState player)
        {
            if (hex == null)
                return false;
            PlayerState sole = null;
            foreach (var u in UnityEngine.Object.FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile != hex)
                    continue;
                if (sole == null)
                    sole = u.Owner;
                else if (sole != u.Owner)
                    return false;
            }

            return sole == player;
        }

        /// <summary>Rubium Dragon ranged: 1d6, hit on 4+ (same as melee profile).</summary>
        public void ExecuteDragonStrike(DragonStrikeOption opt)
        {
            if (DragonPhase == null || opt == null || opt.Dragon == null)
                return;
            if (opt.TargetHex == null || IsTileContested(opt.TargetHex))
            {
                DragonPhase.Options.Remove(opt);
                if (DragonPhase.Options.Count == 0)
                    FinishDragonPhase();
                return;
            }

            var enemies = new List<UnitInstance>();
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile == opt.TargetHex && u.Owner != DragonPhase.Player)
                    enemies.Add(u);
            }

            if (enemies.Count == 0)
            {
                DragonPhase.Options.Remove(opt);
                if (DragonPhase.Options.Count == 0)
                    FinishDragonPhase();
                return;
            }

            int roll = DragonPhase.Rng.Next(1, 7);
            opt.LastRoll = roll;
            if (roll < 4)
            {
                DragonPhase.LastLog = $"Dragon ranged: roll {roll} — miss.";
                Debug.Log(
                    $"[Battle] Dragon: P{DragonPhase.Player.PlayerIndex + 1} at ({opt.Dragon.Tile.Q},{opt.Dragon.Tile.R}) → ({opt.TargetHex.Q},{opt.TargetHex.R}) | roll {roll} miss (need 4+)");
                RemoveAllDragonOptions(opt.Dragon);
                return;
            }

            Debug.Log(
                $"[Battle] Dragon: P{DragonPhase.Player.PlayerIndex + 1} at ({opt.Dragon.Tile.Q},{opt.Dragon.Tile.R}) → ({opt.TargetHex.Q},{opt.TargetHex.R}) | roll {roll} hit — pick target");
            DragonPhase.PendingHit = opt;
            DragonPhase.PendingEnemies = enemies;
        }

        void RemoveAllDragonOptions(UnitInstance dragon)
        {
            if (DragonPhase?.Options == null || dragon == null)
                return;
            DragonPhase.Options.RemoveAll(o => o.Dragon == dragon);
            if (DragonPhase.Options.Count == 0)
                FinishDragonPhase();
        }

        public void DragonStrikeChooseVictim(UnitInstance victim)
        {
            if (DragonPhase?.PendingHit == null || victim == null)
                return;
            if (!DragonPhase.PendingEnemies.Contains(victim))
                return;

            var dragon = DragonPhase.PendingHit.Dragon;
            RemoveUnit(victim);
            DragonPhase.LastLog =
                $"Dragon ranged: roll {DragonPhase.PendingHit.LastRoll} — hit, removed {victim.Definition.Type}.";
            Debug.Log(
                $"[Battle] Dragon hit: removed {victim.Definition.Type} (P{victim.Owner.PlayerIndex + 1}), roll was {DragonPhase.PendingHit.LastRoll}");

            DragonPhase.PendingHit = null;
            DragonPhase.PendingEnemies = null;
            RemoveAllDragonOptions(dragon);
        }

        public void SkipDragonStrikeOption(DragonStrikeOption opt)
        {
            if (DragonPhase == null || opt?.Dragon == null)
                return;
            Debug.Log(
                $"[Battle] Dragon: skipped strike from ({opt.Dragon.Tile.Q},{opt.Dragon.Tile.R}) → ({opt.TargetHex.Q},{opt.TargetHex.R})");
            RemoveAllDragonOptions(opt.Dragon);
        }

        public void SkipAllDragonStrikes()
        {
            Debug.Log("[Battle] Dragon: skip all remaining strikes");
            FinishDragonPhase();
        }

        void FinishDragonPhase()
        {
            var cb = DragonPhase?.OnComplete;
            DragonPhase = null;
            cb?.Invoke();
        }

        void RunLegacyAutoBattle(PlayerState attacker)
        {
            var rng = new System.Random();
            var log = new StringBuilder();
            foreach (var entry in BattlePlan)
            {
                var defender = Players.Find(p => p.PlayerIndex == entry.DefenderPlayerIndex);
                if (defender == null)
                    continue;
                var result = BattleResolver.ResolveBattle(entry.Hex, attacker, defender, Config, rng, RemoveUnit);
                if (result.AttackerEliminatedDefender)
                {
                    attacker.VictoryPoints += result.VictoryPointsAwarded;
                    QueueVictoryPointHudFlight(attacker, result.VictoryPointsAwarded);
                    if (MetaProgression.Instance != null)
                        MetaProgression.Instance.OnBattleWinReward();
                    DrawEnergizeCards(defender, 1);
                    if (CheckGameEndAfterVpChange())
                        break;
                }

                foreach (var line in result.LogLines)
                {
                    log.AppendLine(line);
                    Debug.Log("[Battle] " + line);
                    NexusBattleDebug.LogBattle(line);
                }

                log.AppendLine("---");
            }

            LastBattlePhaseLog = log.ToString().TrimEnd();
            BattlePlan.Clear();
        }
    }

    [Serializable]
    public class PlannedBattleEntry
    {
        public BoardTile Hex;
        public int DefenderPlayerIndex;
    }

    public class CasualtyPickState
    {
        public PlayerState Owner;
        public List<UnitInstance> Pool;
        public int Required;
        public List<UnitInstance> Selected = new List<UnitInstance>();
        public Action<UnitInstance> OnEachRemove;
    }

    public class SecretMissionOfferState
    {
        public PlayerState Attacker;
        public List<int> EligibleIndices;
        public bool Waiting;
    }

    public class DragonPhaseState
    {
        public PlayerState Player;
        public List<DragonStrikeOption> Options;
        public System.Random Rng;
        public DragonStrikeOption PendingHit;
        public List<UnitInstance> PendingEnemies;
        public string LastLog;
        public Action OnComplete;
    }

    public class DragonStrikeOption
    {
        public UnitInstance Dragon;
        public BoardTile TargetHex;
        public int LastRoll;
    }
}
