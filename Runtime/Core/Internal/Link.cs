using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core.Internal;

internal class Link : ILink
{
    public IReactiveNode Dep { get; }
    public IReactiveNode Sub { get; }
    public ILink? PrevSub { get; set; }
    public ILink? NextSub { get; set; }
    public ILink? PrevDep { get; set; }
    public ILink? NextDep { get; set; }

    public Link(IReactiveNode dep, IReactiveNode sub)
    {
        Dep = dep;
        Sub = sub;
    }
}