using AlienSignals.Runtime.Core.Enums;

namespace AlienSignals.Runtime.Core.Interfaces;

public interface IReactiveNode
{
    ILink? Deps { get; set; }
    ILink? DepsTail { get; set; }
    ILink? Subs { get; set; }
    ILink? SubsTail { get; set; }
    ReactiveFlags Flags { get; set; }
}