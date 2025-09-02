using UnityEngine;

namespace Obi
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiCollider))]
	public class ObiForceZone : MonoBehaviour
	{
        [SerializeProperty("sourceCollider")]
        [SerializeField] private ObiCollider m_SourceCollider;

        protected ObiForceZoneHandle forcezoneHandle;

        /// <summary>
        /// The ObiCollider this ObiForceZone should affect.
        /// </summary>
        /// This is automatically set when you first create the ObiForceZone component, but you can override it afterwards.
        public ObiCollider SourceCollider
        {
            set
            {
                if (value != null && value.gameObject != this.gameObject)
                {
                    Debug.LogError("The ObiCollider component must reside in the same GameObject as ObiForceZone.");
                    return;
                }

                RemoveCollider();
                m_SourceCollider = value;
                AddCollider();

            }
            get { return m_SourceCollider; }
        }

        public ObiForceZoneHandle Handle
        {
            get
            {
                // don't check forcezoneHandle.isValid:
                // CreateForceZone may defer creation, so we get a non-null, but invalid handle.
                // If calling handle again right away before it becomes valid, it will call CreateForceZone again and create a second handle to the same zone.
                if (forcezoneHandle == null)
                {
                    var world = ObiColliderWorld.GetInstance();

                    // create the material:
                    forcezoneHandle = world.CreateForceZone();
                    forcezoneHandle.owner = this;
                }
                return forcezoneHandle;
            }
        }

        public ForceZone.ZoneType type;
        public ForceZone.ForceMode mode;
        public float intensity;

        [Header("Damping")]
        public ForceZone.DampingDirection dampingDir;
        public float damping = 0;

        [Header("Falloff")]
        public float minDistance;
        public float maxDistance;
        [Min(0)]
        public float falloffPower = 1;

        [Header("Tint")]
        public Color color = Color.clear;

        [Header("Pulse")]
        public float pulseIntensity;
        public float pulseFrequency;
        public float pulseSeed;

        protected float intensityVariation;

        public void OnEnable()
        {
            forcezoneHandle = ObiColliderWorld.GetInstance().CreateForceZone();
            forcezoneHandle.owner = this;
            FindSourceCollider();
        }

        public void OnDisable()
        {
            RemoveCollider();
            ObiColliderWorld.GetInstance().DestroyForceZone(forcezoneHandle);
        }

        private void FindSourceCollider()
        {
            if (SourceCollider == null)
                SourceCollider = GetComponent<ObiCollider>();
            else
                AddCollider();
        }

        private void AddCollider()
        {
            if (m_SourceCollider != null)
                m_SourceCollider.ForceZone = this;
        }

        private void RemoveCollider()
        {
            if (m_SourceCollider != null)
                m_SourceCollider.ForceZone = null;
        }

        public virtual void UpdateIfNeeded()
        {
            if (!Handle.isValid)
                return;

            var fc = ObiColliderWorld.GetInstance().forceZones[Handle.index];
            fc.type = type;
            fc.mode = mode;
            fc.intensity = intensity + intensityVariation;
            fc.minDistance = minDistance;
            fc.maxDistance = maxDistance;
            fc.falloffPower = falloffPower;
            fc.damping = damping;
            fc.dampingDir = dampingDir;
            fc.color = color;
            ObiColliderWorld.GetInstance().forceZones[Handle.index] = fc;
        }

        public void Update()
        {
            if (Application.isPlaying)
                intensityVariation = Mathf.PerlinNoise(Time.time * pulseFrequency, pulseSeed) * pulseIntensity;
        }
    }
}

