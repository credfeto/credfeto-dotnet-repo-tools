using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using Credfeto.Tsql.Formatter;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

public static class CleanUpSetup
{
    public static IServiceCollection AddCleanUp(this IServiceCollection services)
    {
        return services.AddSingleton<IBulkCodeCleanUp, BulkCodeCleanUp>()
                       .AddSingleton<IProjectXmlRewriter, ProjectXmlRewriter>()
                       .AddSingleton<ISourceFileReformatter, SourceFileReformatter>()
                       .AddSingleton<IXmlDocCommentRemover, XmlDocCommentRemover>()
                       .AddSingleton<IResharperSuppressionToSuppressMessage, ResharperSuppressionToSuppressMessage>()
                       .AddTransactSqlFormatting();
    }
}