namespace AlienSignals.Runtime.Core.Interfaces;

public interface IComputed<T> : IReactiveNode
{
    T Get();
}