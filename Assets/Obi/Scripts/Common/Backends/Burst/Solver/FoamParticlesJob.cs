#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;

namespace Obi
{
    [BurstCompile]
    unsafe struct EmitParticlesJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputVelocities;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputColors;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<int> dispatchBuffer;

        public uint emitterShape;
        public float4 emitterPosition;
        public quaternion emitterRotation;
        public float4 emitterSize;

        public float lifetime;
        public float lifetimeRandom;
        public float particleSize;
        public float sizeRandom;
        public float buoyancy;
        public float drag;
        public float airdrag;
        public float airAging;
        public float isosurface;
        public float4 foamColor;

        public float randomSeed;
        public float deltaTime;

        public void Execute(int i)
        {
            int* dispatch = (int*)dispatchBuffer.GetUnsafePtr();

            // atomically increment alive particle counter:
            int count = Interlocked.Add(ref dispatch[3], 1) - 1;

            if (count < outputPositions.Length)
            {
                // initialize foam particle in a random position inside the cylinder spawned by fluid particle:
                float3 radialVelocity;
                float4 pos;

                if (emitterShape == 0)
                    BurstMath.RandomInCylinder(randomSeed + i, -new float4(0, 1, 0, 0) * emitterSize.y * 0.5f, new float4(0, 1, 0, 0), emitterSize.y, math.max(emitterSize.x, emitterSize.z) * 0.5f, out pos, out radialVelocity);
                else
                    BurstMath.RandomInBox(randomSeed + i, float4.zero, emitterSize, out pos, out radialVelocity);

                float2 random = BurstMath.Hash21(randomSeed - i);

                // calculate initial life/size/color:
                float initialLife = math.max (0, lifetime - lifetime * random.x * lifetimeRandom);
                float initialSize = particleSize - particleSize * random.y * sizeRandom;

                outputPositions[count] = new float4(emitterPosition.xyz + math.rotate(emitterRotation, pos.xyz), 0);
                outputVelocities[count] = new float4(0,0,0, buoyancy);
                outputColors[count] = foamColor;
                outputAttributes[count] = new float4(1, 1 / initialLife, initialSize, BurstMath.PackFloatRGBA(new float4(airAging / 50.0f, airdrag, drag, isosurface)));
            }
        }
    }

    [BurstCompile]
    unsafe struct GenerateParticlesJob : IJobParallelFor
    {
        [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<int> activeParticles;
        [ReadOnly] public NativeArray<float4> positions;
        [ReadOnly] public NativeArray<float4> velocities;
        [ReadOnly] public NativeArray<float4> principalRadii;
        [ReadOnly] public NativeArray<float4> fluidData;
        [NativeDisableParallelForRestriction] public NativeArray<float4> angularVelocities;

        [NativeDisableParallelForRestriction] public NativeArray<float4> outputPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputVelocities;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputColors;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<int> dispatchBuffer;

        public float2 vorticityRange;
        public float2 velocityRange;
        public float potentialIncrease;
        public float potentialDiffusion;
        public float foamGenerationRate;

        public float lifetime;
        public float lifetimeRandom;
        public float particleSize;
        public float sizeRandom;
        public float buoyancy;
        public float drag;
        public float airdrag;
        public float airAging;
        public float isosurface;
        public float4 foamColor;

        public float randomSeed;
        public float deltaTime;

        public void Execute(int i)
        {
            int* dispatch = (int*)dispatchBuffer.GetUnsafePtr();

            int p = activeParticles[i];

            float4 angVel = angularVelocities[p];
            float2 potential = BurstMath.UnpackFloatRG(angVel.w);

            // calculate foam potential increase:
            float vorticityPotential = BurstMath.Remap01(fluidData[p].z, vorticityRange.x, vorticityRange.y);
            float velocityPotential = BurstMath.Remap01(math.length(velocities[p].xyz), velocityRange.x, velocityRange.y);
            float potentialDelta = velocityPotential * vorticityPotential * deltaTime * potentialIncrease;

            // update foam potential:
            potential.y = math.saturate(potential.y * potentialDiffusion + potentialDelta);

            // calculate amount of emitted particles
            potential.x += foamGenerationRate * potential.y * deltaTime;
            int emitCount = (int)potential.x;
            potential.x -= emitCount; 

            for (int j = 0; j < emitCount; ++j)
            {
                // atomically increment alive particle counter:
                int count = Interlocked.Add(ref dispatch[3], 1) - 1;

                if (count < outputPositions.Length) 
                {
                    // initialize foam particle in a random position inside the cylinder spawned by fluid particle:
                    float3 radialVelocity;
                    float4 pos;
                    BurstMath.RandomInCylinder(randomSeed + p + j, positions[p], math.normalizesafe(velocities[p]), math.length(velocities[p]) * deltaTime, principalRadii[p].x, out pos, out radialVelocity);

                    float2 random = BurstMath.Hash21(randomSeed - p - j);

                    // calculate initial life/size/color:
                    float initialLife = velocityPotential * (lifetime - lifetime * random.x * lifetimeRandom);
                    float initialSize = particleSize - particleSize * random.y * sizeRandom;

                    outputPositions[count] = pos;
                    outputVelocities[count] = velocities[p] + new float4(radialVelocity, buoyancy);
                    outputColors[count] = foamColor;
                    outputAttributes[count] = new float4(1, 1/initialLife, initialSize, BurstMath.PackFloatRGBA(new float4(airAging/50.0f, airdrag, drag, isosurface))); 
                }
            }

            angVel.w = BurstMath.PackFloatRG(potential);
            angularVelocities[p] = angVel;
        }
    }

    [BurstCompile]
    unsafe struct UpdateParticlesJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<float4> positions;
        [ReadOnly] public NativeArray<quaternion> orientations;
        [ReadOnly] public NativeArray<float4> velocities;
        [ReadOnly] public NativeArray<float4> angularVelocities;
        [ReadOnly] public NativeArray<float4> principalRadii;
        [ReadOnly] public NativeArray<float4> fluidData;
        [ReadOnly] public NativeArray<float4> fluidMaterial;

        [ReadOnly] public NativeArray<int> simplices;
        [ReadOnly] public SimplexCounts simplexCounts;

        [ReadOnly] public NativeMultilevelGrid<int> grid;
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<int> gridLevels;

        [ReadOnly] public Poly6Kernel densityKernel;

        [ReadOnly] public NativeArray<float4> inputPositions;
        [ReadOnly] public NativeArray<float4> inputVelocities;
        [ReadOnly] public NativeArray<float4> inputColors;
        [ReadOnly] public NativeArray<float4> inputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<float4> outputPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputVelocities;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputColors;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<int> dispatchBuffer;

        [ReadOnly] public Oni.SolverParameters parameters;

        public float3 agingOverPopulation;
        public int minFluidNeighbors;
        public float deltaTime;
        public int currentAliveParticles;

        static readonly int4[] offsets =
        {
                new int4(0, 0, 0, 1),
                new int4(1, 0, 0, 1),
                new int4(0, 1, 0, 1),
                new int4(1, 1, 0, 1),
                new int4(0, 0, 1, 1),
                new int4(1, 0, 1, 1),
                new int4(0, 1, 1, 1),
                new int4(1, 1, 1, 1)
            };

        public void Execute(int i)
        {
            int* dispatch = (int*)dispatchBuffer.GetUnsafePtr();
            int count = Interlocked.Add(ref dispatch[3], -1);

            if (count < inputPositions.Length && inputAttributes[count].x > 0)
            {
                int aliveCount = Interlocked.Add(ref dispatch[7], 1) - 1;

                float4 attributes = inputAttributes[count];
                float4 packedData = BurstMath.UnpackFloatRGBA(attributes.w);

                int offsetCount = ((int)parameters.mode == 1) ? 4 : 8;
                float4 advectedVelocity = float4.zero;
                float4 advectedAngVelocity = float4.zero;
                float kernelSum = -packedData.w;
                uint neighbourCount = 0;

                float4 diffusePos = inputPositions[count];

                for (int k = 0; k < gridLevels.Length; ++k)
                {
                    int l = gridLevels[k];
                    float radius = NativeMultilevelGrid<int>.CellSizeOfLevel(l);
                    float interactionDist = radius * 0.5f;

                    float4 cellCoords = math.floor(diffusePos / radius);

                    cellCoords[3] = 0;
                    if ((int)parameters.mode == 1)
                        cellCoords[2] = 0;

                    float4 posInCell = diffusePos - (cellCoords * radius + new float4(interactionDist));
                    int4 quadrant = (int4)math.sign(posInCell);

                    quadrant[3] = l;

                    for (int o = 0; o < offsetCount; ++o)
                    {
                        int cellIndex;
                        if (grid.TryGetCellIndex((int4)cellCoords + offsets[o] * quadrant, out cellIndex))
                        {
                            var cell = grid.usedCells[cellIndex];
                            for (int n = 0; n < cell.Length; ++n)
                            {
                                int simplexStart = simplexCounts.GetSimplexStartAndSize(cell[n], out int simplexSize);

                                for (int a = 0; a < simplexSize; ++a)
                                {
                                    int p = simplices[simplexStart + a];
                                    float4 normal = diffusePos - positions[p];

                                    normal[3] = 0;
                                    if ((int)parameters.mode == 1)
                                        normal[2] = 0;

                                    float d = math.length(normal);
                                    if (d <= interactionDist)
                                    {
                                        float3 radii = fluidMaterial[p].x * (principalRadii[p].xyz / principalRadii[p].x);

                                        float4 angVel = new float4(math.cross(angularVelocities[p].xyz, normal.xyz), 0);
                                        advectedAngVelocity += angVel * densityKernel.W(d, radii.x) / densityKernel.W(0, radii.x);

                                        normal.xyz = math.mul(math.conjugate(orientations[p]), normal.xyz) / radii;
                                        d = math.length(normal) * radii.x;

                                        // scale by volume (* 1 / normalized density)
                                        float w = densityKernel.W(d, radii.x) / fluidData[p].x;

                                        kernelSum += w;
                                        advectedVelocity += velocities[p] * w;
                                        neighbourCount++;
                                    }
                                }
                            }
                        }
                    }
                }

                float4 forces = float4.zero;
                float velocityScale = 1;
                float agingScale = 1 + BurstMath.Remap01(currentAliveParticles / (float)inputPositions.Length, agingOverPopulation.x, agingOverPopulation.y) * (agingOverPopulation.z - 1);

                // foam/bubble particle:
                if (kernelSum > BurstMath.epsilon && neighbourCount >= minFluidNeighbors)
                {
                    // advection: 
                    forces = packedData.z / deltaTime * (advectedVelocity / (kernelSum + packedData.w) + advectedAngVelocity - inputVelocities[count]);

                    // buoyancy:
                    forces -= new float4(parameters.gravity * parameters.foamGravityScale * inputVelocities[count].w * math.saturate(kernelSum), 0);

                }
                else // ballistic:
                {
                    // gravity:
                    forces += new float4(parameters.gravity * parameters.foamGravityScale, 0);

                    // atmospheric drag/aging:
                    velocityScale = packedData.y;
                    agingScale *= packedData.x * 50;
                }

                // don't change 4th component, as its used to store buoyancy control parameter.
                forces[3] = 0;

                // update particle data:
                attributes.x -= attributes.y * deltaTime * agingScale;
                outputAttributes[aliveCount] = attributes;
                outputColors[aliveCount] = inputColors[count];

                // integrate:
                outputVelocities[aliveCount] = (inputVelocities[count] + forces * deltaTime) * velocityScale;
                outputPositions[aliveCount] = new float4((inputPositions[count] + outputVelocities[aliveCount] * deltaTime).xyz,neighbourCount);
            }
        }
    }

    [BurstCompile]
    unsafe struct CopyJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<float4> inputPositions;
        [ReadOnly] public NativeArray<float4> inputVelocities;
        [ReadOnly] public NativeArray<float4> inputColors;
        [ReadOnly] public NativeArray<float4> inputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<float4> outputPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputVelocities;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputColors;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<int> dispatchBuffer;

        public void Execute(int i)
        {
            if (i == 0)
            {
                dispatchBuffer[3] = dispatchBuffer[7];
                dispatchBuffer[7] = 0;
            }

            outputPositions[i] = inputPositions[i];
            outputVelocities[i] = inputVelocities[i];
            outputColors[i] = inputColors[i];
            outputAttributes[i] = inputAttributes[i];
        }
    }
}
#endif