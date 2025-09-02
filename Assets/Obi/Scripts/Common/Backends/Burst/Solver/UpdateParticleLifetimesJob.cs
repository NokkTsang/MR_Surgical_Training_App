#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;
using System;
using System.Collections;
using System.Threading;

namespace Obi
{
    [BurstCompile]
    unsafe struct UpdateParticleLifetimesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> activeParticles;
        [NativeDisableParallelForRestriction] public NativeArray<float> life;

        [NativeDisableParallelForRestriction] public NativeArray<int> deadParticles;
        [NativeDisableContainerSafetyRestriction] public NativeReference<int> deadParticleCount;

        [ReadOnly] public float dt;

        public void Execute(int i)
        {
            int p = activeParticles[i];

            life[p] -= dt;

            if (life[p] <= 0)
            {
                int* countRef = (int*)deadParticleCount.GetUnsafePtr();
                int count = Interlocked.Increment(ref countRef[0]) - 1;
                deadParticles[count] = p;
                life[p] = 0;
            }
        }
    }
}
#endif