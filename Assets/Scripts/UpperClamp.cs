using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpperClamp : MonoBehaviour
{
    [SerializeField]
    private ForcepsController parentForceps;

    private ForcepsController cachedForcepsController;

    /// <summary>
    /// Get the forceps controller (with automatic fallback search)
    /// </summary>
    private ForcepsController ForcepsController
    {
        get
        {
            if (cachedForcepsController == null)
            {
                // Try manual reference first
                if (parentForceps != null)
                {
                    cachedForcepsController = parentForceps;
                }
                else
                {
                    // Fallback: try to find in parent hierarchy
                    cachedForcepsController = GetComponentInParent<ForcepsController>();
                    
                    // If still not found, try to find in root
                    if (cachedForcepsController == null)
                        cachedForcepsController = FindObjectOfType<ForcepsController>();
                }
            }
            return cachedForcepsController;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ForcepsController != null)
        {
            ForcepsController.OnUpperTriggerEnter(other.gameObject);
        }
        else
        {
            Debug.LogError("UpperClamp: No ForcepsController found! Please assign parentForceps in the inspector or ensure ForcepsController exists in the scene.", this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (ForcepsController != null)
        {
            ForcepsController.OnUpperTriggerExit(other.gameObject);
        }
        else
        {
            Debug.LogError("UpperClamp: No ForcepsController found! Please assign parentForceps in the inspector or ensure ForcepsController exists in the scene.", this);
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        // Validate ForcepsController reference on start
        if (ForcepsController == null)
        {
            Debug.LogError("UpperClamp: Failed to find ForcepsController! Please assign parentForceps in the inspector.", this);
        }
        else
        {
            Debug.Log($"UpperClamp: Successfully connected to ForcepsController: {ForcepsController.name}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
