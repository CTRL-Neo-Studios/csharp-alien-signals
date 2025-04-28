using AlienSignals.Interfaces;

namespace AlienSignals;

public class Signal<T> : IDependency
{
    public T CurrentValue { get; set; }
    public Link Subs { get; set; }
    public Link SubsTail { get; set; }

    public Signal(T initialValue)
    {
        CurrentValue = initialValue;
    }
}