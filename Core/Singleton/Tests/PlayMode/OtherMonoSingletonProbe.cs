namespace GameSDK
{
    public sealed class OtherMonoSingletonProbe : SingletonMono<OtherMonoSingletonProbe>
    {
        private OtherMonoSingletonProbe() { }
        public int InitializeCalls { get; private set; }
        public int ReleaseCalls { get; private set; }
        protected override void OnInitialize() => InitializeCalls++;
        protected override void OnRelease() => ReleaseCalls++;
    }
}
