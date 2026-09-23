// A fake UnityEngine for Tools/CloudProbe, and nothing else.
//
// Tools/UnityShim exists to TYPE-CHECK the game against the engine's signatures and is never run:
// its UnityWebRequest always fails and its PlayerPrefs forgets everything, on purpose. This file
// is the opposite trade. It covers only the handful of engine members the cloud and save code
// touches, and it BEHAVES: PlayerPrefs is a dictionary that survives a simulated relaunch, the
// save file lands in a real temporary folder, and every web request is answered from a script the
// probe writes before it runs a step. That is what lets the probe execute the real
// AccountService, CloudSaveService and SaveService rather than reason about them.
//
// Never compiled into the game or the harness. Only Tools/CloudProbe/run-cloud-probe.ps1 builds it.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace UnityEngine
{
    public class Object { }

    public class YieldInstruction { }
    public class AsyncOperation : YieldInstruction { }
    public sealed class Coroutine : YieldInstruction { }

    public class Behaviour : Object { }

    public class MonoBehaviour : Behaviour
    {
        /// <summary>Runs the routine to completion on the spot. The probe is single-threaded and
        /// has no frames, so "later" and "now" are the same moment.</summary>
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            FakeRunner.RunToEnd(routine);
            return new Coroutine();
        }
    }

    public sealed class TextAsset : Object
    {
        public string text = "";
    }

    public static class Resources
    {
        /// <summary>What Resources.Load returns for a path. Absent means null, which is the
        /// "no supabase.json" case the offline design starts from.</summary>
        public static readonly Dictionary<string, string> Files = new Dictionary<string, string>();

        public static T Load<T>(string path) where T : Object
        {
            string text;
            if (typeof(T) == typeof(TextAsset) && Files.TryGetValue(path, out text))
            {
                TextAsset asset = new TextAsset();
                asset.text = text;
                return (T)(Object)asset;
            }
            return null;
        }
    }

    public static class Application
    {
        public static string persistentDataPath = "";
    }

    public static class Debug
    {
        public static readonly List<string> Lines = new List<string>();
        public static void Log(object message) { Lines.Add("log: " + message); }
        public static void LogWarning(object message) { Lines.Add("warning: " + message); }
        public static void LogError(object message) { Lines.Add("error: " + message); }
    }

    public static class PlayerPrefs
    {
        public static readonly Dictionary<string, string> Store = new Dictionary<string, string>();
        public static string GetString(string key, string defaultValue)
        {
            string value;
            return Store.TryGetValue(key, out value) ? value : defaultValue;
        }
        public static void SetString(string key, string value) { Store[key] = value; }
        public static bool HasKey(string key) { return Store.ContainsKey(key); }
        public static void DeleteKey(string key) { Store.Remove(key); }
        public static void Save() { }
    }

    public static class FakeRunner
    {
        /// <summary>Steps a coroutine and every coroutine it yields, depth first, until all of
        /// them are exhausted. Anything else yielded - a web request's async operation - is a
        /// frame boundary here and is simply stepped past.</summary>
        public static void RunToEnd(IEnumerator routine)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                IEnumerator nested = top.Current as IEnumerator;
                if (nested != null) stack.Push(nested);
            }
        }
    }
}

namespace UnityEngine.Networking
{
    public class DownloadHandler : IDisposable
    {
        internal string Text = "";
        public virtual string text { get { return Text; } }
        public void Dispose() { }
    }

    public sealed class DownloadHandlerBuffer : DownloadHandler { }

    public class UploadHandler : IDisposable
    {
        internal byte[] Data;
        public void Dispose() { }
    }

    public sealed class UploadHandlerRaw : UploadHandler
    {
        public UploadHandlerRaw(byte[] data) { Data = data; }
    }

    public sealed class UnityWebRequestAsyncOperation : AsyncOperation { }

    /// <summary>One scripted answer. Status 0 with a Result other than Success is a transport
    /// failure - no signal, a dropped connection - exactly as the real class reports one.</summary>
    public sealed class FakeResponse
    {
        public long Status;
        public string Body = "";
        public UnityWebRequest.Result Result = UnityWebRequest.Result.Success;
        public string TransportError = "";

        public static FakeResponse Json(long status, string body)
        {
            FakeResponse r = new FakeResponse();
            r.Status = status;
            r.Body = body;
            r.Result = status >= 200 && status < 300 ? UnityWebRequest.Result.Success
                                                     : UnityWebRequest.Result.ProtocolError;
            return r;
        }

        public static FakeResponse NoNetwork(string error)
        {
            FakeResponse r = new FakeResponse();
            r.Status = 0;
            r.Result = UnityWebRequest.Result.ConnectionError;
            r.TransportError = error;
            return r;
        }
    }

    /// <summary>A request as it left the device: what the probe inspects after a step.</summary>
    public sealed class SentRequest
    {
        public string Method;
        public string Url;
        public string Body;
    }

    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }

        /// <summary>Answers, consumed in order. A request with nothing scripted is a transport
        /// failure, so an unexpected request can never be mistaken for a successful one.</summary>
        public static readonly Queue<FakeResponse> Script = new Queue<FakeResponse>();
        public static readonly List<SentRequest> Sent = new List<SentRequest>();

        private readonly SentRequest _sent = new SentRequest();
        private FakeResponse _answer;

        public UnityWebRequest(string url, string method) { _sent.Url = url; _sent.Method = method; }

        public int timeout { get; set; }
        public DownloadHandler downloadHandler { get; set; }
        public UploadHandler uploadHandler { get; set; }

        public long responseCode { get { return _answer != null ? _answer.Status : 0; } }
        public Result result { get { return _answer != null ? _answer.Result : Result.ConnectionError; } }
        public string error
        {
            get
            {
                if (_answer == null) return "nothing scripted";
                if (_answer.Result == Result.Success) return null;
                return !string.IsNullOrEmpty(_answer.TransportError) ? _answer.TransportError
                                                                     : "HTTP/1.1 " + _answer.Status;
            }
        }

        // Headers are accepted and dropped: nothing the probe asserts depends on one, and keeping
        // them would put the key the client sends into a structure a probe line could print.
        public void SetRequestHeader(string name, string value) { }

        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            _sent.Body = uploadHandler != null && uploadHandler.Data != null
                ? Encoding.UTF8.GetString(uploadHandler.Data) : null;
            Sent.Add(_sent);
            _answer = Script.Count > 0 ? Script.Dequeue() : FakeResponse.NoNetwork("nothing scripted");
            if (downloadHandler != null) downloadHandler.Text = _answer.Body ?? "";
            return new UnityWebRequestAsyncOperation();
        }

        public void Dispose() { }
    }
}
