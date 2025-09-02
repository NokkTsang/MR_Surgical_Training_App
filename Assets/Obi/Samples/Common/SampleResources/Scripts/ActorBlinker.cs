using UnityEngine;
using Obi;

namespace Obi.Samples
{
    [RequireComponent(typeof(ObiActor))]
    public class ActorBlinker : MonoBehaviour
    {
        public Color neutralColor = Color.white;
        public Color highlightColor = Color.red;
        private ObiActor actor;

        void Awake()
        {
            actor = GetComponent<ObiActor>();
        }

        public void Blink(int particleIndex)
        {
            if (actor.solver != null)
                actor.solver.colors[particleIndex] = highlightColor;
        }

        void LateUpdate()
        {
            if (actor.solver != null)
                for (int i = 0; i < actor.activeParticleCount; ++i)
                    actor.solver.colors[actor.solverIndices[i]] += (neutralColor - actor.solver.colors[actor.solverIndices[i]]) * Time.deltaTime * 5;
        }

    }
}