using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi.Samples
{
    [RequireComponent(typeof(Rigidbody))]
	public class RigidbodyMaxAngularVel : MonoBehaviour
	{
		public float maxAngularVelocity = 20;

		// Start is called before the first frame update
		void Start()
		{
			GetComponent<Rigidbody>().maxAngularVelocity = maxAngularVelocity;
		}

		// Update is called once per frame
		void Update()
		{

		}
	}
}
