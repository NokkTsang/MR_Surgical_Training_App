using System;
using UnityEngine;

namespace Obi
{
    public class ComputePinConstraints : ComputeConstraintsImpl<ComputePinConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int clearKernel;
        public int initializeKernel;
        public int projectKernel;
        public int applyKernel;
        public int projectRenderableKernel;

        public ComputePinConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Pin)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/PinConstraints"));
            clearKernel = constraintsShader.FindKernel("Clear");
            initializeKernel = constraintsShader.FindKernel("Initialize");
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
            projectRenderableKernel = constraintsShader.FindKernel("ProjectRenderable");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputePinConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputePinConstraintsBatch);
            batch.Destroy();
        }

        public void RequestDataReadback()
        {
            foreach (var batch in batches)
                batch.RequestDataReadback();
        }

        public void WaitForReadback()
        {
            foreach (var batch in batches)
                batch.WaitForReadback();
        }

        public void ProjectRenderablePositions()
        {
            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    batches[i].ProjectRenderablePositions();
                }
            }
        }
    }
}
