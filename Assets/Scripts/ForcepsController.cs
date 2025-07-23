using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ForcepsController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField]
    private InputActionReference _gripAction;

    [Header("Forceps Parts")]
    [SerializeField]
    private Transform _upperClamp;
    [SerializeField]
    private Transform _lowerClamp;

    [Header("Animation Settings")]
    [SerializeField]
    [Range(0.1f, 2.0f)]
    [Tooltip("Duration of the open/close animation in seconds")]
    private float _animationDuration = 0.3f;

    [SerializeField]
    [Tooltip("Animation curve for easing the transition")]
    private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool _isGripPressed = false;
    private bool _isAnimating = false;

    private Quaternion _upperClampDefaultRot;
    private Quaternion _lowerClampDefaultRot;

    private Coroutine _currentAnimation;

    private bool _isObjectInUpperTrigger = false;
    private bool _isObjectInLowerTrigger = false;

    public void OnUpperTriggerEnter(GameObject other)
    {
        // Handle upper clamp trigger enter logic
        Debug.Log($"Upper clamp triggered by: {other.name}");
        _isObjectInUpperTrigger = true;
    }

    public void OnUpperTriggerExit(GameObject other)
    {
        // Handle upper clamp trigger exit logic
        Debug.Log($"Upper clamp exited by: {other.name}");
        _isObjectInUpperTrigger = false;
    }

    public void OnLowerTriggerEnter(GameObject other)
    {
        // Handle lower clamp trigger enter logic
        Debug.Log($"Lower clamp triggered by: {other.name}");
        _isObjectInLowerTrigger = true;
    }

    public void OnLowerTriggerExit(GameObject other)
    {
        // Handle lower clamp trigger exit logic
        Debug.Log($"Lower clamp exited by: {other.name}");
        _isObjectInLowerTrigger = false;
    }

    void Start()
    {
        if (_upperClamp == null || _lowerClamp == null)
        {
            Debug.LogError("Forceps clamps not assigned!");
            return;
        }

        if (_gripAction == null)
        {
            Debug.LogError("Grip Action not assigned!");
            return;
        }

        // bind the grip action to the methods
        _gripAction.action.performed += OnGripPressed;
        _gripAction.action.canceled += OnGripReleased;

        // 1) upper opened: (-45, -90, 90)
        _upperClampDefaultRot = Quaternion.Euler(-45f, -90f, 90f);

        // 2) lower opened: (-45, 90, -90)
        _lowerClampDefaultRot = Quaternion.Euler(-45f, 90f, -90f);

        _upperClamp.localRotation = _upperClampDefaultRot;
        _lowerClamp.localRotation = _lowerClampDefaultRot;

        Debug.Log("ForcepsController initialized. Upper/Lower clamps set to default angles.");
    }

    private void OnGripPressed(InputAction.CallbackContext context)
    {
        _isGripPressed = true;
        StartSmoothAnimation(true);
        Debug.Log("Grip pressed - Forceps closing smoothly.");
    }

    private void OnGripReleased(InputAction.CallbackContext context)
    {
        _isGripPressed = false;
        StartSmoothAnimation(false);
        Debug.Log("Grip released - Forceps opening smoothly.");
    }

    private void StartSmoothAnimation(bool closing)
    {
        // Stop any existing animation
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }

        // Start new animation
        _currentAnimation = StartCoroutine(AnimateForceps(closing));
    }

    private IEnumerator AnimateForceps(bool closing)
    {
        _isAnimating = true;

        // Determine start rotations for clamps only
        Quaternion upperClampStartRot = _upperClamp.localRotation;
        Quaternion lowerClampStartRot = _lowerClamp.localRotation;

        if (closing)
        {
            // Define closed positions for clamps
            Quaternion upperClampTargetRot = Quaternion.Euler(-90f, -90f, 90f);  //close entirely
            Quaternion lowerClampTargetRot = Quaternion.Euler(-90f, 90f, -90f);  //close entirely

            float elapsedTime = 0f;

            // For closing animation, continue until object is detected OR animation duration is reached
            while (!(_isObjectInUpperTrigger || _isObjectInLowerTrigger) && elapsedTime < _animationDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / _animationDuration;

                // Apply animation curve for easing
                float curveValue = _animationCurve.Evaluate(normalizedTime);

                // Interpolate clamp rotations toward closed position
                _upperClamp.localRotation = Quaternion.Slerp(upperClampStartRot, upperClampTargetRot, curveValue);
                _lowerClamp.localRotation = Quaternion.Slerp(lowerClampStartRot, lowerClampTargetRot, curveValue);

                yield return null;
            }

            if (_isObjectInUpperTrigger || _isObjectInLowerTrigger)
            {
                Debug.Log("Object detected in trigger - Forceps stopped closing");
            }
            else
            {
                Debug.Log("Forceps closing animation completed by duration");
            }
        }
        else
        {
            // For opening animation, return to default positions
            Quaternion upperClampEndRot = _upperClampDefaultRot;
            Quaternion lowerClampEndRot = _lowerClampDefaultRot;

            float elapsedTime = 0f;

            while (elapsedTime < _animationDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / _animationDuration;

                // Apply animation curve for easing
                float curveValue = _animationCurve.Evaluate(normalizedTime);

                // Interpolate rotations back to default (open) positions
                _upperClamp.localRotation = Quaternion.Slerp(upperClampStartRot, upperClampEndRot, curveValue);
                _lowerClamp.localRotation = Quaternion.Slerp(lowerClampStartRot, lowerClampEndRot, curveValue);

                yield return null;
            }

            // Ensure final positions are exact
            _upperClamp.localRotation = upperClampEndRot;
            _lowerClamp.localRotation = lowerClampEndRot;

            Debug.Log("Forceps opened to default position");
        }

        _isAnimating = false;
        _currentAnimation = null;
    }

    public bool IsGripPressed => _isGripPressed;
    public bool IsAnimating => _isAnimating;

    void OnDestroy()
    {
        if (_gripAction != null)
        {
            _gripAction.action.performed -= OnGripPressed;
            _gripAction.action.canceled -= OnGripReleased;
        }

        // Clean up any running animation
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }
    }
}
