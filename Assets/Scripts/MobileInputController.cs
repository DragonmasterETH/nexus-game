using UnityEngine;
using UnityEngine.EventSystems;

namespace NexusGame
{
        public class MobileInputController : MonoBehaviour
        {
        public Camera MainCamera;
        public GameController Game;

        [Header("Debug")]
        public bool DebugClicks;

        [Tooltip("Hides verbose CanUnitMoveTo / move warnings (used when AI pathfinding).")]
        public bool SuppressMovementDiagnosticLogs;

            BoardTile _popupTile;
            BoardTile _selectedTile;
            readonly System.Collections.Generic.Dictionary<UnitType, int> _moveSelection =
                new System.Collections.Generic.Dictionary<UnitType, int>();

            // Drag-to-move support:
            // - If user presses on a movable unit, dragging selects all movable units of that unit type on the source hex.
            // - Releasing over a destination hex attempts to move that selected group.
            bool _pendingTap;
            bool _dragging;
            Vector2 _pointerDownPos;
            float _dragThresholdPixels = 6f;
            bool _dragPrepared;
            BoardTile _dragSourceTile;
            UnitType _dragType;
            UnitInstance _dragStartUnit;

            public BoardTile SelectedTile => _selectedTile;
            public System.Collections.Generic.IReadOnlyDictionary<UnitType, int> SelectedMoveCounts => _moveSelection;

        void Start()
        {
            if (MainCamera == null)
                MainCamera = Camera.main;
            if (Game == null)
                Game = FindObjectOfType<GameController>();
        }

        void Update()
        {
            if (Game != null && Game.IsAiControlled(Game.CurrentPlayer))
                return;

            // Touch input for mobile
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    _pendingTap = true;
                    _dragging = false;
                    _dragPrepared = false;
                    _dragStartUnit = null;
                    _pointerDownPos = touch.position;

                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                        _pendingTap = false;

                    PrepareDragFromPointer(touch.position);
                }
                else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    if (_pendingTap && !_dragging && _dragPrepared)
                    {
                        if (Vector2.Distance(touch.position, _pointerDownPos) >= _dragThresholdPixels)
                            _dragging = true;
                    }
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (_dragPrepared && (_dragging || Vector2.Distance(touch.position, _pointerDownPos) >= _dragThresholdPixels))
                    {
                        TryDragMove(touch.position);
                    }
                    else if (_pendingTap)
                    {
                        HandleTap(_pointerDownPos);
                    }

                    _pendingTap = false;
                    _dragging = false;
                    _dragPrepared = false;
                    _dragSourceTile = null;
                    _dragStartUnit = null;
                }
            }

            // Mouse input for PC / editor builds
#if !UNITY_IOS && !UNITY_ANDROID
            if (Input.GetMouseButtonDown(0))
            {
                _pendingTap = true;
                _dragging = false;
                _dragPrepared = false;
                _dragStartUnit = null;
                _pointerDownPos = Input.mousePosition;

                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    _pendingTap = false;

                PrepareDragFromPointer(Input.mousePosition);
            }

            if (Input.GetMouseButton(0))
            {
                if (_pendingTap && !_dragging && _dragPrepared)
                {
                    if (Vector2.Distance(Input.mousePosition, _pointerDownPos) >= _dragThresholdPixels)
                        _dragging = true;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (_dragPrepared && (_dragging || Vector2.Distance(Input.mousePosition, _pointerDownPos) >= _dragThresholdPixels))
                {
                    TryDragMove(Input.mousePosition);
                }
                else if (_pendingTap)
                {
                    HandleTap(_pointerDownPos);
                }

                _pendingTap = false;
                _dragging = false;
                _dragPrepared = false;
                _dragSourceTile = null;
                _dragStartUnit = null;
            }
#endif
        }

        void PrepareDragFromPointer(Vector2 screenPos)
        {
            if (Game == null || Game.CurrentPlayer == null || Game.IsAiControlled(Game.CurrentPlayer))
                return;
            if (MainCamera == null)
                return;

            // Only prepare drag if pointer is on a movable unit belonging to current player.
            var ray = MainCamera.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray, 100f);
            foreach (var h in hits)
            {
                var unitOnTile = h.collider.GetComponentInParent<UnitInstance>();
                if (unitOnTile != null && unitOnTile.Tile != null &&
                    unitOnTile.Owner == Game.CurrentPlayer &&
                    !unitOnTile.HasMovedThisTurn)
                {
                    _dragSourceTile = unitOnTile.Tile;
                    _dragType = unitOnTile.Definition.Type;
                    _dragStartUnit = unitOnTile;
                    _dragPrepared = true;
                    // We already selected on pointer-down, so do not run tap logic on release.
                    _pendingTap = false;

                    // Auto-select all movable units of this type on the source hex.
                    _moveSelection.Clear();
                    SetSelectedTile(_dragSourceTile);
                    _moveSelection.Clear();
                    // Drag behavior: move the exact piece under the pointer.
                    _moveSelection[_dragType] = 1;
                    return;
                }
            }
        }

        void TryDragMove(Vector2 screenPos)
        {
            var target = ResolveTileFromPointer(screenPos);
            if (target == null || _dragSourceTile == null)
                return;
            if (_dragSourceTile == target)
                return;

            bool moved = false;
            if (_dragStartUnit != null &&
                _dragStartUnit.Owner == Game.CurrentPlayer &&
                !_dragStartUnit.HasMovedThisTurn &&
                CanUnitMoveTo(_dragStartUnit, target))
            {
                _dragStartUnit.MoveTo(target);
                target.Owner = _dragStartUnit.Owner;

                // Reveal exploration only when a unit actually moves onto this hex.
                if (!target.ExplorationRevealed && target.ExplorationReward != ExplorationReward.None)
                {
                    RevealExploration(target);
                }
                moved = true;
            }

            // Fallback: if exact dragged unit path failed, use existing grouped move path.
            if (!moved)
                TryMoveGroupTo(target);

            // After moving, focus selection on destination so the existing UI stays in sync.
            SetSelectedTile(target);
        }

        BoardTile ResolveTileFromPointer(Vector2 screenPos)
        {
            if (MainCamera == null || Game == null || Game.Board == null)
                return null;

            var ray = MainCamera.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits.Length == 0)
                return null;

            BoardTile clickedTile = null;
            foreach (var h in hits)
            {
                var unitOnTile = h.collider.GetComponentInParent<UnitInstance>();
                if (unitOnTile != null && unitOnTile.Tile != null)
                {
                    clickedTile = unitOnTile.Tile;
                    break;
                }

                var proxy = h.collider.GetComponentInParent<TileClickProxy>();
                if (proxy != null)
                {
                    clickedTile = Game.Board.GetTile(proxy.Q, proxy.R);
                    if (clickedTile != null)
                        break;
                }
            }

            return clickedTile;
        }

        void HandleTap(Vector2 screenPos)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var ray = MainCamera.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray, 100f);

            if (DebugClicks)
            {
                Debug.Log($"CLICK: screenPos={screenPos}, hits={hits.Length}");
                foreach (var h in hits)
                {
                    var go = h.collider.gameObject;
                    var proxy = go.GetComponentInParent<TileClickProxy>();
                    var unit = go.GetComponentInParent<UnitInstance>();
                    Debug.Log(
                        $"  hit: go={go.name}, parent={go.transform.parent?.name}, " +
                        $"proxy={(proxy != null ? $"{proxy.Q},{proxy.R}" : "none")}, " +
                        $"unit={(unit != null ? unit.Definition.Type.ToString() : "none")}"
                    );
                }
            }

            if (hits.Length == 0)
                return;

            // Determine the tile under the cursor/touch using TileClickProxy
            BoardTile clickedTile = null;
            foreach (var h in hits)
            {
                // If we hit a unit, prefer its tile
                var unitOnTile = h.collider.GetComponentInParent<UnitInstance>();
                if (unitOnTile != null && unitOnTile.Tile != null)
                {
                    clickedTile = unitOnTile.Tile;
                    break;
                }

                var proxy = h.collider.GetComponentInParent<TileClickProxy>();
                if (proxy != null)
                {
                    clickedTile = Game.Board.GetTile(proxy.Q, proxy.R);
                    if (clickedTile != null)
                        break;
                }
            }

            if (clickedTile == null)
            {
                if (DebugClicks)
                    Debug.Log("CLICK RESOLVED: no tile");
                return;
            }

            if (DebugClicks)
                Debug.Log($"CLICK RESOLVED: tile=({clickedTile.Q},{clickedTile.R}) type={clickedTile.Type}");

            // If no source tile is selected yet, or we clicked the same tile, toggle selection/popup.
            if (_selectedTile == null || clickedTile == _selectedTile)
            {
                if (_selectedTile == clickedTile)
                {
                    SetSelectedTile(null);
                }
                else
                {
                    SetSelectedTile(clickedTile);
                }
                return;
            }

            // If we have a source tile selected and a move selection defined, and we click a different tile,
            // attempt to move the selected group there immediately (single-click confirm).
            if (_selectedTile != null && clickedTile != _selectedTile)
            {
                TryMoveGroupTo(clickedTile);
                // After moving, focus selection on the destination so the popup reflects new contents.
                SetSelectedTile(clickedTile);
            }
        }

        void SetSelectedTile(BoardTile tile)
        {
            // Clear previous highlight
            if (_selectedTile != null && _selectedTile.Highlight != null)
            {
                _selectedTile.Highlight.SetActive(false);
            }

            _selectedTile = tile;
            _popupTile = tile;
            _moveSelection.Clear();

            if (_selectedTile != null)
            {
                if (_selectedTile.Highlight != null)
                    _selectedTile.Highlight.SetActive(true);

                // Initialize selection counts to zero for any unit types present
                foreach (var unit in Object.FindObjectsOfType<UnitInstance>())
                {
                    if (unit.Tile == _selectedTile && unit.Owner == Game.CurrentPlayer && !unit.HasMovedThisTurn)
                    {
                        if (!_moveSelection.ContainsKey(unit.Definition.Type))
                            _moveSelection[unit.Definition.Type] = 0;
                    }
                }
            }
        }

        public void RevealExploration(BoardTile tile)
        {
            tile.ExplorationRevealed = true;
            if (tile.ExplorationMarker != null)
            {
                Object.Destroy(tile.ExplorationMarker);
                tile.ExplorationMarker = null;
            }

            string debugMessage = $"RevealExploration at ({tile.Q},{tile.R}): ";

            switch (tile.ExplorationReward)
            {
                case ExplorationReward.FreeHuman:
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.Human, tile);
                    debugMessage += "Free Human";
                    break;
                case ExplorationReward.FreeFungoid:
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.Fungoid, tile);
                    debugMessage += "Free Fungoid";
                    break;
                case ExplorationReward.FreeRockStrider:
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.RockStrider, tile);
                    debugMessage += "Free Rock Strider";
                    break;
                case ExplorationReward.Mine1:
                    tile.ExtraMineYield = 1;
                    UpdateMineLabel(tile);
                    debugMessage += "Mine bonus +1";
                    break;
                case ExplorationReward.Mine2:
                    tile.ExtraMineYield = 2;
                    UpdateMineLabel(tile);
                    debugMessage += "Mine bonus +2";
                    break;
                case ExplorationReward.Mine3:
                    tile.ExtraMineYield = 3;
                    UpdateMineLabel(tile);
                    debugMessage += "Mine bonus +3";
                    break;
                case ExplorationReward.FreeHumanAndMine2:
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.Human, tile);
                    tile.ExtraMineYield = 2;
                    UpdateMineLabel(tile);
                    debugMessage += "Free Human + Mine bonus +2";
                    break;
                default:
                    debugMessage += "No reward (None)";
                    break;
            }

            Debug.Log(debugMessage);
        }

        void UpdateMineLabel(BoardTile tile)
        {
            // Remove label if no bonus.
            if (tile.ExtraMineYield <= 0)
            {
                if (tile.MineLabel != null)
                {
                    Object.Destroy(tile.MineLabel);
                    tile.MineLabel = null;
                }
                return;
            }

            if (tile.MineLabel == null)
            {
                // Container for background shape + number (inherits tile rotation)
                var labelRoot = new GameObject("MineLabel");
                labelRoot.transform.SetParent(tile.View.transform, worldPositionStays: false);
                labelRoot.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                labelRoot.transform.localRotation = Quaternion.identity;
                labelRoot.transform.localScale = Vector3.one * 0.2f;

                // Simple background shape (small quad facing camera)
                var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bg.name = "MineLabelBg";
                bg.transform.SetParent(labelRoot.transform, worldPositionStays: false);
                bg.transform.localPosition = Vector3.zero;
                bg.transform.localRotation = Quaternion.identity;
                bg.transform.localScale = Vector3.one * 0.6f;
                var bgRenderer = bg.GetComponent<Renderer>();
                if (bgRenderer != null)
                {
                    bgRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    bgRenderer.material.color = new Color(0f, 0f, 0f, 0.6f); // dark semi-transparent
                }

                // Number text (slightly in front of background)
                var textGo = new GameObject("MineLabelText");
                textGo.transform.SetParent(labelRoot.transform, worldPositionStays: false);
                textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                textGo.transform.localRotation = Quaternion.identity;
                textGo.transform.localScale = Vector3.one * 0.4f;

                var text = textGo.AddComponent<TextMesh>();
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                text.color = Color.yellow;

                tile.MineLabel = labelRoot;
            }

            var tm = tile.MineLabel.GetComponentInChildren<TextMesh>();
            if (tm != null)
            {
                tm.text = tile.ExtraMineYield.ToString();
            }
        }

        void TryMoveGroupTo(BoardTile target)
        {
            if (Game != null && Game.BattlePhaseBlockingPlay)
            {
                if (!SuppressMovementDiagnosticLogs)
                    Debug.Log("Movement locked until battle phase completes.");
                return;
            }

            if (_selectedTile == null || target == null)
            {
                Debug.LogWarning("MoveGroupTo failed: missing selected tile or target.");
                return;
            }

            // If nothing is selected to move, treat this as just changing focus.
            bool anySelected = false;
            foreach (var kvp in _moveSelection)
            {
                if (kvp.Value > 0)
                {
                    anySelected = true;
                    break;
                }
            }
            if (!anySelected)
            {
                Debug.Log("MoveGroupTo: no unit types selected to move; treating click as focus change only.");
                return;
            }

            bool anyMoved = false;
            foreach (var kvp in _moveSelection)
            {
                var type = kvp.Key;
                int toMove = kvp.Value;
                if (toMove <= 0)
                    continue;

                foreach (var unit in Object.FindObjectsOfType<UnitInstance>())
                {
                    if (toMove <= 0)
                        break;

                    if (unit.Tile == _selectedTile &&
                        unit.Owner == Game.CurrentPlayer &&
                        unit.Definition.Type == type &&
                        !unit.HasMovedThisTurn &&
                        CanUnitMoveTo(unit, target))
                    {
                        unit.MoveTo(target);
                        target.Owner = unit.Owner;
                        // Reveal exploration only when a unit actually moves onto this hex.
                        if (!target.ExplorationRevealed && target.ExplorationReward != ExplorationReward.None)
                        {
                            RevealExploration(target);
                        }
                        toMove--;
                        anyMoved = true;
                    }
                }

                if (toMove > 0)
                {
                    Debug.LogWarning($"MoveGroupTo: could not move {toMove} '{type}' units from ({_selectedTile.Q},{_selectedTile.R}) to ({target.Q},{target.R}).");
                }
            }

            if (!anyMoved)
            {
                Debug.LogWarning($"MoveGroupTo: no units actually moved from ({_selectedTile.Q},{_selectedTile.R}) to ({target.Q},{target.R}).");
            }
        }

        public void AdjustMoveSelection(UnitType type, int delta)
        {
            if (_selectedTile == null)
                return;

            // Count how many movable units of this type are on the selected tile
            int available = 0;
            foreach (var unit in Object.FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile == _selectedTile &&
                    unit.Owner == Game.CurrentPlayer &&
                    unit.Definition.Type == type &&
                    !unit.HasMovedThisTurn)
                {
                    available++;
                }
            }

            if (!_moveSelection.ContainsKey(type))
                _moveSelection[type] = 0;

            int current = _moveSelection[type];
            current = Mathf.Clamp(current + delta, 0, available);
            _moveSelection[type] = current;
        }

        public void ClearSelection()
        {
            SetSelectedTile(null);
        }

        public bool CanUnitMoveTo(UnitInstance unit, BoardTile target)
        {
            if (unit.Tile == null || target == null || unit.Tile == target)
            {
                if (!SuppressMovementDiagnosticLogs)
                    Debug.LogWarning("CanUnitMoveTo: invalid source/target tile.");
                return false;
            }

            // A unit may only move once per player turn.
            if (unit.HasMovedThisTurn)
            {
                if (!SuppressMovementDiagnosticLogs)
                    Debug.Log($"CanUnitMoveTo: {unit.Definition.Type} already moved this turn.");
                return false;
            }

            if (Game != null && Game.EnforceRetreatRules)
            {
                var retreat = MovementRetreatRules.Evaluate(unit, target, Game);
                if (!retreat.Allowed)
                {
                    if (!SuppressMovementDiagnosticLogs)
                        Debug.LogWarning($"CanUnitMoveTo: {retreat.Reason}");
                    return false;
                }
            }

            var def = unit.Definition;
            if (!CanEnter(def, target.Type))
            {
                if (!SuppressMovementDiagnosticLogs)
                    Debug.Log($"CanUnitMoveTo: {unit.Definition.Type} cannot enter terrain type {target.Type}.");
                return false;
            }

            // Use unit's configured max distance. Only Rock Strider gets 2; others are 1.
            int maxDist = Mathf.Max(1, def.MaxMoveDistance);

            // Breadth-first search up to maxDist, respecting terrain and enemy blocking.
            var visited = new System.Collections.Generic.HashSet<BoardTile>();
            var frontier = new System.Collections.Generic.Queue<(BoardTile tile, int dist)>();
            frontier.Enqueue((unit.Tile, 0));
            visited.Add(unit.Tile);

            while (frontier.Count > 0)
            {
                var (current, dist) = frontier.Dequeue();
                if (dist >= maxDist)
                    continue;

                foreach (var n in Game.Board.GetNeighbors(current))
                {
                    if (visited.Contains(n))
                        continue;

                    // For most units with multi-hex movement, they cannot move through enemy spaces.
                    // Rock Striders are allowed to move over other units.
                    if (def.Type != UnitType.RockStrider && maxDist > 1)
                    {
                        bool hasEnemy = false;
                        foreach (var other in Object.FindObjectsOfType<UnitInstance>())
                        {
                            if (other.Tile == n && other.Owner != unit.Owner)
                            {
                                hasEnemy = true;
                                break;
                            }
                        }

                        if (hasEnemy && n != target)
                            continue;
                    }

                    if (!CanEnter(def, n.Type))
                        continue;

                    int ndist = dist + 1;
                    if (ndist <= maxDist)
                    {
                        if (n == target)
                        {
                            if (!SuppressMovementDiagnosticLogs)
                                Debug.Log(
                                    $"CanUnitMoveTo: path found for {unit.Definition.Type} from ({unit.Tile.Q},{unit.Tile.R}) to ({target.Q},{target.R}) in {ndist} step(s).");
                            return true;
                        }

                        visited.Add(n);
                        frontier.Enqueue((n, ndist));
                    }
                }
            }

            // If we get here, no path was found within maxDist
            int dq = unit.Tile.Q - target.Q;
            int dr = unit.Tile.R - target.R;
            int ds = -(unit.Tile.Q + unit.Tile.R) - (-(target.Q + target.R));
            int axialDist = (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
            if (!SuppressMovementDiagnosticLogs)
                Debug.LogWarning(
                    $"CanUnitMoveTo: no path for {unit.Definition.Type} from ({unit.Tile.Q},{unit.Tile.R}) to ({target.Q},{target.R}); axialDist={axialDist}, maxDist={maxDist}.");
            return false;
        }

        /// <summary>All tiles this unit can legally move to this turn (for AI).</summary>
        public System.Collections.Generic.List<BoardTile> GetReachableTiles(UnitInstance unit)
        {
            var list = new System.Collections.Generic.List<BoardTile>();
            if (unit == null || Game == null || Game.Board == null)
                return list;

            bool prev = SuppressMovementDiagnosticLogs;
            SuppressMovementDiagnosticLogs = true;
            try
            {
                foreach (var t in Game.Board.AllTiles)
                {
                    if (t != null && t != unit.Tile && CanUnitMoveTo(unit, t))
                        list.Add(t);
                }
            }
            finally
            {
                SuppressMovementDiagnosticLogs = prev;
            }

            return list;
        }

        /// <summary>Move one unit without using the selection UI (AI / automation).</summary>
        public bool TryAiMoveUnit(UnitInstance unit, BoardTile target)
        {
            if (Game == null || Game.BattlePhaseBlockingPlay || Game.DragonPhase != null)
                return false;
            if (unit == null || unit.Tile == null || target == null || unit.Tile == target)
                return false;
            if (unit.Owner != Game.CurrentPlayer || unit.HasMovedThisTurn)
                return false;

            bool prev = SuppressMovementDiagnosticLogs;
            SuppressMovementDiagnosticLogs = true;
            bool can = CanUnitMoveTo(unit, target);
            SuppressMovementDiagnosticLogs = prev;
            if (!can)
                return false;

            unit.MoveTo(target);
            target.Owner = unit.Owner;
            if (!target.ExplorationRevealed && target.ExplorationReward != ExplorationReward.None)
                RevealExploration(target);
            return true;
        }

        bool CanEnter(UnitDefinition def, TileType type)
        {
            return type switch
            {
                TileType.Plains => def.CanEnterPlains,
                TileType.Forest => def.CanEnterForest,
                TileType.CrystalField => def.CanEnterCrystal,
                TileType.Lava => def.CanEnterLava,
                TileType.Rock => def.CanEnterRock,
                TileType.Monolith => def.CanEnterMonolith,
                TileType.HomeBase => true,
                _ => true
            };
        }
    }
}

