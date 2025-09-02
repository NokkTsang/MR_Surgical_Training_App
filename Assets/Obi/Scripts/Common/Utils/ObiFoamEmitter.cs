using UnityEngine;
using System;

namespace Obi
{

    [AddComponentMenu("Physics/Obi/Obi Foam Emitter", 1000)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiActor))]
    [DisallowMultipleComponent]
    public class ObiFoamEmitter : ObiFoamGenerator
    {
        public enum ShapeType
        {
            Cylinder = 0,
            Box = 1
        }

        [Header("Emission shape")] 
        public ShapeType shape;
        public Transform shapeTransform;
        public Vector3 shapeSize = Vector3.one;

        private float emissionAccumulator = 0;

        public int GetParticleNumberToEmit(float deltaTime)
        {
            emissionAccumulator += foamGenerationRate * deltaTime;
            int particles = (int)emissionAccumulator;
            emissionAccumulator -= particles;
            return particles;
        }

        public void Reset()
        {
            emissionAccumulator = 0;
        }
    }
}
