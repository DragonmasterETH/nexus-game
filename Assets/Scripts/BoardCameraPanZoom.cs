using UnityEngine;
using UnityEngine.EventSystems;

namespace NexusGame
{
    /// <summary>
    /// Mobile: one-finger pan on empty board / non-unit drag; two-finger pinch zoom.
    /// Orthographic-style top-down view (fixed pitch) so flat sprites stay undistorted; zoom adjusts height only.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardCameraPanZoom : MonoBehaviour
    {
        [Header("Pan")]
        [Tooltip("Screen pixels before a drag counts as camera pan (not a tap).")]
        public float PanStartThresholdPixels = 14f;

        [Tooltip("World Y of the ground plane used for pan raycasts.")]
        public float GroundPlaneY = 0f;

        [Header("Zoom (pinch)")]
        public float MinDistanceFromTarget = 5f;
        public float MaxDistanceFromTarget = 24f;

        Camera _cam;
        Vector3 _lookTarget = Vector3.zero;

        /// <summary>Vertical distance from ground plane to camera (strict top-down).</summary>
        float _heightAboveGround = 12f;

        float _lastPinchSeparation;

        Vector2 _singleStart;
        Vector2 _singleLastScreen;
        bool _startedOnMovableUnit;
        bool _panning;
        bool _gestureEndedAsPan;
        bool _singleTouchTracked;

        void Start()
        {
            if (_cam == null)
                _cam = GetComponent<Camera>();
            SyncStateFromTransform();
            ApplyTopDownPose();
        }

        void SyncStateFromTransform()
        {
            var p = transform.position;
            _lookTarget = new Vector3(p.x, GroundPlaneY, p.z);
            _heightAboveGround = Mathf.Max(0.5f, p.y - GroundPlaneY);
            _heightAboveGround = Mathf.Clamp(_heightAboveGround, MinDistanceFromTarget, MaxDistanceFromTarget);
        }

        void ApplyTopDownPose()
        {
            _heightAboveGround = Mathf.Clamp(_heightAboveGround, MinDistanceFromTarget, MaxDistanceFromTarget);
            transform.SetPositionAndRotation(
                new Vector3(_lookTarget.x, GroundPlaneY + _heightAboveGround, _lookTarget.z),
                Quaternion.Euler(90f, 0f, 0f));
        }

        /// <summary>Call from input code on touch Began after you know if the gesture started on a movable unit.</summary>
        public void NotifyTouchBeganOnUnit(bool startedOnMovableUnit)
        {
            _startedOnMovableUnit = startedOnMovableUnit;
        }

        /// <summary>
        /// Process mobile touches for this frame. Returns true if game should not handle board taps/drags this frame.
        /// </summary>
        public bool ProcessTouchesBlockingGame(out bool suppressTapOnTouchEnd)
        {
            suppressTapOnTouchEnd = false;
            if (_cam == null)
                _cam = GetComponent<Camera>();

            // UI consumes touches
            if (Input.touchCount > 0)
            {
                var t0 = Input.GetTouch(0);
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t0.fingerId))
                    return false;
            }

            // Two-finger pinch
            if (Input.touchCount >= 2)
            {
                _singleTouchTracked = false;
                _panning = false;

                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                float sep = Vector2.Distance(a.position, b.position);

                if (_lastPinchSeparation > 0.01f)
                {
                    float ratio = sep / _lastPinchSeparation;
                    ApplyPinchZoom(ratio);
                }

                _lastPinchSeparation = sep;
                ApplyTopDownPose();
                return true;
            }

            _lastPinchSeparation = 0f;

            if (Input.touchCount != 1)
                return false;

            var touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _singleStart = touch.position;
                    _singleLastScreen = touch.position;
                    _panning = false;
                    _gestureEndedAsPan = false;
                    _singleTouchTracked = true;
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (!_singleTouchTracked)
                        break;
                    if (_startedOnMovableUnit)
                        break;

                    if (!_panning &&
                        Vector2.Distance(touch.position, _singleStart) >= PanStartThresholdPixels)
                        _panning = true;

                    if (_panning)
                    {
                        ApplyPanScreenDelta(_singleLastScreen, touch.position);
                        _singleLastScreen = touch.position;
                        _gestureEndedAsPan = true;
                        return true;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    suppressTapOnTouchEnd = _gestureEndedAsPan || _panning;
                    _panning = false;
                    _singleTouchTracked = false;
                    _gestureEndedAsPan = false;
                    _startedOnMovableUnit = false;
                    if (suppressTapOnTouchEnd)
                        return true;
                    break;
            }

            return false;
        }

#if UNITY_EDITOR || (!UNITY_IOS && !UNITY_ANDROID)
        void Update()
        {
            if (_cam == null)
                _cam = GetComponent<Camera>();
            if (_cam == null)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // Scroll = zoom, right-drag = pan (editor / desktop testing)
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float factor = scroll > 0 ? 0.92f : 1.08f;
                ApplyPinchZoom(factor);
            }

            if (Input.GetMouseButtonDown(1))
                _singleLastScreen = Input.mousePosition;
            if (Input.GetMouseButton(1))
            {
                var cur = (Vector2)Input.mousePosition;
                ApplyPanScreenDelta(_singleLastScreen, cur);
                _singleLastScreen = cur;
            }
        }
#endif

        void ApplyPinchZoom(float separationRatioThisFrame)
        {
            if (separationRatioThisFrame < 1e-5f)
                return;
            _heightAboveGround /= separationRatioThisFrame;
            ApplyTopDownPose();
        }

        void ApplyPanScreenDelta(Vector2 prevScreen, Vector2 currScreen)
        {
            if (!TryRayGround(prevScreen, out var p0) || !TryRayGround(currScreen, out var p1))
                return;

            Vector3 delta = p0 - p1;
            delta.y = 0f;
            _lookTarget += delta;
            ApplyTopDownPose();
        }

        bool TryRayGround(Vector2 screen, out Vector3 hit)
        {
            hit = default;
            if (_cam == null)
                return false;
            Ray ray = _cam.ScreenPointToRay(screen);
            float dy = ray.direction.y;
            if (Mathf.Abs(dy) < 1e-5f)
                return false;
            float t = (GroundPlaneY - ray.origin.y) / dy;
            if (t < 0f)
                return false;
            hit = ray.origin + ray.direction * t;
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_lookTarget, 0.15f);
        }
    }
}
