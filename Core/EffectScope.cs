using AlienSignals.Core.Interfaces;

namespace AlienSignals.Core
{
    public class EffectScope : ISubscriber
    {
        public bool IsScope { get; } = true;
        public SubscriberFlags Flags { get; set; }
        public Link Deps { get; set; }
        public Link DepsTail { get; set; }
    }
}