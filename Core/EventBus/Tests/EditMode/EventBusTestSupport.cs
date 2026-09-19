using System;
using System.Collections.Generic;
using GameSDK.EventBusTestSupport;
using UnityEngine;

namespace GameSDK.Tests.EventBusTests
{
    internal sealed class Listener
    {
        internal int Calls;
        internal int Number;
        internal string Text;
        internal SharedEventPayload Payload;
        internal Action Effect;

        internal void Receive() { Calls++; Effect?.Invoke(); }
        internal void Receive(int number) { Number = number; Receive(); }
        internal void Receive(int number, string text) { Text = text; Receive(number); }
        internal void Receive(int number, string text, SharedEventPayload payload)
        {
            Payload = payload;
            Receive(number, text);
        }
        internal void ReceiveOther()
        {
            Receive();
        }
        internal void ReceiveOther(int number)
        {
            Receive(number);
        }
        internal void ReceiveOther(int number, string text)
        {
            Receive(number, text);
        }
        internal void ReceiveOther(int number, string text, SharedEventPayload payload)
        {
            Receive(number, text, payload);
        }
    }

    // Adapters only select public typed overloads; production dispatch is never reflected or replaced.
    internal sealed class EventCase
    {
        internal readonly SharedEventPayload Payload = new SharedEventPayload { Value = 17 };
        internal readonly Func<Listener, Delegate> Callback;
        internal readonly Func<Listener, Delegate> OtherCallback;
        internal readonly Action<string, Delegate> Subscribe;
        internal readonly Action<string, Delegate> Unsubscribe;
        internal readonly Action<string> Publish;

        internal EventCase(EventBus bus, int arity)
        {
            switch (arity)
            {
                case 0:
                    Callback = listener => new Action(listener.Receive);
                    OtherCallback = listener => new Action(listener.ReceiveOther);
                    Subscribe = (key, callback) => bus.Subscribe(key, (Action)callback);
                    Unsubscribe = (key, callback) => bus.Unsubscribe(key, (Action)callback);
                    Publish = bus.Publish;
                    break;
                case 1:
                    Callback = listener => new Action<int>(listener.Receive);
                    OtherCallback = listener => new Action<int>(listener.ReceiveOther);
                    Subscribe = (key, callback) => bus.Subscribe(key, (Action<int>)callback);
                    Unsubscribe = (key, callback) => bus.Unsubscribe(key, (Action<int>)callback);
                    Publish = key => bus.Publish(key, 42);
                    break;
                case 2:
                    Callback = listener => new Action<int, string>(listener.Receive);
                    OtherCallback = listener => new Action<int, string>(listener.ReceiveOther);
                    Subscribe = (key, callback) => bus.Subscribe(key, (Action<int, string>)callback);
                    Unsubscribe = (key, callback) => bus.Unsubscribe(key, (Action<int, string>)callback);
                    Publish = key => bus.Publish(key, 42, "payload");
                    break;
                case 3:
                    Callback = listener => new Action<int, string, SharedEventPayload>(listener.Receive);
                    OtherCallback = listener => new Action<int, string, SharedEventPayload>(listener.ReceiveOther);
                    Subscribe = (key, callback) => bus.Subscribe(key, (Action<int, string, SharedEventPayload>)callback);
                    Unsubscribe = (key, callback) => bus.Unsubscribe(key, (Action<int, string, SharedEventPayload>)callback);
                    Publish = key => bus.Publish(key, 42, "payload", Payload);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(arity));
            }
        }
    }

    internal sealed class EventLogCapture : ILogHandler, IDisposable
    {
        private readonly ILogger logger = Debug.unityLogger;
        private readonly ILogHandler previousHandler;
        private readonly bool previousEnabled;
        private readonly LogType previousFilter;
        internal readonly List<Exception> Exceptions = new List<Exception>();
        internal int Calls;
        internal Action Report;

        internal EventLogCapture()
        {
            previousHandler = logger.logHandler;
            previousEnabled = logger.logEnabled;
            previousFilter = logger.filterLogType;
            logger.logHandler = this;
            logger.logEnabled = true;
            logger.filterLogType = LogType.Log;
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            Calls++;
            Exceptions.Add(exception);
            Report?.Invoke();
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            throw new InvalidOperationException("EventBus must preserve exceptions via UtilsLog.Exception.");
        }

        public void Dispose()
        {
            logger.logHandler = previousHandler;
            logger.logEnabled = previousEnabled;
            logger.filterLogType = previousFilter;
        }
    }
}
