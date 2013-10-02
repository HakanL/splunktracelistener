using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Net.Http;

namespace Haukcode.SplunkTraceListener
{
    public class SplunkTraceListener : TraceListener
    {
        private const int MaxQueueItems = 100;
        private static object lockObject = new object();
        private bool initialized;
        private HttpClient restClient;
        private string requestUrl;
        private StringBuilder lineBuilder;
        private Queue<LogEntry> queue;
        private ManualResetEvent sendEvent;
        private Thread sendThread;

        public SplunkTraceListener()
        {
            this.queue = new Queue<LogEntry>(MaxQueueItems);
            this.sendEvent = new ManualResetEvent(false);
            this.sendThread = new Thread(new ThreadStart(SendThread));
        }

        public SplunkTraceListener(string name)
            : this()
        {
        }

        public override bool IsThreadSafe
        {
            get
            {
                return true;
            }
        }

        public override void TraceTransfer(TraceEventCache eventCache, string source, int id, string message, Guid relatedActivityId)
        {
            base.TraceTransfer(eventCache, source, id, message, relatedActivityId);
        }

        private void SendThread()
        {
            System.Threading.Thread.CurrentThread.IsBackground = true;

            while (true)
            {
                try
                {
                    var logEntries = new List<LogEntry>();

                    lock (lockObject)
                    {
                        while (this.queue.Count > 0)
                            logEntries.Add(this.queue.Dequeue());
                    }

                    if (logEntries.Any())
                    {
                        PostLogEntries(logEntries);
                    }
                    else
                    {
                        if (this.sendEvent.WaitOne(3000))
                            this.sendEvent.Reset();
                    }
                }
                catch
                {
                    // Ignore exceptions
                }
            }
        }

        protected void Initialize()
        {
            lock (lockObject)
            {
                if (this.initialized)
                    return;

                string hostName = Attributes["hostName"];
                string projectId = Attributes["projectId"];
                string accessToken = Attributes["accessToken"];
                string source = Attributes["source"];
                string tz = Attributes["tz"];

                var handler = new HttpClientHandler();
                handler.Credentials = new System.Net.NetworkCredential("x", accessToken);

                string baseUrl = string.Format("https://{0}", hostName);
                this.restClient = new HttpClient(handler);
                this.restClient.Timeout = TimeSpan.FromSeconds(15);
                this.restClient.BaseAddress = new Uri(baseUrl);

                this.requestUrl = string.Format("1/inputs/http?index={0}&sourcetype=json_predefined_timestamp&host={1}&source={2}",
                    projectId,
                    Environment.MachineName,
                    source);

                if (!string.IsNullOrEmpty(tz))
                    this.requestUrl += "&tz=" + tz;

                System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                    (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if(sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch)
                        {
                            if (certificate.Subject.StartsWith("CN=*.splunkstorm.com,"))
                                return true;
                        }

                        return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
                    };

                this.sendThread.Start();

                this.initialized = true;
            }
        }

        protected override string[] GetSupportedAttributes()
        {
            return new string[]
            {
                "hostName",
                "projectId",
                "accessToken",
                "source",
                "tz"
            };
        }

        private string SerializeLogEntry(LogEntry logEntry)
        {
            var sb = new StringBuilder();

            sb.Append("{");
            sb.Append("\"timestamp\":\"");
            sb.Append(logEntry.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
            sb.Append("\",");

            if (!string.IsNullOrEmpty(logEntry.Level))
            {
                sb.Append("\"Level\":\"");
                sb.Append(logEntry.Level);
                sb.Append("\",");
            }

            if (logEntry.ProcessId != 0)
            {
                sb.Append("\"ProcessId\":\"");
                sb.Append(logEntry.ProcessId);
                sb.Append("\",");
            }

            if (!string.IsNullOrEmpty(logEntry.ThreadId))
            {
                sb.Append("\"ThreadId\":\"");
                sb.Append(logEntry.ThreadId);
                sb.Append("\",");
            }

            if (!string.IsNullOrEmpty(logEntry.Path))
            {
                sb.Append("\"Path\":\"");
                sb.Append(logEntry.Path);
                sb.Append("\",");
            }

            sb.Append("\"Message\":\"");
            sb.Append(logEntry.Message.Replace("\"", "\\\""));
            sb.Append("\"");

            sb.AppendLine("}");

            return sb.ToString();
        }

        private void PostLogEntries(IEnumerable<LogEntry> logEntries)
        {
            var sb = new StringBuilder();

            foreach(var logEntry in logEntries)
                sb.Append(SerializeLogEntry(logEntry));

            var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
            var response = this.restClient.PostAsync(this.requestUrl, content).Result;
            response.EnsureSuccessStatusCode();
        }

        public override void Flush()
        {
            base.Flush();

            while (this.queue.Count > 0)
            {
                Thread.Sleep(1);
            }
        }

        private LogEntry CreateLogEntry(string message)
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(),
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id
            };

            return logEntry;
        }

        private void QueueLogEntry(LogEntry logEntry)
        {
            lock (lockObject)
            {
                if (this.queue.Count >= MaxQueueItems)
                    return;

                this.queue.Enqueue(logEntry);

                this.sendEvent.Set();
            }
        }

        public override void Close()
        {
            this.sendThread.Abort();
            base.Close();
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            base.TraceEvent(eventCache, source, eventType, id, format, args);
        }

        public override void Fail(string message)
        {
            base.Fail(message);
        }

        public override void Fail(string message, string detailMessage)
        {
            base.Fail(message, detailMessage);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this.restClient.Dispose();

            base.Dispose(disposing);
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
        {
            base.TraceData(eventCache, source, eventType, id, data);
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
        {
            base.TraceData(eventCache, source, eventType, id, data);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            base.TraceEvent(eventCache, source, eventType, id);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (!initialized)
                Initialize();

            var logEntry = new LogEntry
            {
                Message = message,
                Level = eventType.ToString(),
                Timestamp = eventCache.DateTime.ToLocalTime(),
                ProcessId = eventCache.ProcessId,
                ThreadId = eventCache.ThreadId
            };

            string pathStack = string.Join("/", eventCache.LogicalOperationStack.ToArray());
            if(!string.IsNullOrEmpty(pathStack))
                logEntry.Path = pathStack;

            QueueLogEntry(logEntry);
        }

        public override void Write(string message)
        {
            if (!initialized)
                Initialize();

            lock (lockObject)
            {
                if (this.lineBuilder == null)
                    this.lineBuilder = new StringBuilder();
                this.lineBuilder.Append(message);
            }
        }

        public override void WriteLine(string message)
        {
            if (!initialized)
                Initialize();

            if (this.lineBuilder != null)
            {
                lock (lockObject)
                {
                    if (this.lineBuilder != null)
                    {
                        this.lineBuilder.Append(message);

                        QueueLogEntry(CreateLogEntry(this.lineBuilder.ToString()));

                        this.lineBuilder = null;

                        return;
                    }
                }
            }

            QueueLogEntry(CreateLogEntry(message));
        }
    }
}
