using System.Windows.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// Test case runner that creates and executes tests on an STA thread
/// using WPF's Dispatcher for proper WPF component initialization.
/// </summary>
public class StaTestCaseRunner : XunitTestCaseRunner
{
    public StaTestCaseRunner(
        IXunitTestCase testCase,
        string displayName,
        string skipReason,
        object[] constructorArguments,
        object[] testMethodArguments,
        IMessageBus messageBus,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
        : base(testCase, displayName, skipReason, constructorArguments,
               testMethodArguments, messageBus, aggregator, cancellationTokenSource)
    {
    }

    protected override Task<RunSummary> RunTestAsync()
    {
        var tcs = new TaskCompletionSource<RunSummary>();
        var thread = new Thread(() =>
        {
            try
            {
                // Set STA before creating any WPF objects
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

                // Initialize WPF dispatcher for this thread
                _ = Dispatcher.CurrentDispatcher;

                // Run the actual test
                var result = base.RunTestAsync().GetAwaiter().GetResult();
                tcs.SetResult(result);

                // Shutdown dispatcher
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }
}
