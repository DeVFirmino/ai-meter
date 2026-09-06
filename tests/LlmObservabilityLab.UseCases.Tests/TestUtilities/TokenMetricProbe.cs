using System.Diagnostics.Metrics;
using LlmObservabilityLab.Api.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LlmObservabilityLab.UseCases.Tests.TestUtilities;

public sealed class TokenMetricProbe : IDisposable
{
    private readonly ServiceProvider _provider;

    public TokenMetricProbe()
    {
        _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        IMeterFactory meterFactory = _provider.GetRequiredService<IMeterFactory>();
        Collector = new MetricCollector<long>(meterFactory, AiUsageMeter.MeterName, "ai_meter.tokens");
        Meter = new AiUsageMeter(meterFactory);
    }

    public AiUsageMeter Meter { get; }

    public MetricCollector<long> Collector { get; }

    public void Dispose()
    {
        Collector.Dispose();
        _provider.Dispose();
    }
}
