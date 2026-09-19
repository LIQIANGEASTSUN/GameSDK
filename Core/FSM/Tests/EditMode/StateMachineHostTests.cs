using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace GameSDK
{
    public sealed class StateMachineHostTests
    {
        [Test]
        public void RealHostRejectsOldAToBToAResultAndReturnsEveryOwnedResource()
        {
            var host = new MinimalHost();
            var entryData = new object();
            host.Start(entryData);
            Assert.That(host.Machine.Current, Is.SameAs(host.First));
            Assert.That(host.First.LastFrom, Is.EqualTo(StateMachine.NoState));
            Assert.That(host.First.EntryData, Is.SameAs(entryData));
            LoadRequest oldFirstRequest = host.First.Request;
            host.Tick(0.5f);
            Assert.That(host.First.UpdateCount, Is.EqualTo(1));
            Assert.That(host.First.LastDeltaTime, Is.EqualTo(0.5f));

            var secondEntryData = new object();
            host.Select(2, secondEntryData);
            Assert.That(oldFirstRequest.IsCancelled, Is.True);
            Assert.That(host.First.LastTo, Is.EqualTo(2));
            Assert.That(host.Second.LastFrom, Is.EqualTo(1));
            Assert.That(host.Second.EntryData, Is.SameAs(secondEntryData));
            LoadRequest secondRequest = host.Second.Request;
            host.Select(1);
            LoadRequest newFirstRequest = host.First.Request;
            Assert.That(newFirstRequest, Is.Not.SameAs(oldFirstRequest));
            Assert.That(host.Machine.Current, Is.SameAs(host.First));
            Assert.That(host.First.IsActive, Is.True);
            Assert.That(host.First.LastFrom, Is.EqualTo(2));
            Assert.That(host.First.EntryData, Is.Null);

            var staleResource = new TrackedResource();
            var currentResource = new TrackedResource();
            oldFirstRequest.Complete(staleResource);
            newFirstRequest.Complete(currentResource);
            Assert.That(host.First.Resource, Is.Null, "Completion must be posted to the host thread.");
            host.DrainMainThread();
            Assert.That(staleResource.ReturnCount, Is.EqualTo(1));
            Assert.That(host.First.Resource, Is.SameAs(currentResource));
            Assert.That(currentResource.ReturnCount, Is.Zero);

            host.Dispose();
            Assert.That(currentResource.ReturnCount, Is.EqualTo(1));
            Assert.That(newFirstRequest.IsCancelled, Is.True);
            Assert.That(host.First.LastTo, Is.EqualTo(StateMachine.NoState));
            Assert.That(host.Machine.Current, Is.Null);
            Assert.That(host.First.ReleaseCount, Is.EqualTo(1));
            Assert.That(host.Second.ReleaseCount, Is.EqualTo(1));
            var afterRelease = new TrackedResource();
            secondRequest.Complete(afterRelease);
            host.DrainMainThread();
            Assert.That(afterRelease.ReturnCount, Is.EqualTo(1));
            Assert.That(host.First.Resource, Is.Null);
            Assert.That(host.Second.Resource, Is.Null);
            host.Dispose();
            Assert.That(currentResource.ReturnCount, Is.EqualTo(1));
        }

        [Test]
        public void HostInvalidatesBeforeCancellationAndProcessesItsPostedResultAfterExit()
        {
            var host = new MinimalHost();
            host.Start();
            LoadRequest request = host.First.Request;
            var resource = new TrackedResource();
            request.OnCancel = () =>
            {
                Assert.That(host.Machine.Current, Is.SameAs(host.First));
                Assert.That(host.First.IsActive, Is.False);
                request.Complete(resource);
            };
            host.Select(2);
            Assert.That(resource.ReturnCount, Is.Zero);
            host.DrainMainThread();
            Assert.That(resource.ReturnCount, Is.EqualTo(1));
            Assert.That(host.First.Resource, Is.Null);
            host.Dispose();
        }

        // This consumer models a main-thread host and an async provider whose cancellation
        // cannot prevent an already-completing operation from delivering a resource.
        private sealed class MinimalHost : IDisposable
        {
            private readonly Queue<Action> mainThreadQueue = new Queue<Action>();
            private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
            public readonly StateMachine Machine = new StateMachine();
            public readonly LoadingState First;
            public readonly LoadingState Second;
            public bool IsAlive { get; private set; } = true;

            public MinimalHost()
            {
                First = new LoadingState(1, this);
                Second = new LoadingState(2, this);
                Machine.AddState(First);
                Machine.AddState(Second);
            }

            public void Start(object data = null)
            {
                AssertMainThread();
                Machine.Init();
                Machine.ChangeState(1, data);
            }

            public void Select(int id, object data = null)
            {
                AssertMainThread();
                Machine.ChangeState(id, data);
            }

            public void Tick(float deltaTime)
            {
                AssertMainThread();
                Machine.OnExecute(deltaTime);
            }

            public void Post(Action completion)
            {
                // Tests simulate delivery deterministically; no background thread drives FSM.
                mainThreadQueue.Enqueue(completion);
            }

            public void DrainMainThread()
            {
                AssertMainThread();
                while (mainThreadQueue.Count != 0) mainThreadQueue.Dequeue()();
            }

            public void Dispose()
            {
                AssertMainThread();
                IsAlive = false;
                Machine.OnRelease();
            }

            private void AssertMainThread()
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(threadId));
            }
        }

        private sealed class LoadingState : StateBase
        {
            private readonly MinimalHost host;
            private long generation;
            public bool IsActive { get; private set; }
            public LoadRequest Request { get; private set; }
            public TrackedResource Resource { get; private set; }
            public int UpdateCount { get; private set; }
            public float LastDeltaTime { get; private set; }
            public int ReleaseCount { get; private set; }
            public int LastFrom { get; private set; }
            public int LastTo { get; private set; }
            public object EntryData { get; private set; }

            public LoadingState(int id, MinimalHost host) : base(id)
            {
                this.host = host;
            }

            protected override void OnEnter()
            {
                LastFrom = From;
                EntryData = Data;
                long entryGeneration = ++generation;
                IsActive = true;
                Request = new LoadRequest(host, resource =>
                {
                    if (!host.IsAlive || Machine == null || !ReferenceEquals(Machine.Current, this) ||
                        !IsActive || generation != entryGeneration)
                    {
                        resource.Return();
                        return;
                    }
                    Resource = resource;
                });
            }

            protected override void OnExecute(float deltaTime)
            {
                UpdateCount++;
                LastDeltaTime = deltaTime;
            }

            protected override void OnExit()
            {
                LastTo = To;
                InvalidateAndCleanUp();
            }

            protected override void OnRelease()
            {
                ReleaseCount++;
                InvalidateAndCleanUp();
            }

            private void InvalidateAndCleanUp()
            {
                IsActive = false;
                generation++;
                Request?.Cancel();
                Resource?.Return();
                Resource = null;
            }
        }

        private sealed class LoadRequest
        {
            private readonly MinimalHost host;
            private readonly Action<TrackedResource> completion;
            public bool IsCancelled { get; private set; }
            public Action OnCancel;

            public LoadRequest(MinimalHost host, Action<TrackedResource> completion)
            {
                this.host = host;
                this.completion = completion;
            }

            public void Complete(TrackedResource resource)
            {
                host.Post(() => completion(resource));
            }

            public void Cancel()
            {
                if (IsCancelled) return;
                IsCancelled = true;
                OnCancel?.Invoke();
            }
        }

        private sealed class TrackedResource
        {
            public int ReturnCount { get; private set; }

            public void Return()
            {
                ReturnCount++;
            }
        }
    }
}
