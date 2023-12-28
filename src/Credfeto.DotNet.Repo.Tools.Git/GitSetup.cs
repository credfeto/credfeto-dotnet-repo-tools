using System;
using System.Net;
using System.Net.Http;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Services;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Credfeto.DotNet.Repo.Tools.Git;

public static class GitSetup
{
    private const int RETRY_COUNT = 3;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ClientPolicyTimeout = ClientTimeout.Add(TimeSpan.FromSeconds(1));

    private static readonly TimeSpan SleepDuration = TimeSpan.FromMilliseconds(600);

    public static IServiceCollection AddGit(this IServiceCollection services)
    {
        return services.AddSingleton<IGitRepositoryListLoader, GitRepositoryListLoader>()
                       .AddSingleton<IGitRepositoryFactory, GitRepositoryFactory>()
                       .AddSingleton<IGitRepositoryLocator, GitRepositoryLocator>()
                       .AddRepositoryListLoaderHttpClient();
    }

    private static IServiceCollection AddRepositoryListLoaderHttpClient(this IServiceCollection services)
    {
        return services.AddHttpClient(nameof(GitRepositoryListLoader), configureClient: ConfigureClient)
                       .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                       .ConfigurePrimaryHttpMessageHandler(configureHandler: CreateHandler)
                       .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(ClientPolicyTimeout))
                       .AddTransientHttpErrorPolicy(x => x.WaitAndRetryAsync(retryCount: RETRY_COUNT, sleepDurationProvider: _ => SleepDuration))
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