using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Nymphs_TDC.Scripts
{
    /// <summary>
    /// Professional Nymphs Top Down Camera Controller.
    /// Handles dynamic pitch based on zoom, manual rotation, and weighted zoom smoothing.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The target the camera will follow (usually the player). If empty, Auto Find Target can assign one at runtime.")]
        [SerializeField] private Transform target;

        [Tooltip("If true, the camera will search for a target when none is assigned.")]
        [SerializeField] private bool autoFindTarget = true;

        [Tooltip("Tag to use when auto-finding the target (default Player).")]
        [SerializeField] private string targetTag = "Player";

        [Tooltip("Seconds between target search attempts when none is assigned.")]
        [SerializeField] private float targetSearchInterval = 1f;

        [Tooltip("Vertical offset from the target's pivot point.")]
        [SerializeField] private float heightOffset = 3f;

        [Header("Input Settings")]
        [Tooltip("PlayerInput to read Look/Zoom actions from. If empty, the component will try to find one.")]
        [SerializeField] private PlayerInput playerInput;

        [Tooltip("Input action name used for camera look/rotation (default Look).")]
        [SerializeField] private string lookActionName = "Look";

        [Tooltip("Input action name used for camera zoom (default Zoom).")]
        [SerializeField] private string zoomActionName = "Zoom";

        [Tooltip("If true, uses legacy Mouse/Gamepad input when no PlayerInput actions are found.")]
        [SerializeField] private bool useLegacyInputFallback = true;

        [Header("Zoom Settings")]
        [Tooltip("Initial distance from the target.")]
        [SerializeField] private float distance = 11f;
    
        [Tooltip("Minimum zoom distance.")]
        [SerializeField] private float minDistance = 11f;
    
        [Tooltip("Maximum zoom distance.")]
        [SerializeField] private float maxDistance = 60f;
    
        [Tooltip("How fast the camera zooms in and out.")]
        [SerializeField] private float zoomSpeed = 40f;

        [Tooltip("Zoom smoothing time in seconds. 0 = instant. Higher values = longer tail.")]
        [SerializeField] private float zoomSmoothTime = 0.2f;

        [Tooltip("Mouse wheel zoom multiplier. Higher = bigger zoom steps on the wheel.")]
        [SerializeField] private float mouseWheelZoomMultiplier = 0.5f;

        [Tooltip("Minimum right-stick vertical input required before gamepad zoom starts.")]
        // ReSharper disable once SpellCheckingInspection
        [FormerlySerializedAs("gamepadZoomDeadzone")]
        [SerializeField] private float gamepadZoomDeadZone = 0.5f;

        [Tooltip("Multiplier for gamepad zoom strength after the dead zone is exceeded.")]
        [SerializeField] private float gamepadZoomStrength = 3f;

        [Header("Rotation Settings")]
        [Tooltip("How fast the camera rotates horizontally with mouse drag.")]
        [SerializeField] private float mouseRotationSpeed = 2f;

        [Tooltip("How fast the camera rotates horizontally with the gamepad right stick.")]
        [SerializeField] private float gamepadRotationSpeed = 100f;

        [Tooltip("Minimum right-stick horizontal input required before rotation starts.")]
        // ReSharper disable once SpellCheckingInspection
        [FormerlySerializedAs("gamepadRotationDeadzone")]
        [SerializeField] private float gamepadRotationDeadZone = 0.2f;

        [Tooltip("Extra multiplier for mouse Look action to match raw mouse delta feel.")]
        [SerializeField] private float mouseLookActionSensitivity = 1f;
    
        [Header("Dynamic Pitch Settings")]
        [Tooltip("The camera's pitch angle when at minimum zoom distance.")]
        [SerializeField] private float minPitch = 20f;

        [Tooltip("The camera's pitch angle when at maximum zoom distance.")]
        [SerializeField] private float maxPitch = 40f;
                                                                                                                         
        private float _currentYaw;
        private float _targetDistance;
        private float _zoomVelocity;
        private InputAction _lookAction;
        private InputAction _zoomAction;
        private float _nextTargetSearchTime;
        private bool _warnedMissingTarget;

        private void Awake()
        {
            _currentYaw = transform.eulerAngles.y;
            _targetDistance = distance;
            TryResolveTarget();
            TryResolveInput();
        }

        private void OnEnable()
        {
            EnableActions();
        }

        private void OnDisable()
        {
            _lookAction?.Disable();
            _zoomAction?.Disable();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (autoFindTarget && Time.time >= _nextTargetSearchTime)
                {
                    _nextTargetSearchTime = Time.time + Mathf.Max(0.1f, targetSearchInterval);
                    TryResolveTarget();
                    TryResolveInput();
                }

                if (target == null)
                {
                    if (!_warnedMissingTarget)
                    {
                        Debug.LogWarning("CameraController has no target. Assign Target or tag your player and enable Auto Find Target.", this);
                        _warnedMissingTarget = true;
                    }
                    return;
                }
            }

            _warnedMissingTarget = false;

            HandleZoom();
            HandleRotation();
            UpdateCameraPosition();
        }

        private void HandleZoom()
        {
            bool usedAction = false;
            if (_zoomAction != null)
            {
                float zoomInput = _zoomAction.ReadValue<float>();
                if (Mathf.Abs(zoomInput) > 0.001f)
                {
                    bool isMouse = _zoomAction.activeControl != null && _zoomAction.activeControl.device is Mouse;
                    if (!isMouse)
                    {
                        float deadZone = Mathf.Clamp(gamepadZoomDeadZone, 0f, 0.99f);
                        if (Mathf.Abs(zoomInput) <= deadZone)
                        {
                            zoomInput = 0f;
                        }
                        else
                        {
                            zoomInput = (Mathf.Abs(zoomInput) - deadZone) / (1f - deadZone) * Mathf.Sign(zoomInput);
                        }
                    }

                    if (Mathf.Abs(zoomInput) > 0f)
                    {
                        float scaled = isMouse
                            ? zoomInput * mouseWheelZoomMultiplier
                            : zoomInput * gamepadZoomStrength * Time.deltaTime;
                        _targetDistance -= scaled * zoomSpeed;
                        usedAction = true;
                    }
                }
            }

            if (!usedAction && useLegacyInputFallback)
            {
                // Legacy fallback: mouse scroll + gamepad right stick Y for zoom.
                if (Mouse.current != null)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        _targetDistance -= scroll * mouseWheelZoomMultiplier * zoomSpeed;
                    }
                }

                if (Gamepad.current != null)
                {
                    Vector2 rightStick = Gamepad.current.rightStick.ReadValue();
                    float deadZone = Mathf.Clamp(gamepadZoomDeadZone, 0f, 0.99f);
                    if (Mathf.Abs(rightStick.y) > deadZone)
                    {
                        float zoomInput = (rightStick.y > 0f)
                            ? (rightStick.y - deadZone) / (1f - deadZone)
                            : (rightStick.y + deadZone) / (1f - deadZone);
                        _targetDistance -= zoomInput * zoomSpeed * gamepadZoomStrength * Time.deltaTime;
                    }
                }
            }

            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            if (zoomSmoothTime <= 0f)
            {
                distance = _targetDistance;
                _zoomVelocity = 0f;
            }
            else
            {
                distance = Mathf.SmoothDamp(distance, _targetDistance, ref _zoomVelocity, zoomSmoothTime);
            }
        }

        private void HandleRotation()
        {
            bool usedAction = false;
            if (_lookAction != null)
            {
                bool isMouse = _lookAction.activeControl != null && _lookAction.activeControl.device is Mouse;
                if (isMouse)
                {
                    if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                    {
                        Vector2 look = _lookAction.ReadValue<Vector2>();
                        _currentYaw += look.x * mouseRotationSpeed * mouseLookActionSensitivity;
                        usedAction = true;
                    }
                }
                else
                {
                    Vector2 look = _lookAction.ReadValue<Vector2>();
                    if (look.sqrMagnitude > 0.0001f)
                    {
                        float deadZone = Mathf.Clamp(gamepadRotationDeadZone, 0f, 0.99f);
                        float x = Mathf.Abs(look.x) > deadZone
                            ? (Mathf.Abs(look.x) - deadZone) / (1f - deadZone) * Mathf.Sign(look.x)
                            : 0f;
                        if (Mathf.Abs(x) > 0f)
                        {
                            _currentYaw += x * gamepadRotationSpeed * Time.deltaTime;
                            usedAction = true;
                        }
                    }
                }
            }

            if (!usedAction && useLegacyInputFallback)
            {
                if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                {
                    float mouseX = Mouse.current.delta.ReadValue().x;
                    _currentYaw += mouseX * mouseRotationSpeed * mouseLookActionSensitivity;
                }
            }
        }

        private void UpdateCameraPosition()
        {
            float pitchLerp = (maxDistance > minDistance) ? (distance - minDistance) / (maxDistance - minDistance) : 0f;
            float currentPitch = Mathf.Lerp(minPitch, maxPitch, pitchLerp);

            Quaternion rotation = Quaternion.Euler(currentPitch, _currentYaw, 0);
            Vector3 position = target.position - (rotation * Vector3.forward * distance) + Vector3.up * heightOffset;
        
            transform.position = position;
            transform.LookAt(target.position + Vector3.up * heightOffset);
        }

        private void TryResolveTarget()
        {
            if (target != null) return;

            if (!string.IsNullOrEmpty(targetTag))
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(targetTag);
                if (tagged != null)
                {
                    target = tagged.transform;
                }
            }

            if (target == null)
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        private void TryResolveInput()
        {
            if (playerInput == null)
            {
                if (target != null)
                {
                    playerInput = target.GetComponent<PlayerInput>();
                }

                if (playerInput == null)
                {
                    playerInput = FindAnyObjectByType<PlayerInput>();
                }
            }

            if (playerInput != null && playerInput.actions != null)
            {
                _lookAction = playerInput.actions.FindAction(lookActionName, false);
                _zoomAction = playerInput.actions.FindAction(zoomActionName, false);
                EnableActions();
            }
            else
            {
                _lookAction = null;
                _zoomAction = null;
            }
        }

        private void EnableActions()
        {
            _lookAction?.Enable();
            _zoomAction?.Enable();
        }
    }
}
