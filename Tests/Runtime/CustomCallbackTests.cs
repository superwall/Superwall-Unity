using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using Superwall;
using Superwall.Internal;

namespace Superwall.Tests
{
    /// <summary>
    /// The C# half of a custom callback: the payload below is what both native bridges send, and
    /// the reply is read from the Editor log of the native call. Native holds the paywall until it
    /// gets a reply, so every path here must produce one.
    /// </summary>
    class CustomCallbackTests
    {
        private const string HandlerId = "custom-callback-handler";
        private const int MaxFrames = 500;

        private static readonly Regex ReplyLog =
            new Regex(@"RespondToCustomCallback\(callbackId=(?<id>[^,]+), result=(?<json>.*)\)$");

        private readonly List<(string id, Dictionary<string, object> result, int thread)> _replies =
            new List<(string, Dictionary<string, object>, int)>();

        private void CaptureReply(string message, string stackTrace, LogType type)
        {
            var match = ReplyLog.Match(message);
            if (!match.Success) return;
            _replies.Add((
                match.Groups["id"].Value,
                Json.Deserialize(match.Groups["json"].Value) as Dictionary<string, object>,
                Thread.CurrentThread.ManagedThreadId));
        }

        [SetUp]
        public void SetUp()
        {
            BridgeCallbackHandler.Initialize();
            Application.logMessageReceived += CaptureReply;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CaptureReply;
            BridgeCallbackHandler.Instance.UnregisterPresentationHandler(HandlerId);
        }

        private static void Register(Func<CustomCallback, Task<CustomCallbackResult>> onCustomCallback)
        {
            BridgeCallbackHandler.Instance.RegisterPresentationHandler(HandlerId,
                new PaywallPresentationHandler { OnCustomCallback = onCustomCallback });
        }

        private static void Deliver(
            string callbackId,
            Dictionary<string, object> variables = null,
            string handlerId = HandlerId)
        {
            var customCallback = new Dictionary<string, object> { { "name", "createAccount" } };
            if (variables != null) customCallback["variables"] = variables;

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "onCustomCallback" },
                { "data", new Dictionary<string, object>
                    {
                        { "handlerId", handlerId },
                        { "callbackId", callbackId },
                        { "customCallback", customCallback }
                    }
                }
            }));
        }

        private IEnumerator WaitForReply()
        {
            for (var frame = 0; frame < MaxFrames && _replies.Count == 0; frame++) yield return null;
            Assert.AreEqual(1, _replies.Count, "Native must get exactly one reply");
        }

        private void AssertFailureReply(string callbackId)
        {
            Assert.AreEqual(1, _replies.Count, "Native must get exactly one reply");
            Assert.AreEqual(callbackId, _replies[0].id);
            Assert.AreEqual("failure", _replies[0].result["status"]);
        }

        [UnityTest]
        public IEnumerator Handler_GetsNameAndVariables_AndItsSuccessDataReachesNative()
        {
            CustomCallback received = null;
            Register((callback) =>
            {
                received = callback;
                return Task.FromResult(CustomCallbackResult.Success(new Dictionary<string, object>
                {
                    { "token", "abc" },
                    { "user", new Dictionary<string, object> { { "id", 7 } } }
                }));
            });

            Deliver("cb-success", new Dictionary<string, object> { { "email", "a@b.c" }, { "age", 30 } });
            yield return WaitForReply();

            Assert.AreEqual("createAccount", received.Name);
            Assert.AreEqual("a@b.c", received.Variables["email"]);
            Assert.AreEqual(30L, received.Variables["age"]);
            Assert.AreEqual("cb-success", _replies[0].id);
            Assert.AreEqual("success", _replies[0].result["status"]);
            var data = (Dictionary<string, object>)_replies[0].result["data"];
            Assert.AreEqual("abc", data["token"]);
            Assert.AreEqual(7L, ((Dictionary<string, object>)data["user"])["id"]);
        }

        [UnityTest]
        public IEnumerator Variables_AreNull_WhenThePaywallSentNone()
        {
            var called = false;
            CustomCallback received = null;
            Register((callback) =>
            {
                called = true;
                received = callback;
                return Task.FromResult(CustomCallbackResult.Success());
            });

            Deliver("cb-no-vars");
            yield return WaitForReply();

            Assert.IsTrue(called);
            Assert.IsNull(received.Variables);
            Assert.IsFalse(_replies[0].result.ContainsKey("data"));
        }

        [UnityTest]
        public IEnumerator Failure_KeepsItsData_SoThePaywallCanShowTheError()
        {
            Register((_) => Task.FromResult(CustomCallbackResult.Failure(
                new Dictionary<string, object> { { "message", "Email taken" } })));

            Deliver("cb-failure");
            yield return WaitForReply();

            AssertFailureReply("cb-failure");
            Assert.AreEqual("Email taken", ((Dictionary<string, object>)_replies[0].result["data"])["message"]);
        }

        [UnityTest]
        public IEnumerator Reply_WaitsForTheHandlersTask()
        {
            var signup = new TaskCompletionSource<CustomCallbackResult>();
            Register(async (_) => await signup.Task);

            Deliver("cb-wait");
            for (var frame = 0; frame < 5; frame++) yield return null;
            Assert.AreEqual(0, _replies.Count, "Native must not get a reply while the request is running");

            signup.SetResult(CustomCallbackResult.Success());
            yield return WaitForReply();

            Assert.AreEqual("success", _replies[0].result["status"]);
        }

        [UnityTest]
        public IEnumerator Reply_ComesFromTheMainThread_WhenTheTaskFinishesOnAnother()
        {
            var mainThread = Thread.CurrentThread.ManagedThreadId;
            Register(async (_) =>
            {
                await Task.Run(() => { }).ConfigureAwait(false);
                await Task.Delay(1).ConfigureAwait(false);
                return CustomCallbackResult.Success();
            });

            Deliver("cb-thread");
            yield return WaitForReply();

            Assert.AreEqual(mainThread, _replies[0].thread);
        }

        [UnityTest]
        public IEnumerator HandlerThrowing_RepliesFailure()
        {
            LogAssert.Expect(LogType.Error, new Regex("OnCustomCallback threw for 'createAccount': boom"));
            Register((_) => throw new InvalidOperationException("boom"));

            Deliver("cb-throw");
            yield return WaitForReply();

            AssertFailureReply("cb-throw");
        }

        [UnityTest]
        public IEnumerator TaskFaulting_RepliesFailure()
        {
            LogAssert.Expect(LogType.Error, new Regex("OnCustomCallback threw for 'createAccount': network down"));
            Register(async (_) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("network down");
            });

            Deliver("cb-fault");
            yield return WaitForReply();

            AssertFailureReply("cb-fault");
        }

        [UnityTest]
        public IEnumerator TaskCancelled_RepliesFailure()
        {
            LogAssert.Expect(LogType.Error, new Regex("OnCustomCallback threw for 'createAccount'"));
            Register((_) => Task.FromCanceled<CustomCallbackResult>(new CancellationToken(true)));

            Deliver("cb-cancelled");
            yield return WaitForReply();

            AssertFailureReply("cb-cancelled");
        }

        [UnityTest]
        public IEnumerator NullResult_RepliesFailure()
        {
            Register((_) => Task.FromResult<CustomCallbackResult>(null));

            Deliver("cb-null-result");
            yield return WaitForReply();

            AssertFailureReply("cb-null-result");
        }

        [UnityTest]
        public IEnumerator NullTask_RepliesFailure()
        {
            Register((_) => null);

            Deliver("cb-null-task");
            yield return WaitForReply();

            AssertFailureReply("cb-null-task");
        }

        [UnityTest]
        public IEnumerator PlacementWithoutOnCustomCallback_RepliesFailure()
        {
            // Native can't tell which handlers set it, so it registers one for every handler.
            BridgeCallbackHandler.Instance.RegisterPresentationHandler(HandlerId, new PaywallPresentationHandler());

            Deliver("cb-unset");
            yield return WaitForReply();

            AssertFailureReply("cb-unset");
        }

        [UnityTest]
        public IEnumerator UnknownHandler_RepliesFailure()
        {
            Deliver("cb-unknown-handler", handlerId: "not-registered");
            yield return WaitForReply();

            AssertFailureReply("cb-unknown-handler");
        }

        // Sent by the bridges against a live paywall (SuperwallKit 4.18.3, superwall-android 2.8.7).
        // JSON key order differs between the platforms.
        [UnityTest]
        public IEnumerator PayloadCapturedFromIOS_IsUnderstood()
        {
            yield return AssertUnderstood("{\"method\":\"onCustomCallback\",\"data\":{\"customCallback\":{\"variables\":{\"marketing\":true,\"age\":30,\"email\":\"new@example.com\"},\"name\":\"createAccount\"},\"callbackId\":\"4F42F81F-1986-4AC8-9494-9DAA20CC9E43\",\"handlerId\":\"" + HandlerId + "\"}}");
        }

        [UnityTest]
        public IEnumerator PayloadCapturedFromAndroid_IsUnderstood()
        {
            yield return AssertUnderstood("{\"method\":\"onCustomCallback\",\"data\":{\"handlerId\":\"" + HandlerId + "\",\"callbackId\":\"cd96285d-3b06-4b0d-946f-9a072ed52127\",\"customCallback\":{\"name\":\"createAccount\",\"variables\":{\"email\":\"new@example.com\",\"age\":30,\"marketing\":true}}}}");
        }

        private IEnumerator AssertUnderstood(string payload)
        {
            CustomCallback received = null;
            Register((callback) =>
            {
                received = callback;
                return Task.FromResult(CustomCallbackResult.Success());
            });

            BridgeCallbackHandler.Instance.OnCallback(payload);
            yield return WaitForReply();

            Assert.AreEqual("createAccount", received.Name);
            Assert.AreEqual("new@example.com", received.Variables["email"]);
            Assert.AreEqual(30L, received.Variables["age"]);
            Assert.AreEqual(true, received.Variables["marketing"]);
            Assert.AreEqual("success", _replies[0].result["status"]);
        }

        [Test]
        public void Factories_SetStatusAndData()
        {
            var data = new Dictionary<string, object> { { "k", "v" } };

            var success = CustomCallbackResult.Success(data);
            var failure = CustomCallbackResult.Failure();

            Assert.AreEqual(CustomCallbackResultStatus.Success, success.Status);
            Assert.AreSame(data, success.Data);
            Assert.AreEqual(CustomCallbackResultStatus.Failure, failure.Status);
            Assert.IsNull(failure.Data);
        }
    }
}
