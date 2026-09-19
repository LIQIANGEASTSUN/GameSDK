using UnityEngine;

namespace GameSDK.Tests
{
    [ExecuteAlways]
    public sealed class SingletonMonoEditWorldProbe : SingletonMono<SingletonMonoEditWorldProbe>
    {
        private SingletonMonoEditWorldProbe() { }

        public static int InitializeCount { get; set; }
        public static int ReleaseCount { get; set; }
        public static int EnableCount { get; set; }

        public static void ResetObservations()
        {
            InitializeCount = 0;
            ReleaseCount = 0;
            EnableCount = 0;
        }

        protected override void OnInitialize() => InitializeCount++;
        protected override void OnRelease() => ReleaseCount++;
        private void OnEnable() => EnableCount++;
    }
}
