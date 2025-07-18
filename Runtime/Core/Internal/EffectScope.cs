using System;
using AlienSignals.Runtime.Core.Enums;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core.Internal;

internal class EffectScope : IEffectScope
{
    // IReactiveNode implementation
    public ILink? Deps { get; set; }
    public ILink? DepsTail { get; set; }
    public ILink? Subs { get; set; }
    public ILink? SubsTail { get; set; }
    public ReactiveFlags Flags { get; set; }

    internal EffectScope(Action fn)
    {
        Flags = ReactiveFlags.None;
        Signals.RunEffectScope(this, fn);
    }

    public void Dispose() => Signals.DisposeNode(this);
}