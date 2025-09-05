using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/* ============================================================= */
/*      Extended XRDirectInteractor for Forceps Support        */
/*      Provides attach points for rope interaction via        */
/*      ObiRopeInteractable component                           */
/* ============================================================= */

[AddComponentMenu("XR/Rope XR Direct Interactor", 13)]
public class RopeXRDirectInteractor : XRDirectInteractor
{
    [Header("Forceps Attach Points")]
    [SerializeField]
    [Range(1, 10)]
    [Tooltip("Number of attach points on the forceps")]
    private int m_NumberOfAttachPoints = 3;

    [SerializeField]
    [Tooltip("List of attach points on the forceps")]
    private List<Transform> m_AttachPoints = new List<Transform>();

    [Header("Debug Settings")]
    [SerializeField]
    [Tooltip("Show debug visualization for attach points")]
    private bool m_ShowDebug = true;

    // Public API
    public List<Transform> AttachPoints => new List<Transform>(m_AttachPoints);

    public int NumberOfAttachPoints
    {
        get => m_NumberOfAttachPoints;
        set
        {
            m_NumberOfAttachPoints = Mathf.Clamp(value, 1, 10);
            ResizeAttachPointsList();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        ResizeAttachPointsList();

        if (m_ShowDebug)
            Debug.Log("RopeXRDirectInteractor initialized");
    }

    // Attach points management
    private void ResizeAttachPointsList()
    {
        if (m_AttachPoints == null)
            m_AttachPoints = new List<Transform>();

        while (m_AttachPoints.Count < m_NumberOfAttachPoints)
            m_AttachPoints.Add(null);

        while (m_AttachPoints.Count > m_NumberOfAttachPoints)
            m_AttachPoints.RemoveAt(m_AttachPoints.Count - 1);
    }

    public void SetAttachPoints(List<Transform> attachPoints)
    {
        m_AttachPoints = attachPoints ?? new List<Transform>();
        m_NumberOfAttachPoints = m_AttachPoints.Count;
    }

    public void SetAttachPoints(params Transform[] attachPoints)
    {
        m_AttachPoints = new List<Transform>(attachPoints);
        m_NumberOfAttachPoints = m_AttachPoints.Count;
    }

    public void AddAttachPoint(Transform attachPoint)
    {
        if (m_AttachPoints == null)
            m_AttachPoints = new List<Transform>();

        m_AttachPoints.Add(attachPoint);
        m_NumberOfAttachPoints = m_AttachPoints.Count;
    }

    public void RemoveAttachPoint(Transform attachPoint)
    {
        if (m_AttachPoints != null && m_AttachPoints.Remove(attachPoint))
            m_NumberOfAttachPoints = m_AttachPoints.Count;
    }

    public void ClearAttachPoints()
    {
        if (m_AttachPoints != null)
        {
            m_AttachPoints.Clear();
            m_NumberOfAttachPoints = 0;
        }
    }

    public Transform GetClosestAttachPoint(Vector3 position)
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (var point in m_AttachPoints)
        {
            if (point == null) continue;

            float distance = Vector3.Distance(point.position, position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = point;
            }
        }

        return closest ?? transform;
    }

    public Vector3 GetBestAttachPosition()
    {
        if (m_AttachPoints != null && m_AttachPoints.Count > 0)
        {
            int middleIndex = m_AttachPoints.Count / 2;
            if (m_AttachPoints[middleIndex] != null)
                return m_AttachPoints[middleIndex].position;

            foreach (var point in m_AttachPoints)
            {
                if (point != null)
                    return point.position;
            }
        }

        return transform.position;
    }

    // Integration with ObiRopeInteractable
    public void StartRopeGrab()
    {
        var ropeInteractable = GetComponent<ObiRopeInteractable>();
        if (ropeInteractable != null && ropeInteractable.EnableInteraction)
        {
            if (m_ShowDebug)
                Debug.Log("RopeXRDirectInteractor: Delegating rope grab to ObiRopeInteractable");
        }
        else if (m_ShowDebug)
        {
            Debug.LogWarning("RopeXRDirectInteractor: No ObiRopeInteractable component found");
        }
    }

    public void StopRopeGrab()
    {
        var ropeInteractable = GetComponent<ObiRopeInteractable>();
        if (ropeInteractable != null)
        {
            ropeInteractable.DetachAll();
            if (m_ShowDebug)
                Debug.Log("RopeXRDirectInteractor: Delegated rope release to ObiRopeInteractable");
        }
    }

    public bool IsGrabbingRope()
    {
        var ropeInteractable = GetComponent<ObiRopeInteractable>();
        return ropeInteractable != null && ropeInteractable.IsAttached;
    }

    // XR Integration
    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactableObject == null)
        {
            StartRopeGrab();
        }
        else
        {
            base.OnSelectEntered(args);
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        StopRopeGrab();
        base.OnSelectExited(args);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopRopeGrab();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!m_ShowDebug) return;

        Color[] colors = { 
            Color.red, Color.yellow, Color.green, Color.blue, Color.magenta, 
            Color.cyan, Color.white, new Color(1f, 0.5f, 0f), 
            new Color(0.5f, 0f, 1f), new Color(0f, 1f, 0.5f) 
        };

        for (int i = 0; i < m_AttachPoints.Count; i++)
        {
            if (m_AttachPoints[i] == null) continue;

            Color pointColor = colors[i % colors.Length];
            UnityEditor.Handles.color = pointColor;
            UnityEditor.Handles.DrawWireDisc(m_AttachPoints[i].position, Vector3.up, 0.003f);
            UnityEditor.Handles.Label(m_AttachPoints[i].position + Vector3.up * 0.01f, i.ToString());
        }

        UnityEditor.Handles.color = Color.gray;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, 0.005f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.015f, "Main");
    }

    private void OnValidate()
    {
        m_NumberOfAttachPoints = Mathf.Clamp(m_NumberOfAttachPoints, 1, 10);
        ResizeAttachPointsList();
        
        if (m_AttachPoints != null)
        {
            int nullCount = 0;
            for (int i = 0; i < m_AttachPoints.Count; i++)
            {
                if (m_AttachPoints[i] == null)
                    nullCount++;
            }
            
            if (nullCount > 0)
                Debug.LogWarning($"RopeXRDirectInteractor has {nullCount} unassigned attach points", this);
        }

        if (GetComponent<ObiRopeInteractable>() == null)
        {
            Debug.LogWarning("RopeXRDirectInteractor: Consider adding ObiRopeInteractable component", this);
        }
    }
#endif
}