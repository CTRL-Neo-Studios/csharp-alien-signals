namespace AlienSignals.Core.Interfaces
{
    public interface ISubscriber
    {
        SubscriberFlags Flags { get; set; }
        Link Deps { get; set; }
        Link DepsTail { get; set; }
    }
}