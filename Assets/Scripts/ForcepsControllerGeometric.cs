using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private bool _isGripPressed = false;
    private bool _isAnimating = false;
    private Quaternion _upperClampDefaultRot;
    private Quaternion _lowerClampDefaultRot;
    private Coroutine _currentAnimation;
    private string _currentObjectTag = "Default";

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

    private void OnGripPressed(InputAction.CallbackContext context)
    {
        _isGripPressed = true;
        StartSmoothAnimation(true);
    }

    private void OnGripReleased(InputAction.CallbackContext context)
    {
        _isGripPressed = false;
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
            float radius = GetRadiusForTag(_currentObjectTag);
            float closeAngle = CalculateClampCloseAngle(radius);
            upperTarget = Quaternion.Euler(-closeAngle, -90f, 90f);
            lowerTarget = Quaternion.Euler(-closeAngle, 90f, -90f);
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

    // Geometric calculation: given two clamp positions and object radius, compute clamp close angle
    private float CalculateClampCloseAngle(float objectRadius)
    {
        Vector3 p1 = _upperClamp.position;
        Vector3 p2 = _lowerClamp.position;
        float d = Vector3.Distance(p1, p2);
        float r = objectRadius;
        float halfD = d / 2f;
        float angleRad = 2f * Mathf.Asin(Mathf.Clamp(r / halfD, -1f, 1f));
        float angleDeg = Mathf.Rad2Deg * angleRad;
        return Mathf.Clamp(angleDeg, 10f, 90f);
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

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        float radius = GetRadiusForTag(_currentObjectTag);
        Gizmos.color = Color.blue;
        if (_upperClamp != null) Gizmos.DrawWireSphere(_upperClamp.position, radius);
        if (_lowerClamp != null) Gizmos.DrawWireSphere(_lowerClamp.position, radius);
    }
#endif
}