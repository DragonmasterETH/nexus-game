using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NexusGame
{
        public class MobileInputController : MonoBehaviour
        {
        public Camera MainCamera;
        public GameController Game;
        [Tooltip("Used for the centered deploy modal: block drags on the panel, close on outside tap, double-tap open.")]
        public DemoHUD Hud;

        [Header("Debug")]
        public bool DebugClicks;

        [Tooltip("Hides verbose CanUnitMoveTo / move warnings (used when AI pathfinding).")]
        public bool SuppressMovementDiagnosticLogs;

            BoardTile _popupTile;
            BoardTile _selectedTile;
            readonly System.Collections.Generic.Dictionary<UnitType, int> _moveSelection =
                new System.Collections.Generic.Dictionary<UnitType, int>();
            readonly System.Collections.Generic.HashSet<UnitType> _explicitMoveSelection =
                new System.Collections.Generic.HashSet<UnitType>();
            readonly System.Collections.Generic.HashSet<BoardTile> _activeMoveDestinationHighlights =
                new System.Collections.Generic.HashSet<BoardTile>();
            readonly System.Collections.Generic.HashSet<BoardTile> _activeDragonStrikeHighlights =
                new System.Collections.Generic.HashSet<BoardTile>();
            readonly System.Collections.Generic.HashSet<BoardTile> _activeFortressPlacementHighlights =
                new System.Collections.Generic.HashSet<BoardTile>();

            static readonly Color DragonStrikeRingColor = new Color(1f, 0.52f, 0.08f, 1f);
            static readonly Color MoveDestinationRingColor = new Color(0.2f, 0.95f, 0.25f, 1f);
            static readonly Color FortressPlacementRingColor = new Color(0.45f, 0.82f, 1f, 1f);

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

            BoardCameraPanZoom _boardCam;

            float _lastTapTime = -999f;
            Vector2 _lastTapScreenPos;
            int _lastTapQ;
            int _lastTapR;
            bool _lastTapValid;
            const float DoubleTapMaxGapSeconds = 0.42f;
            const float DoubleTapMaxMovePixels = 42f;

            public BoardTile SelectedTile => _selectedTile;
            public System.Collections.Generic.IReadOnlyDictionary<UnitType, int> SelectedMoveCounts => _moveSelection;

        void Start()
        {
            if (MainCamera == null)
                MainCamera = Camera.main;
            if (Game == null)
                Game = FindObjectOfType<GameController>();
            if (_boardCam == null)
                _boardCam = FindObjectOfType<BoardCameraPanZoom>();
            if (Hud == null)
                Hud = FindObjectOfType<DemoHUD>();
        }

        void LateUpdate()
        {
            RefreshDragonStrikeHighlights();
            RefreshFortressPlacementHighlights();
        }

        void Update()
        {
            if (Game != null && Game.IsGameOver)
            {
                ProcessPinchZoomIfActive();
                return;
            }

            // Two-finger pinch — always, including during battle modals and when it's not your turn.
            if (ProcessPinchZoomIfActive())
                return;

            bool canAct = Game == null || Game.CanLocalPlayerActNow();

            if (IsBattleOverlayBlockingBoardInput())
            {
                if (_selectedTile != null)
                    ClearSelection();
                _pendingTap = false;
                _dragging = false;
                _dragPrepared = false;
                _dragSourceTile = null;
                _dragStartUnit = null;
                return;
            }

            // Touch: single-finger pan (BoardCameraPanZoom) before board taps / unit drags
            if (Input.touchCount == 1)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    _pendingTap = true;
                    _dragging = false;
                    _dragPrepared = false;
                    _dragStartUnit = null;
                    _pointerDownPos = touch.position;

                    if (EventSystem.current != null &&
                        EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        _pendingTap = false;
                    else if (Hud != null && !Hud.IsCenterBuyModalOpen && Hud.ScreenPointOverlapsBlockingHud(touch.position))
                        _pendingTap = false;
                    else if (Hud != null && Hud.ScreenPointOverlapsBuyMenu(touch.position))
                        _pendingTap = false;

                    PrepareDragFromPointer(touch.position);
                    if (!canAct)
                    {
                        _dragPrepared = false;
                        _dragSourceTile = null;
                        _dragStartUnit = null;
                    }

                    if (_boardCam != null)
                        _boardCam.NotifyTouchBeganOnUnit(_dragPrepared);
                }

                if (_boardCam != null && _boardCam.ProcessTouchesBlockingGame(out _))
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _pendingTap = false;
                        _dragging = false;
                        _dragPrepared = false;
                        _dragSourceTile = null;
                        _dragStartUnit = null;
                    }

                    return;
                }

                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    if (_pendingTap && !_dragging && _dragPrepared)
                    {
                        if (Vector2.Distance(touch.position, _pointerDownPos) >= _dragThresholdPixels)
                            _dragging = true;
                    }
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (canAct &&
                        _dragPrepared &&
                        (_dragging ||
                         Vector2.Distance(touch.position, _pointerDownPos) >= _dragThresholdPixels))
                    {
                        TryDragMove(touch.position);
                    }
                    else if (_pendingTap)
                    {
                        HandleTap(_pointerDownPos, canAct);
                    }

                    _pendingTap = false;
                    _dragging = false;
                    _dragPrepared = false;
                    _dragSourceTile = null;
                    _dragStartUnit = null;
                }
            }

            // Mouse: editor + desktop builds (mobile device builds omit this)
#if UNITY_EDITOR || (!UNITY_IOS && !UNITY_ANDROID)
            if (Input.GetMouseButtonDown(0))
            {
                _pendingTap = true;
                _dragging = false;
                _dragPrepared = false;
                _dragStartUnit = null;
                _pointerDownPos = Input.mousePosition;

                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    _pendingTap = false;
                else if (Hud != null && !Hud.IsCenterBuyModalOpen && Hud.ScreenPointOverlapsBlockingHud(Input.mousePosition))
                    _pendingTap = false;
                else if (Hud != null && Hud.ScreenPointOverlapsBuyMenu(Input.mousePosition))
                    _pendingTap = false;

                PrepareDragFromPointer(Input.mousePosition);
                if (!canAct)
                {
                    _dragPrepared = false;
                    _dragSourceTile = null;
                    _dragStartUnit = null;
                }
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
                if (canAct &&
                    _dragPrepared &&
                    (_dragging || Vector2.Distance(Input.mousePosition, _pointerDownPos) >= _dragThresholdPixels))
                {
                    TryDragMove(Input.mousePosition);
                }
                else if (_pendingTap)
                {
                    HandleTap(_pointerDownPos, canAct);
                }

                _pendingTap = false;
                _dragging = false;
                _dragPrepared = false;
                _dragSourceTile = null;
                _dragStartUnit = null;
            }
#endif
        }

        bool ProcessPinchZoomIfActive()
        {
            if (Input.touchCount < 2 || _boardCam == null || !_boardCam.enabled)
                return false;

            // Pinch zoom runs in BoardCameraPanZoom.Update — here we only block taps/drags.
            _pendingTap = false;
            _dragging = false;
            _dragPrepared = false;
            _dragSourceTile = null;
            _dragStartUnit = null;
            return true;
        }

        void PrepareDragFromPointer(Vector2 screenPos)
        {
            if (Game == null || Game.CurrentPlayer == null || !Game.CanLocalPlayerActFor(Game.CurrentPlayer))
                return;
            if (Game.DragonPhase != null || Game.PendingFortressPlacement)
                return;
            if (IsBattleOverlayBlockingBoardInput())
                return;
            if (MainCamera == null)
                return;
            if (Hud != null && Hud.IsCenterBuyModalOpen)
                return;
            if (Hud != null && Hud.ScreenPointOverlapsBlockingHud(screenPos))
                return;
            if (Hud != null && Hud.ScreenPointOverlapsBuyMenu(screenPos))
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
                    // Do not auto-select units on click. Selection happens only through +/- UI
                    // or an actual drag move.
                    return;
                }
            }
        }

        void TryDragMove(Vector2 screenPos)
        {
            if (Game == null || !Game.CanLocalPlayerActNow())
                return;
            if (Hud != null && Hud.IsCenterBuyModalOpen)
                return;
            if (Hud != null && Hud.ScreenPointOverlapsBuyMenu(screenPos))
                return;
            var target = ResolveTileFromPointer(screenPos);
            if (target == null || _dragSourceTile == null)
                return;
            if (_dragSourceTile == target)
                return;

            var selection = new System.Collections.Generic.Dictionary<UnitType, int>
            {
                { _dragType, 1 }
            };
            var explicitTypes = new System.Collections.Generic.HashSet<UnitType> { _dragType };
            NexusGameCommands.RequestMoveGroup(_dragSourceTile, target, selection, explicitTypes);
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

        void HandleTap(Vector2 screenPos, bool canAct)
        {
            if (IsBattleOverlayBlockingBoardInput())
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            if (Hud != null && Hud.IsCenterBuyModalOpen)
            {
                Hud.HandleCenterBuyModalTap(screenPos);
                return;
            }
            if (Hud != null && Hud.ScreenPointOverlapsBlockingHud(screenPos))
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

            if (canAct && Game != null && Game.PendingFortressPlacement)
            {
                if (Game.TryPlaceFortressOnHex(clickedTile))
                    SetSelectedTile(clickedTile);
                RecordLastTapForDoubleTap(clickedTile, screenPos);
                return;
            }

            if (canAct && Game != null && Game.CanUseDeploymentEnergizeNow() && Game.DragonPhase == null &&
                Game.CurrentPlayer != null && Game.TileHasFortressForPlayer(clickedTile, Game.CurrentPlayer) &&
                Game.CanBeginFortressBreathDuringDeploy(clickedTile))
            {
                Game.TryBeginFortressBreathFromHex(clickedTile);
                RecordLastTapForDoubleTap(clickedTile, screenPos);
                return;
            }

            if (Game != null && Game.DragonPhase != null && Game.CanLocalPlayerActFor(Game.DragonPhase.Player) &&
                Game.DragonPhase.PendingHit == null && Game.DragonPhase.Options != null)
            {
                foreach (var opt in Game.DragonPhase.Options)
                {
                    if (opt == null || opt.TargetHex != clickedTile)
                        continue;
                    Game.ExecuteDragonStrike(opt);
                    RecordLastTapForDoubleTap(clickedTile, screenPos);
                    return;
                }

                RecordLastTapForDoubleTap(clickedTile, screenPos);
                return;
            }

            bool isDoubleTap = _lastTapValid &&
                               _lastTapQ == clickedTile.Q &&
                               _lastTapR == clickedTile.R &&
                               (Time.time - _lastTapTime) <= DoubleTapMaxGapSeconds &&
                               Vector2.Distance(screenPos, _lastTapScreenPos) <= DoubleTapMaxMovePixels;

            if (canAct && Game != null && Game.CurrentPlayer != null && isDoubleTap)
            {
                SetSelectedTile(clickedTile);
                if (Hud != null)
                    Hud.OpenCenterBuyModal();
                RecordLastTapForDoubleTap(clickedTile, screenPos);
                return;
            }

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

                RecordLastTapForDoubleTap(clickedTile, screenPos);
                return;
            }

            // If we have a source tile selected and a move selection defined, and we click a different tile,
            // attempt to move the selected group there immediately (single-click confirm).
            if (_selectedTile != null && clickedTile != _selectedTile)
            {
                if (canAct)
                    TryMoveGroupTo(clickedTile);
                SetSelectedTile(clickedTile);
                RecordLastTapForDoubleTap(clickedTile, screenPos);
            }
        }

        void RecordLastTapForDoubleTap(BoardTile tile, Vector2 screenPos)
        {
            if (tile == null)
                return;
            _lastTapTime = Time.time;
            _lastTapScreenPos = screenPos;
            _lastTapQ = tile.Q;
            _lastTapR = tile.R;
            _lastTapValid = true;
        }

        [Tooltip("How much the hex fill darkens while selected (0 = none, 1 = black).")]
        [Range(0f, 0.6f)]
        public float SelectedHexDimTowardBlack = 0.22f;

        void SetSelectedTile(BoardTile tile)
        {
            ClearMoveDestinationHighlights();
            if (_selectedTile != null)
            {
                ApplyHexFillSelectionDim(_selectedTile, false);
                if (_selectedTile.Highlight != null)
                    _selectedTile.Highlight.SetActive(false);
            }

            _selectedTile = tile;
            _popupTile = tile;
            _moveSelection.Clear();
            _explicitMoveSelection.Clear();

            if (_selectedTile != null)
            {
                ApplyHexFillSelectionDim(_selectedTile, true);
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

            RefreshMoveDestinationHighlights();
        }

        /// <summary>Terrain fill only — not outlines, ore chips, or exploration quads.</summary>
        static Renderer PrimaryHexFillRenderer(GameObject view)
        {
            if (view == null)
                return null;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer)
                    continue;
                if (!IsHexTerrainFillRenderer(r.transform))
                    continue;
                return r;
            }

            return null;
        }

        static bool IsHexTerrainFillRenderer(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name == "MineLabel" || t.name == "ExplorationMarker")
                    return false;
            }

            return true;
        }

        void ApplyHexFillSelectionDim(BoardTile tile, bool selected)
        {
            if (tile == null)
                return;
            var rend = PrimaryHexFillRenderer(tile.View);
            if (rend == null)
                return;

            if (!tile.HexFillBaseColorCaptured)
            {
                tile.HexFillBaseColor = rend.material.color;
                tile.HexFillBaseColorCaptured = true;
            }

            float dim = Mathf.Clamp01(SelectedHexDimTowardBlack);
            rend.material.color = selected
                ? Color.Lerp(tile.HexFillBaseColor, Color.black, dim)
                : tile.HexFillBaseColor;
        }

        // Slight lift above hex mesh to avoid z-fighting; centered on the hex (not floating over units).
        const float MineChipLocalY = 0.035f;
        const float MineChipScaleMul = 1.12f;

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
                    // Legacy token — exploration never grants Humans now; treat as Fungoid on this hex.
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.Fungoid, tile);
                    debugMessage += "Legacy FreeHuman token → Free Fungoid";
                    break;
                case ExplorationReward.FreeCrystalline:
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.Crystalline, tile);
                    debugMessage += "Free Crystalline";
                    break;
                case ExplorationReward.FreeLavaLeaper:
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.LavaLeaper, tile);
                    debugMessage += "Free Lava Leaper";
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
                    // Legacy combo token — grant non-human unit + mine (same mine bonus as before).
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.Fungoid, tile);
                    debugMessage += "Legacy FreeHumanAndMine2 → Free Fungoid + Mine bonus +2";
                    tile.ExtraMineYield = 2;
                    UpdateMineLabel(tile);
                    break;
                case ExplorationReward.FreeFungoidAndMine2:
                    Game.SpawnUnit(Game.CurrentPlayer, UnitType.Fungoid, tile);
                    tile.ExtraMineYield = 2;
                    UpdateMineLabel(tile);
                    debugMessage += "Free Fungoid + Mine bonus +2";
                    break;
                default:
                    debugMessage += "No reward (None)";
                    break;
            }

            Debug.Log(debugMessage);
        }

        public void UpdateMineLabel(BoardTile tile)
        {
            if (tile == null)
                return;

            // Home bases always use the refinery overlay (income still uses ExtraMineYield).
            if (tile.Type == TileType.HomeBase)
            {
                if (Game?.Board != null)
                    Game.Board.EnsureHomeRefineryVisual(tile);
                return;
            }

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

            // Drop legacy number+text labels; revealed mines use ore chip art on a quad.
            if (tile.MineLabel != null && tile.MineLabel.GetComponentInChildren<TextMesh>() != null)
            {
                Object.Destroy(tile.MineLabel);
                tile.MineLabel = null;
            }

            float hexR = Game != null && Game.Board != null ? Game.Board.HexRadius : 0.7f;

            if (tile.MineLabel == null)
            {
                var labelRoot = new GameObject("MineLabel");
                labelRoot.transform.SetParent(tile.View.transform, worldPositionStays: false);
                labelRoot.transform.localRotation = Quaternion.identity;
                labelRoot.transform.localScale = Vector3.one;

                var chip = GameObject.CreatePrimitive(PrimitiveType.Quad);
                chip.name = "MineOreChip";
                chip.transform.SetParent(labelRoot.transform, worldPositionStays: false);
                chip.transform.localPosition = Vector3.zero;
                chip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                tile.MineLabel = labelRoot;
            }

            tile.MineLabel.transform.localPosition = new Vector3(0f, MineChipLocalY, 0f);
            var chipTf = tile.MineLabel.transform.Find("MineOreChip");
            if (chipTf != null)
                chipTf.localScale = Vector3.one * (hexR * MineChipScaleMul);

            int yv = tile.ExtraMineYield;
            int matKey = yv < 1 ? 1 : (yv > 3 ? 3 : yv);
            var mat = NexusGuiArt.GetSharedWorldOreChipMaterial(matKey);
            var rend = tile.MineLabel.GetComponentInChildren<Renderer>();
            if (rend != null && mat != null)
                rend.sharedMaterial = mat;
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

            NexusGameCommands.RequestMoveGroup(_selectedTile, target, _moveSelection, _explicitMoveSelection);
        }

        public bool TryExecuteMoveGroup(PlayerState actingPlayer, BoardTile from, BoardTile to,
            System.Collections.Generic.IReadOnlyDictionary<UnitType, int> selection,
            System.Collections.Generic.IReadOnlyCollection<UnitType> explicitTypes)
        {
            if (Game == null || actingPlayer == null || from == null || to == null || selection == null ||
                explicitTypes == null)
                return false;
            if (Game.BattlePhaseBlockingPlay)
                return false;

            bool anyMoved = false;
            foreach (var kvp in selection)
            {
                var type = kvp.Key;
                int toMove = kvp.Value;
                if (toMove <= 0 || !explicitTypes.Contains(type))
                    continue;

                foreach (var unit in Object.FindObjectsOfType<UnitInstance>())
                {
                    if (toMove <= 0)
                        break;

                    if (unit.Tile == from &&
                        unit.Owner == actingPlayer &&
                        unit.Definition.Type == type &&
                        !unit.HasMovedThisTurn &&
                        CanUnitMoveTo(unit, to))
                    {
                        var moveFrom = unit.Tile;
                        unit.MoveTo(to);
                        to.Owner = unit.Owner;
                        Game.NotifyUnitMoved(unit.Owner, moveFrom, to);
                        if (!to.ExplorationRevealed && to.ExplorationReward != ExplorationReward.None)
                            RevealExploration(to);
                        toMove--;
                        anyMoved = true;
                    }
                }
            }

            if (anyMoved)
                Game.AfterOnlineHostMutation();

            return anyMoved;
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
            if (current > 0)
                _explicitMoveSelection.Add(type);
            else
                _explicitMoveSelection.Remove(type);

            RefreshMoveDestinationHighlights();
        }

        public void SetMoveSelection(UnitType type, int amount)
        {
            if (_selectedTile == null)
                return;

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

            int clamped = Mathf.Clamp(amount, 0, available);
            _moveSelection[type] = clamped;
            if (clamped > 0)
                _explicitMoveSelection.Add(type);
            else
                _explicitMoveSelection.Remove(type);

            RefreshMoveDestinationHighlights();
        }

        public void ClearSelection()
        {
            SetSelectedTile(null);
        }

        void RefreshMoveDestinationHighlights()
        {
            ClearMoveDestinationHighlights();

            if (_selectedTile == null || Game == null || Game.Board == null || Game.CurrentPlayer == null)
                return;

            var selectedMovers = CollectSelectedMovers();
            if (selectedMovers.Count == 0)
                return;

            bool prevSuppress = SuppressMovementDiagnosticLogs;
            SuppressMovementDiagnosticLogs = true;
            try
            {
                var intersection = new System.Collections.Generic.HashSet<BoardTile>(Game.Board.AllTiles);
                foreach (var unit in selectedMovers)
                {
                    var unitReachable = GetReachableTiles(unit);
                    intersection.IntersectWith(unitReachable);
                    if (intersection.Count == 0)
                        break;
                }

                intersection.Remove(_selectedTile);
                foreach (var tile in intersection)
                    SetMoveHighlight(tile, true);
            }
            finally
            {
                SuppressMovementDiagnosticLogs = prevSuppress;
            }
        }

        System.Collections.Generic.List<UnitInstance> CollectSelectedMovers()
        {
            var list = new System.Collections.Generic.List<UnitInstance>();
            if (_selectedTile == null || Game == null || Game.CurrentPlayer == null)
                return list;

            var unitsByType = new System.Collections.Generic.Dictionary<UnitType, System.Collections.Generic.List<UnitInstance>>();
            foreach (var unit in Object.FindObjectsOfType<UnitInstance>())
            {
                if (unit == null)
                    continue;
                if (unit.Tile != _selectedTile || unit.Owner != Game.CurrentPlayer || unit.HasMovedThisTurn)
                    continue;

                var type = unit.Definition.Type;
                if (!unitsByType.TryGetValue(type, out var bucket))
                {
                    bucket = new System.Collections.Generic.List<UnitInstance>();
                    unitsByType[type] = bucket;
                }

                bucket.Add(unit);
            }

            foreach (var kvp in _moveSelection)
            {
                int wanted = kvp.Value;
                if (wanted <= 0)
                    continue;
                if (!_explicitMoveSelection.Contains(kvp.Key))
                    continue;
                if (!unitsByType.TryGetValue(kvp.Key, out var bucket) || bucket.Count == 0)
                    continue;

                int take = Mathf.Min(wanted, bucket.Count);
                for (int i = 0; i < take; i++)
                    list.Add(bucket[i]);
            }

            return list;
        }

        void ClearMoveDestinationHighlights()
        {
            var toClear = new System.Collections.Generic.List<BoardTile>(_activeMoveDestinationHighlights);
            foreach (var tile in toClear)
                SetMoveHighlight(tile, false);
            _activeMoveDestinationHighlights.Clear();
        }

        void SetMoveHighlight(BoardTile tile, bool on)
        {
            if (tile == null || tile.View == null)
                return;
            var tf = tile.View.transform.Find("MoveHighlight");
            if (tf == null)
                return;
            var lr = tf.GetComponent<LineRenderer>();
            if (lr != null && on)
                lr.startColor = lr.endColor = MoveDestinationRingColor;

            tf.gameObject.SetActive(on);
            if (on)
                _activeMoveDestinationHighlights.Add(tile);
            else
                _activeMoveDestinationHighlights.Remove(tile);
        }

        void RefreshFortressPlacementHighlights()
        {
            ClearFortressPlacementHighlights();
            if (Game == null || Game.Board == null || !Game.PendingFortressPlacement)
                return;
            if (!Game.CanLocalPlayerActNow() || Game.CurrentPlayer == null)
                return;

            var player = Game.CurrentPlayer;
            foreach (var tile in Game.Board.AllTiles)
            {
                if (Game.CanPlaceFortressOnTile(player, tile))
                    SetFortressPlacementHighlight(tile, true);
            }
        }

        void ClearFortressPlacementHighlights()
        {
            var toClear = new System.Collections.Generic.List<BoardTile>(_activeFortressPlacementHighlights);
            foreach (var tile in toClear)
                SetFortressPlacementHighlight(tile, false);
            _activeFortressPlacementHighlights.Clear();
        }

        void SetFortressPlacementHighlight(BoardTile tile, bool on)
        {
            if (tile == null || tile.View == null)
                return;
            var tf = tile.View.transform.Find("MoveHighlight");
            if (tf == null)
                return;
            var lr = tf.GetComponent<LineRenderer>();
            if (lr != null)
                lr.startColor = lr.endColor = on ? FortressPlacementRingColor : MoveDestinationRingColor;

            tf.gameObject.SetActive(on);
            if (on)
                _activeFortressPlacementHighlights.Add(tile);
            else
                _activeFortressPlacementHighlights.Remove(tile);
        }

        void RefreshDragonStrikeHighlights()
        {
            ClearDragonStrikeHighlights();
            if (Game == null || Game.Board == null)
                return;
            var dp = Game.DragonPhase;
            if (dp == null || dp.Player == null)
                return;
            if (Game.IsAiControlled(dp.Player))
                return;
            if (dp.PendingHit != null)
                return;
            if (dp.Options == null || dp.Options.Count == 0)
                return;

            foreach (var opt in dp.Options)
            {
                if (opt?.TargetHex != null)
                    SetDragonStrikeHighlight(opt.TargetHex, true);
            }
        }

        void ClearDragonStrikeHighlights()
        {
            var toClear = new System.Collections.Generic.List<BoardTile>(_activeDragonStrikeHighlights);
            foreach (var tile in toClear)
                SetDragonStrikeHighlight(tile, false);
            _activeDragonStrikeHighlights.Clear();
        }

        void SetDragonStrikeHighlight(BoardTile tile, bool on)
        {
            if (tile == null || tile.View == null)
                return;
            var tf = tile.View.transform.Find("MoveHighlight");
            if (tf == null)
                return;
            var lr = tf.GetComponent<LineRenderer>();
            if (lr != null)
                lr.startColor = lr.endColor = on ? DragonStrikeRingColor : MoveDestinationRingColor;

            tf.gameObject.SetActive(on);
            if (on)
                _activeDragonStrikeHighlights.Add(tile);
            else
                _activeDragonStrikeHighlights.Remove(tile);
        }

        bool IsBattleOverlayBlockingBoardInput()
        {
            if (Game == null)
                return false;
            if (Game.PendingBattleArrangement)
                return true;
            if (Game.IsBattleScreenActive)
                return true;
            return Game.SecretMissionOverdraw != null && Game.SecretMissionOverdraw.Waiting;
        }

        public bool CanUnitMoveTo(UnitInstance unit, BoardTile target)
        {
            if (Game != null && Game.IsGameOver)
                return false;

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

            var from = unit.Tile;
            unit.MoveTo(target);
            target.Owner = unit.Owner;
            Game.NotifyUnitMoved(unit.Owner, from, target);
            if (!target.ExplorationRevealed && target.ExplorationReward != ExplorationReward.None)
                RevealExploration(target);
            Game.AfterOnlineHostMutation();
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

