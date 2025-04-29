using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core
{
    public class EffectScope : ISubscriber
    {
        public bool IsScope { get; } = true;
        public SubscriberFlags Flags { get; set; }
        public Link Deps { get; set; }
        public Link DepsTail { get; set; }
    }
}