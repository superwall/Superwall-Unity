// A minimal test runner for Tests/Runtime. A [UnityTest] advances one "frame" per yield, and the
// posted continuations run between frames on this thread, which is how Unity runs async code.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Superwall.Tests.Harness
{
    internal sealed class MainThreadContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback callback, object state)> _posted =
            new Queue<(SendOrPostCallback, object)>();

        public override void Post(SendOrPostCallback d, object state)
        {
            lock (_posted) _posted.Enqueue((d, state));
        }

        public void RunPosted()
        {
            while (true)
            {
                (SendOrPostCallback callback, object state) next;
                lock (_posted)
                {
                    if (_posted.Count == 0) return;
                    next = _posted.Dequeue();
                }
                next.callback(next.state);
            }
        }
    }

    internal static class Program
    {
        private const int MaxFrames = 10000;

        private static int Main(string[] args)
        {
            var context = new MainThreadContext();
            SynchronizationContext.SetSynchronizationContext(context);

            var filter = args.FirstOrDefault();
            var tests = Assembly.GetExecutingAssembly().GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(IsTest)
                    .Select(method => (type, method)))
                .Where(test => filter == null || $"{test.type.Name}.{test.method.Name}".Contains(filter))
                .OrderBy(test => test.type.Name).ThenBy(test => test.method.Name)
                .ToList();

            var failed = new List<string>();
            foreach (var (type, method) in tests)
            {
                var name = $"{type.Name}.{method.Name}";
                var problems = Run(type, method, context);
                Console.WriteLine($"{(problems.Count == 0 ? "ok  " : "FAIL")} {name}");
                foreach (var problem in problems) Console.WriteLine("     " + problem.Replace("\n", "\n     "));
                if (problems.Count > 0) failed.Add(name);
            }

            Console.WriteLine($"\n{tests.Count - failed.Count}/{tests.Count} passed");
            if (tests.Count == 0)
            {
                Console.WriteLine("No tests ran.");
                return 1;
            }
            return failed.Count == 0 ? 0 : 1;
        }

        private static bool IsTest(MethodInfo method) =>
            method.GetCustomAttribute<TestAttribute>() != null ||
            method.GetCustomAttribute<UnityTestAttribute>() != null;

        private static List<string> Run(Type type, MethodInfo method, MainThreadContext context)
        {
            var problems = new List<string>();
            try
            {
                var fixture = Activator.CreateInstance(type, true);
                Invoke<SetUpAttribute>(type, fixture, context);
                Invoke<UnitySetUpAttribute>(type, fixture, context);
                try
                {
                    if (method.Invoke(fixture, null) is IEnumerator frames) RunFrames(frames, context);
                    context.RunPosted();
                }
                finally
                {
                    Invoke<UnityTearDownAttribute>(type, fixture, context);
                    Invoke<TearDownAttribute>(type, fixture, context);
                }
            }
            catch (Exception e)
            {
                var cause = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                problems.Add(cause.GetType().Name + ": " + cause.Message);
            }
            problems.AddRange(LogAssert.TakeProblems());
            return problems;
        }

        // Like Unity, a yielded IEnumerator runs to completion before its parent continues.
        private static void RunFrames(IEnumerator root, MainThreadContext context)
        {
            var running = new Stack<IEnumerator>();
            running.Push(root);
            var count = 0;
            while (running.Count > 0)
            {
                var current = running.Peek();
                if (!current.MoveNext())
                {
                    running.Pop();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                {
                    running.Push(nested);
                    continue;
                }
                context.RunPosted();
                Thread.Sleep(1);
                if (++count > MaxFrames) throw new TimeoutException($"Still running after {MaxFrames} frames");
            }
        }

        private static void Invoke<TAttribute>(Type type, object fixture, MainThreadContext context) where TAttribute : Attribute
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttribute<TAttribute>() != null))
            {
                if (method.Invoke(fixture, null) is IEnumerator frames) RunFrames(frames, context);
            }
        }
    }
}
