using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Drives automated seats in <see cref="GameController.VsAiMode"/> (P2) or <see cref="GameController.AiVsAiMode"/> (both).
    /// </summary>
    public class SimpleAiController : MonoBehaviour
    {
        public GameController Game;
        public MobileInputController Input;

        [Min(0.05f)]
        public float ActionDelaySeconds = 0.38f;

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
                if (Game == null || (!Game.VsAiMode && !Game.AiVsAiMode))
                    continue;

                Game.CheckAiTestMatchEndIfNeeded();

                // Battle prompts (Energize, casualties, etc.) target the player whose turn it is in that *step*,
                // not necessarily Game.CurrentPlayer (turn owner stays the attacker for the whole battle phase).
                // Keep resolving even after AI-test match ends so any in-flight battle can finish.
                yield return AiResolveBlockingUi(wait, tick);

                if (Game.AiVsAiMode && Game.AiTestMatchCompleted)
                    continue;

                if (!Game.IsAiControlled(Game.CurrentPlayer))
                    continue;

                yield return AiDoMainTurn(wait, tick);
            }
        }

        IEnumerator AiResolveBlockingUi(WaitForSeconds wait, WaitForSeconds tick)
        {
            if (Game.PendingBattleArrangement && Game.IsAiControlled(Game.CurrentPlayer))
            {
                yield return wait;
                Game.ConfirmBattleArrangement();
                yield return tick;
            }

            while ((Game.EnergizePromptPlayer != null && Game.IsAiControlled(Game.EnergizePromptPlayer)) ||
                   (Game.FocusFirePicker != null && Game.IsAiControlled(Game.FocusFirePicker)))
            {
                yield return wait;
                if (Game.FocusFirePicker != null && Game.IsAiControlled(Game.FocusFirePicker))
                {
                    if (CountFriendlyOnHex(Game.FocusFireBattleHex, Game.FocusFirePicker) == 0)
                        Game.CancelFocusFireRefund();
                    else
                        Game.SubmitFocusFireUnitType(PickFocusFireUnitType());
                }
                else
                    DoEnergizeDecision();
                yield return tick;
            }

            while (Game.CasualtyPick != null && Game.IsAiControlled(Game.CasualtyPick.Owner))
            {
                yield return wait;
                SubmitAiCasualties();
                yield return tick;
            }

            if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting &&
                Game.IsAiControlled(Game.SecretMissionOffer.Attacker))
            {
                yield return wait;
                PlayBestSecretOrSkip();
                yield return tick;
            }

            int guard = 0;
            while (Game.DragonPhase != null && Game.IsAiControlled(Game.DragonPhase.Player) && guard++ < 32)
            {
                yield return wait;
                if (!TryResolveDragonStep())
                    break;
                yield return tick;
            }
        }

        void DoEnergizeDecision()
        {
            var pl = Game.EnergizePromptPlayer;
            if (pl == null || pl.BattleEnergize.Count == 0)
            {
                Game.SubmitEnergizePass();
                return;
            }

            var att = Game.BattleContextAttacker;
            var def = Game.BattleContextDefender;
            bool isAttacker = att != null && pl == att;
            bool isDefender = def != null && pl == def;

            int myPower = CountFriendlyCombatPowerOnHex(Game.BattleContextHex, pl);
            int enemyPower = 0;
            if (isAttacker && def != null)
                enemyPower = CountFriendlyCombatPowerOnHex(Game.BattleContextHex, def);
            else if (isDefender && att != null)
                enemyPower = CountFriendlyCombatPowerOnHex(Game.BattleContextHex, att);

            // Defender: protect high-value stack; don't burn cards when we're already favored.
            if (isDefender && !isAttacker)
            {
                if (enemyPower <= 1 && Random.value < 0.55f)
                {
                    Game.SubmitEnergizePass();
                    return;
                }

                if (TrySubmitFirstEnergize(pl,
                        EnergizeBattleId.Aegis,
                        EnergizeBattleId.Elusive,
                        EnergizeBattleId.DeadlyAim))
                    return;

                if (myPower >= enemyPower + 2 && Random.value < 0.35f)
                {
                    Game.SubmitEnergizePass();
                    return;
                }

                if (TrySubmitFirstEnergize(pl,
                        EnergizeBattleId.BattleFury,
                        EnergizeBattleId.FocusFire,
                        EnergizeBattleId.BattleCache))
                    return;

                Game.SubmitEnergizePass();
                return;
            }

            // Attacker (or unknown): pressure — dice and hit mods first; Focus Fire when we have several dice bodies.
            if (isAttacker || !isDefender)
            {
                if (TrySubmitFirstEnergize(pl,
                        EnergizeBattleId.BattleFury,
                        EnergizeBattleId.DeadlyAim))
                    return;

                int diceBodies = CountDiceRollingBodiesOnHex(Game.BattleContextHex, pl);
                if (diceBodies >= 2 &&
                    pl.BattleEnergize.Contains(EnergizeBattleId.FocusFire) &&
                    CountFriendlyOnHex(Game.BattleContextHex, pl) > 0)
                {
                    Game.SubmitEnergizePlay(EnergizeBattleId.FocusFire);
                    return;
                }

                if (TrySubmitFirstEnergize(pl,
                        EnergizeBattleId.Aegis,
                        EnergizeBattleId.Elusive))
                    return;

                if (TrySubmitFirstEnergize(pl, EnergizeBattleId.BattleCache))
                    return;

                if (pl.BattleEnergize.Contains(EnergizeBattleId.FocusFire) &&
                    CountFriendlyOnHex(Game.BattleContextHex, pl) > 0)
                {
                    Game.SubmitEnergizePlay(EnergizeBattleId.FocusFire);
                    return;
                }

                Game.SubmitEnergizePass();
                return;
            }

            Game.SubmitEnergizePass();
        }

        /// <summary>Sum of attack dice on hex (rough fight weight).</summary>
        static int CountFriendlyCombatPowerOnHex(BoardTile hex, PlayerState owner)
        {
            if (hex == null || owner == null)
                return 0;
            var n = 0;
            foreach (var u in Object.FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile != hex || u.Owner != owner || u.Definition == null)
                    continue;
                n += Mathf.Max(0, u.Definition.AttackDice);
            }

            return n;
        }

        static int CountDiceRollingBodiesOnHex(BoardTile hex, PlayerState owner)
        {
            if (hex == null || owner == null)
                return 0;
            var n = 0;
            foreach (var u in Object.FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile != hex || u.Owner != owner || u.Definition == null)
                    continue;
                if (u.Definition.AttackDice > 0)
                    n++;
            }

            return n;
        }

        bool TrySubmitFirstEnergize(PlayerState pl, params EnergizeBattleId[] order)
        {
            foreach (var id in order)
            {
                if (!pl.BattleEnergize.Contains(id))
                    continue;
                Game.SubmitEnergizePlay(id);
                return true;
            }

            return false;
        }

        UnitType PickFocusFireUnitType()
        {
            var hex = Game.FocusFireBattleHex;
            var me = Game.FocusFirePicker;
            if (hex == null || me == null)
                return UnitType.Human;

            // Same ordering as HUD FocusFireWindow: first type in battle order you have on the hex.
            foreach (var t in BattleResolver.BattleOrder)
            {
                foreach (var u in Object.FindObjectsOfType<UnitInstance>())
                {
                    if (u.Tile == hex && u.Owner == me && u.Definition.Type == t)
                        return t;
                }
            }

            return UnitType.Human;
        }

        void SubmitAiCasualties()
        {
            var pick = Game.CasualtyPick;
            if (pick == null)
                return;

            var victims = BattleResolver.PickCasualtiesWeakestFirst(pick.Pool, pick.Required);
            foreach (var u in victims)
            {
                if (u == null || !pick.Pool.Contains(u))
                    continue;
                if (!pick.Selected.Contains(u))
                    Game.ToggleCasualtyUnit(u);
            }

            if (pick.Selected.Count == pick.Required)
                Game.SubmitCasualtyPick();
        }

        void PlayBestSecretOrSkip()
        {
            var offer = Game.SecretMissionOffer;
            if (offer == null || offer.EligibleIndices == null || offer.EligibleIndices.Count == 0)
            {
                Game.SkipSecretMissionPlay();
                return;
            }

            var p = offer.Attacker;
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

            int safety = 0;
            while (safety++ < 56 &&
                   Game.IsAiControlled(Game.CurrentPlayer) &&
                   !Game.BattlePhaseBlockingPlay &&
                   Game.DragonPhase == null)
            {
                if (TryPlayOneDeployment())
                {
                    yield return wait;
                    continue;
                }

                if (TryBuySomething())
                {
                    yield return wait;
                    continue;
                }

                if (TryOneBestMove())
                {
                    yield return wait;
                    continue;
                }

                break;
            }

            yield return wait;
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
