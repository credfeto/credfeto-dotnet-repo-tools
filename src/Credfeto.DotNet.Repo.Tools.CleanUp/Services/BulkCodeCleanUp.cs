using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class BulkCodeCleanUp : IBulkCodeCleanUp
{
    /*
     * Foreach repo
     *   ResetToMaster
     *   For each $solution
     *      IfExistsBranch cleanup/$solutionName
     *          continue
     *
     *      If !CodeBuilds
     *          continue
     *
     *      OK = RunCodeCleanup
     *
     *      If !OK
     *          CreateBranch cleanup/$solutionName/broken/$sha
     *          CommitAndPush
     *          ResetToMaster
     *      Else
     *          CreateBranch cleanup/$solutionName/clean/$sha
     *          CommitAndPush
     *          ResetToMaster
     *      End
     *
     */

    /*
     * RunCodeCleanup
     *
     *  If RemoveXmlDocsComments
     *     RemoveXmlDocComments
     *     if !CodeBuilds
     *       return false
     *
     *  Convert Resharper Suppression To SuppressMessage
     *     if !CodeBuilds
     *       return false
     *
     *  Foreach project in solution
     *    Project_cleanup (ordering)
     *
     *    if Changes && !CodeBuilds
     *     return false
     *
     *    Project_cleanup (jetbrains)
     *    if !CodeBuilds
     *      return false
     *
     *  Solution Cleanup (jetbrains)
     *    if !CodeBuilds
     *      return false
     *
     *  Return true
     */
    public ValueTask BulkUpdateAsync(string templateRepository,
                                     string trackingFileName,
                                     string packagesFileName,
                                     string workFolder,
                                     string releaseConfigFileName,
                                     IReadOnlyList<string> repositories,
                                     CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Not yet available");
    }
}