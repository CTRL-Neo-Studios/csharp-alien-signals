using System;

namespace AlienSignals.Runtime.Core
{
    [Flags]
    public enum SubscriberFlags
    {
        None = 0,
        Computed = 1 << 0,
        Effect = 1 << 1,
        Tracking = 1 << 2,
        Notified = 1 << 3,
        Recursed = 1 << 4,
        Dirty = 1 << 5,
        PendingComputed = 1 << 6,
        PendingEffect = 1 << 7,
        Propagated = Dirty | PendingComputed | PendingEffect
    }
}