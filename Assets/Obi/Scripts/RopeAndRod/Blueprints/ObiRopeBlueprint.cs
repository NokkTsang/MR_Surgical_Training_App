using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace Obi
{
    /// <summary>
    /// Surgical Suture Blueprint - Fine physics simulation designed for medical training
    /// Features: Lightweight, high resolution, appropriate bending stiffness, fine particle distribution
    /// </summary>
    [CreateAssetMenu(fileName = "surgical suture blueprint", menuName = "Obi/Surgical Suture Blueprint", order = 140)]
    public class ObiRopeBlueprint : ObiRopeBlueprintBase
    {
        public int pooledParticles = 200; // Increase pooled particles to support longer sutures

        public const float DEFAULT_PARTICLE_MASS = 0.02f; // Reduce mass to simulate lightweight sutures

        protected override IEnumerator Initialize()
        {
            if (path.ControlPointCount < 2)
            {
                ClearParticleGroups();
                // Create control points suitable for surgical sutures: finer resolution and appropriate physics parameters
                path.InsertControlPoint(0, Vector3.left * 0.05f, Vector3.left * 0.01f, Vector3.right * 0.01f, Vector3.up, DEFAULT_PARTICLE_MASS, 0.6f, 1, ObiUtils.MakeFilter(ObiUtils.CollideWithEverything,1), new Color(0.9f, 0.9f, 0.95f, 1f), "suture start");
                path.InsertControlPoint(1, Vector3.right * 0.05f, Vector3.left * 0.01f, Vector3.right * 0.01f, Vector3.up, DEFAULT_PARTICLE_MASS, 0.6f, 1, ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 1), new Color(0.9f, 0.9f, 0.95f, 1f), "suture end");
            }

            // Recalculate path length with higher precision for fine sutures
            path.RecalculateLenght(Matrix4x4.identity, 0.000001f, 10); // Higher precision and iteration count

            List<Vector3> particlePositions = new List<Vector3>();
            List<float> particleThicknesses = new List<float>();
            List<float> particleInvMasses = new List<float>();
            List<int> particleFilters = new List<int>();
            List<Color> particleColors = new List<Color>();

            // In case the path is open, add a first particle. In closed paths, the last particle is also the first one.
            if (!path.Closed)
            {
                particlePositions.Add(path.points.GetPositionAtMu(path.Closed, 0));
                particleThicknesses.Add(path.thicknesses.GetAtMu(path.Closed, 0));
                particleInvMasses.Add(ObiUtils.MassToInvMass(path.masses.GetAtMu(path.Closed, 0)));
                particleFilters.Add(path.filters.GetAtMu(path.Closed, 0));
                particleColors.Add(path.colors.GetAtMu(path.Closed, 0));
            }

            // Create a particle group for the first control point:
            groups[0].particleIndices.Clear();
            groups[0].particleIndices.Add(0);

            ReadOnlyCollection<float> lengthTable = path.ArcLengthTable;
            int spans = path.GetSpanCount();

            for (int i = 0; i < spans; i++)
            {
                int firstArcLengthSample = i * (path.ArcLengthSamples + 1);
                int lastArcLengthSample = (i + 1) * (path.ArcLengthSamples + 1);

                float upToSpanLength = lengthTable[firstArcLengthSample];
                float spanLength = lengthTable[lastArcLengthSample] - upToSpanLength;

                // Adaptive particle density: increase density significantly when thickness is small
                // This ensures visual continuity regardless of rope thickness
                float adaptiveResolution = resolution;
                
                // Scale up resolution dramatically for thin ropes to prevent gaps
                if (thickness < 0.01f)
                    adaptiveResolution *= 12.0f; // Very high density for very thin ropes
                else if (thickness < 0.02f)
                    adaptiveResolution *= 8.0f;  // High density for thin ropes
                else if (thickness < 0.05f)
                    adaptiveResolution *= 6.0f;  // Medium-high density
                else
                    adaptiveResolution *= 4.0f;  // Standard high density
                
                int particlesInSpan = 1 + Mathf.FloorToInt(spanLength / thickness * adaptiveResolution);
                
                // Ensure minimum particle density for visual continuity
                int minParticlesInSpan = Mathf.CeilToInt(spanLength / 0.002f); // At least one particle every 2mm
                particlesInSpan = Mathf.Max(particlesInSpan, minParticlesInSpan);
                
                // Debug information for particle density optimization
                if (i == 0) // Only log for first span to avoid spam
                {
                    Debug.Log($"Rope Particle Density - Thickness: {thickness:F4}m, Span Length: {spanLength:F4}m, " +
                             $"Particles in Span: {particlesInSpan}, Distance between particles: {spanLength / particlesInSpan:F4}m");
                }
                
                float distance = spanLength / particlesInSpan;

                for (int j = 0; j < particlesInSpan; ++j)
                {
                    float mu = path.GetMuAtLenght(upToSpanLength + distance * (j + 1));
                    particlePositions.Add(path.points.GetPositionAtMu(path.Closed, mu));
                    particleThicknesses.Add(path.thicknesses.GetAtMu(path.Closed, mu));
                    particleInvMasses.Add(ObiUtils.MassToInvMass(path.masses.GetAtMu(path.Closed, mu)));
                    particleFilters.Add(path.filters.GetAtMu(path.Closed, mu));
                    particleColors.Add(path.colors.GetAtMu(path.Closed, mu));
                }

                // Create a particle group for each control point:
                if (!(path.Closed && i == spans - 1))
                {
                    groups[i + 1].particleIndices.Clear();
                    groups[i + 1].particleIndices.Add(particlePositions.Count - 1);
                }

                if (i % 100 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiRope: generating particles...", i / (float)spans);
            }

            m_ActiveParticleCount = particlePositions.Count;
            totalParticles = m_ActiveParticleCount + pooledParticles;

            int numSegments = m_ActiveParticleCount - (path.Closed ? 0 : 1);
            if (numSegments > 0)
                m_InterParticleDistance = path.Length / (float)numSegments;
            else
                m_InterParticleDistance = 0;

            positions = new Vector3[totalParticles];
            restPositions = new Vector4[totalParticles];
            velocities = new Vector3[totalParticles];
            invMasses = new float[totalParticles];
            principalRadii = new Vector3[totalParticles];
            filters = new int[totalParticles];
            colors = new Color[totalParticles];
            restLengths = new float[totalParticles];

            for (int i = 0; i < m_ActiveParticleCount; i++)
            {
                // Set finer physics parameters for sutures
                invMasses[i] = particleInvMasses[i] * 1.5f; // Slightly increase inverse mass for lighter sutures
                positions[i] = particlePositions[i];
                restPositions[i] = positions[i];
                restPositions[i][3] = 1; // activate rest position.
                // Set particle radius for solid rope appearance - smaller collision but larger visual radius
                principalRadii[i] = Vector3.one * particleThicknesses[i] * thickness * 0.6f; // Increase visual radius for solid appearance
                filters[i] = particleFilters[i];
                colors[i] = particleColors[i];

                if (i % 100 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiRope: generating particles...", i / (float)m_ActiveParticleCount);
            }

            // Deformable edges:
            CreateDeformableEdges(numSegments);

            // Create edge simplices:
            CreateSimplices(numSegments);

            //Create distance constraints for the total number of particles, but only activate for the used ones.
            IEnumerator dc = CreateDistanceConstraints();

            while (dc.MoveNext())
                yield return dc.Current;

            //Create bending constraints:
            IEnumerator bc = CreateBendingConstraints();

            while (bc.MoveNext())
                yield return bc.Current;

            // Create aerodynamic constraints:
            IEnumerator ac = CreateAerodynamicConstraints();

            while (ac.MoveNext())
                yield return ac.Current;

            // Recalculate rest length:
            m_RestLength = 0;
            foreach (float length in restLengths)
                m_RestLength += length;

        }

        protected virtual IEnumerator CreateDistanceConstraints()
        {
            distanceConstraintsData = new ObiDistanceConstraintsData();

            // Add more batches for sutures to achieve better performance
            distanceConstraintsData.AddBatch(new ObiDistanceConstraintsBatch());
            distanceConstraintsData.AddBatch(new ObiDistanceConstraintsBatch());
            distanceConstraintsData.AddBatch(new ObiDistanceConstraintsBatch()); // Third batch

            for (int i = 0; i < totalParticles - 1; i++)
            {
                var batch = distanceConstraintsData.batches[i % 3] as ObiDistanceConstraintsBatch; // Use 3 batches

                if (i < m_ActiveParticleCount - 1)
                {
                    Vector2Int indices = new Vector2Int(i, i + 1);
                    // Set slightly smaller rest length for sutures to add some pre-tension
                    restLengths[i] = Vector3.Distance(positions[indices.x], positions[indices.y]) * 0.98f;
                    batch.AddConstraint(indices, restLengths[i]);
                    batch.activeConstraintCount++;
                }
                else
                {
                    restLengths[i] = m_InterParticleDistance * 0.98f; // Maintain consistent pre-tension
                    batch.AddConstraint(Vector2Int.zero, 0);
                }

                if (i % 500 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiSuture: generating distance constraints...", i / (float)(totalParticles - 1));

            }

            // If path is closed, add loop closing constraints for sutures
            if (path.Closed)
            {
                var loopClosingBatch = new ObiDistanceConstraintsBatch();
                distanceConstraintsData.AddBatch(loopClosingBatch);

                Vector2Int indices = new Vector2Int(m_ActiveParticleCount - 1, 0);
                // Maintain same pre-tension for closed sutures
                restLengths[m_ActiveParticleCount - 2] = Vector3.Distance(positions[indices.x], positions[indices.y]) * 0.98f;
                loopClosingBatch.AddConstraint(indices, restLengths[m_ActiveParticleCount - 2]);
                loopClosingBatch.activeConstraintCount++;
            }

        }

        protected virtual IEnumerator CreateBendingConstraints()
        {
            bendConstraintsData = new ObiBendConstraintsData();

            // Add more batches for sutures to achieve better parallel performance
            bendConstraintsData.AddBatch(new ObiBendConstraintsBatch());
            bendConstraintsData.AddBatch(new ObiBendConstraintsBatch());
            bendConstraintsData.AddBatch(new ObiBendConstraintsBatch());
            bendConstraintsData.AddBatch(new ObiBendConstraintsBatch()); // Fourth batch

            for (int i = 0; i < totalParticles - 2; i++)
            {
                var batch = bendConstraintsData.batches[i % 4] as ObiBendConstraintsBatch; // Use 4 batches

                Vector3Int indices = new Vector3Int(i, i + 2, i + 1);
                // Set appropriate bending stiffness for sutures - allow some bending but maintain shape
                float restBend = 0.1f; // Slight bending resistance simulating natural suture stiffness
                batch.AddConstraint(indices, restBend);

                if (i < m_ActiveParticleCount - 2)
                    batch.activeConstraintCount++;

                if (i % 500 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiSuture: generating bending constraints...", i / (float)(totalParticles - 2));

            }

            // If path is closed, add bending loop closing constraints for sutures
            if (path.Closed)
            {
                var loopClosingBatch = new ObiBendConstraintsBatch();
                bendConstraintsData.AddBatch(loopClosingBatch);

                Vector3Int indices = new Vector3Int(m_ActiveParticleCount - 2, 0, m_ActiveParticleCount - 1);
                loopClosingBatch.AddConstraint(indices, 0.1f); // Maintain slight bending resistance
                loopClosingBatch.activeConstraintCount++;

                var loopClosingBatch2 = new ObiBendConstraintsBatch();
                bendConstraintsData.AddBatch(loopClosingBatch2);

                indices = new Vector3Int(m_ActiveParticleCount - 1, 1, 0);
                loopClosingBatch2.AddConstraint(indices, 0.1f); // Maintain slight bending resistance
                loopClosingBatch2.activeConstraintCount++;
            }
        }

        /// <summary>
        /// Calculate optimal particle density based on rope thickness
        /// This ensures visual continuity across different thickness values
        /// </summary>
        public static float CalculateOptimalParticleDensity(float thickness, float baseResolution = 1.0f)
        {
            // Adaptive scaling based on thickness
            float densityMultiplier;
            
            if (thickness < 0.001f)      // Ultra-thin ropes (< 1mm)
                densityMultiplier = 20.0f;
            else if (thickness < 0.005f) // Very thin ropes (1-5mm)
                densityMultiplier = 15.0f;
            else if (thickness < 0.01f)  // Thin ropes (5-10mm)
                densityMultiplier = 12.0f;
            else if (thickness < 0.02f)  // Medium-thin ropes (10-20mm)
                densityMultiplier = 8.0f;
            else if (thickness < 0.05f)  // Medium ropes (20-50mm)
                densityMultiplier = 6.0f;
            else                         // Thick ropes (>50mm)
                densityMultiplier = 4.0f;
            
            return baseResolution * densityMultiplier;
        }

        /// <summary>
        /// Get recommended minimum particle count per unit length
        /// </summary>
        public static float GetMinimumParticleSpacing(float thickness)
        {
            // Ensure particles are close enough to maintain visual continuity
            // Rule: particle spacing should be no more than half the thickness
            return Mathf.Min(0.002f, thickness * 0.5f);
        }

    }
}