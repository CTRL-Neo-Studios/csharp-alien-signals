using System;
using System.Collections.Generic;
using System.Linq;
using AlienSignals.Runtime.Core.Enums;
using AlienSignals.Runtime.Core.Interfaces;
using AlienSignals.Runtime.Core.Internal;

namespace AlienSignals.Runtime.Core;

public static class Signals
{
    [Flags]
    private enum EffectFlags { Queued = 1 << 6 }
    
    private static readonly ReactiveSystem System;
    private static readonly List<IReactiveNode?> QueuedEffects = new();
    private static readonly Stack<IReactiveNode?> PauseStack = new();

    private static int _batchDepth = 0;
    private static int _notifyIndex = 0;
    private static int _queuedEffectsLength = 0;
    
    [ThreadStatic] private static IReactiveNode? _activeSub;
    [ThreadStatic] private static IEffectScope? _activeScope;

    static Signals()
    {
        System = new ReactiveSystem(Update, Notify, Unwatched);
    }
    
    #region Public API
    
    public static ISignal<T> CreateSignal<T>(T initialValue) => new Signal<T>(initialValue);
    public static IComputed<T> CreateComputed<T>(Func<T?, T> getter) => new Computed<T>(getter);
    public static IEffect CreateEffect(Action fn) => new Effect(fn);
    public static IEffectScope CreateEffectScope(Action fn) => new EffectScope(fn);
    
    public static IReactiveNode? GetCurrentSub() => _activeSub;
    public static IReactiveNode? SetCurrentSub(IReactiveNode? sub)
    {
        var prevSub = _activeSub;
        _activeSub = sub;
        return prevSub;
    }

    public static IEffectScope? GetCurrentScope() => _activeScope;
    public static IEffectScope? SetCurrentScope(IEffectScope? scope)
    {
        var prevScope = _activeScope;
        _activeScope = scope;
        return prevScope;
    }
    
    public static void StartBatch() => ++_batchDepth;
    
    public static void EndBatch()
    {
        if (--_batchDepth == 0)
        {
            Flush();
        }
    }
    
    [Obsolete("Will be removed in the next major version. Use `var pausedSub = SetCurrentSub(null)` instead for better performance.")]
    public static void PauseTracking() => PauseStack.Push(SetCurrentSub(null));

    [Obsolete("Will be removed in the next major version. Use `SetCurrentSub(pausedSub)` instead for better performance.")]
    public static void ResumeTracking() => SetCurrentSub(PauseStack.Pop());
    
    #endregion

    #region Internal Logic (delegated from concrete classes)
    
    internal static T ComputedGet<T>(Computed<T> c)
    {
        var flags = c.Flags;
        if ((flags & ReactiveFlags.Dirty) != 0 ||
            ((flags & ReactiveFlags.Pending) != 0 && System.CheckDirty(c.Deps!, c)))
        {
            if (UpdateComputed(c))
            {
                if (c.Subs != null) System.ShallowPropagate(c.Subs);
            }
        }
        else if ((flags & ReactiveFlags.Pending) != 0)
        {
            c.Flags = flags & ~ReactiveFlags.Pending;
        }

        if (_activeSub != null) System.Link(c, _activeSub);
        else if (_activeScope != null) System.Link(c, _activeScope);

        return c.Value!;
    }
    
    internal static T SignalGet<T>(Signal<T> s)
    {
        if ((s.Flags & ReactiveFlags.Dirty) != 0)
        {
            if(UpdateSignal(s, s.Value))
            {
                if (s.Subs != null) System.ShallowPropagate(s.Subs);
            }
        }

        if (_activeSub != null) System.Link(s, _activeSub);
        
        return s.Value;
    }

    internal static void SignalSet<T>(Signal<T> s, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(s.Value, newValue))
        {
            s.Value = newValue;
            s.Flags = ReactiveFlags.Mutable | ReactiveFlags.Dirty;
            if (s.Subs != null)
            {
                System.Propagate(s.Subs);
                if (_batchDepth == 0) Flush();
            }
        }
    }
    
    internal static void RunEffectInitial(Effect effect)
    {
        if (_activeSub != null) System.Link(effect, _activeSub);
        else if (_activeScope != null) System.Link(effect, _activeScope);

        var prev = SetCurrentSub(effect);
        try
        {
            effect.Fn();
        }
        finally
        {
            SetCurrentSub(prev);
        }
    }

    internal static void RunEffectScope(EffectScope scope, Action fn)
    {
        if (_activeScope != null) System.Link(scope, _activeScope);

        var prevSub = SetCurrentSub(null);
        var prevScope = SetCurrentScope(scope);
        try
        {
            fn();
        }
        finally
        {
            SetCurrentScope(prevScope);
            SetCurrentSub(prevSub);
        }
    }
    
    internal static void DisposeNode(IReactiveNode node)
    {
        var dep = node.Deps;
        while (dep != null)
        {
            dep = System.Unlink(dep, node);
        }

        var sub = node.Subs;
        if (sub != null)
        {
            System.Unlink(sub);
        }
        node.Flags = ReactiveFlags.None;
    }
    
    #endregion
    
    #region Private System Callbacks
    
    private static bool Update(IReactiveNode node)
    {
        if (node is IComputed<object> c) return UpdateComputed(c as dynamic);
        if (node is Signal<object> s) return UpdateSignal(s, s.Value);
        return false;
    }

    private static void Notify(IReactiveNode node)
    {
        var flags = node.Flags;
        if (((int)flags & (int)EffectFlags.Queued) == 0)
        {
            node.Flags = flags | (ReactiveFlags)EffectFlags.Queued;
            var subs = node.Subs;
            if (subs != null)
            {
                Notify(subs.Sub);
            }
            else
            {
                if (_queuedEffectsLength >= QueuedEffects.Count) QueuedEffects.Add(null);
                QueuedEffects[_queuedEffectsLength++] = node;
            }
        }
    }

    private static void Unwatched(IReactiveNode node)
    {
        if (node is IComputed<object> c)
        {
            var toRemove = c.Deps;
            if (toRemove != null)
            {
                c.Flags = ReactiveFlags.Mutable | ReactiveFlags.Dirty;
                do
                {
                    toRemove = System.Unlink(toRemove, c);
                } while (toRemove != null);
            }
        }
        else if (node is IEffect || node is IEffectScope)
        {
            DisposeNode(node);
        }
    }
    
    private static bool UpdateComputed<T>(Computed<T> c)
    {
        var prevSub = SetCurrentSub(c);
        System.StartTracking(c);
        try
        {
            var oldValue = c.Value;
            var newValue = c.Getter(oldValue);
            c.Value = newValue;
            return !EqualityComparer<T>.Default.Equals(oldValue, newValue);
        }
        finally
        {
            SetCurrentSub(prevSub);
            System.EndTracking(c);
        }
    }

    private static bool UpdateSignal<T>(Signal<T> s, T value)
    {
        s.Flags = ReactiveFlags.Mutable;
        var changed = !EqualityComparer<T>.Default.Equals(s.PreviousValue, value);
        s.PreviousValue = value;
        return changed;
    }
    
    private static void Flush()
    {
        while (_notifyIndex < _queuedEffectsLength)
        {
            var effect = QueuedEffects[_notifyIndex]!;
            QueuedEffects[_notifyIndex++] = null; // Clear for GC
            Run(effect, effect.Flags &= ~(ReactiveFlags)EffectFlags.Queued);
        }
        _notifyIndex = 0;
        _queuedEffectsLength = 0;
    }

    private static void Run(IReactiveNode node, ReactiveFlags flags)
    {
        if ((flags & ReactiveFlags.Dirty) != 0 ||
            ((flags & ReactiveFlags.Pending) != 0 && System.CheckDirty(node.Deps!, node)))
        {
            var prev = SetCurrentSub(node);
            System.StartTracking(node);
            try
            {
                if (node is Effect e) e.Fn();
            }
            finally
            {
                SetCurrentSub(prev);
                System.EndTracking(node);
            }
            return;
        }

        if ((flags & ReactiveFlags.Pending) != 0)
        {
            node.Flags = flags & ~ReactiveFlags.Pending;
        }

        var link = node.Deps;
        while (link != null)
        {
            var dep = link.Dep;
            var depFlags = dep.Flags;
            if (((int)depFlags & (int)EffectFlags.Queued) != 0)
            {
                Run(dep, dep.Flags = depFlags & ~(ReactiveFlags)EffectFlags.Queued);
            }
            link = link.NextDep;
        }
    }

    #endregion
}