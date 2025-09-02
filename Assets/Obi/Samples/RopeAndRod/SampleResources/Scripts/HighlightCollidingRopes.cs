using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi.Samples
{
    public class HighlightCollidingRopes : MonoBehaviour
    {
        public void Highlight(ActorActorCollisionDetector.ActorPair pair)
        {
            if (pair.actorA.TryGetComponent(out ActorBlinker blinkerA)) blinkerA.Blink(pair.particleA);
            if (pair.actorB.TryGetComponent(out ActorBlinker blinkerB)) blinkerB.Blink(pair.particleB);
        }
    }
}
