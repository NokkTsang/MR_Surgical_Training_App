using UnityEngine;
using Unity.Profiling;
using System;
using System.Collections;

namespace Obi{

    /**
	 * Small helper class that lets you specify Obi-only properties for rigidbodies.
	 */

    [ExecuteInEditMode]
    public abstract class ObiRigidbodyBase : MonoBehaviour
    {

        public bool kinematicForParticles = false;

        protected ObiRigidbodyHandle rigidbodyHandle;
        public ObiRigidbodyHandle Handle
        {
            get
            {
                // don't check rigidbodyHandle.isValid:
                // CreateRigidbody may defer creation, so we get a non-null, but invalid handle.
                // If calling handle again right away before it becomes valid, it will call CreateRigidbody() again and create a second handle to the same body.
                if (rigidbodyHandle == null) 
                {
                    var world = ObiColliderWorld.GetInstance();

                    // create the material:
                    rigidbodyHandle = world.CreateRigidbody();
                    rigidbodyHandle.owner = this;
                }
                return rigidbodyHandle;
            }
        }

        protected virtual void OnEnable()
        {
            rigidbodyHandle = ObiColliderWorld.GetInstance().CreateRigidbody();
            rigidbodyHandle.owner = this;
        }

		public void OnDisable()
        {
            ObiColliderWorld.GetInstance().DestroyRigidbody(rigidbodyHandle);
        }

		public abstract void UpdateIfNeeded(float stepTime);

		/**
		 * Reads velocities back from the solver.
		 */
		public abstract void UpdateVelocities(Vector3 linearDelta, Vector3 angularDelta);

	}
}

