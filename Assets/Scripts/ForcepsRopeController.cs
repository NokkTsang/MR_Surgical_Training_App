using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* ============================================================= */
/*        Controller for integrating forceps with rope          */
/*        Handles input mapping for rope grab/release           */
/* ============================================================= */

public class ForcepsRopeController : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField]
    [Tooltip("Reference to the RopeXRDirectInteractor (can be on different object).")]
    private RopeXRDirectInteractor m_RopeInteractor;

    [SerializeField]
    [Tooltip("Reference to the ForcepsController for trigger events.")]
    private ForcepsController m_ForcepsController;

    [Header("Auto-Attach Settings")]
    [SerializeField]
    [Tooltip("Enable automatic rope attachment when detected by triggers.")]
    private bool m_EnableAutoAttach = true;

    [SerializeField]
    [Tooltip("Show debug information for rope interactions.")]
    private bool m_ShowDebugInfo = true;

    [SerializeField]
    [Tooltip("Layer for forceps trigger colliders (default: 8).")]
    private int m_ForcepsTriggerLayer = 8;

    private RopeXRDirectInteractor m_CachedRopeInteractor;
    private ForcepsController m_CachedForcepsController;

    /// <summary>
    /// Get the rope interactor (cached for performance)
    /// </summary>
    private RopeXRDirectInteractor RopeInteractor
    {
        get
        {
            if (m_CachedRopeInteractor == null)
            {
                // Try manual reference first
                if (m_RopeInteractor != null)
                {
                    m_CachedRopeInteractor = m_RopeInteractor;
                }
                else
                {
                    // Fallback: try to find on same object
                    m_CachedRopeInteractor = GetComponent<RopeXRDirectInteractor>();
                    
                    // If still not found, try to find in children
                    if (m_CachedRopeInteractor == null)
                        m_CachedRopeInteractor = GetComponentInChildren<RopeXRDirectInteractor>();
                    
                    // If still not found, try to find in parent
                    if (m_CachedRopeInteractor == null)
                        m_CachedRopeInteractor = GetComponentInParent<RopeXRDirectInteractor>();
                }
            }
            return m_CachedRopeInteractor;
        }
    }

    /// <summary>
    /// Get the forceps controller (cached for performance)
    /// </summary>
    private ForcepsController ForcepsController
    {
        get
        {
            if (m_CachedForcepsController == null)
            {
                // Try manual reference first
                if (m_ForcepsController != null)
                {
                    m_CachedForcepsController = m_ForcepsController;
                }
                else
                {
                    // Fallback: try to find on same object
                    m_CachedForcepsController = GetComponent<ForcepsController>();
                    
                    // If still not found, try to find in children
                    if (m_CachedForcepsController == null)
                        m_CachedForcepsController = GetComponentInChildren<ForcepsController>();
                    
                    // If still not found, try to find in parent
                    if (m_CachedForcepsController == null)
                        m_CachedForcepsController = GetComponentInParent<ForcepsController>();
                }
            }
            return m_CachedForcepsController;
        }
    }

    private void Awake()
    {
        // Cache the rope interactor reference
        _ = RopeInteractor;
        
        if (RopeInteractor == null)
        {
            Debug.LogError("ForcepsRopeController: No RopeXRDirectInteractor found. Please assign one manually in the inspector.", this);
        }

        // Cache the forceps controller reference
        _ = ForcepsController;
        
        if (ForcepsController == null)
        {
            Debug.LogError("ForcepsRopeController: No ForcepsController found. Please assign one manually in the inspector.", this);
        }

        // Fix trigger collider conflicts with Obi rope system (only for forceps triggers)
        FixTriggerColliderConflicts();
        
        // Configure Obi solvers to ignore forceps trigger layer
        ConfigureObiSolversForTriggers();
    }

    private void OnEnable()
    {
        // Subscribe to forceps trigger events for rope interaction
        if (ForcepsController != null)
        {
            // Note: We'll monitor the trigger states via polling in Update since
            // ForcepsController doesn't expose events for trigger enter/exit
        }
    }

    private void OnDisable()
    {
        // Release any active rope grabs when disabled
        if (RopeInteractor != null)
        {
            RopeInteractor.StopRopeGrab();
        }
    }

    private void Update()
    {
        // Monitor trigger states for automatic rope attachment
        if (m_EnableAutoAttach && ForcepsController != null)
        {
            MonitorTriggersForRopeAttachment();
        }
    }

    /// <summary>
    /// Check if rope is nearby for interaction
    /// </summary>
    public bool IsRopeNearby()
    {
        if (RopeInteractor == null) 
        {
            if (m_ShowDebugInfo && Time.frameCount % 120 == 0)
                Debug.LogWarning("IsRopeNearby: RopeInteractor is null");
            return false;
        }

        // Use a simple distance check to see if any ropes are nearby
        var obiActors = FindObjectsOfType<Obi.ObiActor>();
        Vector3 grabPosition = transform.position;
        float detectionRadius = RopeInteractor.ropeDetectionRadius * 1.5f;

        if (m_ShowDebugInfo && Time.frameCount % 120 == 0)
        {
            Debug.Log($"IsRopeNearby: Checking {obiActors.Length} ObiActors, detection radius: {detectionRadius}, grab position: {grabPosition}");
        }

        foreach (var actor in obiActors)
        {
            if (actor == null || actor.solver == null) continue;

            for (int i = 0; i < actor.particleCount; i++)
            {
                int solverIndex = actor.solverIndices[i];
                Vector3 particlePosition = actor.solver.positions[solverIndex];
                
                float distance = Vector3.Distance(grabPosition, particlePosition);
                if (distance <= detectionRadius)
                {
                    if (m_ShowDebugInfo)
                        Debug.Log($"Rope nearby! Actor: {actor.name}, distance: {distance:F3}, particle {i}");
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Manually trigger rope grab (can be called from other scripts)
    /// </summary>
    public void TriggerRopeGrab()
    {
        if (RopeInteractor != null)
        {
            RopeInteractor.StartRopeGrab();
        }
    }

    /// <summary>
    /// Manually release rope grab (can be called from other scripts)
    /// </summary>
    public void ReleaseRopeGrab()
    {
        if (RopeInteractor != null)
        {
            RopeInteractor.StopRopeGrab();
        }
    }

    /// <summary>
    /// Check if currently grabbing rope
    /// </summary>
    public bool IsGrabbingRope()
    {
        return RopeInteractor != null && RopeInteractor.IsGrabbingRope();
    }

    /// <summary>
    /// Enable or disable rope interaction
    /// </summary>
    public void SetRopeInteractionEnabled(bool enabled)
    {
        if (RopeInteractor != null)
        {
            RopeInteractor.enableRopeInteraction = enabled;
        }
    }

    /// <summary>
    /// Adjust rope detection sensitivity
    /// </summary>
    public void SetRopeDetectionRadius(float radius)
    {
        if (RopeInteractor != null)
        {
            RopeInteractor.ropeDetectionRadius = radius;
        }
    }

    /// <summary>
    /// Set the number of particles to grab
    /// </summary>
    public void SetRopeGrabParticleCount(int count)
    {
        if (RopeInteractor != null)
        {
            RopeInteractor.ropeGrabParticleCount = count;
        }
    }

    /// <summary>
    /// Monitor trigger states for automatic rope attachment
    /// </summary>
    private void MonitorTriggersForRopeAttachment()
    {
        // Check if rope is nearby and not already grabbing
        bool ropeNearby = IsRopeNearby();
        bool currentlyGrabbing = IsGrabbingRope();
        bool gripPressed = ForcepsController != null && ForcepsController.IsGripPressed;
        
        if (m_ShowDebugInfo)
        {
            // Debug information every 60 frames (1 second at 60fps)
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"RopeAttach Monitor - Rope nearby: {ropeNearby}, Currently grabbing: {currentlyGrabbing}, Grip pressed: {gripPressed}, Auto-attach enabled: {m_EnableAutoAttach}");
                if (RopeInteractor != null)
                {
                    Debug.Log($"RopeInteractor - Enabled: {RopeInteractor.enableRopeInteraction}, Detection radius: {RopeInteractor.ropeDetectionRadius}");
                }
                if (ForcepsController != null)
                {
                    Debug.Log($"ForcepsController found: {ForcepsController.name}, IsGripPressed: {ForcepsController.IsGripPressed}");
                }
            }
        }
        
        // Only attach rope when grip is pressed AND rope is nearby AND not already grabbing
        if (ropeNearby && !currentlyGrabbing && gripPressed)
        {
            TriggerRopeGrab();
            if (m_ShowDebugInfo)
                Debug.Log("Auto-attached rope via grip press + proximity detection");
        }
    }

    /// <summary>
    /// Fix trigger collider conflicts with Obi rope system (simplified and targeted)
    /// </summary>
    [ContextMenu("Fix Trigger Collider Conflicts")]
    private void FixTriggerColliderConflicts()
    {
        // Only process colliders in the forceps hierarchy, not global scene colliders
        var forcepsColliders = GetComponentsInChildren<Collider>();
        
        foreach (var collider in forcepsColliders)
        {
            if (collider.isTrigger)
            {
                // Handle forceps trigger colliders only
                HandleForcepsTriggerCollider(collider);
            }
            // Leave normal colliders completely untouched for Unity physics
        }

        if (m_ShowDebugInfo)
            Debug.Log("Fixed forceps trigger collider conflicts (environment collisions preserved)");
    }

    /// <summary>
    /// Handle a specific forceps trigger collider
    /// </summary>
    private void HandleForcepsTriggerCollider(Collider triggerCollider)
    {
        // Remove ObiCollider from trigger to prevent rope conflicts
        var obiCollider = triggerCollider.GetComponent<Obi.ObiCollider>();
        if (obiCollider != null)
        {
            if (m_ShowDebugInfo)
                Debug.Log($"Removing ObiCollider from forceps trigger: {triggerCollider.name}");
            
            #if UNITY_EDITOR
            DestroyImmediate(obiCollider);
            #else
            Destroy(obiCollider);
            #endif
        }

        // Move trigger to dedicated layer
        if (triggerCollider.gameObject.layer != m_ForcepsTriggerLayer)
        {
            triggerCollider.gameObject.layer = m_ForcepsTriggerLayer;
            if (m_ShowDebugInfo)
                Debug.Log($"Moved forceps trigger {triggerCollider.name} to layer {m_ForcepsTriggerLayer}");
        }

        // Apply frictionless material for clean trigger behavior
        if (triggerCollider.material == null)
        {
            var triggerMaterial = new PhysicMaterial("ForcepsTriggerMaterial")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicMaterialCombine.Minimum,
                bounceCombine = PhysicMaterialCombine.Minimum
            };
            triggerCollider.material = triggerMaterial;
        }
    }

    /// <summary>
    /// Configure Obi solvers to ignore only forceps trigger layer
    /// </summary>
    private void ConfigureObiSolversForTriggers()
    {
        var obiSolvers = FindObjectsOfType<Obi.ObiSolver>();
        
        foreach (var solver in obiSolvers)
        {
            // Get current collision layer mask
            var currentMask = GetObiSolverCollisionMask(solver);
            var forcepsTriggerMask = 1 << m_ForcepsTriggerLayer;
            
            // Remove only the forceps trigger layer, keep all other collisions
            var newMask = currentMask & ~forcepsTriggerMask;
            SetObiSolverCollisionMask(solver, newMask);
            
            if (m_ShowDebugInfo)
                Debug.Log($"Configured ObiSolver {solver.name} to ignore forceps trigger layer {m_ForcepsTriggerLayer}");
        }
    }

    /// <summary>
    /// Get collision mask from ObiSolver (compatible method)
    /// </summary>
    private LayerMask GetObiSolverCollisionMask(Obi.ObiSolver solver)
    {
        // Try different API approaches for different Obi versions
        try
        {
            // For newer Obi versions, use reflection to find the correct property
            var property = solver.GetType().GetProperty("collisionLayerMask");
            if (property != null)
                return (LayerMask)property.GetValue(solver);
            
            // Fallback: return all layers except trigger layer
            return ~(1 << m_ForcepsTriggerLayer);
        }
        catch
        {
            // Safe fallback
            return ~(1 << m_ForcepsTriggerLayer);
        }
    }

    /// <summary>
    /// Set collision mask for ObiSolver (compatible method)
    /// </summary>
    private void SetObiSolverCollisionMask(Obi.ObiSolver solver, LayerMask mask)
    {
        try
        {
            // Try different API approaches for different Obi versions
            var property = solver.GetType().GetProperty("collisionLayerMask");
            if (property != null && property.CanWrite)
            {
                property.SetValue(solver, mask);
                return;
            }
            
            // If direct property access fails, we already handled the important part
            // by removing ObiCollider from triggers
        }
        catch
        {
            // Fail silently - the ObiCollider removal is the primary solution
        }
    }

    /// <summary>
    /// Enable or disable automatic rope attachment
    /// </summary>
    public void SetAutoAttachEnabled(bool enabled)
    {
        m_EnableAutoAttach = enabled;
        if (m_ShowDebugInfo)
            Debug.Log($"Auto-attach rope: {(enabled ? "Enabled" : "Disabled")}");
    }

    /// <summary>
    /// Check if auto-attach is enabled
    /// </summary>
    public bool IsAutoAttachEnabled()
    {
        return m_EnableAutoAttach;
    }

    /// <summary>
    /// Manual method to fix trigger conflicts (can be called from other scripts)
    /// </summary>
    public void RefreshTriggerConfiguration()
    {
        FixTriggerColliderConflicts();
    }

    /// <summary>
    /// Diagnose current collider configuration (useful for debugging)
    /// </summary>
    [ContextMenu("Diagnose Collider Configuration")]
    public void DiagnoseColliderConfiguration()
    {
        var forcepsColliders = GetComponentsInChildren<Collider>();
        
        Debug.Log("=== Forceps Collider Configuration ===");
        
        foreach (var collider in forcepsColliders)
        {
            var obiCollider = collider.GetComponent<Obi.ObiCollider>();
            string status = collider.isTrigger ? "TRIGGER" : "NORMAL";
            string layer = $"Layer {collider.gameObject.layer}";
            string obiStatus = obiCollider != null ? (obiCollider.enabled ? "ObiCollider ENABLED" : "ObiCollider DISABLED") : "NO ObiCollider";
            
            Debug.Log($"{collider.name}: {status} | {layer} | {obiStatus}");
        }
        
        Debug.Log("=== End Diagnosis ===");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Validate component setup in editor
    /// </summary>
    private void OnValidate()
    {
        if (RopeInteractor == null)
        {
            Debug.LogWarning("RopeXRDirectInteractor is not assigned! The script will try to find it automatically.", this);
        }

        if (ForcepsController == null)
        {
            Debug.LogWarning("ForcepsController is not assigned! The script will try to find it automatically.", this);
        }
    }
#endif
}
