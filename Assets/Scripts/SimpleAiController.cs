using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Drives player 2 in <see cref="GameController.VsAiMode"/>: battles, dragon, buys, moves, end turn.
    /// </summary>
    public class SimpleAiController : MonoBehaviour
    {
        public GameController Game;
        public MobileInputController Input;

        [Min(0f)]
        [Tooltip("Pause after the AI becomes the active player before buying, moving, or ending turn.")]
        public float TurnStartDelaySeconds = 1f;

        [Min(0.05f)]
        [Tooltip("Pause after each deployment, purchase, or move during the AI turn.")]
        public float ActionDelaySeconds = 0.5f;

        bool _humanLikePacing;

        public void EnableHumanLikePacing() => _humanLikePacing = true;

        float SampleTurnStartDelay() =>
            _humanLikePacing ? Random.Range(2.2f, 5.8f) : TurnStartDelaySeconds;

        float SampleActionDelay() =>
            _humanLikePacing ? Random.Range(1.1f, 3.4f) : ActionDelaySeconds;

        float SamplePromptDelay() =>
            _humanLikePacing ? Random.Range(1.4f, 4.1f) : ActionDelaySeconds;

        static int AxialDist(BoardTile a, BoardTile b)
        {
            int dq = a.Q - b.Q;
            int dr = a.R - b.R;
            int ds = -(a.Q + a.R) - (-(b.Q + b.R));
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        void Start()
        {
            if (Game == null)
                Game = FindObjectOfType<GameController>();
            if (Input == null)
                Input = FindObjectOfType<MobileInputController>();
        }

        void OnEnable()
        {
            if (Application.isPlaying)
                StartCoroutine(AiMainLoop());
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        IEnumerator AiMainLoop()
        {
            var wait = new WaitForSeconds(ActionDelaySeconds);
            var tick = new WaitForSeconds(0.04f);
            while (enabled)
            {
                yield return null;
                if (Game == null || !Game.VsAiMode || Game.IsGameOver)
                    continue;

                // AI may need to answer battle prompts even when it is not CurrentPlayer
                // (e.g. human attacker vs AI defender during Energize/casualties).
                yield return AiResolveBlockingUi(wait, tick);

                var cur = Game.CurrentPlayer;
                if (!Game.IsAiControlled(cur))
                    continue;
                yield return AiDoMainTurn(wait, tick);
            }
        }

        IEnumerator AiResolveBlockingUi(WaitForSeconds wait, WaitForSeconds tick)
        {
            if (Game.PendingBattleArrangement && Game.IsAiControlled(Game.CurrentPlayer))
            {
                if (_humanLikePacing)
                    yield return new WaitForSeconds(SamplePromptDelay());
                else
                    yield return wait;
                Game.ConfirmBattleArrangement();
                yield return tick;
            }

            // While the attacker is still the current player, humans may need to play Energize / casualties / Focus Fire.
            // Yield until they finish; only auto-resolve when the active prompt belongs to the AI.
            int battleSafety = 0;
            while (Game.BattlePhaseBlockingPlay && battleSafety++ < 20000)
            {
                if (!HasAnyAiOwnedBlockingPrompt())
                    break;

                if (_humanLikePacing)
                    yield return new WaitForSeconds(SamplePromptDelay());
                else
                    yield return wait;

                if (Game.EnergizePromptPlayer != null && !Game.IsAiControlled(Game.EnergizePromptPlayer))
                {
                    yield return tick;
                    continue;
                }

                if (Game.FocusFirePicker != null && !Game.IsAiControlled(Game.FocusFirePicker))
                {
                    yield return tick;
                    continue;
                }

                if (Game.CasualtyPick != null && !Game.IsAiControlled(Game.CasualtyPick.Owner))
                {
                    yield return tick;
                    continue;
                }

                if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting &&
                    !Game.IsAiControlled(Game.SecretMissionOffer.Player))
                {
                    yield return tick;
                    continue;
                }
                if (Game.SecretMissionOverdraw != null && Game.SecretMissionOverdraw.Waiting &&
                    !Game.IsAiControlled(Game.SecretMissionOverdraw.Player))
                {
                    yield return tick;
                    continue;
                }

                if (Game.FocusFirePicker != null && Game.IsAiControlled(Game.FocusFirePicker))
                {
                    if (CountFriendlyOnHex(Game.FocusFireBattleHex, Game.FocusFirePicker) == 0)
                        Game.CancelFocusFireRefund();
                    else
                        Game.SubmitFocusFireUnitType(PickFocusFireUnitType());
                }
                else if (Game.EnergizePromptPlayer != null && Game.IsAiControlled(Game.EnergizePromptPlayer))
                    DoEnergizeDecision();
                else if (Game.CasualtyPick != null && Game.IsAiControlled(Game.CasualtyPick.Owner))
                    SubmitAiCasualties();
                else if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting &&
                         Game.IsAiControlled(Game.SecretMissionOffer.Player))
                    PlayBestSecretOrSkip();
                else if (Game.SecretMissionOverdraw != null && Game.SecretMissionOverdraw.Waiting &&
                         Game.IsAiControlled(Game.SecretMissionOverdraw.Player))
                    Game.ResolveAiSecretMissionOverdraw();

                yield return tick;
            }

            int guard = 0;
            while (Game.DragonPhase != null && Game.IsAiControlled(Game.DragonPhase.Player) && guard++ < 32)
            {
                if (_humanLikePacing)
                    yield return new WaitForSeconds(SamplePromptDelay());
                else
                    yield return wait;
                if (!TryResolveDragonStep())
                    break;
                yield return tick;
            }
        }

        bool HasAnyAiOwnedBlockingPrompt()
        {
            if (Game == null)
                return false;
            if (Game.PendingBattleArrangement && Game.IsAiControlled(Game.CurrentPlayer))
                return true;
            if (Game.EnergizePromptPlayer != null && Game.IsAiControlled(Game.EnergizePromptPlayer))
                return true;
            if (Game.FocusFirePicker != null && Game.IsAiControlled(Game.FocusFirePicker))
                return true;
            if (Game.CasualtyPick != null && Game.IsAiControlled(Game.CasualtyPick.Owner))
                return true;
            if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting &&
                Game.IsAiControlled(Game.SecretMissionOffer.Player))
                return true;
            if (Game.SecretMissionOverdraw != null && Game.SecretMissionOverdraw.Waiting &&
                Game.IsAiControlled(Game.SecretMissionOverdraw.Player))
                return true;
            return false;
        }

        void DoEnergizeDecision()
        {
            var pl = Game.EnergizePromptPlayer;
            if (pl == null || pl.BattleEnergize.Count == 0)
            {
                Game.SubmitEnergizePass();
                return;
            }

            if (Random.value < 0.42f)
            {
                Game.SubmitEnergizePass();
                return;
            }

            var prio = new[]
            {
                EnergizeBattleId.BattleFury,
                EnergizeBattleId.DeadlyAim,
                EnergizeBattleId.Aegis,
                EnergizeBattleId.Elusive,
                EnergizeBattleId.FocusFire,
                EnergizeBattleId.BattleCache
            };

            foreach (var id in prio)
            {
                if (!pl.BattleEnergize.Contains(id))
                    continue;
                Game.SubmitEnergizePlay(id);
                return;
            }

            Game.SubmitEnergizePass();
        }

        UnitType PickFocusFireUnitType()
        {
            var hex = Game.FocusFireBattleHex;
            var me = Game.FocusFirePicker;
            if (hex == null || me == null)
                return UnitType.Human;

            // Match HUD: pick one of *your* unit types present on the battle hex for +2 dice.
            var counts = new Dictionary<UnitType, int>();
            foreach (var u in Object.FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile != hex || u.Owner != me)
                    continue;
                counts.TryGetValue(u.Definition.Type, out int n);
                counts[u.Definition.Type] = n + 1;
            }

            UnitType best = UnitType.Human;
            var bestC = -1;
            foreach (var kv in counts)
            {
                if (kv.Value > bestC)
                {
                    bestC = kv.Value;
                    best = kv.Key;
                }
            }

            return best;
        }

        void SubmitAiCasualties()
        {
            var pick = Game.CasualtyPick;
            if (pick == null)
                return;

            var victims = BattleResolver.PickCasualtiesWeakestFirst(pick.Pool, pick.Required);
            foreach (var u in victims)
                Game.ToggleCasualtyUnit(u);
            Game.SubmitCasualtyPick();
        }

        void PlayBestSecretOrSkip()
        {
            var offer = Game.SecretMissionOffer;
            if (offer == null)
                return;

            if (offer.OffersFallbackBattleVp)
            {
                Game.ClaimFallbackBattleSecretVp();
                return;
            }

            if (offer.EligibleIndices == null || offer.EligibleIndices.Count == 0)
            {
                Game.SkipSecretMissionPlay();
                return;
            }

            var p = offer.Player;
            int bestIdx = -1;
            var bestVp = -1;
            foreach (var i in offer.EligibleIndices)
            {
                if (i < 0 || i >= p.SecretMissions.Count)
                    continue;
                var s = p.SecretMissions[i];
                if (s.VictoryPoints > bestVp)
                {
                    bestVp = s.VictoryPoints;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
                Game.PlaySecretMissionAtIndex(bestIdx);
            else
                Game.SkipSecretMissionPlay();
        }

        bool TryResolveDragonStep()
        {
            var dp = Game.DragonPhase;
            if (dp == null)
                return false;

            if (dp.PendingHit != null && dp.PendingEnemies != null && dp.PendingEnemies.Count > 0)
            {
                var victims = BattleResolver.PickCasualtiesWeakestFirst(dp.PendingEnemies, 1);
                if (victims.Count > 0)
                {
                    Game.DragonStrikeChooseVictim(victims[0]);
                    return true;
                }
            }

            if (dp.Options == null || dp.Options.Count == 0)
            {
                Game.SkipAllDragonStrikes();
                return false;
            }

            Game.ExecuteDragonStrike(dp.Options[0]);
            return true;
        }

        IEnumerator AiDoMainTurn(WaitForSeconds wait, WaitForSeconds tick)
        {
            if (!Game.IsAiControlled(Game.CurrentPlayer))
                yield break;
            if (Game.BattlePhaseBlockingPlay || Game.DragonPhase != null)
                yield break;

            float turnDelay = SampleTurnStartDelay();
            if (turnDelay > 0.001f)
                yield return new WaitForSeconds(turnDelay);

            int safety = 0;
            while (safety++ < 56 &&
                   Game.IsAiControlled(Game.CurrentPlayer) &&
                   !Game.BattlePhaseBlockingPlay &&
                   Game.DragonPhase == null)
            {
                if (TryPlayOneDeployment())
                {
                    yield return new WaitForSeconds(SampleActionDelay());
                    continue;
                }

                if (TryBuySomething())
                {
                    yield return new WaitForSeconds(SampleActionDelay());
                    continue;
                }

                if (TryOneBestMove())
                {
                    yield return new WaitForSeconds(SampleActionDelay());
                    continue;
                }

                break;
            }

            yield return new WaitForSeconds(SampleActionDelay());
            if (Game.IsAiControlled(Game.CurrentPlayer) &&
                !Game.BattlePhaseBlockingPlay &&
                Game.DragonPhase == null)
                Game.EndTurn();

            yield return tick;
        }

        bool TryPlayOneDeployment()
        {
            var p = Game.CurrentPlayer;
            if (p.DeployEnergize.Count == 0)
                return false;

            var home = Game.FindHomeBaseForPlayer(p);
            var ordered = new[]
            {
                EnergizeDeploymentId.StripMine,
                EnergizeDeploymentId.SupplyRun,
                EnergizeDeploymentId.Convoy,
                EnergizeDeploymentId.RushOrder,
                EnergizeDeploymentId.FreeHuman
            };

            foreach (var id in ordered)
            {
                if (!p.DeployEnergize.Contains(id))
                    continue;
                if (Game.TryPlayDeploymentEnergize(id, id == EnergizeDeploymentId.FreeHuman ? home : null))
                    return true;
            }

            return false;
        }

        bool TryBuySomething()
        {
            var p = Game.CurrentPlayer;
            if (Game.Config == null || Game.Config.UnitDefinitions == null)
                return false;

            foreach (var def in Game.Config.UnitDefinitions.OrderByDescending(u => u.Cost))
            {
                if (Game.TryPurchaseUnit(p, def.Type, def.Cost))
                    return true;
            }

            return false;
        }

        BoardTile FindMonolithTile()
        {
            if (Game.Board == null)
                return null;
            foreach (var t in Game.Board.AllTiles)
            {
                if (t != null && t.Type == TileType.Monolith)
                    return t;
            }

            return null;
        }

        static int CountFriendlyOnHex(BoardTile hex, PlayerState me)
        {
            if (hex == null || me == null)
                return 0;
            var n = 0;
            foreach (var u in Object.FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile == hex && u.Owner == me)
                    n++;
            }

            return n;
        }

        static int MinAxialDistToPlayerUnits(BoardTile t, PlayerState targetPlayer)
        {
            if (t == null || targetPlayer == null)
                return 999;
            var best = 999;
            foreach (var u in Object.FindObjectsOfType<UnitInstance>())
            {
                if (u.Owner != targetPlayer || u.Tile == null)
                    continue;
                best = Mathf.Min(best, AxialDist(t, u.Tile));
            }

            return best;
        }

        static PlayerState OpponentOf(PlayerState me, GameController game)
        {
            return game.Players.FirstOrDefault(pl => pl != me);
        }

        bool TryOneBestMove()
        {
            if (Input == null)
                return false;

            var p = Game.CurrentPlayer;
            var opp = OpponentOf(p, Game);
            var mono = FindMonolithTile();

            var units = Object.FindObjectsOfType<UnitInstance>()
                .Where(u => u.Owner == p && !u.HasMovedThisTurn && u.Tile != null)
                .OrderBy(_ => Random.value)
                .ToList();

            UnitInstance bestUnit = null;
            BoardTile bestTile = null;
            var bestScore = int.MaxValue;

            foreach (var u in units)
            {
                foreach (var t in Input.GetReachableTiles(u))
                {
                    int dEnemy = opp != null ? MinAxialDistToPlayerUnits(t, opp) : 20;
                    if (dEnemy >= 900)
                        dEnemy = 24;
                    int dMono = mono != null ? AxialDist(t, mono) : 0;
                    int score = dEnemy * 80 + dMono;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestUnit = u;
                        bestTile = t;
                    }
                }
            }

            return bestUnit != null && bestTile != null && Input.TryAiMoveUnit(bestUnit, bestTile);
        }
    }
}
