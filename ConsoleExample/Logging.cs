using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Haukcode.ConsoleExample
{
    public static class Log
    {
        public static LogContext Context(string context)
        {
            return new LogContext(context);
        }
    }

    public class LogContext : IDisposable
    {
        public LogContext(string context)
        {
            Trace.CorrelationManager.StartLogicalOperation(context);
        }

        public void Dispose()
        {
            Trace.CorrelationManager.StopLogicalOperation();
        }

        public void Info(string format, params object[] args)
        {
            Trace.TraceInformation(format, args);
        }
    }
}
