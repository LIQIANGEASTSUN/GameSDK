namespace GameSDK.Tests
{
    public sealed class PublicConstructorMonoProbe : SingletonMono<PublicConstructorMonoProbe>
    {
        public PublicConstructorMonoProbe() { }

        public int InitializeCalls { get; private set; }
        public int ReleaseCalls { get; private set; }

        protected override void OnInitialize() => InitializeCalls++;
        protected override void OnRelease() => ReleaseCalls++;
    }

    public sealed class PublicOverloadMonoProbe : SingletonMono<PublicOverloadMonoProbe>
    {
        private PublicOverloadMonoProbe() { }
        public PublicOverloadMonoProbe(int ignored) { }
    }

    public class ProtectedConstructorMonoProbe : SingletonMono<ProtectedConstructorMonoProbe>
    {
        protected ProtectedConstructorMonoProbe() { }
    }

    public abstract class AbstractMonoProbe : SingletonMono<AbstractMonoProbe>
    {
        private AbstractMonoProbe() { }
    }
}
