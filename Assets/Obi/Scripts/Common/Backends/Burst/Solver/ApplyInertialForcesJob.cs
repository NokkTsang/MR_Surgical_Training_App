#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System;
using System.Collections;

namespace Obi
{
    [BurstCompile]
    struct ApplyInertialForcesJob : IJobParallelFor
    {
		[ReadOnly] public NativeArray<int> activeParticles;
        [ReadOnly] public NativeArray<float4> positions;
		[ReadOnly] public NativeArray<float> invMasses;

		[ReadOnly] public float4 angularVel;
		[ReadOnly] public float4 inertialAccel;
		[ReadOnly] public float4 eulerAccel;

		[ReadOnly] public float worldLinearInertiaScale;
		[ReadOnly] public float worldAngularInertiaScale;

        [NativeDisableParallelForRestriction] public NativeArray<float4> velocities;
        [NativeDisableParallelForRestriction] public NativeArray<float4> wind;

        [ReadOnly] public float deltaTime;
        [ReadOnly] public float4 ambientWind;
        [ReadOnly] public BurstInertialFrame inertialFrame;
        [ReadOnly] public bool inertialWind;

        public void Execute(int index)
        {
            int i = activeParticles[index];

            if (invMasses[i] > 0)
			{
				float4 euler = new float4(math.cross(eulerAccel.xyz, positions[i].xyz), 0);
				float4 centrifugal = new float4(math.cross(angularVel.xyz, math.cross(angularVel.xyz, positions[i].xyz)), 0);
				float4 coriolis = 2 * new float4(math.cross(angularVel.xyz, velocities[i].xyz), 0);
				float4 angularAccel = euler + coriolis + centrifugal;

				velocities[i] -= (inertialAccel * worldLinearInertiaScale + angularAccel * worldAngularInertiaScale) * deltaTime;
			}

            wind[i] = ambientWind;

            if (inertialWind)
            {
                float4 wsPos = inertialFrame.frame.TransformPoint(positions[i]);
                wind[i] -= inertialFrame.frame.InverseTransformVector(inertialFrame.VelocityAtPoint(wsPos));
            }
        }
    }
}
#endif