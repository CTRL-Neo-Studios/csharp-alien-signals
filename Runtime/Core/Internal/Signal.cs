using AlienSignals.Runtime.Core.Enums;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core.Internal;

// Internal implementation class for ISignal
internal class Signal<T> : ISignal<T>
{
    public T Value { get; set; }
    public T PreviousValue { get; set; }
    
    // IReactiveNode implementation
    public ILink? Deps { get; set; }
    public ILink? DepsTail { get; set; }
    public ILink? Subs { get; set; }
    public ILink? SubsTail { get; set; }
    public ReactiveFlags Flags { get; set; }

    internal Signal(T initialValue)
    {
        Value = initialValue;
        PreviousValue = initialValue;
        Flags = ReactiveFlags.Mutable;
    }

    public T Get() => Signals.SignalGet(this);
    public void Set(T value) => Signals.SignalSet(this, value);
}