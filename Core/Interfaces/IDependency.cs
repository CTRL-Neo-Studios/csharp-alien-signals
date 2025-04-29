namespace AlienSignals.Core.Interfaces
{
    public interface IDependency
    {
        Link Subs { get; set; }
        Link SubsTail { get; set; }
    }
}