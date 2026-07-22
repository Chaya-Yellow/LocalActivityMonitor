using Xunit.Abstractions;
using Xunit.Sdk;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// Xunit test case that forces execution on an STA thread
/// (required for WPF components like FrameworkElement and Brushes).
/// </summary>
public class StaTestCase : XunitTestCase
{
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public StaTestCase() { }

    public StaTestCase(
        IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions,
        ITestMethod testMethod,
        object[]? testMethodArguments = null)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments)
    {
    }

    public override Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        object[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        return new StaTestCaseRunner(
            this, DisplayName, SkipReason, constructorArguments,
            TestMethodArguments, messageBus, aggregator, cancellationTokenSource).RunAsync();
    }
}
