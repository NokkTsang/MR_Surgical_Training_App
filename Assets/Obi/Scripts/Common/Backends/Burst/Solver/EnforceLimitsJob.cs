#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace Obi
{
    [BurstCompile]
    struct EnforceLimitsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<float4> positions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> prevPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float> life;
        [ReadOnly] public NativeArray<int> phases;

        [ReadOnly] public NativeArray<int> activeParticles;
        [ReadOnly] public bool killOffLimits;

        [ReadOnly] public BurstAabb boundaryLimits;

        public void Execute(int index)
        {
            int i = activeParticles[index];

            float4 pos = positions[i];
            float4 prevPos = prevPositions[i];

            bool outside = math.any(math.step(pos, boundaryLimits.min).xyz + math.step(boundaryLimits.max, pos).xyz);

            if ((phases[i] & (int)ObiUtils.ParticleFlags.Isolated) != 0)
                life[i] = outside && killOffLimits ? 0 : life[i];

            pos.xyz = math.clamp(pos, boundaryLimits.min, boundaryLimits.max).xyz;
            prevPos.xyz = math.clamp(prevPos, boundaryLimits.min, boundaryLimits.max).xyz;

            positions[i] = pos;
            prevPositions[i] = prevPos;
        }
    }
}
#endif