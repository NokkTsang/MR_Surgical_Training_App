using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

/* ============================================================= */
/*          ObiRopeInteractable Component                       */
/*    Enables Obi ropes to interact with forceps triggers      */
/*    Automatically attaches nearest particles to attach points */
/* ============================================================= */

[RequireComponent(typeof(ObiActor))]
public class ObiRopeInteractable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField]
    [Tooltip("Enable rope interaction with forceps")]
    private bool m_EnableInteraction = true;

    [SerializeField]
    [Range(0.001f, 0.1f)]
    [Tooltip("Detection radius for finding nearby forceps")]
    private float m_DetectionRadius = 0.002f;

    [SerializeField]
    [Range(1, 10)]
    [Tooltip("Number of consecutive particles to attach")]
    private int m_AttachParticleCount = 3;

    [SerializeField]
    [Range(0.1f, 5.0f)]
    [Tooltip("Maximum distance to consider for attachment")]
    private float m_MaxAttachDistance = 0.05f;

    [Header("Attachment Behavior")]
    [SerializeField]
    [Tooltip("Use kinematic attachment (more stable)")]
    private bool m_UseKinematicAttach = true;

    [SerializeField]
    [Range(0.1f, 3.0f)]
    [Tooltip("Force multiplier for dynamic attachment")]
    private float m_AttachForceMultiplier = 1.5f;

    [SerializeField]
    [Tooltip("Smooth attachment transition")]
    private bool m_SmoothAttachment = true;

    [SerializeField]
    [Range(0.1f, 2.0f)]
    [Tooltip("Attachment transition duration")]
    private float m_AttachmentDuration = 0.3f;

    [Header("Debug Settings")]
    [SerializeField]
    [Tooltip("Show debug information")]
    private bool m_ShowDebugInfo = true;

    [SerializeField]
    [Tooltip("Show visual debug gizmos")]
    private bool m_ShowDebugGizmos = true;

    // Internal state
    private ObiActor m_RopeActor;
    private List<ForcepsController> m_NearbyForceps = new List<ForcepsController>();
    private Dictionary<int, AttachmentInfo> m_ActiveAttachments = new Dictionary<int, AttachmentInfo>();
    
    // Attachment information
    private class AttachmentInfo
    {
        public Transform attachPoint;
        public List<int> particleIndices = new List<int>();
        public List<Vector3> originalPositions = new List<Vector3>();
        public List<float> originalInvMasses = new List<float>();
        public Vector3 localOffset;
        public float attachTime;
        public bool isTransitioning;
        public ForcepsController forceps;
    }

    /// <summary>
    /// Public properties
    /// </summary>
    public bool EnableInteraction
    {
        get => m_EnableInteraction;
        set => m_EnableInteraction = value;
    }

    public float DetectionRadius
    {
        get => m_DetectionRadius;
        set => m_DetectionRadius = Mathf.Max(0.001f, value);
    }

    public bool IsAttached => m_ActiveAttachments.Count > 0;

    private void Awake()
    {
        // Get required Obi components
        m_RopeActor = GetComponent<ObiActor>();
        if (m_RopeActor == null)
        {
            Debug.LogError($"ObiRopeInteractable: No ObiActor found on {gameObject.name}. This component requires an ObiActor.", this);
            enabled = false;
            return;
        }

        // Ensure the rope has the correct tag for forceps interaction
        if (!gameObject.CompareTag("Rope"))
        {
            gameObject.tag = "Rope";
            if (m_ShowDebugInfo)
                Debug.Log($"ObiRopeInteractable: Set rope tag on {gameObject.name}");
        }

        if (m_ShowDebugInfo)
            Debug.Log($"ObiRopeInteractable initialized on {gameObject.name}");
    }

    private void Update()
    {
        if (!m_EnableInteraction || m_RopeActor?.solver == null) return;

        // Update nearby forceps detection
        UpdateNearbyForcepsDetection();

        // Process attachment logic
        ProcessAttachmentLogic();

        // Update active attachments
        UpdateActiveAttachments();
    }

    /// <summary>
    /// Find nearby forceps within detection range
    /// </summary>
    private void UpdateNearbyForcepsDetection()
    {
        m_NearbyForceps.Clear();
        
        // Find all forceps in the scene
        var allForceps = FindObjectsOfType<ForcepsController>();
        
        foreach (var forceps in allForceps)
        {
            if (forceps == null) continue;

            // Check if any rope particles are near any forceps attach points
            if (IsForcepsNearRope(forceps))
            {
                m_NearbyForceps.Add(forceps);
            }
        }

        // Debug info
        if (m_ShowDebugInfo && Time.frameCount % 120 == 0) // Every 2 seconds
        {
            Debug.Log($"ObiRopeInteractable ({gameObject.name}): Found {m_NearbyForceps.Count} nearby forceps");
        }
    }

    /// <summary>
    /// Check if forceps is near any rope particles
    /// </summary>
    private bool IsForcepsNearRope(ForcepsController forceps)
    {
        // Get forceps attach points from RopeXRDirectInteractor
        var ropeInteractor = forceps.GetComponent<RopeXRDirectInteractor>();
        if (ropeInteractor == null) 
            ropeInteractor = forceps.GetComponentInChildren<RopeXRDirectInteractor>();

        List<Vector3> checkPositions = new List<Vector3>();
        
        if (ropeInteractor != null)
        {
            // Use attach points from RopeXRDirectInteractor if available
            var attachPoints = GetRopeInteractorAttachPoints(ropeInteractor);
            foreach (var point in attachPoints)
            {
                if (point != null)
                    checkPositions.Add(point.position);
            }
        }
        
        // Fallback to forceps transform position
        if (checkPositions.Count == 0)
        {
            checkPositions.Add(forceps.transform.position);
        }

        // Check distance to rope particles
        foreach (var position in checkPositions)
        {
            if (GetDistanceToNearestParticle(position) <= m_DetectionRadius)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get attach points from RopeXRDirectInteractor using reflection (safe method)
    /// </summary>
    private List<Transform> GetRopeInteractorAttachPoints(RopeXRDirectInteractor interactor)
    {
        var attachPoints = new List<Transform>();
        
        try
        {
            // Use reflection to access private attach points list
            var field = typeof(RopeXRDirectInteractor).GetField("m_AttachPoints", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                var points = field.GetValue(interactor) as List<Transform>;
                if (points != null)
                    attachPoints.AddRange(points);
            }
        }
        catch (System.Exception)
        {
            // Fallback: use main transform
            attachPoints.Add(interactor.transform);
        }

        return attachPoints;
    }

    /// <summary>
    /// Get distance from position to nearest rope particle
    /// </summary>
    private float GetDistanceToNearestParticle(Vector3 position)
    {
        if (m_RopeActor?.solver == null) return float.MaxValue;

        float minDistance = float.MaxValue;
        
        for (int i = 0; i < m_RopeActor.particleCount; i++)
        {
            int solverIndex = m_RopeActor.solverIndices[i];
            Vector3 particlePos = m_RopeActor.solver.positions[solverIndex];
            
            float distance = Vector3.Distance(position, particlePos);
            if (distance < minDistance)
                minDistance = distance;
        }

        return minDistance;
    }

    /// <summary>
    /// Process attachment logic for nearby forceps
    /// </summary>
    private void ProcessAttachmentLogic()
    {
        foreach (var forceps in m_NearbyForceps)
        {
            if (forceps == null) continue;

            bool gripPressed = forceps.IsGripPressed;
            int forcepsId = forceps.GetInstanceID();
            bool isAttached = m_ActiveAttachments.ContainsKey(forcepsId);

            // Attach logic: grip pressed + not already attached + rope in trigger zone
            if (gripPressed && !isAttached && IsRopeInForcepsTriggerZone(forceps))
            {
                TryAttachToForceps(forceps);
            }
            // Detach logic: grip released + currently attached
            else if (!gripPressed && isAttached)
            {
                DetachFromForceps(forceps);
            }
        }
    }

    /// <summary>
    /// Check if rope is in forceps trigger zone
    /// </summary>
    private bool IsRopeInForcepsTriggerZone(ForcepsController forceps)
    {
        // This would ideally check the trigger state from the forceps
        // For now, use proximity as an approximation
        return IsForcepsNearRope(forceps);
    }

    /// <summary>
    /// Try to attach rope to forceps
    /// </summary>
    private void TryAttachToForceps(ForcepsController forceps)
    {
        // Find closest particle and attach point
        var attachmentData = FindBestAttachmentPoint(forceps);
        if (attachmentData == null) return;

        // Create attachment info
        var attachInfo = new AttachmentInfo
        {
            forceps = forceps,
            attachPoint = attachmentData.attachPoint,
            attachTime = Time.time,
            isTransitioning = m_SmoothAttachment
        };

        // Find particles to attach
        var particlesToAttach = GetParticlesToAttach(attachmentData.particleIndex);
        var solver = m_RopeActor.solver;

        foreach (int particleIndex in particlesToAttach)
        {
            int solverIndex = m_RopeActor.solverIndices[particleIndex];
            attachInfo.particleIndices.Add(solverIndex);
            attachInfo.originalPositions.Add(solver.positions[solverIndex]);
            attachInfo.originalInvMasses.Add(solver.invMasses[solverIndex]);
        }

        // Calculate local offset
        Vector3 centerPos = CalculateParticleCenter(attachInfo.particleIndices);
        attachInfo.localOffset = attachInfo.attachPoint.InverseTransformPoint(centerPos);

        // Apply kinematic attachment if enabled
        if (m_UseKinematicAttach)
        {
            foreach (int solverIndex in attachInfo.particleIndices)
            {
                solver.invMasses[solverIndex] = 0f; // Make kinematic
            }
        }

        // Store attachment
        int forcepsId = forceps.GetInstanceID();
        m_ActiveAttachments[forcepsId] = attachInfo;

        if (m_ShowDebugInfo)
            Debug.Log($"Attached rope {gameObject.name} to forceps {forceps.name} with {attachInfo.particleIndices.Count} particles");
    }

    /// <summary>
    /// Data structure for attachment point finding
    /// </summary>
    private class AttachmentData
    {
        public Transform attachPoint;
        public int particleIndex;
        public float distance;
    }

    /// <summary>
    /// Find the best attachment point for the forceps
    /// </summary>
    private AttachmentData FindBestAttachmentPoint(ForcepsController forceps)
    {
        var ropeInteractor = forceps.GetComponent<RopeXRDirectInteractor>();
        if (ropeInteractor == null)
            ropeInteractor = forceps.GetComponentInChildren<RopeXRDirectInteractor>();

        List<Transform> attachPoints = new List<Transform>();
        
        if (ropeInteractor != null)
        {
            attachPoints = GetRopeInteractorAttachPoints(ropeInteractor);
        }
        
        if (attachPoints.Count == 0)
        {
            attachPoints.Add(forceps.transform);
        }

        AttachmentData bestAttachment = null;
        float bestDistance = float.MaxValue;

        foreach (var attachPoint in attachPoints)
        {
            if (attachPoint == null) continue;

            for (int i = 0; i < m_RopeActor.particleCount; i++)
            {
                int solverIndex = m_RopeActor.solverIndices[i];
                Vector3 particlePos = m_RopeActor.solver.positions[solverIndex];
                
                float distance = Vector3.Distance(attachPoint.position, particlePos);
                
                if (distance < bestDistance && distance <= m_MaxAttachDistance)
                {
                    bestDistance = distance;
                    bestAttachment = new AttachmentData
                    {
                        attachPoint = attachPoint,
                        particleIndex = i,
                        distance = distance
                    };
                }
            }
        }

        return bestAttachment;
    }

    /// <summary>
    /// Get list of particles to attach around center particle
    /// </summary>
    private List<int> GetParticlesToAttach(int centerParticleIndex)
    {
        var particles = new List<int>();
        
        int startIndex = Mathf.Max(0, centerParticleIndex - m_AttachParticleCount / 2);
        int endIndex = Mathf.Min(m_RopeActor.particleCount - 1, startIndex + m_AttachParticleCount - 1);
        
        // Adjust start if we hit the end
        if (endIndex - startIndex + 1 < m_AttachParticleCount)
        {
            startIndex = Mathf.Max(0, endIndex - m_AttachParticleCount + 1);
        }
        
        for (int i = startIndex; i <= endIndex; i++)
        {
            particles.Add(i);
        }
        
        return particles;
    }

    /// <summary>
    /// Calculate center position of particles
    /// </summary>
    private Vector3 CalculateParticleCenter(List<int> solverIndices)
    {
        Vector3 center = Vector3.zero;
        var solver = m_RopeActor.solver;
        
        foreach (int solverIndex in solverIndices)
        {
            center += (Vector3)solver.positions[solverIndex];
        }
        
        return center / solverIndices.Count;
    }

    /// <summary>
    /// Detach rope from forceps
    /// </summary>
    private void DetachFromForceps(ForcepsController forceps)
    {
        int forcepsId = forceps.GetInstanceID();
        
        if (!m_ActiveAttachments.TryGetValue(forcepsId, out var attachInfo))
            return;

        // Restore original particle properties
        var solver = m_RopeActor.solver;
        for (int i = 0; i < attachInfo.particleIndices.Count && i < attachInfo.originalInvMasses.Count; i++)
        {
            int solverIndex = attachInfo.particleIndices[i];
            solver.invMasses[solverIndex] = attachInfo.originalInvMasses[i];
        }

        m_ActiveAttachments.Remove(forcepsId);

        if (m_ShowDebugInfo)
            Debug.Log($"Detached rope {gameObject.name} from forceps {forceps.name}");
    }

    /// <summary>
    /// Update positions of attached particles
    /// </summary>
    private void UpdateActiveAttachments()
    {
        foreach (var kvp in m_ActiveAttachments)
        {
            var attachInfo = kvp.Value;
            if (attachInfo?.attachPoint == null) continue;

            UpdateAttachmentPosition(attachInfo);
        }
    }

    /// <summary>
    /// Update single attachment position
    /// </summary>
    private void UpdateAttachmentPosition(AttachmentInfo attachInfo)
    {
        Vector3 targetPosition = attachInfo.attachPoint.TransformPoint(attachInfo.localOffset);
        var solver = m_RopeActor.solver;

        // Handle smooth transition
        float transitionFactor = 1f;
        if (attachInfo.isTransitioning)
        {
            float elapsed = Time.time - attachInfo.attachTime;
            transitionFactor = Mathf.Clamp01(elapsed / m_AttachmentDuration);
            
            if (transitionFactor >= 1f)
                attachInfo.isTransitioning = false;
        }

        // Calculate current center and apply offset
        Vector3 currentCenter = CalculateParticleCenter(attachInfo.particleIndices);
        Vector3 offset = (targetPosition - currentCenter) * transitionFactor;

        // Apply to particles
        for (int i = 0; i < attachInfo.particleIndices.Count; i++)
        {
            int solverIndex = attachInfo.particleIndices[i];
            
            if (m_UseKinematicAttach)
            {
                // Direct position update for kinematic
                Vector3 newPos = (Vector3)solver.positions[solverIndex] + offset;
                solver.positions[solverIndex] = newPos;
            }
            else
            {
                // Force-based update for dynamic
                Vector3 force = offset * m_AttachForceMultiplier;
                solver.externalForces[solverIndex] += (Vector4)force;
            }
        }
    }

    /// <summary>
    /// Force detach all attachments
    /// </summary>
    public void DetachAll()
    {
        var attachmentsCopy = new Dictionary<int, AttachmentInfo>(m_ActiveAttachments);
        
        foreach (var kvp in attachmentsCopy)
        {
            if (kvp.Value?.forceps != null)
                DetachFromForceps(kvp.Value.forceps);
        }
    }

    /// <summary>
    /// Check if attached to specific forceps
    /// </summary>
    public bool IsAttachedTo(ForcepsController forceps)
    {
        if (forceps == null) return false;
        return m_ActiveAttachments.ContainsKey(forceps.GetInstanceID());
    }

    /// <summary>
    /// Get number of active attachments
    /// </summary>
    public int GetAttachmentCount()
    {
        return m_ActiveAttachments.Count;
    }

    private void OnDisable()
    {
        DetachAll();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draw debug gizmos
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!m_ShowDebugGizmos || !m_EnableInteraction) return;

        // Draw detection radius around rope particles
        if (m_RopeActor?.solver != null)
        {
            UnityEditor.Handles.color = Color.cyan;
            for (int i = 0; i < m_RopeActor.particleCount; i++)
            {
                int solverIndex = m_RopeActor.solverIndices[i];
                Vector3 particlePos = m_RopeActor.solver.positions[solverIndex];
                UnityEditor.Handles.DrawWireDisc(particlePos, Vector3.up, m_DetectionRadius);
            }
        }

        // Draw active attachments
        UnityEditor.Handles.color = Color.red;
        foreach (var attachInfo in m_ActiveAttachments.Values)
        {
            if (attachInfo?.attachPoint == null) continue;
            
            Vector3 targetPos = attachInfo.attachPoint.TransformPoint(attachInfo.localOffset);
            Vector3 currentCenter = CalculateParticleCenter(attachInfo.particleIndices);
            
            UnityEditor.Handles.DrawLine(currentCenter, targetPos);
            UnityEditor.Handles.DrawWireDisc(targetPos, Vector3.up, 0.005f);
        }
    }
#endif
}
