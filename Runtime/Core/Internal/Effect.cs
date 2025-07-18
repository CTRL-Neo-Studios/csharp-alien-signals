using System;
using AlienSignals.Runtime.Core.Enums;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core.Internal;

internal class Effect : IEffect
{
    internal readonly Action Fn;
    
    // IReactiveNode implementation
    public ILink? Deps { get; set; }
    public ILink? DepsTail { get; set; }
    public ILink? Subs { get; set; }
    public ILink? SubsTail { get; set; }
    public ReactiveFlags Flags { get; set; }

    internal Effect(Action fn)
    {
        Fn = fn;
        Flags = ReactiveFlags.Watching;
        
        // This constructor automatically runs the effect for the first time
        Signals.RunEffectInitial(this);
    }
    
    public void Dispose() => Signals.DisposeNode(this);
}