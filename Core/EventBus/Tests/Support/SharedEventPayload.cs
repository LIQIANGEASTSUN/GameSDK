namespace GameSDK.EventBusTestSupport
{
    // Defined once in this assembly and consumed by listeners in both test assemblies.
    public sealed class SharedEventPayload
    {
        public int Value;
    }

    public sealed class SharedPayloadConsumer
    {
        public SharedEventPayload Received { get; private set; }
        public int Calls { get; private set; }

        public void Subscribe(EventBus bus, string key)
        {
            bus.Subscribe<SharedEventPayload>(key, Receive);
        }
        public void Unsubscribe(EventBus bus, string key)
        {
            bus.Unsubscribe<SharedEventPayload>(key, Receive);
        }

        private void Receive(SharedEventPayload payload)
        {
            Received = payload;
            Calls++;
        }
    }
}
