using System;

namespace GameSDK
{
    /// <summary>
    /// 抽象状态基类。
    /// </summary>
    public abstract class StateBase
    {
        public int State { get; }
        protected int From { get; private set; } = StateMachine.NoState;
        protected int To { get; private set; } = StateMachine.NoState;
        protected object Data { get; private set; }

        /// <summary>所属状态机；未注册或已释放时为 null。</summary>
        protected internal StateMachine Machine { get; internal set; }
        
        protected StateBase(int state)
        {
            State = state;
        }

        internal void SetFrom(int from)
        {
            From = from;
        }

        internal void SetTo(int to)
        {
            To = to;
        }

        internal void SetData(object data)
        {
            Data = data;
        }

        protected internal virtual void OnEnter() { }
        protected internal virtual void OnExecute() { }
        protected internal virtual void OnExecute(float deltaTime) { }
        protected internal virtual void OnExit() { }
        protected internal virtual void OnRelease() { }
    }
}
