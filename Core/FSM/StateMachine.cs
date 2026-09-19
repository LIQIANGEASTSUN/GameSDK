using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace GameSDK
{
    /// <summary>
    /// 同步、单线程状态机，切换立即完成。
    /// </summary>
    public sealed class StateMachine
    {
        public const int NoState = -1;
        private readonly Dictionary<int, StateBase> _states = new ();
        public StateBase Current { get; private set; }

        public void AddState(StateBase state)
        {
            if (_states.ContainsKey(state.State))
                throw new InvalidOperationException("A state with the same ID is already registered.");
            if (state.Machine != null)
                throw new InvalidOperationException("The state is already registered with a state machine.");

            _states.Add(state.State, state);
            state.Machine = this;
        }

        /// <summary>退出当前状态，保留注册。</summary>
        public void Init()
        {
            OnExit();
        }

        public void ChangeState(int stateId, object data = null)
        {
            if (!_states.TryGetValue(stateId, out StateBase target))
                throw new ArgumentException("The target state is not registered.", nameof(stateId));
            if (ReferenceEquals(Current, target))
                return;

            int from = Current == null ? NoState : Current.State;
            ExitCurrent(target.State);
            target.SetFrom(from);
            target.SetTo(NoState);
            target.SetData(data);
            Current = target;
            try
            {
                target.OnEnter();
            }
            catch (Exception enterError)
            {
                try
                {
                    ExitCurrent(NoState);
                }
                catch (Exception exitError)
                {
                    throw new AggregateException("State entry and cleanup both failed.", enterError, exitError);
                }
                throw;
            }
        }

        public void OnExecute()
        {
            Current?.OnExecute();
        }
        
        public void OnExecute(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Time must be finite and non-negative.");

            Current?.OnExecute(deltaTime);
        }

        public void OnExit()
        {
            ExitCurrent(NoState);
        }

        /// <summary>退出当前状态，释放所有注册状态并解除关联。</summary>
        public void OnRelease()
        {
            List<Exception> errors = null;
            try
            {
                try
                {
                    OnExit();
                }
                catch (Exception error)
                {
                    RecordError(ref errors, error);
                }

                foreach (StateBase state in _states.Values)
                {
                    try
                    {
                        state.OnRelease();
                    }
                    catch (Exception error)
                    {
                        RecordError(ref errors, error);
                    }
                }
            }
            finally
            {
                foreach (StateBase state in _states.Values)
                {
                    state.Machine = null;
                    state.SetFrom(NoState);
                    state.SetTo(NoState);
                    state.SetData(null);
                }
                _states.Clear();
                Current = null;
            }

            if (errors == null)
                return;
            if (errors.Count == 1)
                ExceptionDispatchInfo.Capture(errors[0]).Throw();
            throw new AggregateException("State cleanup encountered multiple errors.", errors);
        }

        private void ExitCurrent(int to)
        {
            StateBase state = Current;
            if (state == null)
                return;

            state.SetTo(to);
            try
            {
                state.OnExit();
            }
            finally
            {
                Current = null;
                state.SetData(null);
            }
        }

        private static void RecordError(ref List<Exception> errors, Exception error)
        {
            if (errors == null)
                errors = new List<Exception>();
            errors.Add(error);
        }
    }
}
