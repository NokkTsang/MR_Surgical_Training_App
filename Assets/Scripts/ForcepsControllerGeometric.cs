using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls forceps behavior including animation, trigger detection, and interaction with objects and Obi ropes.
/// Supports both Unity collider-based objects (balls) and Obi rope particle-based detection.
/// </summary>

public class ForcepsControllerGeometric : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference _gripAction;

    [Header("Forceps Parts")]
    [SerializeField] private Transform _upperClamp;
    [SerializeField] private Transform _lowerClamp;

    [Header("Animation")]
    [SerializeField, Range(0.1f, 2.0f)] private float _animationDuration = 0.3f;
    [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Object Radii by Tag")]
    [Tooltip("Set the radius for each tag that can be clamped. If tag not found, uses default.")]
    public List<TagRadius> TagRadii = new List<TagRadius> { new TagRadius("Default", 0.01f) };
    [Tooltip("Default radius if tag not found.")]
    public float DefaultRadius = 0.01f;

    [Header("Interaction Settings")]
    [SerializeField]
    [Tooltip("Objects with these tags can be grabbed by the forceps")]
    private List<string> _interactableTags = new List<string> { "GrabbableSphere", "Rope" };

    [SerializeField]
    [Tooltip("Enable debug logs for interaction events")]
    private bool _showTagDebugInfo = true;

    [Header("Rope Detection")]
    [SerializeField]
    [Tooltip("Enable Obi rope particle detection")]
    private bool _enableRopeDetection = true;

    [Header("Attach Points (Geometric)")]
    [SerializeField] private List<Transform> AttachPoints; // inner,middle,outer 

    private bool _isGripPressed = false;
    private bool _isAnimating = false;
    private Quaternion _upperClampDefaultRot;
    private Quaternion _lowerClampDefaultRot;
    private Coroutine _currentAnimation;

    private ObiRopeInteractable _ropeInteractable;
    private string _currentObjectTag = "Default";
    private GameObject _currentObject = null;

    [Serializable]
    public class TagRadius
    {
        public string Tag;
        public float Radius;
        public TagRadius(string tag, float radius) { Tag = tag; Radius = radius; }
    }

    private void Start()
    {
        _upperClampDefaultRot = _upperClamp.localRotation;
        _lowerClampDefaultRot = _lowerClamp.localRotation;
        _gripAction.action.performed += OnGripPressed;
        _gripAction.action.canceled += OnGripReleased;
    }

    private void OnDestroy()
    {
        if (_gripAction != null)
        {
            _gripAction.action.performed -= OnGripPressed;
            _gripAction.action.canceled -= OnGripReleased;
        }
        if (_currentAnimation != null)
            StopCoroutine(_currentAnimation);
    }

    // Call this externally when a new object is detected for clamping
    public void SetClampedObjectTag(string tag)
    {
        _currentObjectTag = tag;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_interactableTags.Contains(other.tag)) return;
        _currentObjectTag = other.tag;
        _currentObject = other.gameObject;
        if (_showTagDebugInfo) Debug.Log($"Trigger enter: {other.name} ({other.tag})");
        // Rope detection
        var rope = other.GetComponent<ObiRopeInteractable>();
        if (_enableRopeDetection && rope != null)
        {
            _ropeInteractable = rope;
            // Request rope to use red for selected particles (if it supports such messages)
            _ropeInteractable.SendMessage("EnableAttachmentColoring", true, SendMessageOptions.DontRequireReceiver);
            _ropeInteractable.SendMessage("SetAttachedParticleColor", Color.red, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_currentObject == other.gameObject)
        {
            _currentObject = null;
            _currentObjectTag = "Default";
            if (_showTagDebugInfo) Debug.Log($"Trigger exit: {other.name} ({other.tag})");
            if (_enableRopeDetection && other.GetComponent<ObiRopeInteractable>() != null)
                _ropeInteractable = null;
        }
    }

    private void OnGripPressed(InputAction.CallbackContext context)
    {
        _isGripPressed = true;
        if (_ropeInteractable != null)
        {
            // Attach using public API for geometric controller
            _ropeInteractable.AttachToForceps(this);
            // Optional: set coloring if available
            _ropeInteractable.EnableAttachmentColoring = true;
            _ropeInteractable.AttachedParticleColor = Color.red;
            if (_showTagDebugInfo) Debug.Log("Forceps attached to rope (Geometric API) with red selection color");
        }
        StartSmoothAnimation(true);
    }

    private void OnGripReleased(InputAction.CallbackContext context)
    {
        _isGripPressed = false;
        if (_ropeInteractable != null)
        {
            _ropeInteractable.DetachFromForceps(this);
            if (_showTagDebugInfo) Debug.Log("Forceps detached from rope (Geometric API)");
        }
        StartSmoothAnimation(false);
    }

    private void StartSmoothAnimation(bool closing)
    {
        if (_currentAnimation != null)
            StopCoroutine(_currentAnimation);
        _currentAnimation = StartCoroutine(AnimateForceps(closing));
    }

    private IEnumerator AnimateForceps(bool closing)
    {
        _isAnimating = true;
        Quaternion upperStart = _upperClamp.localRotation;
        Quaternion lowerStart = _lowerClamp.localRotation;
        Quaternion upperTarget, lowerTarget;

        if (closing)
        {
            // Always use the fixed closed target rotations for both clamps
            upperTarget = Quaternion.Euler(-90f, -90f, 90f);
            lowerTarget = Quaternion.Euler(-90f, 90f, -90f);
        }
        else
        {
            upperTarget = _upperClampDefaultRot;
            lowerTarget = _lowerClampDefaultRot;
        }

        float elapsedTime = 0f;
        while (elapsedTime < _animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = _animationCurve.Evaluate(elapsedTime / _animationDuration);
            _upperClamp.localRotation = Quaternion.Slerp(upperStart, upperTarget, progress);
            _lowerClamp.localRotation = Quaternion.Slerp(lowerStart, lowerTarget, progress);
            yield return null;
        }
        _upperClamp.localRotation = upperTarget;
        _lowerClamp.localRotation = lowerTarget;
        _isAnimating = false;
        _currentAnimation = null;
    }

    // Geometric calculation: given attach point and object radius, compute clamp close angle relative to the normal (middle) line
    private float CalculateClampCloseAngleFromAttachPoint(Transform attachPoint, float objectRadius)
    {
        if (attachPoint == null) return 30f; // fallback
        // The normal (middle) line is the vector between the two clamp pivots
        Vector3 middle = (_upperClamp.position + _lowerClamp.position) * 0.5f;
        Vector3 clampLine = (_upperClamp.position - _lowerClamp.position).normalized;
        Vector3 attachDir = (attachPoint.position - middle).normalized;
        // The angle between the attach direction and the clamp line is the reference
        float angle = Vector3.Angle(attachDir, clampLine);
        // Now, use the object radius to further adjust the angle if needed (optional, or just use this angle)
        // Clamp to reasonable range
        return Mathf.Clamp(angle, 5f, 90f);
    }

    private float GetRadiusForTag(string tag)
    {
        foreach (var entry in TagRadii)
        {
            if (entry.Tag == tag)
                return entry.Radius;
        }
        return DefaultRadius;
    }

}