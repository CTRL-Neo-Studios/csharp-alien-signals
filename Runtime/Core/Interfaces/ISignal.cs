namespace AlienSignals.Runtime.Core.Interfaces;

public interface ISignal<T> : IReactiveNode
{
    T Get();
    void Set(T value);
}