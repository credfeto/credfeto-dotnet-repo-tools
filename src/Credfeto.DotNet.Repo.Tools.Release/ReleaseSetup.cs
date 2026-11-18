using System;
using System.Net;
using System.Net.Http;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Credfeto.DotNet.Repo.Tools.Release;

public static class ReleaseSetup
{
    private const int RETRY_COUNT = 3;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ClientPolicyTimeout = ClientTimeout.Add(TimeSpan.FromSeconds(1));

    private static readonly TimeSpan SleepDuration = TimeSpan.FromMilliseconds(600);

    public static IServiceCollection AddReleaseGeneration(this IServiceCollection services)
    {
        return services
            .AddSingleton<IReleaseConfigLoader, ReleaseConfigLoader>()
            .AddSingleton<IReleaseGeneration, ReleaseGeneration>()
            .AddReleaseConfigLoaderLoaderHttpClient();
    }

    private static IServiceCollection AddReleaseConfigLoaderLoaderHttpClient(this IServiceCollection services)
    {
        return services
            .AddHttpClient(nameof(ReleaseConfigLoader), configureClient: ConfigureClient)
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(configureHandler: CreateHandler)
            .AddResilienceHandler(pipelineName: nameof(ReleaseConfigLoader), configure: ConfigureResilience)
            .Services;
    }

    private static void ConfigureResilience(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder
            .AddTimeout(ClientPolicyTimeout)
            .AddRetry(
                new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = RETRY_COUNT,
                    BackoffType = DelayBackoffType.Constant,
                    Delay = SleepDuration,
                    UseJitter = false,
                }
            );
    }

    private static HttpClientHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new() { AutomaticDecompression = DecompressionMethods.All };
    }

    private static void ConfigureClient(HttpClient httpClient)
    {
        httpClient.Timeout = ClientTimeout;
    }
}
