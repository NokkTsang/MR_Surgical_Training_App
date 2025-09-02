#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using System;
using Unity.Jobs;

namespace Obi
{
    public class BurstPinConstraints : BurstConstraintsImpl<BurstPinConstraintsBatch>
    {
        public BurstPinConstraints(BurstSolverImpl solver) : base(solver, Oni.ConstraintType.Pin)
        {
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new BurstPinConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as BurstPinConstraintsBatch);
            batch.Destroy();
        }

        public JobHandle ProjectRenderablePositions(JobHandle inputDeps)
        {
            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    inputDeps = batches[i].ProjectRenderablePositions(inputDeps);
                    m_Solver.ScheduleBatchedJobsIfNeeded();
                }
            }

            return inputDeps;
        }
    }
}
#endif