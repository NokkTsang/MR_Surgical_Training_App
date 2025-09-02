using UnityEngine;
using UnityEngine.Events;

namespace Obi.Samples
{
    [RequireComponent(typeof(ObiSolver))]
    public class ActorActorCollisionDetector : MonoBehaviour
    {
        public struct ActorPair
        {
            public readonly ObiActor actorA;
            public readonly ObiActor actorB;
            public int particleA;
            public int particleB;

            public ActorPair(ObiActor actorA, ObiActor actorB, int particleA, int particleB)
            {
                this.actorA = actorA;
                this.actorB = actorB;
                this.particleA = particleA;
                this.particleB = particleB;
            }
        }

        public UnityEvent<ActorPair> callback;
        ObiSolver solver;

        void OnEnable()
        {
            solver = GetComponent<Obi.ObiSolver>();
            solver.OnParticleCollision += Solver_OnCollision;
        }

        void OnDisable()
        {
            solver.OnParticleCollision -= Solver_OnCollision;
        }

        void Solver_OnCollision(object sender, ObiNativeContactList e)
        {
            if (!solver.initialized || callback == null) return;

            // just iterate over all contacts in the current frame:
            foreach (Oni.Contact contact in e)
            {
                // if this one is an actual collision:
                if (contact.distance < 0.01)
                {
                    // get the index of the first entry in the simplices array for both bodies:
                    int startA = solver.simplexCounts.GetSimplexStartAndSize(contact.bodyA, out _);
                    int startB = solver.simplexCounts.GetSimplexStartAndSize(contact.bodyB, out _);

                    // retrieve the index of both particles from the simplices array:
                    int particleA = solver.simplices[startA];
                    int particleB = solver.simplices[startB];

                    // retrieve info about both actors involved in the collision:
                    var particleInActorA = solver.particleToActor[particleA];
                    var particleInActorB = solver.particleToActor[particleB];

                    // if they're not the same actor, trigger a callback:
                    if (particleInActorA != null && particleInActorB != null && particleInActorA.actor != particleInActorB.actor)
                        callback.Invoke(new ActorPair(particleInActorA.actor, particleInActorB.actor, particleA, particleB));
                }
            }
        }

    }
}
