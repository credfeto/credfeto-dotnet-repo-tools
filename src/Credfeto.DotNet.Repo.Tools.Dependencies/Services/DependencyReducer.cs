using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Dependencies.Helpers;
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services.LoggingExtensions;
using Credfeto.Extensions.Linq;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services;

public sealed class DependencyReducer : IDependencyReducer
{
    private const string MINIMAL_SDK = "Microsoft.NET.Sdk";
    private const string PACKAGE_REFERENCES_PATH = "/Project/ItemGroup/PackageReference";
    private const string PROJECT_REFERENCES_PATH = "/Project/ItemGroup/ProjectReference";

    private readonly IDotNetBuild _dotNetBuild;
    private readonly ILogger<DependencyReducer> _logger;

    private readonly IProjectFinder _projectFinder;

    public DependencyReducer(IProjectFinder projectFinder, IDotNetBuild dotNetBuild, ILogger<DependencyReducer> logger)
    {
        this._projectFinder = projectFinder;
        this._dotNetBuild = dotNetBuild;
        this._logger = logger;
    }

    public async ValueTask<bool> CheckReferencesAsync(string sourceDirectory, ReferenceConfig config, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> projects = await this.GetProjectsAsync(sourceDirectory: sourceDirectory, config: config, cancellationToken: cancellationToken);
        this._logger.ProjectsToCheck(projects.Count);

        BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(projects: projects, cancellationToken: cancellationToken);
        BuildOverride buildOverride = new(PreRelease: true);

        this._logger.CheckingProjects();

        Stopwatch stopwatch = Stopwatch.StartNew();
        DependencyTracking tracking = new();

        int projectCount = projects.Count;
        int projectInstance = 0;

        foreach (string project in projects.Where(project => !config.IsIgnoreProject(project)))
        {
            projectInstance++;

            // ! Project directory guaranteed not to be null here
            string projectDirectory = Path.GetDirectoryName(project)!;

            ProjectUpdateContext projectUpdateContext = new(SourceDirectory: sourceDirectory,
                                                            ProjectDirectory: projectDirectory,
                                                            Config: config,
                                                            ProjectInstance: projectInstance,
                                                            ProjectCount: projectCount,
                                                            Project: project,
                                                            BuildSettings: buildSettings,
                                                            BuildOverride: buildOverride,
                                                            Tracking: tracking);

            await this.CheckProjectDependenciesAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken);
        }

        this._logger.FinishedCheckingProjects();

        this.OutputSummary(stopwatch: stopwatch, tracking: tracking);

        return tracking.Obsolete.Count + tracking.ChangeSdk.Count + tracking.ReduceReferences.Count > 0;
    }

    private void OutputSummary(Stopwatch stopwatch, DependencyTracking tracking)
    {
        this._logger.AnalyzeCompletedInDuration(stopwatch.Elapsed.TotalSeconds);
        this._logger.SdkNarrowReferenceCount(tracking.ChangeSdk.Count);
        this._logger.ReferencesCouldBeRemoved(tracking.Obsolete.Count);
        this._logger.ReferencesCouldBeSwitched(tracking.ReduceReferences.Count);

        this.PrintResults(header: "SDK:", items: tracking.ChangeSdk);
        this.PrintResults(header: "Obsolete:", items: tracking.Obsolete);
        this.PrintResults(header: "Reduce Scope:", items: tracking.ReduceReferences);

        this._logger.WriteStatistics(section: "SDK", value: tracking.ChangeSdk.Count);
        this._logger.WriteStatistics(section: "Obsolete", value: tracking.Obsolete.Count);
        this._logger.WriteStatistics(section: "Reduce", value: tracking.ReduceReferences.Count);
    }

    private async ValueTask CheckProjectDependenciesAsync(ProjectUpdateContext projectUpdateContext, CancellationToken cancellationToken)
    {
        this._logger.StartTestingProject(projectInstance: projectUpdateContext.ProjectInstance, projectCount: projectUpdateContext.ProjectCount, project: projectUpdateContext.Project);

        FileContent fileContent = await FileContent.LoadAsync(fileName: projectUpdateContext.Project, cancellationToken: cancellationToken);

        if (!await this.BuildProjectAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken))
        {
            this._logger.DoesNotBuildWithoutChanges();

            throw new DotNetBuildErrorException("Failed to build a project");
        }

        IReadOnlyList<FilePackageReference> childPackageReferences =
            GetPackageReferences(fileName: projectUpdateContext.Project, includeReferences: false, includeChildReferences: true, config: projectUpdateContext.Config);
        IReadOnlyList<FileProjectReference> childProjectReferences = GetProjectReferences(fileName: projectUpdateContext.Project, includeReferences: false, includeChildReferences: true);

        await this.CheckProjectSdkAsync(projectUpdateContext: projectUpdateContext, fileContent: fileContent, cancellationToken: cancellationToken);

        await this.CheckProjectReferenceAsync(projectUpdateContext: projectUpdateContext,
                                              childProjectReferences: childProjectReferences,
                                              fileContent: fileContent,
                                              cancellationToken: cancellationToken);

        await this.CheckPackageReferenceAsync(projectUpdateContext: projectUpdateContext,
                                              childPackageReferences: childPackageReferences,
                                              fileContent: fileContent,
                                              cancellationToken: cancellationToken);

        await fileContent.SaveAsync(cancellationToken: cancellationToken);

        if (!await this.BuildProjectAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken))
        {
            this._logger.FailedToBuildProjectAfterRestore(projectUpdateContext.Project);

            throw new DotNetBuildErrorException("Failed to build project after restore");
        }

        this._logger.FinishTestingProject(projectInstance: projectUpdateContext.ProjectInstance, projectCount: projectUpdateContext.ProjectCount, project: projectUpdateContext.Project);
    }

    private async ValueTask CheckPackageReferenceAsync(ProjectUpdateContext projectUpdateContext,
                                                       IReadOnlyList<FilePackageReference> childPackageReferences,
                                                       FileContent fileContent,
                                                       CancellationToken cancellationToken)
    {
        IReadOnlyList<PackageReference> packageReferences = GetPackageReferencesFromFileContent(fileContent);

        IReadOnlyList<string> allPackageIds = ExtractPackageIds(packageReferences);

        foreach (PackageReference packageReference in packageReferences)
        {
            XmlElement? node = FindPackageReference(xml: fileContent.Xml, packageReference: packageReference);

            if (node is null)
            {
                continue;
            }

            if (projectUpdateContext.Config.IsDoNotRemovePackage(packageId: packageReference.PackageId, allPackageIds: allPackageIds))
            {
                this._logger.SkippingDoNotRemovePackage(packageReference.PackageId);

                continue;
            }

            bool restore = await this.TestProjectPackageReferenceRemovalChangeAsync(projectUpdateContext: projectUpdateContext,
                                                                                    childPackageReferences: childPackageReferences,
                                                                                    fileContent: fileContent,
                                                                                    packageReference: packageReference,
                                                                                    node: node,
                                                                                    cancellationToken: cancellationToken);

            if (restore)
            {
                await fileContent.ResetAsync(cancellationToken);
            }
            else
            {
                await fileContent.ReloadAsync(cancellationToken: cancellationToken);
            }
        }
    }

    private async ValueTask<bool> TestProjectPackageReferenceRemovalChangeAsync(ProjectUpdateContext projectUpdateContext,
                                                                                IReadOnlyList<FilePackageReference> childPackageReferences,
                                                                                FileContent fileContent,
                                                                                PackageReference packageReference,
                                                                                XmlElement node,
                                                                                CancellationToken cancellationToken)
    {
        this._logger.CheckingPackage(packageId: packageReference.PackageId, version: packageReference.Version);

        RemoveNodeFromProject(projectUpdateContext: projectUpdateContext, fileContent: fileContent, node: node);

        bool needToBuild = this.ProjectDoesNotAlreadyIncludePackageThroughChild(projectUpdateContext: projectUpdateContext,
                                                                                childPackageReferences: childPackageReferences,
                                                                                packageReference: packageReference);

        bool buildOk = !needToBuild || await this.BuildProjectAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken);

        if (buildOk)
        {
            projectUpdateContext.Tracking.AddObsolete(new(ProjectFileName: projectUpdateContext.Project,
                                                          Type: ReferenceType.PACKAGE,
                                                          Name: packageReference.PackageId,
                                                          Version: packageReference.Version));

            return !await this.BuildSolutionAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken);
        }

        if (await ShouldHaveNarrowerPackageReferenceAsync(projectFolder: projectUpdateContext.ProjectDirectory, packageId: packageReference.PackageId, cancellationToken: cancellationToken))
        {
            projectUpdateContext.Tracking.AddReduceReferences(new(ProjectFileName: projectUpdateContext.Project,
                                                                  Type: ReferenceType.PACKAGE,
                                                                  Name: packageReference.PackageId,
                                                                  Version: packageReference.Version));
        }

        return true;
    }

    private static void RemoveNodeFromProject(in ProjectUpdateContext projectUpdateContext, FileContent fileContent, XmlElement node)
    {
        XmlNode? parentNode = node.ParentNode;
        parentNode?.RemoveChild(node);

        fileContent.Xml.Save(projectUpdateContext.Project);
    }

    private static XmlElement? FindPackageReference(XmlDocument xml, PackageReference packageReference)
    {
        IReadOnlyList<XmlNode> xmlPackageReferences = GetNodes(xml: xml, xpath: PACKAGE_REFERENCES_PATH);

        return xmlPackageReferences.OfType<XmlElement>()
                                   .FirstOrDefault(element =>
                                                   {
                                                       PackageReference? pr = PackageExtractor.ExtractPackageReference(element);

                                                       return pr == packageReference;
                                                   });
    }

    private bool ProjectDoesNotAlreadyIncludePackageThroughChild(in ProjectUpdateContext projectUpdateContext,
                                                                 IReadOnlyList<FilePackageReference> childPackageReferences,
                                                                 PackageReference packageReference)
    {
        FilePackageReference? existingChildInclude = FindChildPackage(childPackageReferences: childPackageReferences, includeName: packageReference.PackageId, version: packageReference.Version);

        if (existingChildInclude is null)
        {
            this._logger.BuildingProjectWithoutPackage(project: projectUpdateContext.Project, packageId: packageReference.PackageId, version: packageReference.Version);

            return true;
        }

        this._logger.ChildProjectReferencesPackage(project: projectUpdateContext.Project,
                                                   packageId: packageReference.PackageId,
                                                   version: packageReference.Version,
                                                   childProject: existingChildInclude.File);

        return false;
    }

    private static IReadOnlyList<PackageReference> GetPackageReferencesFromFileContent(FileContent fileContent)
    {
        IReadOnlyList<XmlNode> xmlPackageReferences = GetNodes(xml: fileContent.Xml, xpath: PACKAGE_REFERENCES_PATH);

        return
        [
            .. xmlPackageReferences.OfType<XmlElement>()
                                   .Select(PackageExtractor.ExtractPackageReference)
                                   .RemoveNulls()
        ];
    }

    private async ValueTask CheckProjectReferenceAsync(ProjectUpdateContext projectUpdateContext,
                                                       IReadOnlyList<FileProjectReference> childProjectReferences,
                                                       FileContent fileContent,
                                                       CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectReference> projectReferences = GetProjectReferencesFromFileContent(fileContent);

        foreach (ProjectReference projectReference in projectReferences)
        {
            XmlElement? node = FindProjectReference(xml: fileContent.Xml, projectReference: projectReference);

            if (node is null)
            {
                continue;
            }

            this._logger.CheckingProjectReference(projectReference.RelativeInclude);

            RemoveNodeFromProject(projectUpdateContext: projectUpdateContext, fileContent: fileContent, node: node);
            fileContent.Xml.Save(projectUpdateContext.Project);

            bool needToBuild = this.ProjectDoesNotAlreadyIncludeProjectReferenceThroughChild(projectUpdateContext: projectUpdateContext,
                                                                                             childProjectReferences: childProjectReferences,
                                                                                             projectReference: projectReference);

            bool restore = await this.TestProjectProjectReferenceRemovalChangeAsync(projectUpdateContext: projectUpdateContext,
                                                                                    needToBuild: needToBuild,
                                                                                    projectReference: projectReference,
                                                                                    cancellationToken: cancellationToken);

            if (restore)
            {
                await fileContent.ResetAsync(cancellationToken: cancellationToken);
            }
            else
            {
                await fileContent.ReloadAsync(cancellationToken: cancellationToken);
            }
        }
    }

    private async ValueTask<bool> TestProjectProjectReferenceRemovalChangeAsync(ProjectUpdateContext projectUpdateContext,
                                                                                bool needToBuild,
                                                                                ProjectReference projectReference,
                                                                                CancellationToken cancellationToken)
    {
        bool buildOk = !needToBuild || await this.BuildProjectAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken);

        if (buildOk)
        {
            projectUpdateContext.Tracking.AddObsolete(new(ProjectFileName: projectUpdateContext.Project, Type: ReferenceType.PROJECT, Name: projectReference.RelativeInclude));

            return !await this.BuildSolutionAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken);
        }

        string? packageId = ExtractProjectFromReference(projectReference.RelativeInclude);

        if (!string.IsNullOrEmpty(packageId) &&
            await ShouldHaveNarrowerPackageReferenceAsync(projectFolder: projectUpdateContext.ProjectDirectory, packageId: packageId, cancellationToken: cancellationToken))
        {
            projectUpdateContext.Tracking.AddReduceReferences(new(ProjectFileName: projectUpdateContext.Project, Type: ReferenceType.PROJECT, Name: projectReference.RelativeInclude));
        }

        return true;
    }

    private bool ProjectDoesNotAlreadyIncludeProjectReferenceThroughChild(in ProjectUpdateContext projectUpdateContext,
                                                                          IReadOnlyList<FileProjectReference> childProjectReferences,
                                                                          ProjectReference projectReference)
    {
        FileProjectReference? existingChildInclude = FindChildProject(childProjectReferences: childProjectReferences, includeName: projectReference.RelativeInclude);

        if (existingChildInclude is null)
        {
            this._logger.BuildingProjectWithoutProject(project: projectUpdateContext.Project, relativeInclude: projectReference.RelativeInclude);

            return true;
        }

        this._logger.ChildProjectReferencesProject(project: projectUpdateContext.Project, relativeInclude: projectReference.RelativeInclude, childProject: existingChildInclude.File);

        return false;
    }

    private static XmlElement? FindProjectReference(XmlDocument xml, ProjectReference projectReference)
    {
        IReadOnlyList<XmlNode> projectReferences = GetNodes(xml: xml, xpath: PROJECT_REFERENCES_PATH);

        return projectReferences.OfType<XmlElement>()
                                .FirstOrDefault(element =>
                                                {
                                                    ProjectReference? pr = ProjectExtractor.ExtractProjectReference(element);

                                                    return pr == projectReference;
                                                });
    }

    private static IReadOnlyList<ProjectReference> GetProjectReferencesFromFileContent(FileContent fileContent)
    {
        IReadOnlyList<XmlNode> projectReferences = GetNodes(xml: fileContent.Xml, xpath: PROJECT_REFERENCES_PATH);

        return
        [
            .. projectReferences.OfType<XmlElement>()
                                .Select(ProjectExtractor.ExtractProjectReference)
                                .RemoveNulls()
        ];
    }

    private async ValueTask CheckProjectSdkAsync(ProjectUpdateContext projectUpdateContext, FileContent fileContent, CancellationToken cancellationToken)
    {
        XmlNode? projectNode = fileContent.Xml.SelectSingleNode("/Project");

        XmlAttribute? sdkAttr = projectNode?.Attributes?["Sdk"];

        if (sdkAttr is null)
        {
            return;
        }

        string sdk = sdkAttr.Value;

        if (ShouldCheckSdk(sdk: sdk, projectFolder: projectUpdateContext.ProjectDirectory, xml: fileContent.Xml))
        {
            sdkAttr.Value = MINIMAL_SDK;
            fileContent.Xml.Save(projectUpdateContext.Project);

            bool restore = await this.TestProjectMinimalSdkChangeAsync(projectUpdateContext: projectUpdateContext, sdk: sdk, cancellationToken: cancellationToken);

            if (restore)
            {
                await fileContent.ResetAsync(cancellationToken);
            }
            else
            {
                await fileContent.ReloadAsync(cancellationToken);
            }
        }
        else
        {
            this._logger.SdkDoesNotNeedChanging(project: projectUpdateContext.Project, sdk: MINIMAL_SDK);
        }
    }

    private async ValueTask<bool> TestProjectMinimalSdkChangeAsync(ProjectUpdateContext projectUpdateContext, string sdk, CancellationToken cancellationToken)
    {
        this._logger.BuildingProjectWithMinimalSdk(project: projectUpdateContext.Project, minimalSdk: MINIMAL_SDK, currentSdk: sdk);
        bool buildOk = await this.BuildProjectAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken);

        if (buildOk)
        {
            projectUpdateContext.Tracking.AddChangeSdk(new(ProjectFileName: projectUpdateContext.Project, Type: ReferenceType.SDK, Name: sdk));

            return !await this.BuildSolutionAsync(projectUpdateContext: projectUpdateContext, cancellationToken: cancellationToken);
        }

        return true;
    }

    private async ValueTask<IReadOnlyList<string>> GetProjectsAsync(string sourceDirectory, ReferenceConfig config, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> projects = await this._projectFinder.FindProjectsAsync(basePath: sourceDirectory, cancellationToken: cancellationToken);

        return
        [
            ..projects.Where(config.IsIgnoreProject)
        ];
    }

    private static FileProjectReference? FindChildProject(IReadOnlyList<FileProjectReference> childProjectReferences, string includeName)
    {
        return childProjectReferences.FirstOrDefault(p => IsMatchingProjectName(p: p, includeName: includeName));
    }

    private static FilePackageReference? FindChildPackage(IReadOnlyList<FilePackageReference> childPackageReferences, string includeName, string version)
    {
        return childPackageReferences.FirstOrDefault(packageReference => IsMatching(p: packageReference, includeName: includeName, version: version));

        static bool IsMatching(FilePackageReference p, string includeName, string version)
        {
            return IsMatchingPackageName(p: p, includeName: includeName) && IsMatchingPackageVersion(p: p, version: version);
        }
    }

    private static bool IsMatchingPackageVersion(FilePackageReference p, string version)
    {
        return StringComparer.Ordinal.Equals(x: p.Version, y: version);
    }

    private static bool IsMatchingPackageName(FilePackageReference p, string includeName)
    {
        return StringComparer.Ordinal.Equals(x: p.PackageId, y: includeName);
    }

    private static bool IsMatchingProjectName(FileProjectReference p, string includeName)
    {
        return StringComparer.Ordinal.Equals(x: p.RelativeInclude, y: includeName);
    }

    private static IReadOnlyList<string> ExtractPackageIds(IReadOnlyList<PackageReference> packageReferences)
    {
        return
        [
            ..packageReferences.Select(packageReference => packageReference.PackageId)
                               .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static IReadOnlyList<XmlNode> GetNodes(XmlDocument xml, string xpath)
    {
        XmlNodeList? nodes = xml.SelectNodes(xpath);

        if (nodes is null)
        {
            return [];
        }

        return [..nodes.Cast<XmlNode>()];
    }

    private void PrintResults(string header, IReadOnlyList<ReferenceCheckResult> items)
    {
        using (this._logger.BeginScope(header))
        {
            this._logger.LogSection(header);
            string? previousFile = null;

            foreach (ReferenceCheckResult item in items)
            {
                if (!StringComparer.Ordinal.Equals(x: previousFile, y: item.ProjectFileName))
                {
                    this._logger.LogProject(item.ProjectFileName);
                    Console.WriteLine($"\nProject: {item.ProjectFileName}");
                }

                switch (item.Type)
                {
                    case ReferenceType.PACKAGE: this._logger.ProjectPackageReference(item.ProjectFileName, item.Name, item.Version); break;
                    case ReferenceType.PROJECT: this._logger.ProjectChildProjectReference(item.ProjectFileName, item.Name); break;
                    case ReferenceType.SDK: this._logger.ProjectSdkReference(item.ProjectFileName, item.Name); break;
                    default: this._logger.UnknownReference(item.ProjectFileName, item.Name); break;
                }

                previousFile = item.ProjectFileName;
            }
        }
    }

    private async ValueTask<bool> BuildProjectAsync(ProjectUpdateContext projectUpdateContext, CancellationToken cancellationToken)
    {
        try
        {
            // $results = dotnet build $FileName -warnAsError -nodeReuse:False /p:SolutionDir=$solutionDirectory
            await this._dotNetBuild.BuildAsync(projectFileName: projectUpdateContext.Project,
                                               basePath: projectUpdateContext.SourceDirectory,
                                               buildSettings: projectUpdateContext.BuildSettings,
                                               buildOverride: projectUpdateContext.BuildOverride,
                                               cancellationToken: cancellationToken);

            return true;
        }
        catch (DotNetBuildErrorException)
        {
            return false;
        }
    }

    private async ValueTask<bool> BuildSolutionAsync(ProjectUpdateContext projectUpdateContext, CancellationToken cancellationToken)
    {
        try
        {
            await this._dotNetBuild.BuildAsync(basePath: projectUpdateContext.SourceDirectory,
                                               buildSettings: projectUpdateContext.BuildSettings,
                                               buildOverride: projectUpdateContext.BuildOverride,
                                               cancellationToken: cancellationToken);

            return true;
        }
        catch (DotNetBuildErrorException)
        {
            return false;
        }
    }

    // private static void WriteProgress(string message)
    // {
    //     Console.WriteLine(message);
    // }
    //
    // private static void WriteSectionEnd(string message)
    // {
    //     Console.WriteLine("=== End " + message + " ===");
    // }
    //
    // private static void WriteStatistics(string section, int value)
    // {
    //     Console.WriteLine($"{section}: {value}");
    // }

    private static string? ExtractProjectFromReference(string reference)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        // Get just the filename (e.g., "MyApp.csproj")
        string filename = Path.GetFileName(reference);

        if (string.IsNullOrEmpty(filename))
        {
            return null;
        }

        // Ensure it's a .csproj file
        if (filename.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            // Strip the extension and return
            return Path.GetFileNameWithoutExtension(filename);
        }

        return null;
    }

    private static IReadOnlyList<FilePackageReference> GetPackageReferences(string fileName, bool includeReferences, bool includeChildReferences, ReferenceConfig config)
    {
        string? baseDir = Path.GetDirectoryName(fileName);

        if (string.IsNullOrEmpty(baseDir))
        {
            throw new FileNotFoundException(message: "Unable to find project file.", fileName: fileName);
        }

        XmlDocument doc = LoadProjectXmlFromFile(fileName);

        if (!includeReferences && !includeChildReferences)
        {
            return [];
        }

        List<FilePackageReference> references = [];
        List<string> allPackageIds = [];

        if (includeReferences)
        {
            XmlNodeList? packageReferenceNodes = doc.SelectNodes(PACKAGE_REFERENCES_PATH);

            if (packageReferenceNodes is not null)
            {
                IncludeReferencedPackages(allPackageIds: allPackageIds, packageReferenceNodes: packageReferenceNodes);

                references.AddRange(packageReferenceNodes.OfType<XmlElement>()
                                                         .Select(node => PackageExtractor.ExtractPackageReference(config: config, node: node, allPackageIds: allPackageIds, baseDir: baseDir))
                                                         .RemoveNulls());
            }
        }

        if (!includeChildReferences)
        {
            return references;
        }

        XmlNodeList? projectReferenceNodes = doc.SelectNodes(PROJECT_REFERENCES_PATH);

        if (projectReferenceNodes is null)
        {
            return references;
        }

        foreach (string childPath in projectReferenceNodes.OfType<XmlElement>()
                                                          .Select(ProjectExtractor.ExtractProjectReference)
                                                          .RemoveNulls()
                                                          .Select(projectReference => projectReference.RelativeInclude)
                                                          .Select(relativeInclude => Path.Combine(path1: baseDir, path2: relativeInclude)))
        {
            references.AddRange(GetPackageReferences(fileName: childPath, includeReferences: true, includeChildReferences: true, config: config));
        }

        return references;
    }

    private static XmlDocument LoadProjectXmlFromFile(string fileName)
    {
        XmlDocument doc = new();
        doc.Load(fileName);

        return doc;
    }

    private static void IncludeReferencedPackages(List<string> allPackageIds, XmlNodeList packageReferenceNodes)
    {
        allPackageIds.AddRange(packageReferenceNodes.OfType<XmlElement>()
                                                    .Select(node => node.GetAttribute("Include"))
                                                    .Where(include => !string.IsNullOrEmpty(include) && !allPackageIds.Contains(value: include, comparer: StringComparer.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<FileProjectReference> GetProjectReferences(string fileName, bool includeReferences, bool includeChildReferences)
    {
        string? baseDir = Path.GetDirectoryName(fileName);

        if (string.IsNullOrEmpty(baseDir))
        {
            throw new FileNotFoundException(message: "Unable to find project file.", fileName: fileName);
        }

        XmlDocument doc = LoadProjectXmlFromFile(fileName);

        if (!includeReferences && !includeChildReferences)
        {
            return [];
        }

        XmlNodeList? nodes = doc.SelectNodes(PROJECT_REFERENCES_PATH);

        if (nodes is null)
        {
            return [];
        }

        IReadOnlyList<FileProjectReference> baseReferences =
        [
            .. nodes.OfType<XmlElement>()
                    .Select(node => ProjectExtractor.ExtractProjectReference(fileName: baseDir, node: node))
                    .RemoveNulls()
        ];

        if (baseReferences is [])
        {
            return [];
        }

        List<FileProjectReference> references = [];

        if (includeReferences)
        {
            references.AddRange(baseReferences);
        }

        if (!includeChildReferences)
        {
            return references;
        }

        foreach (string childPath in baseReferences.Select(reference => reference.RelativeInclude)
                                                   .Select(relativeInclude => Path.Combine(path1: baseDir, path2: relativeInclude)))
        {
            references.AddRange(GetProjectReferences(fileName: childPath, includeReferences: true, includeChildReferences: true));
        }

        return references;
    }

    private static async ValueTask<bool> ShouldHaveNarrowerPackageReferenceAsync(string projectFolder, string packageId, CancellationToken cancellationToken)
    {
        if (!packageId.StartsWith(value: "FunFair.", comparisonType: StringComparison.Ordinal))
        {
            // Not a package we control
            return false;
        }

        if (packageId.EndsWith(value: ".All", comparisonType: StringComparison.Ordinal))
        {
            // This is explicitly a grouping package
            return false;
        }

        IEnumerable<string> sourceFiles = Directory.EnumerateFiles(path: projectFolder, searchPattern: "*.cs", searchOption: SearchOption.AllDirectories);

        string searchUsing = $"using {packageId}";
        string searchNamespace = $"namespace {packageId}.";

        foreach (string file in sourceFiles)
        {
            string content = await File.ReadAllTextAsync(path: file, cancellationToken: cancellationToken);

            if (content.Contains(value: searchUsing, comparisonType: StringComparison.Ordinal))
            {
                return false;
            }

            if (content.Contains(value: searchNamespace, comparisonType: StringComparison.Ordinal))
            {
                return false;
            }
        }

        Console.WriteLine($"  - Did not Find {packageId} source reference in project");

        return true;
    }

    private static bool ShouldCheckSdk(string sdk, string projectFolder, XmlDocument xml)
    {
        if (!sdk.StartsWith(value: "Microsoft.NET.Sdk.", comparisonType: StringComparison.Ordinal))
        {
            return false;
        }

        if (StringComparer.Ordinal.Equals(x: sdk, y: "Microsoft.NET.Sdk.Razor"))
        {
            string[] cshtmlFiles = Directory.GetFiles(path: projectFolder, searchPattern: "*.cshtml", searchOption: SearchOption.AllDirectories);

            if (cshtmlFiles.Length == 0)
            {
                return true;
            }

            return false;
        }

        if (StringComparer.Ordinal.Equals(x: sdk, y: "Microsoft.NET.Sdk.Web"))
        {
            XPathNavigator navigator = xml.CreateNavigator() ?? throw new InvalidDataException("Could not create navigator");
            XPathNavigator? outputTypeNode = navigator.SelectSingleNode("/Project/PropertyGroup/OutputType");

            if (outputTypeNode is not null)
            {
                if (StringComparer.Ordinal.Equals(x: outputTypeNode.InnerXml, y: "Exe"))
                {
                    // Assume Exes are of the right type
                    return false;
                }
            }
        }

        return true;
    }

    [DebuggerDisplay("{ProjectInstance}/{ProjectCount}: {Project}")]
    private readonly record struct ProjectUpdateContext(
        string SourceDirectory,
        string ProjectDirectory,
        ReferenceConfig Config,
        int ProjectInstance,
        int ProjectCount,
        string Project,
        BuildSettings BuildSettings,
        BuildOverride BuildOverride,
        DependencyTracking Tracking);

    private sealed class FileContent
    {
        private readonly string _fileName;

        private byte[] _source;

        private FileContent(string fileName, byte[] source, XmlDocument xml)
        {
            this._fileName = fileName;
            this._source = source;
            this.Xml = xml;
        }

        public XmlDocument Xml { get; private set; }

        public async ValueTask ReloadAsync(CancellationToken cancellationToken)
        {
            this._source = await File.ReadAllBytesAsync(path: this._fileName, cancellationToken: cancellationToken);
            this.Xml = await LoadProjectXmlAsync(source: this._source, cancellationToken: cancellationToken);
        }

        public async ValueTask SaveAsync(CancellationToken cancellationToken)
        {
            await File.WriteAllBytesAsync(path: this._fileName, bytes: this._source, cancellationToken: cancellationToken);
        }

        public static async ValueTask<FileContent> LoadAsync(string fileName, CancellationToken cancellationToken)
        {
            byte[] data = await File.ReadAllBytesAsync(path: fileName, cancellationToken: cancellationToken);
            XmlDocument xml = await LoadProjectXmlAsync(source: data, cancellationToken: cancellationToken);

            return new(fileName: fileName, source: data, xml: xml);
        }

        private static async ValueTask<XmlDocument> LoadProjectXmlAsync(byte[] source, CancellationToken cancellationToken)
        {
            XmlDocument xml = new();

            await using (MemoryStream memoryStream = new(buffer: source, writable: false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                xml.Load(memoryStream);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return xml;
        }

        public async ValueTask ResetAsync(CancellationToken cancellationToken)
        {
            this.Xml = await LoadProjectXmlAsync(source: this._source, cancellationToken: cancellationToken);
            await File.WriteAllBytesAsync(path: this._fileName, bytes: this._source, cancellationToken: cancellationToken);
        }
    }

    private sealed class DependencyTracking
    {
        private readonly List<ReferenceCheckResult> _changeSdk;
        private readonly List<ReferenceCheckResult> _obsoletes;
        private readonly List<ReferenceCheckResult> _reduceReferences;

        public DependencyTracking()
        {
            this._obsoletes = [];
            this._reduceReferences = [];
            this._changeSdk = [];
        }

        public IReadOnlyList<ReferenceCheckResult> Obsolete => this._obsoletes;

        public IReadOnlyList<ReferenceCheckResult> ReduceReferences => this._reduceReferences;

        public IReadOnlyList<ReferenceCheckResult> ChangeSdk => this._changeSdk;

        public void AddObsolete(ReferenceCheckResult referenceCheckResult)
        {
            this._obsoletes.Add(referenceCheckResult);
        }

        public void AddReduceReferences(ReferenceCheckResult referenceCheckResult)
        {
            this._reduceReferences.Add(referenceCheckResult);
        }

        public void AddChangeSdk(ReferenceCheckResult referenceCheckResult)
        {
            this._changeSdk.Add(referenceCheckResult);
        }
    }
}