using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.XR.Interaction.Toolkit;

/// Controls a lightsaber attached under the XR Origin right-hand controller.
/// Replaces the old keyboard-driven GameObjectController by:
/// - Activating/deactivating the tool via XR input (trigger/button) in hold or toggle mode
/// - Syncing a Light component's color/state
/// - Driving SofaUnity.SofaLaserModel.ActivateTool
/// - Optionally deactivating an alternate tool when this one is enabled
[DisallowMultipleComponent]
public class LightSaberController : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("GameObject that holds the Light to show saber state (enable + color). Typically a child with a Light component.")]
    [SerializeField] private GameObject m_light = null;

    [Header("Sofa Tool Integration")]
    [Tooltip("Optional link to Sofa laser tool implementation to mirror activation state.")]
    [SerializeField] private SofaUnity.SofaLaserModel m_toolImpl = null;

    [Header("Coordination")]
    [Tooltip("If assigned, this other tool will be disabled when this one becomes active.")]
    [SerializeField] private LightSaberController m_otherTool = null;

    [Header("Activation Behavior")]
    [Tooltip("Start active when enabled.")]
    [SerializeField] private bool m_startActive = false;
    [Tooltip("If true, a button press toggles active state. If false, holding the action keeps it active.")]
    [SerializeField] private bool m_toggleMode = false;

#if ENABLE_INPUT_SYSTEM
    [Tooltip("XR Input action used to activate the saber (e.g., RightHand Trigger). In toggle mode this is the toggle button.")]
    [SerializeField] private InputActionProperty m_activateAction;

    [Tooltip("Optional separate action to deactivate (useful if you want a different button to turn off in toggle mode). Leave empty to use only Activate action.")]
    [SerializeField] private InputActionProperty m_deactivateAction;
#endif

    [Header("Feedback")]
    [Tooltip("Color of the Light component when active.")]
    [SerializeField] private Color m_activeColor = Color.red;
    [Tooltip("Color of the Light component when inactive.")]
    [SerializeField] private Color m_inactiveColor = Color.green;
    [Tooltip("Optional XR controller to send a small haptic impulse on activation.")]
    [SerializeField] private XRBaseController m_hapticController = null;

    // Internal state
    private bool m_isActive = false;

    private Light CachedLight => (m_light != null) ? m_light.GetComponent<Light>() : null;

    private void OnEnable()
    {
        // Initial state
        ApplyActiveState(m_startActive, force: true);

#if ENABLE_INPUT_SYSTEM
        if (m_activateAction.action != null) m_activateAction.action.Enable();
        if (m_deactivateAction.action != null) m_deactivateAction.action.Enable();
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (m_activateAction.action != null) m_activateAction.action.Disable();
        if (m_deactivateAction.action != null) m_deactivateAction.action.Disable();
#endif
        // Ensure tool is off when disabled
        ApplyActiveState(false, force: true);
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        bool activatePressed = m_activateAction.action != null && m_activateAction.action.IsPressed();
        bool activateDown = m_activateAction.action != null && m_activateAction.action.WasPressedThisFrame();
        bool deactivateDown = m_deactivateAction.action != null && m_deactivateAction.action.WasPressedThisFrame();

        if (m_toggleMode)
        {
            if (activateDown || deactivateDown)
                SetActive(!m_isActive);
        }
        else
        {
            // Hold-to-activate behavior primarily driven by activate action (e.g., trigger)
            SetActive(activatePressed);
        }
#else
        // Fallback for legacy input or in-Editor quick testing (mirrors old behavior keys)
        if (Input.GetKeyDown(KeyCode.C))
            SetActive(true);
        if (Input.GetKeyDown(KeyCode.V))
            SetActive(false);
#endif
    }

    /// Public API to set the saber active state.
    public void SetActive(bool value)
    {
        if (m_isActive == value)
        {
            // Keep toolImpl synced even if value didn't change (useful in hold mode)
            if (m_toolImpl != null)
                m_toolImpl.ActivateTool = value;
            return;
        }

        ApplyActiveState(value, force: false);
    }

    /// Public API to toggle from events (e.g., XR UI).
    public void ToggleActive()
    {
        SetActive(!m_isActive);
    }

    private void ApplyActiveState(bool value, bool force)
    {
        m_isActive = value;

        if (m_light != null)
        {
            m_light.SetActive(m_isActive);
            var lt = CachedLight;
            if (lt != null)
                lt.color = m_isActive ? m_activeColor : m_inactiveColor;
        }

        if (m_toolImpl != null)
            m_toolImpl.ActivateTool = m_isActive;

        // Ensure mutual exclusivity if configured
        if (m_isActive && m_otherTool != null)
            m_otherTool.SetActive(false);

        // Light haptic feedback on activation
        if (m_isActive && m_hapticController != null)
            m_hapticController.SendHapticImpulse(0.5f, 0.05f);
    }

    // Optional Editor convenience: allow clicking the saber object in Scene to toggle
    private void OnMouseDown()
    {
        ToggleActive();
    }
}
