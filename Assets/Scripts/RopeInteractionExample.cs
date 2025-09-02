using UnityEngine;
using Obi;

/* ============================================================= */
/*              Rope Interaction Usage Example                  */
/*    Demonstrates how to set up and use ObiRopeInteractable    */
/* ============================================================= */

public class RopeInteractionExample : MonoBehaviour
{
    [Header("Setup Example")]
    [SerializeField]
    [Tooltip("The rope GameObject with ObiActor")]
    private GameObject ropeObject;

    [SerializeField] 
    [Tooltip("The forceps GameObject with ForcepsController")]
    private GameObject forcepsObject;

    [Header("Runtime Info")]
    [SerializeField]
    [Tooltip("Show runtime interaction status")]
    private bool showRuntimeInfo = true;

    private ObiRopeInteractable ropeInteractable;
    private ForcepsController forcepsController;

    void Start()
    {
        SetupRopeInteraction();
    }

    /// <summary>
    /// Automatically set up rope interaction components
    /// </summary>
    [ContextMenu("Setup Rope Interaction")]
    public void SetupRopeInteraction()
    {
        // Find rope if not assigned
        if (ropeObject == null)
        {
            var obiActor = FindObjectOfType<ObiActor>();
            if (obiActor != null)
                ropeObject = obiActor.gameObject;
        }

        // Find forceps if not assigned  
        if (forcepsObject == null)
        {
            forcepsController = FindObjectOfType<ForcepsController>();
            if (forcepsController != null)
                forcepsObject = forcepsController.gameObject;
        }

        // Add ObiRopeInteractable to rope if missing
        if (ropeObject != null)
        {
            ropeInteractable = ropeObject.GetComponent<ObiRopeInteractable>();
            if (ropeInteractable == null)
            {
                ropeInteractable = ropeObject.AddComponent<ObiRopeInteractable>();
                Debug.Log($"Added ObiRopeInteractable to {ropeObject.name}");
            }

            // Ensure rope has correct tag
            if (!ropeObject.CompareTag("Rope"))
            {
                ropeObject.tag = "Rope";
                Debug.Log($"Set rope tag on {ropeObject.name}");
            }
        }

        // Ensure forceps has RopeXRDirectInteractor
        if (forcepsObject != null)
        {
            var ropeInteractor = forcepsObject.GetComponent<RopeXRDirectInteractor>();
            if (ropeInteractor == null)
            {
                ropeInteractor = forcepsObject.AddComponent<RopeXRDirectInteractor>();
                Debug.Log($"Added RopeXRDirectInteractor to {forcepsObject.name}");
            }

            // Set up attach points on RopeXRDirectInteractor
            SetupAttachPoints(ropeInteractor);
        }

        Debug.Log("Rope interaction setup complete!");
    }

    /// <summary>
    /// Set up attach points for the rope interactor
    /// </summary>
    private void SetupAttachPoints(RopeXRDirectInteractor interactor)
    {
        // Try to find clamp transforms for attach points
        var forcepsController = forcepsObject.GetComponent<ForcepsController>();
        if (forcepsController != null)
        {
            // Use reflection to access private fields safely
            var upperClampField = typeof(ForcepsController).GetField("_upperClamp", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lowerClampField = typeof(ForcepsController).GetField("_lowerClamp",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var attachPoints = new System.Collections.Generic.List<Transform>();

            if (upperClampField != null)
            {
                var upperClamp = upperClampField.GetValue(forcepsController) as Transform;
                if (upperClamp != null) attachPoints.Add(upperClamp);
            }

            if (lowerClampField != null)
            {
                var lowerClamp = lowerClampField.GetValue(forcepsController) as Transform;
                if (lowerClamp != null) attachPoints.Add(lowerClamp);
            }

            // Fallback: use main transform
            if (attachPoints.Count == 0)
                attachPoints.Add(forcepsObject.transform);

            // Set attach points
            interactor.SetAttachPoints(attachPoints);
            Debug.Log($"Set up {attachPoints.Count} attach points for RopeXRDirectInteractor");
        }
    }

    void Update()
    {
        if (showRuntimeInfo && ropeInteractable != null)
        {
            // Display runtime information every 2 seconds
            if (Time.frameCount % 120 == 0)
            {
                Debug.Log($"Rope Interaction Status - Attached: {ropeInteractable.IsAttached}, " +
                         $"Attachment Count: {ropeInteractable.GetAttachmentCount()}, " +
                         $"Interaction Enabled: {ropeInteractable.EnableInteraction}");
            }
        }
    }

    /// <summary>
    /// Test methods for debugging
    /// </summary>
    [ContextMenu("Test - Enable Rope Interaction")]
    public void EnableRopeInteraction()
    {
        if (ropeInteractable != null)
        {
            ropeInteractable.EnableInteraction = true;
            Debug.Log("Rope interaction enabled");
        }
    }

    [ContextMenu("Test - Disable Rope Interaction")]
    public void DisableRopeInteraction()
    {
        if (ropeInteractable != null)
        {
            ropeInteractable.EnableInteraction = false;
            Debug.Log("Rope interaction disabled");
        }
    }

    [ContextMenu("Test - Detach All")]
    public void DetachAll()
    {
        if (ropeInteractable != null)
        {
            ropeInteractable.DetachAll();
            Debug.Log("Detached all rope attachments");
        }
    }

    [ContextMenu("Test - Adjust Detection Radius")]
    public void AdjustDetectionRadius()
    {
        if (ropeInteractable != null)
        {
            // Cycle through different detection radii
            float[] radii = { 0.01f, 0.02f, 0.05f, 0.1f };
            float currentRadius = ropeInteractable.DetectionRadius;
            
            int currentIndex = System.Array.IndexOf(radii, currentRadius);
            int nextIndex = (currentIndex + 1) % radii.Length;
            
            ropeInteractable.DetectionRadius = radii[nextIndex];
            Debug.Log($"Detection radius changed to: {radii[nextIndex]}");
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor validation
    /// </summary>
    void OnValidate()
    {
        if (ropeObject != null && ropeObject.GetComponent<ObiActor>() == null)
        {
            Debug.LogWarning("Rope object does not have an ObiActor component!", this);
        }

        if (forcepsObject != null && forcepsObject.GetComponent<ForcepsController>() == null)
        {
            Debug.LogWarning("Forceps object does not have a ForcepsController component!", this);
        }
    }
#endif
}
