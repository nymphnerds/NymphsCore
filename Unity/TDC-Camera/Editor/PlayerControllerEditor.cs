using UnityEditor;
using UnityEngine;
using Nymphs_TDC.Scripts;

namespace Nymphs_TDC.Scripts.Editor
{
    [CustomEditor(typeof(PlayerController))]
    public sealed class PlayerControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _moveSpeed;
        private SerializedProperty _cameraTransform;
        private SerializedProperty _sprintMultiplier;
        private SerializedProperty _rotationSpeed;
        private SerializedProperty _gravity;

        private SerializedProperty _inputSmoothTime;

        private SerializedProperty _enableMouseFollow;
        private SerializedProperty _mouseFollowStopDistance;
        private SerializedProperty _mouseFollowRunDistance;
        private SerializedProperty _mouseFollowMaxRayDistance;
        private SerializedProperty _mouseFollowGroundMask;
        private SerializedProperty _clickToMoveMaxHoldTime;

        private SerializedProperty _clickIndicatorPrefab;
        private SerializedProperty _clickIndicatorDuration;
        private SerializedProperty _clickIndicatorOffset;
        private SerializedProperty _clickIndicatorPulseUntilArrived;
        private SerializedProperty _clickIndicatorPulseSpeed;
        private SerializedProperty _clickIndicatorPulseRepeatCount;
        private SerializedProperty _useRuntimeClickIndicatorFallback;
        private SerializedProperty _runtimeClickIndicatorColor;
        private SerializedProperty _runtimeClickIndicatorSegments;
        private SerializedProperty _runtimeClickIndicatorLineWidth;
        private SerializedProperty _runtimeClickIndicatorStartScale;
        private SerializedProperty _runtimeClickIndicatorEndScale;

        private SerializedProperty _useNavMeshForMouseFollow;
        private SerializedProperty _navMeshAgent;

        private SerializedProperty _idleStateName;
        private SerializedProperty _walkStateName;
        private SerializedProperty _runStateName;
        private SerializedProperty _animationCrossfade;
        private SerializedProperty _baseAnimSpeed;

        private static bool _showClickIndicatorSettings = true;

        private void OnEnable()
        {
            _moveSpeed = serializedObject.FindProperty("moveSpeed");
            _cameraTransform = serializedObject.FindProperty("cameraTransform");
            _sprintMultiplier = serializedObject.FindProperty("sprintMultiplier");
            _rotationSpeed = serializedObject.FindProperty("rotationSpeed");
            _gravity = serializedObject.FindProperty("gravity");

            _inputSmoothTime = serializedObject.FindProperty("inputSmoothTime");

            _enableMouseFollow = serializedObject.FindProperty("enableMouseFollow");
            _mouseFollowStopDistance = serializedObject.FindProperty("mouseFollowStopDistance");
            _mouseFollowRunDistance = serializedObject.FindProperty("mouseFollowRunDistance");
            _mouseFollowMaxRayDistance = serializedObject.FindProperty("mouseFollowMaxRayDistance");
            _mouseFollowGroundMask = serializedObject.FindProperty("mouseFollowGroundMask");
            _clickToMoveMaxHoldTime = serializedObject.FindProperty("clickToMoveMaxHoldTime");

            _clickIndicatorPrefab = serializedObject.FindProperty("clickIndicatorPrefab");
            _clickIndicatorDuration = serializedObject.FindProperty("clickIndicatorDuration");
            _clickIndicatorOffset = serializedObject.FindProperty("clickIndicatorOffset");
            _clickIndicatorPulseUntilArrived = serializedObject.FindProperty("clickIndicatorPulseUntilArrived");
            _clickIndicatorPulseSpeed = serializedObject.FindProperty("clickIndicatorPulseSpeed");
            _clickIndicatorPulseRepeatCount = serializedObject.FindProperty("clickIndicatorPulseRepeatCount");
            _useRuntimeClickIndicatorFallback = serializedObject.FindProperty("useRuntimeClickIndicatorFallback");
            _runtimeClickIndicatorColor = serializedObject.FindProperty("runtimeClickIndicatorColor");
            _runtimeClickIndicatorSegments = serializedObject.FindProperty("runtimeClickIndicatorSegments");
            _runtimeClickIndicatorLineWidth = serializedObject.FindProperty("runtimeClickIndicatorLineWidth");
            _runtimeClickIndicatorStartScale = serializedObject.FindProperty("runtimeClickIndicatorStartScale");
            _runtimeClickIndicatorEndScale = serializedObject.FindProperty("runtimeClickIndicatorEndScale");

            _useNavMeshForMouseFollow = serializedObject.FindProperty("useNavMeshForMouseFollow");
            _navMeshAgent = serializedObject.FindProperty("navMeshAgent");

            _idleStateName = serializedObject.FindProperty("idleStateName");
            _walkStateName = serializedObject.FindProperty("walkStateName");
            _runStateName = serializedObject.FindProperty("runStateName");
            _animationCrossfade = serializedObject.FindProperty("animationCrossfade");
            _baseAnimSpeed = serializedObject.FindProperty("baseAnimSpeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Movement Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_moveSpeed);
            EditorGUILayout.PropertyField(_cameraTransform);
            EditorGUILayout.PropertyField(_sprintMultiplier);
            EditorGUILayout.PropertyField(_rotationSpeed);
            EditorGUILayout.PropertyField(_gravity);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Input Smoothing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_inputSmoothTime);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mouse Follow", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableMouseFollow);
            EditorGUILayout.PropertyField(_mouseFollowStopDistance);
            EditorGUILayout.PropertyField(_mouseFollowRunDistance);
            EditorGUILayout.PropertyField(_mouseFollowMaxRayDistance);
            EditorGUILayout.PropertyField(_mouseFollowGroundMask);
            EditorGUILayout.PropertyField(_clickToMoveMaxHoldTime);

            EditorGUILayout.Space();
            _showClickIndicatorSettings = EditorGUILayout.Foldout(_showClickIndicatorSettings, "Click Indicator Settings", true);
            if (_showClickIndicatorSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_clickIndicatorPrefab);
                EditorGUILayout.PropertyField(_clickIndicatorDuration);
                EditorGUILayout.PropertyField(_clickIndicatorOffset);
                EditorGUILayout.PropertyField(_clickIndicatorPulseUntilArrived);
                EditorGUILayout.PropertyField(_clickIndicatorPulseSpeed);
                EditorGUILayout.PropertyField(_clickIndicatorPulseRepeatCount);
                EditorGUILayout.PropertyField(_useRuntimeClickIndicatorFallback);
                EditorGUILayout.PropertyField(_runtimeClickIndicatorColor);
                EditorGUILayout.PropertyField(_runtimeClickIndicatorSegments);
                EditorGUILayout.PropertyField(_runtimeClickIndicatorLineWidth);
                EditorGUILayout.PropertyField(_runtimeClickIndicatorStartScale);
                EditorGUILayout.PropertyField(_runtimeClickIndicatorEndScale);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("NavMesh (Optional)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useNavMeshForMouseFollow);
            EditorGUILayout.PropertyField(_navMeshAgent);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_idleStateName);
            EditorGUILayout.PropertyField(_walkStateName);
            EditorGUILayout.PropertyField(_runStateName);
            EditorGUILayout.PropertyField(_animationCrossfade);
            EditorGUILayout.PropertyField(_baseAnimSpeed);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
