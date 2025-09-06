using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls forceps behavior including animation, trigger detection, and interaction with objects and Obi ropes.
/// Supports both Unity collider-based objects (balls) and Obi rope particle-based detection.
/// </summary>
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

    [SerializeField]
    [Tooltip("Rope detection update frequency (Hz)")]
    private float _ropeCheckFrequency = 30f;

    [Header("Animation")]
    [SerializeField]
    [Range(0.1f, 2.0f)]
    [Tooltip("Animation duration in seconds")]
    private float _animationDuration = 0.3f;

    [SerializeField]
    [Tooltip("Animation easing curve")]
    private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Animation state
    private bool _isGripPressed = false;
    private bool _isAnimating = false;
    private Quaternion _upperClampDefaultRot;
    private Quaternion _lowerClampDefaultRot;
    private Coroutine _currentAnimation;

    // Trigger detection states
    private bool _isObjectInUpperTrigger = false;
    private bool _isObjectInLowerTrigger = false;
    private bool _isRopeInUpperTrigger = false;
    private bool _isRopeInLowerTrigger = false;

    // Rope detection components
    private ObiRopeInteractable _ropeInteractable;
    private float _lastRopeCheck = 0f;

    // Properties
    public bool IsGripPressed => _isGripPressed;
    public bool IsAnimating => _isAnimating;
    public List<string> InteractableTags => new List<string>(_interactableTags);

    #region Unity Lifecycle

    void Start()
    {
        InitializeComponent();
    }

    void Update()
    {
        if (_enableRopeDetection)
        {
            UpdateRopeDetection();
        }
    }

    void OnDestroy()
    {
        CleanupComponent();
    }

    #endregion

    #region Initialization & Cleanup

    /// <summary>
    /// Initialize all forceps components and validate configuration
    /// </summary>
    private void InitializeComponent()
    {
        ValidateComponents();

        if (!IsValidConfiguration()) return;

        InitializeRopeDetection();
        InitializeInputActions();
        InitializeClampRotations();
        
        LogInitializationComplete();
    }

    /// <summary>
    /// Check if all required components are properly configured
    /// </summary>
    private bool IsValidConfiguration()
    {
        return _upperClamp != null && _lowerClamp != null && _gripAction != null;
    }

    /// <summary>
    /// Setup rope detection if enabled
    /// </summary>
    private void InitializeRopeDetection()
    {
        if (!_enableRopeDetection) return;

        _ropeInteractable = FindObjectOfType<ObiRopeInteractable>();
        
        if (_ropeInteractable != null)
        {
            Debug.Log("✅ Rope detection enabled");
        }
        else
        {
            Debug.LogWarning("⚠️ ObiRopeInteractable not found - rope detection disabled");
        }
    }

    /// <summary>
    /// Bind input action events
    /// </summary>
    private void InitializeInputActions()
    {
        _gripAction.action.performed += OnGripPressed;
        _gripAction.action.canceled += OnGripReleased;
    }

    /// <summary>
    /// Set default clamp rotations
    /// </summary>
    private void InitializeClampRotations()
    {
        _upperClampDefaultRot = Quaternion.Euler(-45f, -90f, 90f);
        _lowerClampDefaultRot = Quaternion.Euler(-45f, 90f, -90f);
        
        _upperClamp.localRotation = _upperClampDefaultRot;
        _lowerClamp.localRotation = _lowerClampDefaultRot;
    }

    /// <summary>
    /// Log successful initialization
    /// </summary>
    private void LogInitializationComplete()
    {
        Debug.Log("ForcepsController initialized successfully");
        Debug.Log($"Interactable tags: [{string.Join(", ", _interactableTags)}]");
    }

    /// <summary>
    /// Clean up resources on destroy
    /// </summary>
    private void CleanupComponent()
    {
        if (_gripAction != null)
        {
            _gripAction.action.performed -= OnGripPressed;
            _gripAction.action.canceled -= OnGripReleased;
        }

        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }
    }

    #endregion

    #region Object Interaction Logic

    /// <summary>
    /// Check if object can be interacted with based on its tag
    /// </summary>
    private bool IsObjectInteractable(GameObject obj)
    {
        if (obj == null)
        {
            if (_showTagDebugInfo)
                Debug.LogWarning("Null GameObject received for interaction check");
            return false;
        }

        EnsureInteractableTagsInitialized();

        // No tag restrictions means all objects are interactable
        if (_interactableTags.Count == 0)
        {
            if (_showTagDebugInfo)
                Debug.Log($"No tag restrictions - allowing {obj.name}");
            return true;
        }

        bool isInteractable = _interactableTags.Contains(obj.tag);
        
        if (_showTagDebugInfo)
        {
            string status = isInteractable ? "✓" : "✗";
            Debug.Log($"{status} {obj.name} (tag: {obj.tag}) - {(isInteractable ? "interactable" : "not interactable")}");
        }

        return isInteractable;
    }

    /// <summary>
    /// Ensure interactable tags list is properly initialized
    /// </summary>
    private void EnsureInteractableTagsInitialized()
    {
        if (_interactableTags == null)
        {
            _interactableTags = new List<string> { "GrabbableSphere", "Rope" };
            Debug.LogWarning("Interactable tags was null - initialized with defaults");
        }
    }

    #endregion

    #region Unity Trigger Events

    public void OnUpperTriggerEnter(GameObject other)
    {
        if (!IsValidTriggerObject(other, "OnUpperTriggerEnter")) return;
        if (!IsObjectInteractable(other)) return;

        Debug.Log($"🎯 Upper clamp: {other.name} entered");
        _isObjectInUpperTrigger = true;
    }

    public void OnUpperTriggerExit(GameObject other)
    {
        if (!IsValidTriggerObject(other, "OnUpperTriggerExit")) return;
        if (!IsObjectInteractable(other)) return;

        Debug.Log($"🎯 Upper clamp: {other.name} exited");
        _isObjectInUpperTrigger = false;
    }

    public void OnLowerTriggerEnter(GameObject other)
    {
        if (!IsValidTriggerObject(other, "OnLowerTriggerEnter")) return;
        if (!IsObjectInteractable(other)) return;

        Debug.Log($"🎯 Lower clamp: {other.name} entered");
        _isObjectInLowerTrigger = true;
    }

    public void OnLowerTriggerExit(GameObject other)
    {
        if (!IsValidTriggerObject(other, "OnLowerTriggerExit")) return;
        if (!IsObjectInteractable(other)) return;

        Debug.Log($"🎯 Lower clamp: {other.name} exited");
        _isObjectInLowerTrigger = false;
    }

    /// <summary>
    /// Validate trigger object and log warnings if invalid
    /// </summary>
    private bool IsValidTriggerObject(GameObject obj, string methodName)
    {
        if (obj == null)
        {
            Debug.LogWarning($"{methodName}: Null GameObject received");
            return false;
        }
        return true;
    }

    #endregion

    #region Rope Trigger Events

    public void OnRopeEnterUpperTrigger()
    {
        Debug.Log("🟢 Rope entered upper trigger");
        _isRopeInUpperTrigger = true;
    }

    public void OnRopeExitUpperTrigger()
    {
        Debug.Log("🔴 Rope exited upper trigger");
        _isRopeInUpperTrigger = false;
    }

    public void OnRopeEnterLowerTrigger()
    {
        Debug.Log("🟢 Rope entered lower trigger");
        _isRopeInLowerTrigger = true;
    }

    public void OnRopeExitLowerTrigger()
    {
        Debug.Log("🔴 Rope exited lower trigger");
        _isRopeInLowerTrigger = false;
    }

    #endregion

    #region Rope Detection

    /// <summary>
    /// Update rope proximity detection and trigger events
    /// </summary>
    private void UpdateRopeDetection()
    {
        if (!CanPerformRopeDetection()) return;

        bool ropeNearby = IsRopeNearForceps();
        UpdateRopeTriggerStates(ropeNearby);
    }

    /// <summary>
    /// Check if rope detection can be performed
    /// </summary>
    private bool CanPerformRopeDetection()
    {
        if (!_enableRopeDetection || _ropeInteractable == null) return false;

        // Throttle detection for performance
        if (Time.time - _lastRopeCheck < 1f / _ropeCheckFrequency) return false;
        
        _lastRopeCheck = Time.time;
        return true;
    }

    /// <summary>
    /// Check if rope is near this forceps using ObiRopeInteractable logic
    /// </summary>
    private bool IsRopeNearForceps()
    {
        try
        {
            var method = typeof(ObiRopeInteractable).GetMethod("IsForcepsNearRope", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                return (bool)method.Invoke(_ropeInteractable, new object[] { this });
            }
        }
        catch (System.Exception ex)
        {
            if (_showTagDebugInfo)
                Debug.LogWarning($"Rope detection failed: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Update rope trigger states based on proximity
    /// </summary>
    private void UpdateRopeTriggerStates(bool ropeNearby)
    {
        if (ropeNearby)
        {
            if (!_isRopeInUpperTrigger) OnRopeEnterUpperTrigger();
            if (!_isRopeInLowerTrigger) OnRopeEnterLowerTrigger();
        }
        else
        {
            if (_isRopeInUpperTrigger) OnRopeExitUpperTrigger();
            if (_isRopeInLowerTrigger) OnRopeExitLowerTrigger();
        }
    }

    #endregion

    #region Input Handling

    private void OnGripPressed(InputAction.CallbackContext context)
    {
        _isGripPressed = true;
        StartSmoothAnimation(true);
        Debug.Log("Grip pressed - closing forceps");
    }

    private void OnGripReleased(InputAction.CallbackContext context)
    {
        _isGripPressed = false;
        StartSmoothAnimation(false);
        Debug.Log("Grip released - opening forceps");
    }

    #endregion

    #region Animation System

    /// <summary>
    /// Start forceps animation (opening or closing)
    /// </summary>
    private void StartSmoothAnimation(bool closing)
    {
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }

        _currentAnimation = StartCoroutine(AnimateForceps(closing));
    }

    /// <summary>
    /// Animate forceps opening or closing with object detection
    /// </summary>
    private IEnumerator AnimateForceps(bool closing)
    {
        _isAnimating = true;

        Quaternion upperStart = _upperClamp.localRotation;
        Quaternion lowerStart = _lowerClamp.localRotation;

        if (closing)
        {
            yield return AnimateClosing(upperStart, lowerStart);
        }
        else
        {
            yield return AnimateOpening(upperStart, lowerStart);
        }

        _isAnimating = false;
        _currentAnimation = null;
    }

    /// <summary>
    /// Handle closing animation with object detection
    /// </summary>
    private IEnumerator AnimateClosing(Quaternion upperStart, Quaternion lowerStart)
    {
        Quaternion upperTarget = Quaternion.Euler(-90f, -90f, 90f);
        Quaternion lowerTarget = Quaternion.Euler(-90f, 90f, -90f);

        float elapsedTime = 0f;

        while (!ShouldStopClosingAnimation() && elapsedTime < _animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = _animationCurve.Evaluate(elapsedTime / _animationDuration);

            _upperClamp.localRotation = Quaternion.Slerp(upperStart, upperTarget, progress);
            _lowerClamp.localRotation = Quaternion.Slerp(lowerStart, lowerTarget, progress);

            yield return null;
        }

        LogAnimationResult();
    }

    /// <summary>
    /// Handle opening animation
    /// </summary>
    private IEnumerator AnimateOpening(Quaternion upperStart, Quaternion lowerStart)
    {
        float elapsedTime = 0f;

        while (elapsedTime < _animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = _animationCurve.Evaluate(elapsedTime / _animationDuration);

            _upperClamp.localRotation = Quaternion.Slerp(upperStart, _upperClampDefaultRot, progress);
            _lowerClamp.localRotation = Quaternion.Slerp(lowerStart, _lowerClampDefaultRot, progress);

            yield return null;
        }

        // Ensure exact final positions
        _upperClamp.localRotation = _upperClampDefaultRot;
        _lowerClamp.localRotation = _lowerClampDefaultRot;

        Debug.Log("Forceps opened");
    }

    /// <summary>
    /// MODIFIED: Determine if closing animation should stop based on new rope logic
    /// Unity objects (balls): Any trigger stops animation
    /// Rope objects: BOTH upper AND lower triggers required to stop animation
    /// </summary>
    private bool ShouldStopClosingAnimation()
    {
        // Unity objects (like balls): Any single trigger stops animation immediately
        bool unityObjectDetected = _isObjectInUpperTrigger || _isObjectInLowerTrigger;
        
        // Rope objects: BOTH upper AND lower triggers required to stop animation
        bool ropeProperlyClamped = _isRopeInUpperTrigger && _isRopeInLowerTrigger;
        
        if (_showTagDebugInfo && (unityObjectDetected || ropeProperlyClamped))
        {
            if (unityObjectDetected)
                Debug.Log("🛑 Unity object detected - stopping animation");
            if (ropeProperlyClamped)
                Debug.Log("🛑 Rope properly clamped (both triggers) - stopping animation");
        }
        
        return unityObjectDetected || ropeProperlyClamped;
    }

    /// <summary>
    /// DEPRECATED: Keep for backward compatibility but use ShouldStopClosingAnimation instead
    /// </summary>
    private bool IsAnyObjectInTrigger()
    {
        return ShouldStopClosingAnimation();
    }

    /// <summary>
    /// Log the result of closing animation
    /// </summary>
    private void LogAnimationResult()
    {
        if (ShouldStopClosingAnimation())
        {
            Debug.Log("🛑 Forceps stopped - object detected");
            LogTriggerDetails();
        }
        else
        {
            Debug.Log("Forceps closed completely");
        }
    }

    /// <summary>
    /// Log detailed trigger information for debugging
    /// </summary>
    private void LogTriggerDetails()
    {
        if (_isObjectInUpperTrigger) Debug.Log("  ↳ Unity object in upper trigger");
        if (_isObjectInLowerTrigger) Debug.Log("  ↳ Unity object in lower trigger");
        if (_isRopeInUpperTrigger) Debug.Log("  ↳ Rope in upper trigger");
        if (_isRopeInLowerTrigger) Debug.Log("  ↳ Rope in lower trigger");
        
        // Special case logging
        if (_isRopeInUpperTrigger && _isRopeInLowerTrigger)
        {
            Debug.Log("  ✅ Rope properly clamped between both triggers");
        }
        else if (_isRopeInUpperTrigger || _isRopeInLowerTrigger)
        {
            Debug.Log("  ⚠️ Rope detected in only one trigger - animation continues");
        }
    }

    #endregion

    #region Tag Management API

    public void AddInteractableTag(string tag)
    {
        if (string.IsNullOrEmpty(tag) || _interactableTags.Contains(tag)) return;

        _interactableTags.Add(tag);
        if (_showTagDebugInfo)
            Debug.Log($"Added interactable tag: {tag}");
    }

    public void RemoveInteractableTag(string tag)
    {
        if (_interactableTags.Remove(tag) && _showTagDebugInfo)
        {
            Debug.Log($"Removed interactable tag: {tag}");
        }
    }

    public void ClearInteractableTags()
    {
        _interactableTags.Clear();
        if (_showTagDebugInfo)
            Debug.Log("Cleared all interactable tags");
    }

    public void SetInteractableTags(List<string> tags)
    {
        _interactableTags = tags ?? new List<string>();
        if (_showTagDebugInfo)
            Debug.Log($"Set interactable tags: [{string.Join(", ", _interactableTags)}]");
    }

    public bool IsTagInteractable(string tag)
    {
        return _interactableTags.Contains(tag);
    }

    #endregion

    #region Component Validation

    /// <summary>
    /// Validate all required component references
    /// </summary>
    private void ValidateComponents()
    {
        bool hasErrors = false;

        hasErrors |= ValidateComponent(_upperClamp, "Upper Clamp Transform");
        hasErrors |= ValidateComponent(_lowerClamp, "Lower Clamp Transform");
        hasErrors |= ValidateComponent(_gripAction, "Grip Action Reference");

        if (hasErrors)
        {
            Debug.LogError("ForcepsController has missing references - check Inspector", this);
        }
    }

    /// <summary>
    /// Validate individual component and log error if missing
    /// </summary>
    private bool ValidateComponent(UnityEngine.Object component, string componentName)
    {
        if (component == null)
        {
            Debug.LogError($"{componentName} is not assigned!", this);
            return true;
        }
        return false;
    }

    #endregion

    #region Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateInteractableTags();
        ValidateAnimationSettings();
    }

    /// <summary>
    /// Validate interactable tags configuration
    /// </summary>
    private void ValidateInteractableTags()
    {
        if (_interactableTags == null) return;

        // Remove empty entries
        for (int i = _interactableTags.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrEmpty(_interactableTags[i]))
            {
                Debug.LogWarning($"Empty tag entry at index {i} - consider removing", this);
            }
        }

        // Warn if no tags specified
        if (_interactableTags.Count == 0)
        {
            Debug.LogWarning("No interactable tags set - will interact with all objects", this);
        }
    }

    /// <summary>
    /// Validate animation settings
    /// </summary>
    private void ValidateAnimationSettings()
    {
        if (_animationDuration <= 0)
        {
            Debug.LogWarning("Animation duration should be greater than 0", this);
        }

        if (_ropeCheckFrequency <= 0)
        {
            Debug.LogWarning("Rope check frequency should be greater than 0", this);
        }
    }
#endif

    #endregion
}
