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
    private float m_MaxAttachDistance = 0.1f;

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

    [Header("Visual Feedback")]
    [SerializeField]
    [Tooltip("Enable visual coloring of attached particles")]
    private bool m_EnableAttachmentColoring = true;

    [SerializeField]
    [Tooltip("Color for attached particles")]
    private Color m_AttachedParticleColor = Color.red;

    [SerializeField]
    [Tooltip("Color for particles during transition")]
    private Color m_TransitionParticleColor = Color.yellow;

    [SerializeField]
    [Tooltip("Smoothly blend colors during attachment transition")]
    private bool m_SmoothColorTransition = true;

    [Header("Debug Settings")]
    [SerializeField]
    [Tooltip("Show debug information")]
    private bool m_ShowDebugInfo = true;

    [SerializeField]
    [Tooltip("Show visual debug gizmos")]
    private bool m_ShowDebugGizmos = true;

    // Core components and state
    private ObiActor m_RopeActor;
    private List<ForcepsController> m_NearbyForceps = new List<ForcepsController>();
    private Dictionary<int, AttachmentInfo> m_ActiveAttachments = new Dictionary<int, AttachmentInfo>();
    private Dictionary<int, Color> m_OriginalParticleColors = new Dictionary<int, Color>();
    
    // Data structure to store attachment information
    private class AttachmentInfo
    {
        public Transform attachPoint;
        public List<int> particleIndices = new List<int>();
        public List<Vector3> originalPositions = new List<Vector3>();
        public List<float> originalInvMasses = new List<float>();
        public List<Color> originalColors = new List<Color>();
        public Vector3 localOffset;
        public float attachTime;
        public bool isTransitioning;
        public ForcepsController forceps;
    }

    // Helper data structure for finding best attachment point
    private class AttachmentData
    {
        public Transform attachPoint;
        public int particleIndex;
        public float distance;
    }

    // Public API properties
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

    public bool EnableAttachmentColoring
    {
        get => m_EnableAttachmentColoring;
        set => m_EnableAttachmentColoring = value;
    }

    public Color AttachedParticleColor
    {
        get => m_AttachedParticleColor;
        set => m_AttachedParticleColor = value;
    }

    // Unity lifecycle methods
    private void Awake()
    {
        InitializeComponent();
    }

    private void Update()
    {
        if (!m_EnableInteraction || !IsRopeActorReady()) return;

        UpdateNearbyForcepsDetection();
        ProcessAttachmentLogic();
        UpdateActiveAttachments();

        if (m_EnableAttachmentColoring)
        {
            UpdateAttachedParticleColors();
        }
    }

    private void OnDisable()
    {
        DetachAll();
    }

    // Core initialization
    private void InitializeComponent()
    {
        m_RopeActor = GetComponent<ObiActor>();
        if (m_RopeActor == null)
        {
            Debug.LogError($"ObiRopeInteractable: No ObiActor found on {gameObject.name}. This component requires an ObiActor.", this);
            enabled = false;
            return;
        }

        // Ensure correct tag for forceps interaction
        if (!gameObject.CompareTag("Rope"))
        {
            gameObject.tag = "Rope";
            if (m_ShowDebugInfo)
                Debug.Log($"ObiRopeInteractable: Set rope tag on {gameObject.name}");
        }

        if (m_ShowDebugInfo)
            Debug.Log($"ObiRopeInteractable initialized on {gameObject.name}");
    }

    // Check if ObiActor is ready for interaction
    private bool IsRopeActorReady()
    {
        return m_RopeActor != null &&
               m_RopeActor.solver != null &&
               m_RopeActor.particleCount > 0 &&
               m_RopeActor.solverIndices != null &&
               m_RopeActor.solverIndices.count > 0 &&
               m_RopeActor.solver.positions != null &&
               m_RopeActor.solver.positions.count > 0;
    }

    // Visual feedback - particle coloring system
    private void StoreOriginalParticleColor(int solverIndex)
    {
        if (!m_OriginalParticleColors.ContainsKey(solverIndex))
        {
            Color originalColor = m_RopeActor.GetParticleColor(solverIndex);
            m_OriginalParticleColors[solverIndex] = originalColor;
        }
    }

    private void SetParticleColor(int solverIndex, Color color)
    {
        if (m_RopeActor?.solver?.colors != null && 
            solverIndex >= 0 && 
            solverIndex < m_RopeActor.solver.colors.count)
        {
            m_RopeActor.solver.colors[solverIndex] = color;
        }
    }

    private void UpdateAttachedParticleColors()
    {
        foreach (var attachInfo in m_ActiveAttachments.Values)
        {
            if (attachInfo?.particleIndices == null) continue;

            Color targetColor = m_AttachedParticleColor;

            // Handle smooth color transition
            if (m_SmoothColorTransition && attachInfo.isTransitioning)
            {
                float elapsed = Time.time - attachInfo.attachTime;
                float transitionFactor = Mathf.Clamp01(elapsed / m_AttachmentDuration);
                targetColor = Color.Lerp(m_TransitionParticleColor, m_AttachedParticleColor, transitionFactor);
            }

            // Apply color to all attached particles
            foreach (int solverIndex in attachInfo.particleIndices)
            {
                SetParticleColor(solverIndex, targetColor);
            }
        }
    }

    private void RestoreParticleColors(AttachmentInfo attachInfo)
    {
        if (attachInfo?.particleIndices == null || attachInfo.originalColors == null) return;

        for (int i = 0; i < attachInfo.particleIndices.Count && i < attachInfo.originalColors.Count; i++)
        {
            int solverIndex = attachInfo.particleIndices[i];
            Color originalColor = attachInfo.originalColors[i];
            
            SetParticleColor(solverIndex, originalColor);
            m_OriginalParticleColors.Remove(solverIndex);
        }
    }

    public void RestoreAllParticleColors()
    {
        foreach (var kvp in m_OriginalParticleColors)
        {
            SetParticleColor(kvp.Key, kvp.Value);
        }
        
        m_OriginalParticleColors.Clear();
        
        if (m_ShowDebugInfo)
            Debug.Log("Restored all particle colors to original");
    }

    // Forceps detection system
    private void UpdateNearbyForcepsDetection()
    {
        m_NearbyForceps.Clear();
        
        var allForceps = FindObjectsOfType<ForcepsController>();
        
        foreach (var forceps in allForceps)
        {
            if (forceps != null && IsForcepsNearRope(forceps))
            {
                m_NearbyForceps.Add(forceps);
            }
        }

        // Debug logging (throttled to every 2 seconds)
        if (m_ShowDebugInfo && Time.frameCount % 120 == 0)
        {
            Debug.Log($"ObiRopeInteractable ({gameObject.name}): Found {m_NearbyForceps.Count} nearby forceps");
        }
    }

    private bool IsForcepsNearRope(ForcepsController forceps)
    {
        if (!IsRopeActorReady()) return false;

        // Get forceps attach points
        var ropeInteractor = forceps.GetComponent<RopeXRDirectInteractor>() ?? 
                           forceps.GetComponentInChildren<RopeXRDirectInteractor>();

        List<Vector3> checkPositions = GetForcepsCheckPositions(forceps, ropeInteractor);

        // Check distance to rope particles
        foreach (var position in checkPositions)
        {
            float distance = GetDistanceToNearestParticle(position);
            if (distance != float.MaxValue && distance <= m_DetectionRadius)
                return true;
        }

        return false;
    }

    private List<Vector3> GetForcepsCheckPositions(ForcepsController forceps, RopeXRDirectInteractor ropeInteractor)
    {
        List<Vector3> checkPositions = new List<Vector3>();
        
        if (ropeInteractor != null)
        {
            var attachPoints = GetRopeInteractorAttachPoints(ropeInteractor);
            foreach (var point in attachPoints)
            {
                if (point != null)
                    checkPositions.Add(point.position);
            }
        }
        
        // Fallback to forceps transform position
        if (checkPositions.Count == 0)
            checkPositions.Add(forceps.transform.position);

        return checkPositions;
    }

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
        catch
        {
            // Fallback: use main transform
            attachPoints.Add(interactor.transform);
        }

        return attachPoints;
    }

    private float GetDistanceToNearestParticle(Vector3 position)
    {
        if (!IsRopeActorReady()) return float.MaxValue;

        float minDistance = float.MaxValue;
        var solver = m_RopeActor.solver;
        int particleCount = Mathf.Min(m_RopeActor.particleCount, m_RopeActor.solverIndices.count);
        
        for (int i = 0; i < particleCount; i++)
        {
            try
            {
                int solverIndex = m_RopeActor.solverIndices[i];
                
                if (solverIndex < 0 || solverIndex >= solver.positions.count)
                    continue;
                
                Vector3 particlePos = solver.positions[solverIndex];
                float distance = Vector3.Distance(position, particlePos);
                if (distance < minDistance)
                    minDistance = distance;
            }
            catch
            {
                continue; // Skip problematic particles
            }
        }

        return minDistance;
    }

    // Attachment logic system
    private void ProcessAttachmentLogic()
    {
        // 1) Handle new attachments/detachments for nearby forceps
        foreach (var forceps in m_NearbyForceps)
        {
            if (forceps == null) continue;

            bool gripPressed = forceps.IsGripPressed;
            int forcepsId = forceps.GetInstanceID();
            bool isAttached = m_ActiveAttachments.ContainsKey(forcepsId);

            if (gripPressed && !isAttached)
            {
                TryAttachToForceps(forceps);
            }
            else if (!gripPressed && isAttached)
            {
                DetachFromForceps(forceps);
            }
        }

        // 2) Robust detachment pass for all active attachments regardless of proximity
        //    (fixes issue where grip is released outside detection radius and particles remain kinematic)
        if (m_ActiveAttachments.Count > 0)
        {
            // Copy to avoid collection modification during iteration
            var attachmentsSnapshot = new List<KeyValuePair<int, AttachmentInfo>>(m_ActiveAttachments);
            foreach (var kvp in attachmentsSnapshot)
            {
                int forcepsId = kvp.Key;
                var attachInfo = kvp.Value;
                var forceps = attachInfo.forceps;

                bool gripPressed = forceps != null && forceps.IsGripPressed;
                bool shouldDetach = !gripPressed; // detach if no forceps or grip not pressed

                if (shouldDetach)
                {
                    if (forceps != null)
                    {
                        DetachFromForceps(forceps);
                    }
                    else
                    {
                        // Forceps reference lost/destroyed: detach by id
                        DetachAttachmentById(forcepsId, attachInfo);
                    }
                }
            }
        }
    }

    private void TryAttachToForceps(ForcepsController forceps)
    {
        var attachmentData = FindBestAttachmentPoint(forceps);
        if (attachmentData == null) return;

        var attachInfo = CreateAttachmentInfo(forceps, attachmentData);
        var particlesToAttach = GetParticlesToAttach(attachmentData.particleIndex);
        var solver = m_RopeActor.solver;

        // Store particle data and apply attachment
        foreach (int particleIndex in particlesToAttach)
        {
            int solverIndex = m_RopeActor.solverIndices[particleIndex];
            
            StoreParticleData(attachInfo, solverIndex, solver);
            
            if (m_UseKinematicAttach)
                solver.invMasses[solverIndex] = 0f; // Make kinematic
        }

        // Calculate spatial relationship and apply visual feedback
        CalculateLocalOffset(attachInfo);
        ApplyInitialColoring(attachInfo);

        // Store attachment
        m_ActiveAttachments[forceps.GetInstanceID()] = attachInfo;

        if (m_ShowDebugInfo)
            Debug.Log($"Attached rope {gameObject.name} to forceps {forceps.name} with {attachInfo.particleIndices.Count} particles");
    }

    private AttachmentInfo CreateAttachmentInfo(ForcepsController forceps, AttachmentData attachmentData)
    {
        return new AttachmentInfo
        {
            forceps = forceps,
            attachPoint = attachmentData.attachPoint,
            attachTime = Time.time,
            isTransitioning = m_SmoothAttachment
        };
    }

    private void StoreParticleData(AttachmentInfo attachInfo, int solverIndex, ObiSolver solver)
    {
        attachInfo.particleIndices.Add(solverIndex);
        attachInfo.originalPositions.Add(solver.positions[solverIndex]);
        attachInfo.originalInvMasses.Add(solver.invMasses[solverIndex]);

        if (m_EnableAttachmentColoring)
        {
            Color originalColor = m_RopeActor.GetParticleColor(solverIndex);
            attachInfo.originalColors.Add(originalColor);
            StoreOriginalParticleColor(solverIndex);
        }
    }

    private void CalculateLocalOffset(AttachmentInfo attachInfo)
    {
        Vector3 centerPos = CalculateParticleCenter(attachInfo.particleIndices);
        attachInfo.localOffset = attachInfo.attachPoint.InverseTransformPoint(centerPos);
    }

    private void ApplyInitialColoring(AttachmentInfo attachInfo)
    {
        if (!m_EnableAttachmentColoring) return;

        Color initialColor = m_SmoothColorTransition ? m_TransitionParticleColor : m_AttachedParticleColor;
        foreach (int solverIndex in attachInfo.particleIndices)
        {
            SetParticleColor(solverIndex, initialColor);
        }
    }

    private AttachmentData FindBestAttachmentPoint(ForcepsController forceps)
    {
        if (!IsRopeActorReady()) return null;

        var ropeInteractor = forceps.GetComponent<RopeXRDirectInteractor>() ?? 
                           forceps.GetComponentInChildren<RopeXRDirectInteractor>();

        List<Transform> attachPoints = new List<Transform>();
        
        if (ropeInteractor != null)
            attachPoints = GetRopeInteractorAttachPoints(ropeInteractor);
        
        if (attachPoints.Count == 0)
            attachPoints.Add(forceps.transform);

        return FindClosestParticleToAttachPoints(attachPoints);
    }

    private AttachmentData FindClosestParticleToAttachPoints(List<Transform> attachPoints)
    {
        AttachmentData bestAttachment = null;
        float bestDistance = float.MaxValue;

        foreach (var attachPoint in attachPoints)
        {
            if (attachPoint == null) continue;

            for (int i = 0; i < m_RopeActor.particleCount && i < m_RopeActor.solverIndices.count; i++)
            {
                try
                {
                    int solverIndex = m_RopeActor.solverIndices[i];
                    
                    if (solverIndex < 0 || solverIndex >= m_RopeActor.solver.positions.count)
                        continue;
                        
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
                catch
                {
                    continue; // Skip problematic particles
                }
            }
        }

        return bestAttachment;
    }

    private List<int> GetParticlesToAttach(int centerParticleIndex)
    {
        var particles = new List<int>();

        // Calculate start and end indices around center particle
        int startIndex = Mathf.Max(0, centerParticleIndex - m_AttachParticleCount / 2);
        int endIndex = Mathf.Min(m_RopeActor.particleCount - 1, startIndex + m_AttachParticleCount - 1);
        
        // Adjust start if we hit the end boundary
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

    private void DetachFromForceps(ForcepsController forceps)
    {
        int forcepsId = forceps.GetInstanceID();
        
        if (!m_ActiveAttachments.TryGetValue(forcepsId, out var attachInfo))
            return;

        // Restore original particle properties
        RestoreParticleProperties(attachInfo);

        // Restore original particle colors
        if (m_EnableAttachmentColoring)
            RestoreParticleColors(attachInfo);

        m_ActiveAttachments.Remove(forcepsId);

        if (m_ShowDebugInfo)
            Debug.Log($"Detached rope {gameObject.name} from forceps {forceps.name}");
    }

    private void RestoreParticleProperties(AttachmentInfo attachInfo)
    {
        var solver = m_RopeActor.solver;
        for (int i = 0; i < attachInfo.particleIndices.Count && i < attachInfo.originalInvMasses.Count; i++)
        {
            int solverIndex = attachInfo.particleIndices[i];
            // Safety: bounds check before restoring
            if (solverIndex >= 0 && solverIndex < solver.invMasses.count)
                solver.invMasses[solverIndex] = attachInfo.originalInvMasses[i];

            // Clear any accumulated external forces from dynamic attachment mode
            if (solver.externalForces != null && solverIndex >= 0 && solverIndex < solver.externalForces.count)
                solver.externalForces[solverIndex] = Vector4.zero;
        }
    }

    // Helper to detach when we only have the attachment id (forceps may be destroyed or out of range)
    private void DetachAttachmentById(int forcepsId, AttachmentInfo attachInfo)
    {
        if (attachInfo == null) return;

        // Restore original particle properties and colors
        RestoreParticleProperties(attachInfo);
        if (m_EnableAttachmentColoring)
            RestoreParticleColors(attachInfo);

        m_ActiveAttachments.Remove(forcepsId);

        if (m_ShowDebugInfo)
            Debug.Log($"Detached rope {gameObject.name} from forceps id {forcepsId}");
    }

    // Position update system for attached particles
    private void UpdateActiveAttachments()
    {
        foreach (var attachInfo in m_ActiveAttachments.Values)
        {
            if (attachInfo?.attachPoint != null)
                UpdateAttachmentPosition(attachInfo);
        }
    }

    private void UpdateAttachmentPosition(AttachmentInfo attachInfo)
    {
        Vector3 targetPosition = attachInfo.attachPoint.TransformPoint(attachInfo.localOffset);
        var solver = m_RopeActor.solver;

        // Handle smooth transition
        float transitionFactor = CalculateTransitionFactor(attachInfo);

        // Apply position update
        Vector3 currentCenter = CalculateParticleCenter(attachInfo.particleIndices);
        Vector3 offset = (targetPosition - currentCenter) * transitionFactor;

        ApplyOffsetToParticles(attachInfo, offset, solver);
    }

    private float CalculateTransitionFactor(AttachmentInfo attachInfo)
    {
        if (!attachInfo.isTransitioning) return 1f;

        float elapsed = Time.time - attachInfo.attachTime;
        float transitionFactor = Mathf.Clamp01(elapsed / m_AttachmentDuration);
        
        if (transitionFactor >= 1f)
            attachInfo.isTransitioning = false;

        return transitionFactor;
    }

    private void ApplyOffsetToParticles(AttachmentInfo attachInfo, Vector3 offset, ObiSolver solver)
    {
        foreach (int solverIndex in attachInfo.particleIndices)
        {
            if (m_UseKinematicAttach)
            {
                // Direct position update for kinematic attachment
                Vector3 newPos = (Vector3)solver.positions[solverIndex] + offset;
                solver.positions[solverIndex] = newPos;
            }
            else
            {
                // Force-based update for dynamic attachment
                Vector3 force = offset * m_AttachForceMultiplier;
                solver.externalForces[solverIndex] += (Vector4)force;
            }
        }
    }

    // Public API methods
    public void DetachAll()
    {
        var attachmentsCopy = new Dictionary<int, AttachmentInfo>(m_ActiveAttachments);
        
        foreach (var attachInfo in attachmentsCopy.Values)
        {
            if (attachInfo == null) continue;

            if (attachInfo.forceps != null)
            {
                DetachFromForceps(attachInfo.forceps);
            }
            else
            {
                // Forceps lost: detach using id lookup
                foreach (var kvp in m_ActiveAttachments)
                {
                    if (kvp.Value == attachInfo)
                    {
                        DetachAttachmentById(kvp.Key, attachInfo);
                        break;
                    }
                }
            }
        }

        if (m_EnableAttachmentColoring)
            RestoreAllParticleColors();
    }

    public bool IsAttachedTo(ForcepsController forceps)
    {
        return forceps != null && m_ActiveAttachments.ContainsKey(forceps.GetInstanceID());
    }

    public int GetAttachmentCount()
    {
        return m_ActiveAttachments.Count;
    }

#if UNITY_EDITOR
    // Debug visualization in editor
    private void OnDrawGizmosSelected()
    {
        if (!m_ShowDebugGizmos || !m_EnableInteraction || !IsRopeActorReady()) return;

        DrawDetectionRadius();
        DrawActiveAttachments();
    }

    private void DrawDetectionRadius()
    {
        UnityEditor.Handles.color = Color.cyan;
        int particleCount = Mathf.Min(m_RopeActor.particleCount, m_RopeActor.solverIndices.count);
        
        for (int i = 0; i < particleCount; i++)
        {
            try
            {
                int solverIndex = m_RopeActor.solverIndices[i];
                if (solverIndex >= 0 && solverIndex < m_RopeActor.solver.positions.count)
                {
                    Vector3 particlePos = m_RopeActor.solver.positions[solverIndex];
                    UnityEditor.Handles.DrawWireDisc(particlePos, Vector3.up, m_DetectionRadius);
                }
            }
            catch
            {
                continue; // Skip problematic particles in debug view
            }
        }
    }

    private void DrawActiveAttachments()
    {
        UnityEditor.Handles.color = Color.red;
        foreach (var attachInfo in m_ActiveAttachments.Values)
        {
            if (attachInfo?.attachPoint != null)
            {
                Vector3 targetPos = attachInfo.attachPoint.TransformPoint(attachInfo.localOffset);
                Vector3 currentCenter = CalculateParticleCenter(attachInfo.particleIndices);
                
                UnityEditor.Handles.DrawLine(currentCenter, targetPos);
                UnityEditor.Handles.DrawWireDisc(targetPos, Vector3.up, 0.005f);
            }
        }
    }
#endif
}
