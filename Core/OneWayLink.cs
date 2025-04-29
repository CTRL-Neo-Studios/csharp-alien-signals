namespace AlienSignals.Core
{
    public class OneWayLink<T>
    {
        public T Target { get; set; }
        public OneWayLink<T> Linked { get; set; }
    }
}