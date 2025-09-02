using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Obi;

/* ============================================================= */
/*      Extended XRDirectInteractor for Obi Rope Support       */
/*      Enables forceps to grab and manipulate rope segments    */
/* ============================================================= */

[AddComponentMenu("XR/Rope XR Direct Interactor", 13)]
public class RopeXRDirectInteractor : XRDirectInteractor
{
    [Header("Rope Interaction Settings")]
    [SerializeField]
    [Tooltip("Enable rope segment interaction. When enabled, forceps can grab individual rope segments.")]
    private bool m_EnableRopeInteraction = true;

    [SerializeField]
    [Range(0.001f, 0.1f)]
    [Tooltip("Detection radius for rope particles. Smaller values require more precision.")]
    private float m_RopeDetectionRadius = 0.01f;

    [SerializeField]
    [Range(1, 10)]
    [Tooltip("Number of consecutive rope particles to grab for better control.")]
    private int m_RopeGrabParticleCount = 3;

    [SerializeField]
    [Range(0.1f, 2.0f)]
    [Tooltip("Force multiplier for rope particle manipulation.")]
    private float m_RopeForceMultiplier = 1.0f;

    [SerializeField]
    [Tooltip("Use kinematic mode for grabbed rope particles (more stable but less realistic).")]
    private bool m_UseKinematicGrab = true;

    [SerializeField]
    [Tooltip("Show debug visualization for rope interactions.")]
    private bool m_ShowRopeDebug = true;

    [Header("Forceps Attach Points")]
    [SerializeField]
    [Range(1, 10)]
    [Tooltip("Number of attach points on the forceps")]
    private int m_NumberOfAttachPoints = 3;

    [SerializeField]
    [Tooltip("List of attach points on the forceps (will be resized based on number of points)")]
    private List<Transform> m_AttachPoints = new List<Transform>();

    // Rope interaction data
    public class RopeGrabInfo
    {
        public ObiActor ropeActor;
        public List<int> grabbedParticleIndices = new List<int>();
        public List<Vector3> originalPositions = new List<Vector3>();
        public List<float> originalInvMasses = new List<float>();
        public Transform attachTransform;
        public Vector3 localOffset;
    }

    private Dictionary<string, RopeGrabInfo> m_ActiveRopeGrabs = new Dictionary<string, RopeGrabInfo>();
    private List<ObiActor> m_NearbyRopes = new List<ObiActor>();
    private bool m_IsGrabbingRope = false;

    /// <summary>
    /// Whether rope interaction is enabled
    /// </summary>
    public bool enableRopeInteraction
    {
        get => m_EnableRopeInteraction;
        set => m_EnableRopeInteraction = value;
    }

    /// <summary>
    /// Detection radius for rope particles
    /// </summary>
    public float ropeDetectionRadius
    {
        get => m_RopeDetectionRadius;
        set => m_RopeDetectionRadius = Mathf.Max(0.001f, value);
    }

    /// <summary>
    /// Resize the attach points list based on the number of points
    /// </summary>
    private void ResizeAttachPointsList()
    {
        if (m_AttachPoints == null)
            m_AttachPoints = new List<Transform>();

        // Resize the list to match the number of attach points
        while (m_AttachPoints.Count < m_NumberOfAttachPoints)
        {
            m_AttachPoints.Add(null);
        }

        while (m_AttachPoints.Count > m_NumberOfAttachPoints)
        {
            m_AttachPoints.RemoveAt(m_AttachPoints.Count - 1);
        }
    }

    /// <summary>
    /// Number of rope particles to grab
    /// </summary>
    public int ropeGrabParticleCount
    {
        get => m_RopeGrabParticleCount;
        set => m_RopeGrabParticleCount = Mathf.Clamp(value, 1, 10);
    }





    /// <summary>
    /// Set the attach points for the forceps
    /// </summary>
    public void SetAttachPoints(List<Transform> attachPoints)
    {
        m_AttachPoints = attachPoints ?? new List<Transform>();
        m_NumberOfAttachPoints = m_AttachPoints.Count;
    }

    /// <summary>
    /// Set the attach points for the forceps (params version for convenience)
    /// </summary>
    public void SetAttachPoints(params Transform[] attachPoints)
    {
        m_AttachPoints = new List<Transform>(attachPoints);
        m_NumberOfAttachPoints = m_AttachPoints.Count;
    }

    /// <summary>
    /// Add an attach point to the list
    /// </summary>
    public void AddAttachPoint(Transform attachPoint)
    {
        if (m_AttachPoints == null)
            m_AttachPoints = new List<Transform>();
        
        m_AttachPoints.Add(attachPoint);
        m_NumberOfAttachPoints = m_AttachPoints.Count;
    }

    /// <summary>
    /// Remove an attach point from the list
    /// </summary>
    public void RemoveAttachPoint(Transform attachPoint)
    {
        if (m_AttachPoints != null && m_AttachPoints.Remove(attachPoint))
        {
            m_NumberOfAttachPoints = m_AttachPoints.Count;
        }
    }

    /// <summary>
    /// Clear all attach points
    /// </summary>
    public void ClearAttachPoints()
    {
        if (m_AttachPoints != null)
        {
            m_AttachPoints.Clear();
            m_NumberOfAttachPoints = 0;
        }
    }



    protected override void Awake()
    {
        base.Awake();
        
        if (m_ActiveRopeGrabs == null)
            m_ActiveRopeGrabs = new Dictionary<string, RopeGrabInfo>();

        // Initialize attach points list
        ResizeAttachPointsList();

        if (m_ShowRopeDebug)
            Debug.Log("RopeXRDirectInteractor initialized with forceps-style rope interaction support.");
    }

    private void Update()
    {
        if (m_EnableRopeInteraction)
        {
            UpdateRopeInteractions();
            UpdateGrabbedRopeParticles();
        }
    }

    /// <summary>
    /// Update rope interactions and detection
    /// </summary>
    private void UpdateRopeInteractions()
    {
        // Find nearby ropes
        m_NearbyRopes.Clear();
        var obiActors = FindObjectsOfType<ObiActor>();
        
        if (m_ShowRopeDebug && Time.frameCount % 120 == 0) // Debug every 2 seconds
        {
            Debug.Log($"UpdateRopeInteractions: Found {obiActors.Length} ObiActors to check");
        }
        
        foreach (var actor in obiActors)
        {
            if (IsRopeNearby(actor))
            {
                m_NearbyRopes.Add(actor);
                if (m_ShowRopeDebug)
                    Debug.Log($"Added nearby rope: {actor.name}");
            }
        }

        if (m_ShowRopeDebug && Time.frameCount % 120 == 0)
        {
            Debug.Log($"UpdateRopeInteractions complete: {m_NearbyRopes.Count} nearby ropes detected");
        }
    }





    /// <summary>
    /// Check if a rope is nearby and can be interacted with
    /// </summary>
    private bool IsRopeNearby(ObiActor actor)
    {
        if (actor == null || actor.solver == null) return false;

        // Method 1: Check distance to all attach points
        bool foundNearAttachPoint = false;
        foreach (var attachPoint in m_AttachPoints)
        {
            if (attachPoint == null) continue;
            
            float distance = GetDistanceToRope(actor, attachPoint.position);
            if (distance <= m_RopeDetectionRadius * 3f) // Larger detection radius
            {
                foundNearAttachPoint = true;
                if (m_ShowRopeDebug)
                    Debug.Log($"Rope {actor.name} near attach point {attachPoint.name}, distance: {distance:F3}");
                break;
            }
        }

        // Method 2: If no attach points or none are close, check distance to main transform
        if (!foundNearAttachPoint)
        {
            float distance = GetDistanceToRope(actor, transform.position);
            if (distance <= m_RopeDetectionRadius * 5f) // Even larger radius for main transform
            {
                if (m_ShowRopeDebug)
                    Debug.Log($"Rope {actor.name} near main transform, distance: {distance:F3}");
                return true;
            }
        }

        return foundNearAttachPoint;
    }

    /// <summary>
    /// Get minimum distance from a position to any particle in the rope
    /// </summary>
    private float GetDistanceToRope(ObiActor actor, Vector3 position)
    {
        if (actor == null || actor.solver == null) return float.MaxValue;

        float minDistance = float.MaxValue;
        var solver = actor.solver;
        
        for (int i = 0; i < actor.particleCount; i++)
        {
            int solverIndex = actor.solverIndices[i];
            Vector3 particlePosition = (Vector3)solver.positions[solverIndex];
            
            float distance = Vector3.Distance(position, particlePosition);
            if (distance < minDistance)
                minDistance = distance;
        }

        return minDistance;
    }

    /// <summary>
    /// Try to grab rope at the current position
    /// </summary>
    public bool TryGrabRope()
    {
        if (m_ShowRopeDebug)
        {
            Debug.Log($"TryGrabRope called - Enabled: {m_EnableRopeInteraction}, Nearby ropes: {m_NearbyRopes.Count}, Already grabbing: {m_IsGrabbingRope}");
        }

        if (!m_EnableRopeInteraction || m_NearbyRopes.Count == 0 || m_IsGrabbingRope)
        {
            if (m_ShowRopeDebug)
            {
                if (!m_EnableRopeInteraction) Debug.Log("TryGrabRope failed: Rope interaction disabled");
                if (m_NearbyRopes.Count == 0) Debug.Log("TryGrabRope failed: No nearby ropes detected");
                if (m_IsGrabbingRope) Debug.Log("TryGrabRope failed: Already grabbing rope");
            }
            return false;
        }

        // Find the closest rope particle to grab
        ObiActor closestRope = null;
        int closestParticleIndex = -1;
        float closestDistance = float.MaxValue;
        Transform bestAttachTransform = null;

        Vector3 grabPosition = GetBestGrabPosition();

        if (m_ShowRopeDebug)
        {
            Debug.Log($"TryGrabRope: Searching from position {grabPosition}, detection radius: {m_RopeDetectionRadius}");
        }

        foreach (var rope in m_NearbyRopes)
        {
            if (rope == null || rope.solver == null) continue;

            for (int i = 0; i < rope.particleCount; i++)
            {
                int solverIndex = rope.solverIndices[i];
                Vector3 particlePosition = (Vector3)rope.solver.positions[solverIndex];
                
                float distance = Vector3.Distance(grabPosition, particlePosition);
                if (distance < closestDistance && distance <= m_RopeDetectionRadius)
                {
                    closestDistance = distance;
                    closestRope = rope;
                    closestParticleIndex = i;
                    bestAttachTransform = GetClosestAttachTransformToPosition(particlePosition);
                }
            }
        }

        if (closestRope != null && closestParticleIndex >= 0)
        {
            var grabInfo = CreateRopeGrabInfo(closestRope, closestParticleIndex, bestAttachTransform);
            string grabKey = $"{closestRope.GetInstanceID()}_{Time.time}";
            m_ActiveRopeGrabs[grabKey] = grabInfo;
            m_IsGrabbingRope = true;
            
            if (m_ShowRopeDebug)
                Debug.Log($"Successfully started rope grab on {closestRope.name} with key {grabKey}, distance: {closestDistance:F3}");
            
            return true;
        }

        if (m_ShowRopeDebug)
        {
            Debug.Log($"TryGrabRope failed: No rope particles within detection radius. Closest distance found: {(closestDistance == float.MaxValue ? "None" : closestDistance.ToString("F3"))}");
        }

        return false;
    }

    /// <summary>
    /// Get the best position for grabbing (closest attach transform or main transform)
    /// </summary>
    private Vector3 GetBestGrabPosition()
    {
        // Find the best attach point based on nearby ropes
        if (m_NearbyRopes.Count > 0)
        {
            Vector3 averageRopePosition = Vector3.zero;
            int validParticles = 0;

            foreach (var rope in m_NearbyRopes)
            {
                if (rope?.solver == null) continue;

                for (int i = 0; i < rope.particleCount; i++)
                {
                    int solverIndex = rope.solverIndices[i];
                    averageRopePosition += (Vector3)rope.solver.positions[solverIndex];
                    validParticles++;
                }
            }

            if (validParticles > 0)
            {
                averageRopePosition /= validParticles;
                Transform closestAttach = GetClosestAttachPoint(averageRopePosition);
                if (closestAttach != null)
                    return closestAttach.position;
            }
        }

        // Fallback to middle attach point or transform
        if (m_AttachPoints != null && m_AttachPoints.Count > 0)
        {
            // Try to find a middle point, or use the first available
            int middleIndex = m_AttachPoints.Count / 2;
            if (m_AttachPoints[middleIndex] != null)
                return m_AttachPoints[middleIndex].position;
            
            // Find first non-null attach point
            foreach (var point in m_AttachPoints)
            {
                if (point != null)
                    return point.position;
            }
        }
        
        return transform.position;
    }

    /// <summary>
    /// Find the closest attach point to a rope particle
    /// </summary>
    private Transform GetClosestAttachPoint(Vector3 ropePosition)
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (var point in m_AttachPoints)
        {
            if (point == null) continue;

            float distance = Vector3.Distance(point.position, ropePosition);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = point;
            }
        }

        return closest ?? transform;
    }



    /// <summary>
    /// Get the closest attach transform to a given position
    /// </summary>
    private Transform GetClosestAttachTransformToPosition(Vector3 position)
    {
        return GetClosestAttachPoint(position);
    }

    /// <summary>
    /// Create rope grab information
    /// </summary>
    private RopeGrabInfo CreateRopeGrabInfo(ObiActor rope, int centerParticleIndex, Transform attachTransform)
    {
        var grabInfo = new RopeGrabInfo
        {
            ropeActor = rope,
            attachTransform = attachTransform ?? transform
        };

        // Calculate which particles to grab around the center particle
        int startIndex = Mathf.Max(0, centerParticleIndex - m_RopeGrabParticleCount / 2);
        int endIndex = Mathf.Min(rope.particleCount - 1, startIndex + m_RopeGrabParticleCount - 1);

        // Adjust start index if we hit the end
        if (endIndex - startIndex + 1 < m_RopeGrabParticleCount)
        {
            startIndex = Mathf.Max(0, endIndex - m_RopeGrabParticleCount + 1);
        }

        // Store particle information
        var solver = rope.solver;
        Vector3 centerPosition = Vector3.zero;
        
        for (int i = startIndex; i <= endIndex; i++)
        {
            int solverIndex = rope.solverIndices[i];
            grabInfo.grabbedParticleIndices.Add(solverIndex);
            
            Vector3 particlePosition = (Vector3)solver.positions[solverIndex];
            grabInfo.originalPositions.Add(particlePosition);
            grabInfo.originalInvMasses.Add(solver.invMasses[solverIndex]);
            
            centerPosition += particlePosition;
        }

        // Calculate local offset from attach transform to rope center
        centerPosition /= grabInfo.grabbedParticleIndices.Count;
        grabInfo.localOffset = grabInfo.attachTransform.InverseTransformPoint(centerPosition);

        // Lock particles if using kinematic mode
        if (m_UseKinematicGrab)
        {
            LockRopeParticles(grabInfo);
        }

        if (m_ShowRopeDebug)
        {
            Debug.Log($"Grabbed rope with {grabInfo.grabbedParticleIndices.Count} particles at attach transform {grabInfo.attachTransform.name}");
        }

        return grabInfo;
    }

    /// <summary>
    /// Lock rope particles for kinematic grabbing
    /// </summary>
    private void LockRopeParticles(RopeGrabInfo grabInfo)
    {
        if (grabInfo?.ropeActor?.solver == null) return;

        var solver = grabInfo.ropeActor.solver;
        
        foreach (int particleIndex in grabInfo.grabbedParticleIndices)
        {
            // Set inverse mass to zero to make particles kinematic
            solver.invMasses[particleIndex] = 0f;
        }
    }

    /// <summary>
    /// Unlock rope particles
    /// </summary>
    private void UnlockRopeParticles(RopeGrabInfo grabInfo)
    {
        if (grabInfo?.ropeActor?.solver == null) return;

        var solver = grabInfo.ropeActor.solver;
        
        for (int i = 0; i < grabInfo.grabbedParticleIndices.Count && i < grabInfo.originalInvMasses.Count; i++)
        {
            int particleIndex = grabInfo.grabbedParticleIndices[i];
            solver.invMasses[particleIndex] = grabInfo.originalInvMasses[i];
        }
    }

    /// <summary>
    /// Update positions of grabbed rope particles
    /// </summary>
    private void UpdateGrabbedRopeParticles()
    {
        foreach (var kvp in m_ActiveRopeGrabs)
        {
            var grabInfo = kvp.Value;
            if (grabInfo?.ropeActor?.solver == null || grabInfo.attachTransform == null) continue;

            UpdateRopeParticlePositions(grabInfo);
        }
    }

    /// <summary>
    /// Update rope particle positions based on attach transform
    /// </summary>
    private void UpdateRopeParticlePositions(RopeGrabInfo grabInfo)
    {
        var solver = grabInfo.ropeActor.solver;
        Vector3 targetPosition = grabInfo.attachTransform.TransformPoint(grabInfo.localOffset);

        // Calculate center of grabbed particles
        Vector3 currentCenter = Vector3.zero;
        foreach (int particleIndex in grabInfo.grabbedParticleIndices)
        {
            currentCenter += (Vector3)solver.positions[particleIndex];
        }
        currentCenter /= grabInfo.grabbedParticleIndices.Count;

        // Calculate offset to apply
        Vector3 offset = targetPosition - currentCenter;

        // Apply offset to all grabbed particles
        foreach (int particleIndex in grabInfo.grabbedParticleIndices)
        {
            if (m_UseKinematicGrab)
            {
                // Direct position setting for kinematic mode
                solver.positions[particleIndex] = (Vector4)((Vector3)solver.positions[particleIndex] + offset);
            }
            else
            {
                // Apply force for dynamic mode
                Vector3 force = offset * m_RopeForceMultiplier;
                solver.externalForces[particleIndex] += (Vector4)force;
            }
        }
    }

    /// <summary>
    /// Manual method to start rope grabbing (call this on trigger press)
    /// </summary>
    public void StartRopeGrab()
    {
        if (!m_IsGrabbingRope)
        {
            TryGrabRope();
        }
    }

    /// <summary>
    /// Manual method to stop rope grabbing (call this on trigger release)
    /// </summary>
    public void StopRopeGrab()
    {
        ReleaseAllRopeGrabs();
    }

    /// <summary>
    /// Force release all rope grabs
    /// </summary>
    public void ReleaseAllRopeGrabs()
    {
        foreach (var kvp in m_ActiveRopeGrabs)
        {
            UnlockRopeParticles(kvp.Value);
            
            if (m_ShowRopeDebug)
                Debug.Log($"Released rope grab with key {kvp.Key}");
        }
        
        m_ActiveRopeGrabs.Clear();
        m_IsGrabbingRope = false;
        
        if (m_ShowRopeDebug)
            Debug.Log("Released all rope grabs");
    }

    /// <summary>
    /// Check if currently grabbing any rope
    /// </summary>
    public bool IsGrabbingRope()
    {
        return m_IsGrabbingRope && m_ActiveRopeGrabs.Count > 0;
    }

    /// <summary>
    /// Get information about active rope grabs
    /// </summary>
    public List<RopeGrabInfo> GetActiveRopeGrabs()
    {
        return new List<RopeGrabInfo>(m_ActiveRopeGrabs.Values);
    }

    /// <summary>
    /// Override to integrate rope grabbing with normal selection
    /// </summary>
    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        // If no regular interactable is selected, try rope grabbing
        if (args.interactableObject == null && m_EnableRopeInteraction)
        {
            TryGrabRope();
        }
        else
        {
            base.OnSelectEntered(args);
        }
    }

    /// <summary>
    /// Override to integrate rope releasing with normal selection
    /// </summary>
    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        // Always release rope grabs when selection ends
        if (m_IsGrabbingRope)
        {
            ReleaseAllRopeGrabs();
        }
        
        base.OnSelectExited(args);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ReleaseAllRopeGrabs();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draw debug gizmos for rope interaction
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!m_EnableRopeInteraction || !m_ShowRopeDebug) return;

        // Draw attach points and trigger zones
        Color[] predefinedColors = { Color.red, Color.yellow, Color.green, Color.blue, Color.magenta, Color.cyan, Color.white, new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f), new Color(0f, 1f, 0.5f) };

        for (int i = 0; i < m_AttachPoints.Count; i++)
        {
            if (m_AttachPoints[i] == null) continue;

            // Use predefined colors, cycling if we have more points than colors
            Color pointColor = predefinedColors[i % predefinedColors.Length];

            // Draw attach point
            UnityEditor.Handles.color = pointColor;
            UnityEditor.Handles.DrawWireDisc(m_AttachPoints[i].position, Vector3.up, 0.003f);

            // Draw point number label
            UnityEditor.Handles.Label(m_AttachPoints[i].position + Vector3.up * 0.01f, i.ToString());
        }

        // Draw rope detection radius around best grab position
        UnityEditor.Handles.color = Color.cyan;
        Vector3 grabPosition = GetBestGrabPosition();
        UnityEditor.Handles.DrawWireDisc(grabPosition, Vector3.up, m_RopeDetectionRadius);

        // Draw grabbed rope particles
        UnityEditor.Handles.color = Color.magenta;
        foreach (var grabInfo in m_ActiveRopeGrabs.Values)
        {
            if (grabInfo?.ropeActor?.solver == null) continue;

            var solver = grabInfo.ropeActor.solver;
            foreach (int particleIndex in grabInfo.grabbedParticleIndices)
            {
                Vector3 particlePos = (Vector3)solver.positions[particleIndex];
                UnityEditor.Handles.DrawWireDisc(particlePos, Vector3.up, 0.002f);
            }
        }
        
        // Draw connection lines to attach transforms
        UnityEditor.Handles.color = Color.white;
        foreach (var grabInfo in m_ActiveRopeGrabs.Values)
        {
            if (grabInfo?.attachTransform == null) continue;
            
            Vector3 targetPos = grabInfo.attachTransform.TransformPoint(grabInfo.localOffset);
            UnityEditor.Handles.DrawLine(grabInfo.attachTransform.position, targetPos);
        }
    }
#endif

#if UNITY_EDITOR
    /// <summary>
    /// Validate configuration in editor
    /// </summary>
    private void OnValidate()
    {
        // Ensure number of attach points is within valid range
        m_NumberOfAttachPoints = Mathf.Clamp(m_NumberOfAttachPoints, 1, 10);
        
        // Resize the attach points list to match the number
        ResizeAttachPointsList();
        
        // Warn if attach points are not assigned
        if (m_AttachPoints != null)
        {
            int nullCount = 0;
            for (int i = 0; i < m_AttachPoints.Count; i++)
            {
                if (m_AttachPoints[i] == null)
                    nullCount++;
            }
            
            if (nullCount > 0)
                Debug.LogWarning($"RopeXRDirectInteractor has {nullCount} unassigned attach points. Please assign all attach point transforms.", this);
        }
    }
#endif
}
