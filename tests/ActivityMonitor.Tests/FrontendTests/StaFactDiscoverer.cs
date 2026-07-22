using Xunit.Abstractions;
using Xunit.Sdk;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// Test case discoverer for [StaFact] that forces tests to run on STA threads.
/// Required for WPF components (FrameworkElement, Brushes, Path, etc.).
/// </summary>
public class StaFactDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _diagnosticMessageSink;

    public StaFactDiscoverer(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        yield return new StaTestCase(
            _diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod);
    }
}
