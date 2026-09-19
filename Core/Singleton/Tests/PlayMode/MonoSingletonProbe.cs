using System;

namespace GameSDK
{
    public class MonoSingletonProbe : SingletonMono<MonoSingletonProbe>
    {
        private MonoSingletonProbe() { }

        public static Action<MonoSingletonProbe> Initializing;
        public static Action<MonoSingletonProbe> Releasing;
        public static Action<MonoSingletonProbe> Awaking;
        public static Action<MonoSingletonProbe> Enabling;
        public static int InitializeCount;
        public static int ReleaseCount;
        public static MonoSingletonProbe LastInitialized;

        public int InitializeCalls { get; private set; }
        public int ReleaseCalls { get; private set; }
        public bool HasResource { get; private set; }

        public static void ResetObservations()
        {
            Initializing = null;
            Releasing = null;
            Awaking = null;
            Enabling = null;
            InitializeCount = 0;
            ReleaseCount = 0;
            LastInitialized = null;
        }

        protected override void OnInitialize()
        {
            InitializeCalls++;
            InitializeCount++;
            HasResource = true;
            LastInitialized = this;
            Initializing?.Invoke(this);
        }

        protected override void OnRelease()
        {
            ReleaseCalls++;
            ReleaseCount++;
            HasResource = false;
            Releasing?.Invoke(this);
        }

        private void Awake() => Awaking?.Invoke(this);
        private void OnEnable() => Enabling?.Invoke(this);

        // Nesting permits a real secondary subtype while keeping T's constructor private.
        public sealed class WrongSubtype : MonoSingletonProbe
        {
            private WrongSubtype() { }
        }
    }
}
