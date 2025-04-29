using System;
using System.Collections.Generic;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core.Systems
{
    public class ReactiveSystem
    {
        private readonly Func<ISubscriber, bool> _updateComputed;
        private readonly Func<ISubscriber, bool> _notifyEffect;
        
        private readonly List<ISubscriber> _notifyBuffer = new List<ISubscriber>();
        private int _notifyIndex = 0;
        private int _notifyBufferLength = 0;

        public ReactiveSystem(Func<ISubscriber, bool> updateComputed, Func<ISubscriber, bool> notifyEffect)
        {
            _updateComputed = updateComputed;
            _notifyEffect = notifyEffect;
        }

        public Link Link(IDependency dep, ISubscriber sub)
        {
            var currentDep = sub.DepsTail;
            if (currentDep != null && currentDep.Dep == dep)
            {
                return null;
            }

            var nextDep = currentDep != null ? currentDep.NextDep : sub.Deps;
            if (nextDep != null && nextDep.Dep == dep)
            {
                sub.DepsTail = nextDep;
                return null;
            }

            var depLastSub = dep.SubsTail;
            if (depLastSub != null && depLastSub.Sub == sub && IsValidLink(depLastSub, sub))
            {
                return null;
            }

            return LinkNewDep(dep, sub, nextDep, currentDep);
        }

        public void Propagate(Link current)
        {
            var next = current.NextSub;
            OneWayLink<Link> branchs = null;
            var branchDepth = 0;
            var targetFlag = SubscriberFlags.Dirty;

            while (true)
            {
                var sub = current.Sub;
                var subFlags = sub.Flags;

                var shouldNotify = false;

                if ((subFlags & (SubscriberFlags.Tracking | SubscriberFlags.Recursed | SubscriberFlags.Propagated)) == 0)
                {
                    sub.Flags = subFlags | targetFlag | SubscriberFlags.Notified;
                    shouldNotify = true;
                }
                else if ((subFlags & SubscriberFlags.Recursed) != 0 && (subFlags & SubscriberFlags.Tracking) == 0)
                {
                    sub.Flags = (subFlags & ~SubscriberFlags.Recursed) | targetFlag | SubscriberFlags.Notified;
                    shouldNotify = true;
                }
                else if ((subFlags & SubscriberFlags.Propagated) == 0 && IsValidLink(current, sub))
                {
                    sub.Flags = subFlags | SubscriberFlags.Recursed | targetFlag | SubscriberFlags.Notified;
                    shouldNotify = (sub as IDependency)?.Subs != null;
                }

                if (shouldNotify)
                {
                    var subSubs = (sub as IDependency)?.Subs;
                    if (subSubs != null)
                    {
                        current = subSubs;
                        if (subSubs.NextSub != null)
                        {
                            branchs = new OneWayLink<Link> { Target = next, Linked = branchs };
                            branchDepth++;
                            next = current.NextSub;
                            targetFlag = SubscriberFlags.PendingComputed;
                            continue;
                        }
                        else
                        {
                            targetFlag = (subFlags & SubscriberFlags.Effect) != 0
                                ? SubscriberFlags.PendingEffect
                                : SubscriberFlags.PendingComputed;
                        }
                        continue;
                    }
                    if ((subFlags & SubscriberFlags.Effect) != 0)
                    {
                        if (_notifyBuffer.Count <= _notifyBufferLength)
                            _notifyBuffer.Add(sub);
                        else
                            _notifyBuffer[_notifyBufferLength] = sub;
                        _notifyBufferLength++;
                    }
                }
                else if ((subFlags & (SubscriberFlags.Tracking | targetFlag)) == 0)
                {
                    sub.Flags = subFlags | targetFlag | SubscriberFlags.Notified;
                    if ((subFlags & (SubscriberFlags.Effect | SubscriberFlags.Notified)) == SubscriberFlags.Effect)
                    {
                        if (_notifyBuffer.Count <= _notifyBufferLength)
                            _notifyBuffer.Add(sub);
                        else
                            _notifyBuffer[_notifyBufferLength] = sub;
                        _notifyBufferLength++;
                    }
                }
                else if ((subFlags & targetFlag) == 0 
                         && (subFlags & SubscriberFlags.Propagated) != 0 
                         && IsValidLink(current, sub))
                {
                    sub.Flags = subFlags | targetFlag;
                }

                if ((current = next) != null)
                {
                    next = current.NextSub;
                    targetFlag = branchDepth > 0
                        ? SubscriberFlags.PendingComputed
                        : SubscriberFlags.Dirty;
                    continue;
                }

                while (branchDepth > 0)
                {
                    branchDepth--;
                    current = branchs.Target;
                    branchs = branchs.Linked;
                    if (current != null)
                    {
                        next = current.NextSub;
                        targetFlag = branchDepth > 0
                            ? SubscriberFlags.PendingComputed
                            : SubscriberFlags.Dirty;
                        continue;
                    }
                }

                break;
            }
        }

        public void StartTracking(ISubscriber sub)
        {
            sub.DepsTail = null;
            sub.Flags = (sub.Flags & ~(SubscriberFlags.Notified | SubscriberFlags.Recursed | SubscriberFlags.Propagated)) | SubscriberFlags.Tracking;
        }

        public void EndTracking(ISubscriber sub)
        {
            var depsTail = sub.DepsTail;
            if (depsTail != null)
            {
                var nextDep = depsTail.NextDep;
                if (nextDep != null)
                {
                    ClearTracking(nextDep);
                    depsTail.NextDep = null;
                }
            }
            else if (sub.Deps != null)
            {
                ClearTracking(sub.Deps);
                sub.Deps = null;
            }
            sub.Flags &= ~SubscriberFlags.Tracking;
        }

        public bool UpdateDirtyFlag(ISubscriber sub, SubscriberFlags flags)
        {
            if (CheckDirty(sub.Deps))
            {
                sub.Flags = flags | SubscriberFlags.Dirty;
                return true;
            }
            else
            {
                sub.Flags = flags & ~SubscriberFlags.PendingComputed;
                return false;
            }
        }

        public void ProcessComputedUpdate(ISubscriber computed, SubscriberFlags flags)
        {
            if ((flags & SubscriberFlags.Dirty) != 0 || CheckDirty(computed.Deps))
            {
                if (_updateComputed(computed))
                {
                    var subs = (computed as IDependency)?.Subs;
                    if (subs != null)
                    {
                        ShallowPropagate(subs);
                    }
                }
            }
            else
            {
                computed.Flags = flags & ~SubscriberFlags.PendingComputed;
            }
        }

        public void ProcessPendingInnerEffects(ISubscriber sub, SubscriberFlags flags)
        {
            if ((flags & SubscriberFlags.PendingEffect) != 0)
            {
                sub.Flags = flags & ~SubscriberFlags.PendingEffect;
                var link = sub.Deps;
                do
                {
                    var dep = link.Dep;
                    if (dep is ISubscriber depSub 
                        && (depSub.Flags & SubscriberFlags.Effect) != 0 
                        && (depSub.Flags & SubscriberFlags.Propagated) != 0)
                    {
                        _notifyEffect(depSub);
                    }
                    link = link.NextDep;
                } while (link != null);
            }
        }

        public void ProcessEffectNotifications()
        {
            while (_notifyIndex < _notifyBufferLength)
            {
                var effect = _notifyBuffer[_notifyIndex];
                _notifyBuffer[_notifyIndex] = null;
                if (!_notifyEffect(effect))
                {
                    effect.Flags &= ~SubscriberFlags.Notified;
                }
                _notifyIndex++;
            }
            _notifyIndex = 0;
            _notifyBufferLength = 0;
        }

        private Link LinkNewDep(IDependency dep, ISubscriber sub, Link nextDep, Link depsTail)
        {
            var newLink = new Link
            {
                Dep = dep,
                Sub = sub,
                NextDep = nextDep,
                PrevSub = null,
                NextSub = null
            };

            if (depsTail == null)
            {
                sub.Deps = newLink;
            }
            else
            {
                depsTail.NextDep = newLink;
            }

            if (dep.Subs == null)
            {
                dep.Subs = newLink;
            }
            else
            {
                var oldTail = dep.SubsTail;
                newLink.PrevSub = oldTail;
                oldTail.NextSub = newLink;
            }

            sub.DepsTail = newLink;
            dep.SubsTail = newLink;
            return newLink;
        }

        private bool CheckDirty(Link current)
        {
            OneWayLink<Link> prevLinks = null;
            var checkDepth = 0;
            var dirty = false;

            while (true)
            {
                dirty = false;
                var dep = current.Dep;

                if ((current.Sub.Flags & SubscriberFlags.Dirty) != 0)
                {
                    dirty = true;
                }
                else if (dep is ISubscriber depSub)
                {
                    var depFlags = depSub.Flags;
                    if ((depFlags & (SubscriberFlags.Computed | SubscriberFlags.Dirty)) == (SubscriberFlags.Computed | SubscriberFlags.Dirty))
                    {
                        if (_updateComputed(depSub))
                        {
                            var subs = (depSub as IDependency)?.Subs;
                            if (subs?.NextSub != null)
                            {
                                ShallowPropagate(subs);
                            }
                            dirty = true;
                        }
                    }
                    else if ((depFlags & (SubscriberFlags.Computed | SubscriberFlags.PendingComputed)) == (SubscriberFlags.Computed | SubscriberFlags.PendingComputed))
                    {
                        if (current.NextSub != null || current.PrevSub != null)
                        {
                            prevLinks = new OneWayLink<Link> { Target = current, Linked = prevLinks };
                        }
                        current = depSub.Deps;
                        checkDepth++;
                        continue;
                    }
                }

                if (!dirty && current.NextDep != null)
                {
                    current = current.NextDep;
                    continue;
                }

                while (checkDepth > 0)
                {
                    checkDepth--;
                    var sub = current.Sub as ISubscriber;
                    var firstSub = (sub as IDependency)?.Subs;
                    if (dirty)
                    {
                        if (_updateComputed(sub))
                        {
                            if (firstSub?.NextSub != null)
                            {
                                current = prevLinks.Target;
                                prevLinks = prevLinks.Linked;
                                ShallowPropagate(firstSub);
                            }
                            else
                            {
                                current = firstSub;
                            }
                            continue;
                        }
                    }
                    else
                    {
                        sub.Flags &= ~SubscriberFlags.PendingComputed;
                    }
                    if (firstSub?.NextSub != null)
                    {
                        current = prevLinks.Target;
                        prevLinks = prevLinks.Linked;
                    }
                    else
                    {
                        current = firstSub;
                    }
                    if (current?.NextDep != null)
                    {
                        current = current.NextDep;
                        continue;
                    }
                    dirty = false;
                }

                return dirty;
            }
        }

        private void ShallowPropagate(Link link)
        {
            do
            {
                var sub = link.Sub;
                var subFlags = sub.Flags;
                if ((subFlags & (SubscriberFlags.PendingComputed | SubscriberFlags.Dirty)) == SubscriberFlags.PendingComputed)
                {
                    sub.Flags = subFlags | SubscriberFlags.Dirty | SubscriberFlags.Notified;
                    if ((subFlags & (SubscriberFlags.Effect | SubscriberFlags.Notified)) == SubscriberFlags.Effect)
                    {
                        if (_notifyBuffer.Count <= _notifyBufferLength)
                            _notifyBuffer.Add(sub);
                        else
                            _notifyBuffer[_notifyBufferLength] = sub;
                        _notifyBufferLength++;
                    }
                }
                link = link.NextSub;
            } while (link != null);
        }

        private bool IsValidLink(Link checkLink, ISubscriber sub)
        {
            var depsTail = sub.DepsTail;
            if (depsTail != null)
            {
                var link = sub.Deps;
                do
                {
                    if (link == checkLink)
                    {
                        return true;
                    }
                    if (link == depsTail)
                    {
                        break;
                    }
                    link = link.NextDep;
                } while (link != null);
            }
            return false;
        }

        private void ClearTracking(Link link)
        {
            do
            {
                var dep = link.Dep;
                var nextDep = link.NextDep;
                var nextSub = link.NextSub;
                var prevSub = link.PrevSub;

                if (nextSub != null)
                {
                    nextSub.PrevSub = prevSub;
                }
                else
                {
                    dep.SubsTail = prevSub;
                }

                if (prevSub != null)
                {
                    prevSub.NextSub = nextSub;
                }
                else
                {
                    dep.Subs = nextSub;
                }

                if (dep.Subs == null && dep is ISubscriber depSub)
                {
                    var depFlags = depSub.Flags;
                    if ((depFlags & SubscriberFlags.Dirty) == 0)
                    {
                        depSub.Flags = depFlags | SubscriberFlags.Dirty;
                    }
                    var depDeps = depSub.Deps;
                    if (depDeps != null)
                    {
                        link = depDeps;
                        depSub.DepsTail.NextDep = nextDep;
                        depSub.Deps = null;
                        depSub.DepsTail = null;
                        continue;
                    }
                }
                link = nextDep;
            } while (link != null);
        }
    }
}