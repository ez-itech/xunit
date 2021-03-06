using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Xunit.Abstractions;

namespace Xunit.Runner.Reporters
{
    public class AppVeyorReporterMessageHandler : DefaultRunnerReporterMessageHandler
    {
        const int MaxLength = 4096;

        string assemblyFileName;
        string baseUri;
        readonly Dictionary<string, int> testMethods = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public AppVeyorReporterMessageHandler(IRunnerLogger logger, string baseUri)
            : base(logger)
        {
            this.baseUri = baseUri.TrimEnd('/');
        }

        protected override bool Visit(ITestAssemblyStarting assemblyStarting)
        {
            assemblyFileName = Path.GetFileName(assemblyStarting.TestAssembly.Assembly.AssemblyPath);

            return base.Visit(assemblyStarting);
        }

        protected override bool Visit(ITestStarting testStarting)
        {
            var testName = testStarting.Test.DisplayName;

            lock (testMethods)
                if (testMethods.ContainsKey(testName))
                    testName = $"{testName} {testMethods[testName]}";

            AppVeyorAddTest(testName, "xUnit", assemblyFileName, "Running", null, null, null, null);

            return base.Visit(testStarting);
        }

        protected override bool Visit(ITestPassed testPassed)
        {
            AppVeyorUpdateTest(GetFinishedTestName(testPassed.Test.DisplayName), "xUnit", assemblyFileName, "Passed",
                               Convert.ToInt64(testPassed.ExecutionTime * 1000), null, null, testPassed.Output);

            return base.Visit(testPassed);
        }

        protected override bool Visit(ITestSkipped testSkipped)
        {
            AppVeyorUpdateTest(GetFinishedTestName(testSkipped.Test.DisplayName), "xUnit", assemblyFileName, "Skipped",
                               Convert.ToInt64(testSkipped.ExecutionTime * 1000), null, null, null);

            return base.Visit(testSkipped);
        }

        protected override bool Visit(ITestFailed testFailed)
        {
            AppVeyorUpdateTest(GetFinishedTestName(testFailed.Test.DisplayName), "xUnit", assemblyFileName, "Failed",
                               Convert.ToInt64(testFailed.ExecutionTime * 1000), ExceptionUtility.CombineMessages(testFailed),
                               ExceptionUtility.CombineStackTraces(testFailed), testFailed.Output);

            return base.Visit(testFailed);
        }

        // AppVeyor API helpers

        string GetFinishedTestName(string methodName)
        {
            lock (testMethods)
            {
                var testName = methodName;
                var number = 0;

                if (testMethods.ContainsKey(methodName))
                {
                    number = testMethods[methodName];
                    testName = $"{methodName} {number}";
                }

                testMethods[methodName] = number + 1;
                return testName;
            }
        }

        void AppVeyorAddTest(string testName, string testFramework, string fileName, string outcome, long? durationMilliseconds,
                             string errorMessage, string errorStackTrace, string stdOut)
        {
            var body = new AddUpdateTestRequest
            {
                TestName = testName,
                TestFramework = testFramework,
                FileName = fileName,
                Outcome = outcome,
                DurationMilliseconds = durationMilliseconds,
                ErrorMessage = errorMessage,
                ErrorStackTrace = errorStackTrace,
                StdOut = TrimStdOut(stdOut),
            };

            AppVeyorClient.SendRequest(Logger, $"{baseUri}/api/tests", HttpMethod.Post, body);
        }

        void AppVeyorUpdateTest(string testName, string testFramework, string fileName, string outcome, long? durationMilliseconds,
                                string errorMessage, string errorStackTrace, string stdOut)
        {
            var body = new AddUpdateTestRequest
            {
                TestName = testName,
                TestFramework = testFramework,
                FileName = fileName,
                Outcome = outcome,
                DurationMilliseconds = durationMilliseconds,
                ErrorMessage = errorMessage,
                ErrorStackTrace = errorStackTrace,
                StdOut = TrimStdOut(stdOut),
            };

            AppVeyorClient.SendRequest(Logger, $"{baseUri}/api/tests", HttpMethod.Put, body);
        }

        static string TrimStdOut(string str)
        {
            return str != null && str.Length > MaxLength ? str.Substring(0, MaxLength) : str;
        }

        public class AddUpdateTestRequest
        {
            public string TestName { get; set; }
            public string FileName { get; set; }
            public string TestFramework { get; set; }
            public string Outcome { get; set; }
            public long? DurationMilliseconds { get; set; }
            public string ErrorMessage { get; set; }
            public string ErrorStackTrace { get; set; }
            public string StdOut { get; set; }
            public string StdErr { get; set; }
        }
    }
}
