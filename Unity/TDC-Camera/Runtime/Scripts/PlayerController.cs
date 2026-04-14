using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

namespace Nymphs_TDC.Scripts
{
    /// <summary>
    /// Professional Nymphs Top Down Camera Player Controller.
    /// Handles camera-relative movement, smooth input interpolation, sprinting, and state-based animation.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Base movement speed in units per second.")]
        [SerializeField] private float moveSpeed = 6f;

        [Tooltip("Camera transform used for camera-relative movement. Assign your top-down camera here. If empty, uses Camera.main.")]
        [SerializeField] private Transform cameraTransform;
    
        [Tooltip("Multiplier applied to moveSpeed when sprinting.")]
        [SerializeField] private float sprintMultiplier = 1.8f;
    
        [Tooltip("How fast the character rotates to face the movement direction.")]
        [SerializeField] private float rotationSpeed = 15f;
    
        [Tooltip("Gravity force applied to the character when airborne.")]
        [SerializeField] private float gravity = 20f;

        [Header("Input Smoothing")]
        [Tooltip("How long it takes for input to reach the target value. Lower = snappier, higher = smoother.")]
        [SerializeField] private float inputSmoothTime = 0.15f;

        [Header("Mouse Follow")]
        [Tooltip("If true, mouse-to-move is enabled.")]
        [SerializeField] private bool enableMouseFollow = true;

        [Tooltip("How close to the cursor before movement stops.")]
        [SerializeField] private float mouseFollowStopDistance = 0.6f;

        [Tooltip("Distance from cursor at which sprinting starts.")]
        [SerializeField] private float mouseFollowRunDistance = 6f;

        [Tooltip("Max ray distance for mouse-to-world projection.")]
        [SerializeField] private float mouseFollowMaxRayDistance = 200f;

        [Tooltip("Layers considered valid for mouse-to-world projection. Ensure your ground has a collider on a valid layer.")]
        [SerializeField] private LayerMask mouseFollowGroundMask = ~0;

        [InspectorName("Click Max Hold Time")]
        [Tooltip("Max time (seconds) a press can be held and still count as a click-to-move.")]
        [SerializeField] private float clickToMoveMaxHoldTime = 0.2f;

        [InspectorName("Indicator Prefab")]
        [Tooltip("Optional prefab to spawn at the click point (e.g., ring or flash).")]
        [SerializeField] private GameObject clickIndicatorPrefab;

        [InspectorName("Indicator Duration")]
        [Tooltip("Seconds before the click indicator is destroyed.")]
        [SerializeField] private float clickIndicatorDuration = 0.6f;

        [InspectorName("Indicator Offset")]
        [Tooltip("Offset applied to the click indicator position.")]
        [SerializeField] private Vector3 clickIndicatorOffset = new Vector3(0f, 0.02f, 0f);

        [InspectorName("Pulse Until Arrived")]
        [Tooltip("If true, the click indicator stays and can pulse until the player reaches the destination.")]
        [SerializeField] private bool clickIndicatorPulseUntilArrived = true;

        [InspectorName("Pulse Speed")]
        [Tooltip("Pulse speed for the built-in indicator (cycles per second). Works with or without Pulse Until Arrived.")]
        [SerializeField] private float clickIndicatorPulseSpeed = 2f;

        [InspectorName("Pulse Repeat Count")]
        [Tooltip("Pulse repeat count. 0 = infinite while active.")]
        [SerializeField] private int clickIndicatorPulseRepeatCount = 0;

        [InspectorName("Use Built-in Indicator")]
        [Tooltip("If no prefab is set, spawn a simple built-in indicator.")]
        [SerializeField] private bool useRuntimeClickIndicatorFallback = true;

        [InspectorName("Ring Color")]
        [Tooltip("Color used for the built-in indicator.")]
        [SerializeField] private Color runtimeClickIndicatorColor = new Color(0.2f, 0.8f, 1f, 0.9f);

        [InspectorName("Ring Segments")]
        [Tooltip("Segments used to draw the built-in ring. Higher = smoother. Ignored if a prefab is assigned.")]
        [SerializeField] private int runtimeClickIndicatorSegments = 48;

        [InspectorName("Ring Width")]
        [Tooltip("Line width for the built-in ring.")]
        [SerializeField] private float runtimeClickIndicatorLineWidth = 0.06f;

        [InspectorName("Ring Start Scale")]
        [Tooltip("Start scale for the built-in indicator.")]
        [SerializeField] private float runtimeClickIndicatorStartScale = 0.4f;

        [InspectorName("Ring End Scale")]
        [Tooltip("End scale for the built-in indicator.")]
        [SerializeField] private float runtimeClickIndicatorEndScale = 1.2f;

        [Header("NavMesh (Optional)")]
        [Tooltip("If true and a NavMeshAgent is available, click-to-move will use NavMesh pathfinding.")]
        [SerializeField] private bool useNavMeshForMouseFollow = false;

        [Tooltip("Optional NavMeshAgent reference. Required when using NavMesh click-to-move.")]
        [SerializeField] private NavMeshAgent navMeshAgent;

        [Header("Animation Settings")]
        [Tooltip("The name of the Idle state in your Animator Controller.")]
        [SerializeField] private string idleStateName = "IdleBreathe";
    
        [Tooltip("The name of the Walk state in your Animator Controller.")]
        [SerializeField] private string walkStateName = "Walk";

        [Tooltip("The name of the Run state in your Animator Controller (used when sprinting).")]
        [SerializeField] private string runStateName = "Walk";
    
        [Tooltip("The duration of the crossfade between animation states.")]
        [SerializeField] private float animationCrossfade = 0.1f;

        [Tooltip("The base speed of the animation. Sprinting scales from this value.")]
        [SerializeField] private float baseAnimSpeed = 1.0f;

        private CharacterController _controller;
        private Animator _animator;
        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private Camera _camera;
    
        private Vector3 _moveDirection;
        private string _currentAnimState;
        private Vector2 _smoothedInput;
        private Vector2 _inputVelocity;
        private bool _isSprinting;
        private bool _mouseMoveActive;
        private Vector3 _mouseMoveDestination;
        private bool _useNavMeshMove;
        private bool _mouseMoveFollowCursor;
        private float _mousePressTime;
        private GameObject _activeClickIndicator;
        private bool _shiftPressedOnClick;
        private struct QueuedMovePoint
        {
            public Vector3 Point;
            public GameObject Indicator;

            public QueuedMovePoint(Vector3 point, GameObject indicator)
            {
                Point = point;
                Indicator = indicator;
            }
        }

        private readonly Queue<QueuedMovePoint> _mouseMoveQueue = new Queue<QueuedMovePoint>();

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _playerInput = GetComponent<PlayerInput>();
            if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
        
            if (_playerInput != null && _playerInput.actions != null)
            {
                _moveAction = _playerInput.actions.FindAction("Move");
                _sprintAction = _playerInput.actions.FindAction("Sprint");
            }
        
            if (_animator != null) _animator.applyRootMotion = false;

            if (_moveAction == null)
            {
                Debug.LogWarning("PlayerController could not find a 'Move' action on PlayerInput.", this);
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                _camera = cameraTransform.GetComponent<Camera>();
                if (_camera == null && Camera.main != null)
                {
                    _camera = Camera.main;
                }
            }
        }

        private void Update()
        {
            HandleInput();
            HandleMovement();
            HandleRotation();
            HandleAnimations();
            UpdateAnimationSpeed();
        }

        private void HandleInput()
        {
            Vector2 targetInput = Vector2.zero;
            bool sprintPressed = false;
            _useNavMeshMove = false;
            if (!enableMouseFollow) _mouseMoveActive = false;

            if (enableMouseFollow && Mouse.current != null)
            {
                bool leftHeld = Mouse.current.leftButton.isPressed;
                bool leftClicked = Mouse.current.leftButton.wasPressedThisFrame;
                bool leftReleased = Mouse.current.leftButton.wasReleasedThisFrame;
                bool shiftPressed = Keyboard.current != null &&
                                    (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

                if (leftClicked)
                {
                    _mousePressTime = Time.time;
                    _shiftPressedOnClick = shiftPressed;
                }

                if (!shiftPressed && leftHeld && TryGetMouseWorldPoint(out Vector3 holdPoint))
                {
                    _mouseMoveDestination = holdPoint;
                    _mouseMoveActive = true;
                    _mouseMoveFollowCursor = true;
                    ClearQueuedIndicators();
                    ClearClickIndicator();
                }

                if (!leftHeld && leftReleased && Time.time - _mousePressTime <= clickToMoveMaxHoldTime)
                {
                    if (TryGetMouseWorldPoint(out Vector3 clickPoint))
                    {
                        bool shiftClick = _shiftPressedOnClick;
                        if (shiftClick && _mouseMoveActive && !_mouseMoveFollowCursor)
                        {
                            GameObject queuedIndicator = SpawnClickIndicator(
                                clickPoint,
                                clickIndicatorPulseUntilArrived,
                                !clickIndicatorPulseUntilArrived
                            );
                            _mouseMoveQueue.Enqueue(new QueuedMovePoint(clickPoint, queuedIndicator));
                        }
                        else
                        {
                            ClearQueuedIndicators();
                            _mouseMoveDestination = clickPoint;
                            _mouseMoveActive = true;
                            _mouseMoveFollowCursor = false;
                            _activeClickIndicator = SpawnClickIndicator(clickPoint, clickIndicatorPulseUntilArrived, !clickIndicatorPulseUntilArrived);
                        }
                    }
                    else
                    {
                        _mouseMoveActive = false;
                    }
                }
                else if (!leftHeld && _mouseMoveFollowCursor)
                {
                    _mouseMoveActive = false;
                }

                if (_mouseMoveActive)
                {
                    Vector3 toTarget = _mouseMoveDestination - transform.position;
                    toTarget.y = 0f;
                    float distanceToTarget = toTarget.magnitude;

                    if (distanceToTarget <= mouseFollowStopDistance)
                    {
                        if (!_mouseMoveFollowCursor && _mouseMoveQueue.Count > 0)
                        {
                            QueuedMovePoint next = _mouseMoveQueue.Dequeue();
                            _mouseMoveDestination = next.Point;
                            _mouseMoveActive = true;
                            if (_activeClickIndicator != null && _activeClickIndicator != next.Indicator)
                            {
                                Destroy(_activeClickIndicator);
                            }
                            _activeClickIndicator = next.Indicator;
                            if (_activeClickIndicator == null && clickIndicatorPulseUntilArrived)
                            {
                                _activeClickIndicator = SpawnClickIndicator(_mouseMoveDestination, clickIndicatorPulseUntilArrived, !clickIndicatorPulseUntilArrived);
                            }
                        }
                        else
                        {
                            _mouseMoveActive = false;
                            if (!_mouseMoveFollowCursor) ClearClickIndicator();
                        }
                    }
                    else
                    {
                        if (useNavMeshForMouseFollow && navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                        {
                            _useNavMeshMove = true;
                        }
                        else
                        {
                            if (cameraTransform == null && Camera.main != null)
                            {
                                cameraTransform = Camera.main.transform;
                            }

                            if (cameraTransform == null)
                            {
                                targetInput = Vector2.zero;
                            }
                            else
                            {
                            Vector3 forward = cameraTransform.forward;
                            forward.y = 0f;
                            forward.Normalize();

                            Vector3 right = cameraTransform.right;
                            right.y = 0f;
                            right.Normalize();

                            Vector3 desiredDir = toTarget.normalized;
                            float x = Vector3.Dot(desiredDir, right);
                            float y = Vector3.Dot(desiredDir, forward);
                            targetInput = new Vector2(x, y);
                            if (targetInput.sqrMagnitude > 1f) targetInput.Normalize();
                            }
                        }

                        if (distanceToTarget >= mouseFollowRunDistance)
                        {
                            sprintPressed = true;
                        }
                    }
                }
            }

            if (!_mouseMoveActive)
            {
                if (_moveAction != null) targetInput = _moveAction.ReadValue<Vector2>();
                if (_sprintAction != null) sprintPressed = _sprintAction.IsPressed();
                if (!_mouseMoveFollowCursor) ClearClickIndicator();
            }

            _isSprinting = sprintPressed;
            if (targetInput.sqrMagnitude > 1f) targetInput.Normalize();
            _smoothedInput = Vector2.SmoothDamp(_smoothedInput, targetInput, ref _inputVelocity, inputSmoothTime);
        }

        private void HandleMovement()
        {
            if (_useNavMeshMove && useNavMeshForMouseFollow && navMeshAgent != null)
            {
                if (_controller != null && _controller.enabled) _controller.enabled = false;
                navMeshAgent.isStopped = false;
                navMeshAgent.updateRotation = true;
                navMeshAgent.updatePosition = true;
                navMeshAgent.speed = moveSpeed * (_isSprinting ? sprintMultiplier : 1f);
                navMeshAgent.SetDestination(_mouseMoveDestination);
                return;
            }

            if (useNavMeshForMouseFollow && navMeshAgent != null)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
                if (_controller != null && !_controller.enabled) _controller.enabled = true;
            }

            if (cameraTransform == null)
            {
                if (Camera.main != null)
                {
                    cameraTransform = Camera.main.transform;
                }
            }

            if (cameraTransform == null)
            {
                return;
            }

            Vector3 forward = cameraTransform.forward;
            forward.y = 0;
            forward.Normalize();
        
            Vector3 right = cameraTransform.right;
            right.y = 0;
            right.Normalize();

            float currentSpeed = moveSpeed * (_isSprinting ? sprintMultiplier : 1f);
            Vector3 horizontalMove = (forward * _smoothedInput.y + right * _smoothedInput.x) * currentSpeed;

            if (_controller.isGrounded)
            {
                _moveDirection = horizontalMove;
                _moveDirection.y = -2f;
            }
            else
            {
                _moveDirection.x = horizontalMove.x;
                _moveDirection.z = horizontalMove.z;
                _moveDirection.y -= gravity * Time.deltaTime;
            }

            _controller.Move(_moveDirection * Time.deltaTime);
        }

        private void HandleRotation()
        {
            if (_useNavMeshMove && useNavMeshForMouseFollow && navMeshAgent != null)
            {
                // Let NavMeshAgent handle rotation when using click-to-move.
                return;
            }

            if (_smoothedInput.sqrMagnitude > 0.01f)
            {
                Vector3 lookDir = new Vector3(_moveDirection.x, 0, _moveDirection.z);
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * 100f * Time.deltaTime);
                }
            }
        }

        private void HandleAnimations()
        {
            if (_animator == null) return;

            if (_smoothedInput.sqrMagnitude > 0.05f)
            {
                ChangeAnimationState(_isSprinting ? runStateName : walkStateName);
            }
            else
            {
                ChangeAnimationState(idleStateName);
            }
        }

        private void ChangeAnimationState(string newState)
        {
            if (_currentAnimState == newState) return;
            _animator.CrossFade(newState, animationCrossfade);
            _currentAnimState = newState;
        }

        private void UpdateAnimationSpeed()
        {
            if (_animator == null) return;
            float targetAnimSpeed = _isSprinting ? baseAnimSpeed * sprintMultiplier : baseAnimSpeed;
            _animator.speed = Mathf.Lerp(_animator.speed, targetAnimSpeed, Time.deltaTime * 5f);
        }

        private bool TryGetMouseWorldPoint(out Vector3 point)
        {
            point = default;
            if (Mouse.current == null) return false;

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (_camera == null && cameraTransform != null)
            {
                _camera = cameraTransform.GetComponent<Camera>();
                if (_camera == null && Camera.main != null)
                {
                    _camera = Camera.main;
                }
            }

            if (_camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, mouseFollowMaxRayDistance, mouseFollowGroundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            return false;
        }

        private GameObject SpawnClickIndicator(Vector3 worldPoint, bool pulseUntilArrived, bool autoDestroy)
        {
            if (clickIndicatorPrefab != null)
            {
                GameObject instance = Instantiate(clickIndicatorPrefab, worldPoint + clickIndicatorOffset, Quaternion.identity);

                if (pulseUntilArrived)
                {
                    ClickIndicatorRuntime indicatorRuntimePrefab = instance.GetComponent<ClickIndicatorRuntime>();
                    if (indicatorRuntimePrefab != null)
                    {
                        indicatorRuntimePrefab.Initialize(instance.GetComponent<LineRenderer>(), runtimeClickIndicatorColor, 0f, runtimeClickIndicatorStartScale, runtimeClickIndicatorEndScale, true, clickIndicatorPulseSpeed);
                        indicatorRuntimePrefab.SetPulseRepeatCount(clickIndicatorPulseRepeatCount);
                    }
                }
                else if (autoDestroy && clickIndicatorDuration > 0f)
                {
                    Destroy(instance, clickIndicatorDuration);
                }
                return instance;
            }

            if (!useRuntimeClickIndicatorFallback) return null;

            GameObject indicator = new GameObject("ClickIndicatorRuntime");
            indicator.name = "ClickIndicatorRuntime";
            indicator.transform.position = worldPoint + clickIndicatorOffset;
            indicator.transform.rotation = Quaternion.identity;

            LineRenderer line = indicator.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            int segments = Mathf.Clamp(runtimeClickIndicatorSegments, 3, 256);
            line.positionCount = segments;
            line.widthMultiplier = Mathf.Max(0.001f, runtimeClickIndicatorLineWidth);
            line.numCapVertices = 4;

            float radius = 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                line.SetPosition(i, new Vector3(x, 0f, z));
            }

            ClickIndicatorRuntime indicatorRuntimeFallback = indicator.AddComponent<ClickIndicatorRuntime>();
            indicatorRuntimeFallback.Initialize(line, runtimeClickIndicatorColor, autoDestroy ? clickIndicatorDuration : 0f, runtimeClickIndicatorStartScale, runtimeClickIndicatorEndScale, pulseUntilArrived, clickIndicatorPulseSpeed);
            indicatorRuntimeFallback.SetPulseRepeatCount(clickIndicatorPulseRepeatCount);
            return indicator;
        }

        private void ClearClickIndicator()
        {
            if (_activeClickIndicator == null) return;
            Destroy(_activeClickIndicator);
            _activeClickIndicator = null;
        }

        private void ClearQueuedIndicators()
        {
            while (_mouseMoveQueue.Count > 0)
            {
                QueuedMovePoint qp = _mouseMoveQueue.Dequeue();
                if (qp.Indicator != null)
                {
                    Destroy(qp.Indicator);
                }
            }
        }
    }
}
