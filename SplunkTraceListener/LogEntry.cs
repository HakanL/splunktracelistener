using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haukcode.SplunkTraceListener
{
    internal class LogEntry
    {
        public DateTime Timestamp;
        public string Message;
        public string Level;
        public int ProcessId;
        public string ThreadId;
        public string Path;
    }
}
