using System;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core
{
    public class Effect : ISubscriber, IDependency
    {
        public Action Fn { get; }
        public SubscriberFlags Flags { get; set; }
        public Link Subs { get; set; }
        public Link SubsTail { get; set; }
        public Link Deps { get; set; }
        public Link DepsTail { get; set; }

        public Effect(Action fn)
        {
            Fn = fn;
            Flags = SubscriberFlags.Effect;
        }
    }
}