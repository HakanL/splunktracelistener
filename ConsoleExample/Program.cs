using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Haukcode.ConsoleExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using (var log = Log.Context("Main"))
            {
                //            Trace.WriteLine("This is a test", "Cat");

                log.Info("This is information {0}", 123);

                Trace.TraceInformation("This is information");

                Trace.CorrelationManager.ActivityId = Guid.NewGuid();
                Trace.CorrelationManager.StartLogicalOperation("Main");
                Trace.TraceWarning("You have been warned");

                TestFunction();

                Trace.CorrelationManager.StopLogicalOperation();
            }

            Trace.Flush();

            Console.WriteLine("Press ENTER to quit...");
            Console.ReadLine();
        }

        public static void TestFunction()
        {
            using (var log = Log.Context("Main"))
            {
                Trace.CorrelationManager.StartLogicalOperation("TestFunction");
                Trace.TraceWarning("This is in the test function");
                Trace.CorrelationManager.StopLogicalOperation();
            }
        }
    }
}
