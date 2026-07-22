using Xunit.Sdk;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// Custom xUnit Fact that runs tests on an STA thread.
/// Required for WPF types that need STA apartment state
/// (e.g., FrameworkElement, Brushes, Dispatcher).
/// </summary>
[XunitTestCaseDiscoverer("ActivityMonitor.Tests.FrontendTests.StaFactDiscoverer",
    "ActivityMonitor.Tests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class StaFactAttribute : Xunit.FactAttribute
{
}
