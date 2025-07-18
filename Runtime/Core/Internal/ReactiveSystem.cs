using System;
using System.Collections.Generic;
using AlienSignals.Runtime.Core.Enums;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core.Internal;

internal class ReactiveSystem
{
    private readonly Func<IReactiveNode, bool> _update;
    private readonly Action<IReactiveNode> _notify;
    private readonly Action<IReactiveNode> _unwatched;

    public ReactiveSystem(Func<IReactiveNode, bool> update, Action<IReactiveNode> notify, Action<IReactiveNode> unwatched)
    {
        _update = update;
        _notify = notify;
        _unwatched = unwatched;
    }

    public void Link(IReactiveNode dep, IReactiveNode sub)
    {
        var prevDep = sub.DepsTail;
        if (prevDep != null && prevDep.Dep == dep)
        {
            return;
        }

        ILink? nextDep = null;
        bool recursedCheck = (sub.Flags & ReactiveFlags.RecursedCheck) != 0;
        if (recursedCheck)
        {
            nextDep = (prevDep != null) ? prevDep.NextDep : sub.Deps;
            if (nextDep != null && nextDep.Dep == dep)
            {
                sub.DepsTail = nextDep;
                return;
            }
        }

        var prevSub = dep.SubsTail;
        if (prevSub != null && prevSub.Sub == sub && (!recursedCheck || IsValidLink(prevSub, sub)))
        {
            return;
        }

        var newLink = new Link(dep, sub)
        {
            PrevDep = prevDep,
            NextDep = nextDep,
            PrevSub = prevSub,
            NextSub = null
        };

        sub.DepsTail = newLink;
        dep.SubsTail = newLink;

        if (nextDep != null) nextDep.PrevDep = newLink;
        if (prevDep != null) prevDep.NextDep = newLink;
        else sub.Deps = newLink;

        if (prevSub != null) prevSub.NextSub = newLink;
        else dep.Subs = newLink;
    }

    public ILink? Unlink(ILink link, IReactiveNode? sub = null)
    {
        sub ??= link.Sub;
        var dep = link.Dep;
        var prevDep = link.PrevDep;
        var nextDep = link.NextDep;
        var nextSub = link.NextSub;
        var prevSub = link.PrevSub;

        if (nextDep != null) nextDep.PrevDep = prevDep;
        else sub.DepsTail = prevDep;

        if (prevDep != null) prevDep.NextDep = nextDep;
        else sub.Deps = nextDep;

        if (nextSub != null) nextSub.PrevSub = prevSub;
        else dep.SubsTail = prevSub;

        if (prevSub != null) prevSub.NextSub = nextSub;
        else if ((dep.Subs = nextSub) == null)
        {
            _unwatched(dep);
        }

        return nextDep;
    }

    public void Propagate(ILink link)
    {
        var next = link.NextSub;
        var stack = new Stack<ILink?>();

        top:
        do
        {
            var sub = link.Sub;
            var flags = sub.Flags;

            if ((flags & (ReactiveFlags.Mutable | ReactiveFlags.Watching)) != 0)
            {
                if ((flags & (ReactiveFlags.RecursedCheck | ReactiveFlags.Recursed | ReactiveFlags.Dirty | ReactiveFlags.Pending)) == 0)
                {
                    sub.Flags = flags | ReactiveFlags.Pending;
                }
                else if ((flags & (ReactiveFlags.RecursedCheck | ReactiveFlags.Recursed)) == 0)
                {
                    flags = ReactiveFlags.None;
                }
                else if ((flags & ReactiveFlags.RecursedCheck) == 0)
                {
                    sub.Flags = (flags & ~ReactiveFlags.Recursed) | ReactiveFlags.Pending;
                }
                else if ((flags & (ReactiveFlags.Dirty | ReactiveFlags.Pending)) == 0 && IsValidLink(link, sub))
                {
                    sub.Flags = flags | ReactiveFlags.Recursed | ReactiveFlags.Pending;
                    flags &= ReactiveFlags.Mutable;
                }
                else
                {
                    flags = ReactiveFlags.None;
                }

                if ((flags & ReactiveFlags.Watching) != 0) _notify(sub);

                if ((flags & ReactiveFlags.Mutable) != 0)
                {
                    var subSubs = sub.Subs;
                    if (subSubs != null)
                    {
                        link = subSubs;
                        if (subSubs.NextSub != null)
                        {
                            stack.Push(next);
                            next = link.NextSub;
                        }
                        continue;
                    }
                }
            }
            
            if (next != null)
            {
                link = next;
                next = link.NextSub;
                continue;
            }

            while (stack.Count > 0)
            {
                var poppedLink = stack.Pop();
                if (poppedLink != null)
                {
                    link = poppedLink;
                    next = link.NextSub;
                    goto top;
                }
            }

            break;
        } while (true);
    }
    
    public void ShallowPropagate(ILink link)
    {
        do
        {
            var sub = link.Sub;
            var subFlags = sub.Flags;
            
            if ((subFlags & (ReactiveFlags.Pending | ReactiveFlags.Dirty)) == ReactiveFlags.Pending)
            {
                sub.Flags = subFlags | ReactiveFlags.Dirty;
                if ((subFlags & ReactiveFlags.Watching) != 0)
                {
                    _notify(sub);
                }
            }
            link = link.NextSub!;
        } while (link != null);
    }

    public void StartTracking(IReactiveNode sub)
    {
        sub.DepsTail = null;
        sub.Flags = (sub.Flags & ~(ReactiveFlags.Recursed | ReactiveFlags.Dirty | ReactiveFlags.Pending)) | ReactiveFlags.RecursedCheck;
    }

    public void EndTracking(IReactiveNode sub)
    {
        var toRemove = (sub.DepsTail != null) ? sub.DepsTail.NextDep : sub.Deps;
        while (toRemove != null)
        {
            toRemove = Unlink(toRemove, sub);
        }
        sub.Flags &= ~ReactiveFlags.RecursedCheck;
    }
    
    public bool CheckDirty(ILink link, IReactiveNode sub)
    {
        var stack = new Stack<ILink>();
        int checkDepth = 0;

        top:
        do
        {
            var dep = link.Dep;
            var depFlags = dep.Flags;
            bool dirty = false;
            
            if ((sub.Flags & ReactiveFlags.Dirty) != 0)
            {
                dirty = true;
            }
            else if ((depFlags & (ReactiveFlags.Mutable | ReactiveFlags.Dirty)) == (ReactiveFlags.Mutable | ReactiveFlags.Dirty))
            {
                if (_update(dep))
                {
                    var subs = dep.Subs!;
                    if (subs.NextSub != null)
                    {
                        ShallowPropagate(subs);
                    }
                    dirty = true;
                }
            }
            else if ((depFlags & (ReactiveFlags.Mutable | ReactiveFlags.Pending)) == (ReactiveFlags.Mutable | ReactiveFlags.Pending))
            {
                if (link.NextSub != null || link.PrevSub != null)
                {
                    stack.Push(link);
                }
                link = dep.Deps!;
                sub = dep;
                checkDepth++;
                continue;
            }

            if (!dirty && link.NextDep != null)
            {
                link = link.NextDep;
                continue;
            }
            
            while (checkDepth > 0)
            {
                checkDepth--;
                var firstSub = sub.Subs!;
                var hasMultipleSubs = firstSub.NextSub != null;

                if (hasMultipleSubs)
                {
                    link = stack.Pop();
                }
                else
                {
                    link = firstSub;
                }

                if (dirty)
                {
                    if (_update(sub))
                    {
                        if (hasMultipleSubs)
                        {
                            ShallowPropagate(firstSub);
                        }
                        sub = link.Sub;
                        continue;
                    }
                }
                else
                {
                    sub.Flags &= ~ReactiveFlags.Pending;
                }
                
                sub = link.Sub;

                if (link.NextDep != null)
                {
                    link = link.NextDep;
                    goto top;
                }
                dirty = false;
            }
            
            return dirty;
        } while (true);
    }
    
    private bool IsValidLink(ILink checkLink, IReactiveNode sub)
    {
        if (sub.DepsTail != null)
        {
            var link = sub.Deps!;
            do
            {
                if (link == checkLink) return true;
                if (link == sub.DepsTail) break;
                link = link.NextDep!;
            } while (link != null);
        }
        return false;
    }
}