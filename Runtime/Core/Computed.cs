using System;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core
{
    public class Computed<T> : ISubscriber, IDependency
    {
        public Func<T, T> Getter { get; }
        public T CurrentValue { get; set; }
        public SubscriberFlags Flags { get; set; }
        public Link Subs { get; set; }
        public Link SubsTail { get; set; }
        public Link Deps { get; set; }
        public Link DepsTail { get; set; }

        public Computed(Func<T, T> getter)
        {
            Getter = getter;
            Flags = SubscriberFlags.Computed | SubscriberFlags.Dirty;
        }
    }
}