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

    public DependencyReducer(IDotNetBuild dotNetBuild, ILogger<DependencyReducer> logger)
    {
        this._dotNetBuild = dotNetBuild;
        this._logger = logger;
    }

    public async ValueTask<bool> CheckReferencesAsync(
        DotNetFiles dotNetFiles,
        ReferenceConfig config,
        CancellationToken cancellationToken
    )
    {
        this._logger.ProjectsToCheck(dotNetFiles.Projects.Count);

        BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(
            projects: dotNetFiles.Projects,
            cancellationToken: cancellationToken
        );
        BuildOverride buildOverride = new(PreRelease: true);

        this._logger.CheckingProjects();

        Stopwatch stopwatch = Stopwatch.StartNew();
        DependencyTracking tracking = new();

        int projectCount = dotNetFiles.Projects.Count;
        int projectInstance = 0;

        foreach (string project in dotNetFiles.Projects.Where(project => !config.IsIgnoreProject(project)))
        {
            projectInstance++;

            // ! Project directory guaranteed not to be null here
            string projectDirectory = Path.GetDirectoryName(project)!;

            ProjectUpdateContext projectUpdateContext = new(
                SourceDirectory: dotNetFiles.SourceDirectory,
                ProjectDirectory: projectDirectory,
                Config: config,
                ProjectInstance: projectInstance,
                ProjectCount: projectCount,
                Project: project,
                BuildSettings: buildSettings,
                BuildOverride: buildOverride,
                Tracking: tracking
            );

            await this.CheckProjectDependenciesAsync(
                projectUpdateContext: projectUpdateContext,
                cancellationToken: cancellationToken
            );
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

    private async ValueTask CheckProjectDependenciesAsync(
        ProjectUpdateContext projectUpdateContext,
        CancellationToken cancellationToken
    )
    {
        this._logger.StartTestingProject(
            projectInstance: projectUpdateContext.ProjectInstance,
            projectCount: projectUpdateContext.ProjectCount,
            project: projectUpdateContext.Project
        );

        FileContent fileContent = await FileContent.LoadAsync(
            fileName: projectUpdateContext.Project,
            cancellationToken: cancellationToken
        );

        await this.VerifyInitialBuildAsync(
            projectUpdateContext: projectUpdateContext,
            cancellationToken: cancellationToken
        );

        IReadOnlyList<FilePackageReference> childPackageReferences = GetPackageReferences(
            fileName: projectUpdateContext.Project,
            includeReferences: false,
            config: projectUpdateContext.Config
        );
        IReadOnlyList<FileProjectReference> childProjectReferences = GetProjectReferences(
            fileName: projectUpdateContext.Project,
            includeReferences: false
        );

        await this.CheckProjectSdkAsync(
            projectUpdateContext: projectUpdateContext,
            fileContent: fileContent,
            cancellationToken: cancellationToken
        );

        await this.CheckProjectReferenceAsync(
            projectUpdateContext: projectUpdateContext,
            childProjectReferences: childProjectReferences,
            fileContent: fileContent,
            cancellationToken: cancellationToken
        );

        await this.CheckPackageReferenceAsync(
            projectUpdateContext: projectUpdateContext,
            childPackageReferences: childPackageReferences,
            fileContent: fileContent,
            cancellationToken: cancellationToken
        );

        await this.VerifyFinalBuildAsync(
            projectUpdateContext: projectUpdateContext,
            cancellationToken: cancellationToken
        );

        this._logger.FinishTestingProject(
            projectInstance: projectUpdateContext.ProjectInstance,
            projectCount: projectUpdateContext.ProjectCount,
            project: projectUpdateContext.Project
        );
    }

    private async ValueTask VerifyInitialBuildAsync(
        ProjectUpdateContext projectUpdateContext,
        CancellationToken cancellationToken
    )
    {
        if (
            !await this.BuildProjectAsync(
                projectUpdateContext: projectUpdateContext,
                cancellationToken: cancellationToken
            )
        )
        {
            this._logger.DoesNotBuildWithoutChanges();
            throw new DotNetBuildErrorException("Failed to build a project");
        }
    }

    private async ValueTask VerifyFinalBuildAsync(
        ProjectUpdateContext projectUpdateContext,
        CancellationToken cancellationToken
    )
    {
        if (
            !await this.BuildProjectAsync(
                projectUpdateContext: projectUpdateContext,
                cancellationToken: cancellationToken
            )
        )
        {
            this._logger.FailedToBuildProjectAfterRestore(projectUpdateContext.Project);
            throw new DotNetBuildErrorException("Failed to build project after restore");
        }
    }

    private async ValueTask CheckPackageReferenceAsync(
        ProjectUpdateContext projectUpdateContext,
        IReadOnlyList<FilePackageReference> childPackageReferences,
        FileContent fileContent,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<XmlNode> xmlPackageNodes = GetNodes(xml: fileContent.Xml, xpath: PACKAGE_REFERENCES_PATH);
        IReadOnlyList<PackageReference> packageReferences = GetPackageReferencesFromNodes(xmlPackageNodes);

        IReadOnlyList<string> allPackageIds = ExtractPackageIds(packageReferences);

        foreach (PackageReference packageReference in packageReferences)
        {
            XmlElement? node = FindPackageReference(xmlNodes: xmlPackageNodes, packageReference: packageReference);

            if (node is null)
            {
                continue;
            }

            if (
                projectUpdateContext.Config.IsDoNotRemovePackage(
                    packageId: packageReference.PackageId,
                    allPackageIds: allPackageIds
                )
            )
            {
                this._logger.SkippingDoNotRemovePackage(packageReference.PackageId);

                continue;
            }

            bool restore = await this.TestProjectPackageReferenceRemovalChangeAsync(
                projectUpdateContext: projectUpdateContext,
                childPackageReferences: childPackageReferences,
                fileContent: fileContent,
                packageReference: packageReference,
                node: node,
                cancellationToken: cancellationToken
            );

            if (restore)
            {
                await fileContent.ResetAsync(cancellationToken);
            }
            else
            {
                string commitMessage = $"Removed reference to {packageReference.PackageId}";
                await projectUpdateContext.Config.OnSuccessfulRemoval(
                    arg1: projectUpdateContext.Project,
                    arg2: commitMessage,
                    arg3: cancellationToken
                );

                await fileContent.ReloadAsync(cancellationToken: cancellationToken);
            }

            xmlPackageNodes = GetNodes(xml: fileContent.Xml, xpath: PACKAGE_REFERENCES_PATH);
            allPackageIds = ExtractPackageIds(GetPackageReferencesFromNodes(xmlPackageNodes));
        }
    }

    private async ValueTask<bool> TestProjectPackageReferenceRemovalChangeAsync(
        ProjectUpdateContext projectUpdateContext,
        IReadOnlyList<FilePackageReference> childPackageReferences,
        FileContent fileContent,
        PackageReference packageReference,
        XmlElement node,
        CancellationToken cancellationToken
    )
    {
        this._logger.CheckingPackage(packageId: packageReference.PackageId, version: packageReference.Version);

        RemoveNodeFromProject(projectUpdateContext: projectUpdateContext, fileContent: fileContent, node: node);

        bool needToBuild = this.ProjectDoesNotAlreadyIncludePackageThroughChild(
            projectUpdateContext: projectUpdateContext,
            childPackageReferences: childPackageReferences,
            packageReference: packageReference
        );

        bool buildOk =
            !needToBuild
            || await this.BuildProjectAsync(
                projectUpdateContext: projectUpdateContext,
                cancellationToken: cancellationToken
            );

        if (buildOk)
        {
            bool solutionBuildOk = await this.BuildSolutionAsync(
                projectUpdateContext: projectUpdateContext,
                cancellationToken: cancellationToken
            );

            if (solutionBuildOk)
            {
                projectUpdateContext.Tracking.AddObsolete(
                    new(
                        ProjectFileName: projectUpdateContext.Project,
                        Type: ReferenceType.PACKAGE,
                        Name: packageReference.PackageId,
                        Version: packageReference.Version
                    )
                );
            }

            return !solutionBuildOk;
        }

        if (
            await ShouldHaveNarrowerPackageReferenceAsync(
                projectFolder: projectUpdateContext.ProjectDirectory,
                packageId: packageReference.PackageId,
                cancellationToken: cancellationToken
            )
        )
        {
            projectUpdateContext.Tracking.AddReduceReferences(
                new(
                    ProjectFileName: projectUpdateContext.Project,
                    Type: ReferenceType.PACKAGE,
                    Name: packageReference.PackageId,
                    Version: packageReference.Version
                )
            );
        }

        return true;
    }

    private static void RemoveNodeFromProject(
        in ProjectUpdateContext projectUpdateContext,
        FileContent fileContent,
        XmlElement node
    )
    {
        XmlNode? parentNode = node.ParentNode;
        parentNode?.RemoveChild(node);

        fileContent.Xml.Save(projectUpdateContext.Project);
    }

    private static XmlElement? FindPackageReference(IReadOnlyList<XmlNode> xmlNodes, PackageReference packageReference)
    {
        return xmlNodes
            .OfType<XmlElement>()
            .FirstOrDefault(element =>
            {
                PackageReference? pr = PackageExtractor.ExtractPackageReference(element);

                return pr == packageReference;
            });
    }

    private bool ProjectDoesNotAlreadyIncludePackageThroughChild(
        in ProjectUpdateContext projectUpdateContext,
        IReadOnlyList<FilePackageReference> childPackageReferences,
        PackageReference packageReference
    )
    {
        FilePackageReference? existingChildInclude = FindChildPackage(
            childPackageReferences: childPackageReferences,
            includeName: packageReference.PackageId,
            version: packageReference.Version
        );

        if (existingChildInclude is null)
        {
            this._logger.BuildingProjectWithoutPackage(
                project: projectUpdateContext.Project,
                packageId: packageReference.PackageId,
                version: packageReference.Version
            );

            return true;
        }

        this._logger.ChildProjectReferencesPackage(
            project: projectUpdateContext.Project,
            packageId: packageReference.PackageId,
            version: packageReference.Version,
            childProject: existingChildInclude.File
        );

        return false;
    }

    private static IReadOnlyList<PackageReference> GetPackageReferencesFromNodes(IReadOnlyList<XmlNode> xmlNodes)
    {
        return [.. xmlNodes.OfType<XmlElement>().Select(PackageExtractor.ExtractPackageReference).RemoveNulls()];
    }

    private async ValueTask CheckProjectReferenceAsync(
        ProjectUpdateContext projectUpdateContext,
        IReadOnlyList<FileProjectReference> childProjectReferences,
        FileContent fileContent,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<XmlNode> xmlProjectNodes = GetNodes(xml: fileContent.Xml, xpath: PROJECT_REFERENCES_PATH);
        IReadOnlyList<ProjectReference> projectReferences = GetProjectReferencesFromNodes(xmlProjectNodes);

        foreach (ProjectReference projectReference in projectReferences)
        {
            XmlElement? node = FindProjectReference(xmlNodes: xmlProjectNodes, projectReference: projectReference);

            if (node is null)
            {
                continue;
            }

            this._logger.CheckingProjectReference(projectReference.RelativeInclude);

            RemoveNodeFromProject(projectUpdateContext: projectUpdateContext, fileContent: fileContent, node: node);

            bool needToBuild = this.ProjectDoesNotAlreadyIncludeProjectReferenceThroughChild(
                projectUpdateContext: projectUpdateContext,
                childProjectReferences: childProjectReferences,
                projectReference: projectReference
            );

            bool restore = await this.TestProjectProjectReferenceRemovalChangeAsync(
                projectUpdateContext: projectUpdateContext,
                needToBuild: needToBuild,
                projectReference: projectReference,
                cancellationToken: cancellationToken
            );

            if (restore)
            {
                await fileContent.ResetAsync(cancellationToken: cancellationToken);
            }
            else
            {
                string projectBeingRemoved = Path.GetFileNameWithoutExtension(projectReference.RelativeInclude);
                string commitMessage = $"Removed reference to {projectBeingRemoved}";
                await projectUpdateContext.Config.OnSuccessfulRemoval(
                    arg1: projectUpdateContext.Project,
                    arg2: commitMessage,
                    arg3: cancellationToken
                );

                await fileContent.ReloadAsync(cancellationToken: cancellationToken);
            }

            xmlProjectNodes = GetNodes(xml: fileContent.Xml, xpath: PROJECT_REFERENCES_PATH);
        }
    }

    private async ValueTask<bool> TestProjectProjectReferenceRemovalChangeAsync(
        ProjectUpdateContext projectUpdateContext,
        bool needToBuild,
        ProjectReference projectReference,
        CancellationToken cancellationToken
    )
    {
        bool buildOk =
            !needToBuild
            || await this.BuildProjectAsync(
                projectUpdateContext: projectUpdateContext,
                cancellationToken: cancellationToken
            );

        if (buildOk)
        {
            bool solutionBuildOk = await this.BuildSolutionAsync(
                projectUpdateContext: projectUpdateContext,
                cancellationToken: cancellationToken
            );

            if (solutionBuildOk)
            {
                projectUpdateContext.Tracking.AddObsolete(
                    new(
                        ProjectFileName: projectUpdateContext.Project,
                        Type: ReferenceType.PROJECT,
                        Name: projectReference.RelativeInclude
                    )
                );
            }

            return !solutionBuildOk;
        }

        string? packageId = ExtractProjectFromReference(projectReference.RelativeInclude);

        if (
            !string.IsNullOrEmpty(packageId)
            && await ShouldHaveNarrowerPackageReferenceAsync(
                projectFolder: projectUpdateContext.ProjectDirectory,
                packageId: packageId,
                cancellationToken: cancellationToken
            )
        )
        {
            projectUpdateContext.Tracking.AddReduceReferences(
                new(
                    ProjectFileName: projectUpdateContext.Project,
                    Type: ReferenceType.PROJECT,
                    Name: projectReference.RelativeInclude
                )
            );
        }

        return true;
    }

    private bool ProjectDoesNotAlreadyIncludeProjectReferenceThroughChild(
        in ProjectUpdateContext projectUpdateContext,
        IReadOnlyList<FileProjectReference> childProjectReferences,
        ProjectReference projectReference
    )
    {
        FileProjectReference? existingChildInclude = FindChildProject(
            childProjectReferences: childProjectReferences,
            includeName: projectReference.RelativeInclude
        );

        if (existingChildInclude is null)
        {
            this._logger.BuildingProjectWithoutProject(
                project: projectUpdateContext.Project,
                relativeInclude: projectReference.RelativeInclude
            );

            return true;
        }

        this._logger.ChildProjectReferencesProject(
            project: projectUpdateContext.Project,
            relativeInclude: projectReference.RelativeInclude,
            childProject: existingChildInclude.File
        );

        return false;
    }

    private static XmlElement? FindProjectReference(IReadOnlyList<XmlNode> xmlNodes, ProjectReference projectReference)
    {
        return xmlNodes
            .OfType<XmlElement>()
            .FirstOrDefault(element =>
            {
                ProjectReference? pr = ProjectExtractor.ExtractProjectReference(element);

                return pr == projectReference;
            });
    }

    private static IReadOnlyList<ProjectReference> GetProjectReferencesFromNodes(IReadOnlyList<XmlNode> xmlNodes)
    {
        return [.. xmlNodes.OfType<XmlElement>().Select(ProjectExtractor.ExtractProjectReference).RemoveNulls()];
    }

    private async ValueTask CheckProjectSdkAsync(
        ProjectUpdateContext projectUpdateContext,
        FileContent fileContent,
        CancellationToken cancellationToken
    )
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

            bool restore = await this.TestProjectMinimalSdkChangeAsync(
                projectUpdateContext: projectUpdateContext,
                sdk: sdk,
                cancellationToken: cancellationToken
            );

            if (restore)
            {
                await fileContent.ResetAsync(cancellationToken);
            }
            else
            {
                string commitMessage = $"Reduces SDK reference from {sdk} to {MINIMAL_SDK}";
                await projectUpdateContext.Config.OnSuccessfulRemoval(
                    arg1: projectUpdateContext.Project,
                    arg2: commitMessage,
                    arg3: cancellationToken
                );

                await fileContent.ReloadAsync(cancellationToken);
            }
        }
        else
        {
            this._logger.SdkDoesNotNeedChanging(project: projectUpdateContext.Project, sdk: MINIMAL_SDK);
        }
    }

    private async ValueTask<bool> TestProjectMinimalSdkChangeAsync(
        ProjectUpdateContext projectUpdateContext,
        string sdk,
        CancellationToken cancellationToken
    )
    {
        this._logger.BuildingProjectWithMinimalSdk(
            project: projectUpdateContext.Project,
            minimalSdk: MINIMAL_SDK,
            currentSdk: sdk
        );
        bool buildOk = await this.BuildProjectAsync(
            projectUpdateContext: projectUpdateContext,
            cancellationToken: cancellationToken
        );

        if (buildOk)
        {
            bool solutionBuildOk = await this.BuildSolutionAsync(
                projectUpdateContext: projectUpdateContext,
                cancellationToken: cancellationToken
            );

            if (solutionBuildOk)
            {
                projectUpdateContext.Tracking.AddChangeSdk(
                    new(ProjectFileName: projectUpdateContext.Project, Type: ReferenceType.SDK, Name: sdk)
                );
            }

            return !solutionBuildOk;
        }

        return true;
    }

    private static FileProjectReference? FindChildProject(
        IReadOnlyList<FileProjectReference> childProjectReferences,
        string includeName
    )
    {
        return childProjectReferences.FirstOrDefault(p => IsMatchingProjectName(p: p, includeName: includeName));
    }

    private static FilePackageReference? FindChildPackage(
        IReadOnlyList<FilePackageReference> childPackageReferences,
        string includeName,
        string version
    )
    {
        return childPackageReferences.FirstOrDefault(packageReference =>
            IsMatching(p: packageReference, includeName: includeName, version: version)
        );

        static bool IsMatching(FilePackageReference p, string includeName, string version)
        {
            return IsMatchingPackageName(p: p, includeName: includeName)
                && IsMatchingPackageVersion(p: p, version: version);
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
            .. packageReferences
                .Select(packageReference => packageReference.PackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static IReadOnlyList<XmlNode> GetNodes(XmlDocument xml, string xpath)
    {
        XmlNodeList? nodes = xml.SelectNodes(xpath);

        return nodes is null ? [] : [.. nodes.Cast<XmlNode>()];
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
                    case ReferenceType.PACKAGE:
                        this._logger.ProjectPackageReference(item.ProjectFileName, item.Name, item.Version);
                        break;
                    case ReferenceType.PROJECT:
                        this._logger.ProjectChildProjectReference(item.ProjectFileName, item.Name);
                        break;
                    case ReferenceType.SDK:
                        this._logger.ProjectSdkReference(item.ProjectFileName, item.Name);
                        break;
                    default:
                        throw new UnreachableException();
                }

                previousFile = item.ProjectFileName;
            }
        }
    }

    private async ValueTask<bool> BuildProjectAsync(
        ProjectUpdateContext projectUpdateContext,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await this._dotNetBuild.BuildAsync(
                projectFileName: projectUpdateContext.Project,
                buildContext: projectUpdateContext.BuildContext,
                cancellationToken: cancellationToken
            );

            return true;
        }
        catch (DotNetBuildErrorException)
        {
            return false;
        }
    }

    private async ValueTask<bool> BuildSolutionAsync(
        ProjectUpdateContext projectUpdateContext,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await this._dotNetBuild.BuildAsync(
                buildContext: projectUpdateContext.BuildContext,
                cancellationToken: cancellationToken
            );

            return true;
        }
        catch (DotNetBuildErrorException)
        {
            return false;
        }
    }

    private static string? ExtractProjectFromReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        string filename = Path.GetFileName(reference);

        if (string.IsNullOrEmpty(filename))
        {
            return null;
        }

        if (filename.EndsWith(value: ".csproj", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(filename);
        }

        return null;
    }

    private static IReadOnlyList<FilePackageReference> GetPackageReferences(
        string fileName,
        bool includeReferences,
        ReferenceConfig config,
        HashSet<string>? visited = null
    )
    {
        string canonicalPath = Path.GetFullPath(GetPlatformFileName(fileName));
        string? baseDir = Path.GetDirectoryName(canonicalPath);

        if (string.IsNullOrEmpty(baseDir))
        {
            throw new FileNotFoundException(message: "Unable to find project file.", fileName: fileName);
        }

        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!visited.Add(canonicalPath))
        {
            return [];
        }

        XmlDocument doc = LoadProjectXmlFromFile(canonicalPath);
        List<FilePackageReference> references = [];

        if (includeReferences)
        {
            AddDirectPackageReferences(references: references, doc: doc, config: config, baseDir: baseDir);
        }

        foreach (
            string childPath in GetNodes(xml: doc, xpath: PROJECT_REFERENCES_PATH)
                .OfType<XmlElement>()
                .Select(ProjectExtractor.ExtractProjectReference)
                .RemoveNulls()
                .Select(projectReference => projectReference.RelativeInclude)
                .Select(relativeInclude => Path.Combine(path1: baseDir, path2: relativeInclude))
        )
        {
            references.AddRange(
                GetPackageReferences(fileName: childPath, includeReferences: true, config: config, visited: visited)
            );
        }

        return references;
    }

    private static void AddDirectPackageReferences(
        List<FilePackageReference> references,
        XmlDocument doc,
        ReferenceConfig config,
        string baseDir
    )
    {
        List<string> allPackageIds = [];
        List<XmlElement> packageReferenceElements = [.. GetNodes(doc, PACKAGE_REFERENCES_PATH).OfType<XmlElement>()];

        IncludeReferencedPackages(allPackageIds: allPackageIds, packageReferenceElements: packageReferenceElements);

        references.AddRange(
            packageReferenceElements
                .Select(node =>
                    PackageExtractor.ExtractPackageReference(
                        config: config,
                        node: node,
                        allPackageIds: allPackageIds,
                        baseDir: baseDir
                    )
                )
                .RemoveNulls()
        );
    }

    private static XmlDocument LoadProjectXmlFromFile(string fileName)
    {
        string projectFileName = GetPlatformFileName(fileName);
        XmlDocument doc = new();
        doc.Load(projectFileName);

        return doc;
    }

    private static string GetPlatformFileName(string fileName)
    {
        if (
            Path.DirectorySeparatorChar != '\\'
            && fileName.Contains(value: '\\', comparisonType: StringComparison.Ordinal)
        )
        {
            return fileName.Replace(oldChar: '\\', newChar: Path.DirectorySeparatorChar);
        }

        if (
            Path.DirectorySeparatorChar != '/'
            && fileName.Contains(value: '/', comparisonType: StringComparison.Ordinal)
        )
        {
            return fileName.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar);
        }

        return fileName;
    }

    private static void IncludeReferencedPackages(List<string> allPackageIds, List<XmlElement> packageReferenceElements)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (XmlElement node in packageReferenceElements)
        {
            string include = node.GetAttribute("Include");

            if (!string.IsNullOrEmpty(include) && seen.Add(include))
            {
                allPackageIds.Add(include);
            }
        }
    }

    private static IReadOnlyList<FileProjectReference> GetProjectReferences(
        string fileName,
        bool includeReferences,
        HashSet<string>? visited = null
    )
    {
        string canonicalPath = Path.GetFullPath(GetPlatformFileName(fileName));
        string? baseDir = Path.GetDirectoryName(canonicalPath);

        if (string.IsNullOrEmpty(baseDir))
        {
            throw new FileNotFoundException(message: "Unable to find project file.", fileName: fileName);
        }

        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!visited.Add(canonicalPath))
        {
            return [];
        }

        XmlDocument doc = LoadProjectXmlFromFile(canonicalPath);
        IReadOnlyList<FileProjectReference> baseReferences =
        [
            .. GetNodes(doc, PROJECT_REFERENCES_PATH)
                .OfType<XmlElement>()
                .Select(node => ProjectExtractor.ExtractProjectReference(fileName: baseDir, node: node))
                .RemoveNulls(),
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

        foreach (
            string childPath in baseReferences
                .Select(reference => reference.RelativeInclude)
                .Select(relativeInclude => Path.Combine(path1: baseDir, path2: relativeInclude))
        )
        {
            references.AddRange(GetProjectReferences(fileName: childPath, includeReferences: true, visited: visited));
        }

        return references;
    }

    private static async ValueTask<bool> ShouldHaveNarrowerPackageReferenceAsync(
        string projectFolder,
        string packageId,
        CancellationToken cancellationToken
    )
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

        IEnumerable<string> sourceFiles = Directory.EnumerateFiles(
            path: projectFolder,
            searchPattern: "*.cs",
            searchOption: SearchOption.AllDirectories
        );

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
            string[] cshtmlFiles = Directory.GetFiles(
                path: projectFolder,
                searchPattern: "*.cshtml",
                searchOption: SearchOption.AllDirectories
            );

            if (cshtmlFiles.Length == 0)
            {
                return true;
            }

            return false;
        }

        if (StringComparer.Ordinal.Equals(x: sdk, y: "Microsoft.NET.Sdk.Web"))
        {
            XPathNavigator navigator =
                xml.CreateNavigator() ?? throw new InvalidDataException("Could not create navigator");
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
        DependencyTracking Tracking
    )
    {
        public BuildContext BuildContext =>
            new(
                SourceDirectory: this.SourceDirectory,
                BuildSettings: this.BuildSettings,
                BuildOverride: this.BuildOverride
            );
    }

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

        public static async ValueTask<FileContent> LoadAsync(string fileName, CancellationToken cancellationToken)
        {
            byte[] data = await File.ReadAllBytesAsync(path: fileName, cancellationToken: cancellationToken);
            XmlDocument xml = await LoadProjectXmlAsync(source: data, cancellationToken: cancellationToken);

            return new(fileName: fileName, source: data, xml: xml);
        }

        private static async ValueTask<XmlDocument> LoadProjectXmlAsync(
            byte[] source,
            CancellationToken cancellationToken
        )
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
            await File.WriteAllBytesAsync(
                path: this._fileName,
                bytes: this._source,
                cancellationToken: cancellationToken
            );
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
