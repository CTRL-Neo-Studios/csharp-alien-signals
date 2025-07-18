namespace AlienSignals.Runtime.Core.Interfaces;

public interface ILink
{
    IReactiveNode Dep { get; }
    IReactiveNode Sub { get; }
    ILink? PrevSub { get; set; }
    ILink? NextSub { get; set; }
    ILink? PrevDep { get; set; }
    ILink? NextDep { get; set; }
}