using System;
using System.Collections.Generic;
using AlienSignals.Core.Interfaces;
using AlienSignals.Core.Systems;

namespace AlienSignals.Core
{
    public static class Reactive
    {
        private static readonly Stack<ISubscriber> _pauseStack = new Stack<ISubscriber>();
        private static readonly ReactiveSystem _system;
        private static int _batchDepth = 0;
        private static ISubscriber _activeSub = null;
        private static EffectScope _activeScope = null;

        static Reactive()
        {
            _system = new ReactiveSystem(UpdateComputed, NotifyEffect);
        }

        public static void StartBatch()
        {
            _batchDepth++;
        }

        public static void EndBatch()
        {
            if (--_batchDepth == 0)
            {
                _system.ProcessEffectNotifications();
            }
        }

        public static void PauseTracking()
        {
            _pauseStack.Push(_activeSub);
            _activeSub = null;
        }

        public static void ResumeTracking()
        {
            _activeSub = _pauseStack.Pop();
        }

        public static Signal<T> CreateSignal<T>(T initialValue = default)
        {
            return new Signal<T>(initialValue);
        }

        public static Computed<T> CreateComputed<T>(Func<T, T> getter)
        {
            return new Computed<T>(getter);
        }

        public static Effect CreateEffect(Action fn)
        {
            var e = new Effect(fn);
            if (_activeSub != null)
            {
                _system.Link(e, _activeSub);
            }
            else if (_activeScope != null)
            {
                _system.Link(e, _activeScope);
            }

            var prevSub = _activeSub;
            _activeSub = e;
            try
            {
                e.Fn();
            }
            finally
            {
                _activeSub = prevSub;
            }
            return e;
        }

        public static EffectScope CreateEffectScope(Action fn)
        {
            var e = new EffectScope();
            var prevScope = _activeScope;
            _activeScope = e;
            try
            {
                fn();
            }
            finally
            {
                _activeScope = prevScope;
            }
            return e;
        }

        public static T GetValue<T>(this Computed<T> computed)
        {
            var flags = computed.Flags;
            if ((flags & (SubscriberFlags.Dirty | SubscriberFlags.PendingComputed)) != 0)
            {
                _system.ProcessComputedUpdate(computed, flags);
            }
            if (_activeSub != null)
            {
                _system.Link(computed, _activeSub);
            }
            else if (_activeScope != null)
            {
                _system.Link(computed, _activeScope);
            }
            return computed.CurrentValue;
        }

        public static T GetValue<T>(this Signal<T> signal)
        {
            if (_activeSub != null)
            {
                _system.Link(signal, _activeSub);
            }
            return signal.CurrentValue;
        }

        public static void SetValue<T>(this Signal<T> signal, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(signal.CurrentValue, value))
            {
                signal.CurrentValue = value;
                var subs = signal.Subs;
                if (subs != null)
                {
                    _system.Propagate(subs);
                    if (_batchDepth == 0)
                    {
                        _system.ProcessEffectNotifications();
                    }
                }
            }
        }

        public static void Stop(this ISubscriber subscriber)
        {
            _system.StartTracking(subscriber);
            _system.EndTracking(subscriber);
        }

        private static bool UpdateComputed(ISubscriber computed)
        {
            var prevSub = _activeSub;
            _activeSub = computed;
            _system.StartTracking(computed);
            try
            {
                var computedT = computed as Computed<object>;
                var oldValue = computedT.CurrentValue;
                var newValue = computedT.Getter(oldValue);
                if (!EqualityComparer<object>.Default.Equals(oldValue, newValue))
                {
                    computedT.CurrentValue = newValue;
                    return true;
                }
                return false;
            }
            finally
            {
                _activeSub = prevSub;
                _system.EndTracking(computed);
            }
        }

        private static bool NotifyEffect(ISubscriber e)
        {
            if (e is EffectScope scope)
            {
                return NotifyEffectScope(scope);
            }
            else if (e is Effect effect)
            {
                return NotifyEffect(effect);
            }
            return false;
        }

        private static bool NotifyEffect(Effect e)
        {
            var flags = e.Flags;
            if ((flags & SubscriberFlags.Dirty) != 0 
                || ((flags & SubscriberFlags.PendingComputed) != 0 && _system.UpdateDirtyFlag(e, flags)))
            {
                var prevSub = _activeSub;
                _activeSub = e;
                _system.StartTracking(e);
                try
                {
                    e.Fn();
                }
                finally
                {
                    _activeSub = prevSub;
                    _system.EndTracking(e);
                }
            }
            else
            {
                _system.ProcessPendingInnerEffects(e, e.Flags);
            }
            return true;
        }

        private static bool NotifyEffectScope(EffectScope e)
        {
            var flags = e.Flags;
            if ((flags & SubscriberFlags.PendingEffect) != 0)
            {
                _system.ProcessPendingInnerEffects(e, e.Flags);
                return true;
            }
            return false;
        }
    }
}