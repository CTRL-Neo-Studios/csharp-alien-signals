using System;
using AlienSignals.Runtime.Core.Enums;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core.Internal;

internal class Computed<T> : IComputed<T>
{
    public T? Value { get; set; }
    internal readonly Func<T?, T> Getter;

    // IReactiveNode implementation
    public ILink? Deps { get; set; }
    public ILink? DepsTail { get; set; }
    public ILink? Subs { get; set; }
    public ILink? SubsTail { get; set; }
    public ReactiveFlags Flags { get; set; }

    internal Computed(Func<T?, T> getter)
    {
        Getter = getter;
        Value = default;
        Flags = ReactiveFlags.Mutable | ReactiveFlags.Dirty;
    }
    
    public T Get() => Signals.ComputedGet(this);
}