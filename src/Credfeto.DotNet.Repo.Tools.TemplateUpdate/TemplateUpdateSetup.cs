using System;
using System.Net;
using System.Net.Http;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public static class TemplateUpdateSetup
{
    private const int RETRY_COUNT = 3;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ClientPolicyTimeout = ClientTimeout.Add(TimeSpan.FromSeconds(1));

    private static readonly TimeSpan SleepDuration = TimeSpan.FromMilliseconds(600);

    public static IServiceCollection AddTemplateUpdate(this IServiceCollection services)
    {
        return services
            .AddSingleton<ITemplateConfigLoader, TemplateConfigLoader>()
            .AddSingleton<IBulkTemplateUpdater, BulkTemplateUpdater>()
            .AddSingleton<IFileUpdater, FileUpdater>()
            .AddSingleton<IDependaBotConfigBuilder, DependaBotConfigBuilder>()
            .AddSingleton<ILabelsBuilder, LabelsBuilder>()
            .AddTemplateConfigLoaderLoaderHttpClient();
    }

    private static IServiceCollection AddTemplateConfigLoaderLoaderHttpClient(this IServiceCollection services)
    {
        return services
            .AddHttpClient(nameof(TemplateConfigLoader), configureClient: ConfigureClient)
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(configureHandler: CreateHandler)
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(ClientPolicyTimeout))
            .AddTransientHttpErrorPolicy(x =>
                x.WaitAndRetryAsync(retryCount: RETRY_COUNT, sleepDurationProvider: _ => SleepDuration)
            )
            .Services;
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
