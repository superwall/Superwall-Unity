// The part of UnityEngine that Runtime/ and Tests/Runtime/ use, with the behaviour the tests rely on:
// log events, LogAssert expectations and a single main thread. CI only, never shipped.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityEngine
{
    public enum LogType { Error, Assert, Warning, Log, Exception }

    public class Object
    {
        public static void DontDestroyOnLoad(Object target) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }

    public class Component : Object { }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }

    public class GameObject : Object
    {
        private static readonly Dictionary<string, GameObject> Objects = new Dictionary<string, GameObject>();

        public GameObject(string name) { Objects[name] = this; }

        private readonly List<Component> _components = new List<Component>();

        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T();
            _components.Add(component);
            return component;
        }

        public T GetComponent<T>() where T : class => _components.OfType<T>().FirstOrDefault();

        public static GameObject Find(string name) =>
            Objects.TryGetValue(name, out var found) ? found : null;
    }

#if UNITY_ANDROID
    // Only compiled, never run: the csharp job builds with UNITY_ANDROID to check that platform's branches.
    public class AndroidJavaObject
    {
        public AndroidJavaObject(string className, params object[] args) { }
        public void Call(string methodName, params object[] args) { }
        public T Call<T>(string methodName, params object[] args) => default;
    }
#endif

    public static class Application
    {
        public delegate void LogCallback(string condition, string stackTrace, LogType type);

        public static event LogCallback logMessageReceived;

        public static void OpenURL(string url) { }

        internal static void Raise(string message, LogType type) => logMessageReceived?.Invoke(message, "", type);
    }

    public static class Debug
    {
        public static void Log(object message) => Emit(message, LogType.Log);
        public static void LogWarning(object message) => Emit(message, LogType.Warning);
        public static void LogError(object message) => Emit(message, LogType.Error);

        private static void Emit(object message, LogType type)
        {
            var text = message?.ToString() ?? "null";
            TestTools.LogAssert.Record(text, type);
            Application.Raise(text, type);
        }
    }
}

namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UnityTestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class UnitySetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class UnityTearDownAttribute : Attribute { }

    // Like Unity: an expected log is consumed by its match, every expected log must appear, and an
    // error log nobody expected fails the test.
    public static class LogAssert
    {
        private static readonly List<(LogType type, Regex pattern)> Expected = new List<(LogType, Regex)>();
        private static readonly List<string> Unexpected = new List<string>();

        public static void Expect(LogType type, Regex pattern) => Expected.Add((type, pattern));

        public static void Expect(LogType type, string message) =>
            Expected.Add((type, new Regex("^" + Regex.Escape(message) + "$")));

        internal static void Record(string message, LogType type)
        {
            lock (Expected)
            {
                var index = Expected.FindIndex(e => e.type == type && e.pattern.IsMatch(message));
                if (index >= 0)
                {
                    Expected.RemoveAt(index);
                    return;
                }
                if (type == LogType.Error || type == LogType.Exception) Unexpected.Add(message);
            }
        }

        // Returns what went wrong for the test that just ran, and resets for the next one.
        internal static List<string> TakeProblems()
        {
            lock (Expected)
            {
                var problems = new List<string>();
                foreach (var message in Unexpected) problems.Add("Unexpected error log: " + message);
                foreach (var (type, pattern) in Expected) problems.Add($"Expected {type} log never appeared: {pattern}");
                Expected.Clear();
                Unexpected.Clear();
                return problems;
            }
        }
    }
}
