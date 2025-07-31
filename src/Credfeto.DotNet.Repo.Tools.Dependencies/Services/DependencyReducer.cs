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
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services.LoggingExtensions;
using Credfeto.Extensions.Linq;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services;

public sealed class DependencyReducer : IDependencyReducer
{
    private const string MINIMAL_SDK = "Microsoft.NET.Sdk";
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

            await this.CheckProjectDependenciesAsync(sourceDirectory: sourceDirectory,
                                                     projectDirectory: projectDirectory,
                                                     config: config,
                                                     projectInstance: projectInstance,
                                                     projectCount: projectCount,
                                                     project: project,
                                                     buildSettings: buildSettings,
                                                     buildOverride: buildOverride,
                                                     tracking: tracking,
                                                     cancellationToken: cancellationToken);
        }

        this._logger.FinishedCheckingProjects();

        OutputSummary(stopwatch: stopwatch, tracking: tracking);

        return tracking.Obsolete.Count + tracking.ChangeSdk.Count + tracking.ReduceReferences.Count > 0;
    }

    private static void OutputSummary(Stopwatch stopwatch, DependencyTracking tracking)
    {
        WriteProgress("");
        WriteProgress("-------------------------------------------------------------------------");
        WriteProgress($"Analyse completed in {stopwatch.Elapsed.TotalSeconds} seconds");
        WriteProgress($"{tracking.ChangeSdk.Count} SDK reference(s) could potentially be narrowed.");
        WriteProgress($"{tracking.Obsolete.Count} reference(s) could potentially be removed.");
        WriteProgress($"{tracking.ReduceReferences.Count} reference(s) could potentially be switched.");

        PrintResults(header: "SDK:", items: tracking.ChangeSdk);
        PrintResults(header: "Obsolete:", items: tracking.Obsolete);
        PrintResults(header: "Reduce Scope:", items: tracking.ReduceReferences);

        WriteStatistics(section: "SDK", value: tracking.ChangeSdk.Count);
        WriteStatistics(section: "Obsolete", value: tracking.Obsolete.Count);
        WriteStatistics(section: "Reduce", value: tracking.ReduceReferences.Count);
    }

    private async ValueTask CheckProjectDependenciesAsync(string sourceDirectory,
                                                          string projectDirectory,
                                                          ReferenceConfig config,
                                                          int projectInstance,
                                                          int projectCount,
                                                          string project,
                                                          BuildSettings buildSettings,
                                                          BuildOverride buildOverride,
                                                          DependencyTracking tracking,
                                                          CancellationToken cancellationToken)
    {
        this._logger.StartTestingProject(projectInstance: projectInstance, projectCount: projectCount, project: project);

        byte[] rawFileContent = await File.ReadAllBytesAsync(path: project, cancellationToken: cancellationToken);

        if (!await this.BuildProjectAsync(projectFileName: project, basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken))
        {
            this._logger.DoesNotBuildWithoutChanges();

            throw new DotNetBuildErrorException("Failed to build a project");
        }

        List<PackageReference> childPackageReferences = GetPackageReferences(fileName: project, includeReferences: false, includeChildReferences: true, config: config);
        List<ProjectReference> childProjectReferences = GetProjectReferences(fileName: project, includeReferences: false, includeChildReferences: true);

        XmlDocument xml = await LoadProjectXmlAsync(rawFileContent: rawFileContent, cancellationToken: cancellationToken);

        await this.CheckProjectSdkAsync(sourceDirectory: sourceDirectory,
                                        projectDirectory: projectDirectory,
                                        project: project,
                                        buildSettings: buildSettings,
                                        buildOverride: buildOverride,
                                        tracking: tracking,
                                        xml: xml,
                                        rawFileContent: rawFileContent,
                                        cancellationToken: cancellationToken);

        IReadOnlyList<XmlNode> packageReferences = GetNodes(xml: xml, xpath: "/Project/ItemGroup/PackageReference");
        IReadOnlyList<XmlNode> projectReferences = GetNodes(xml: xml, xpath: "/Project/ItemGroup/ProjectReference");

        List<string> allPackageIds = ExtractPackageIds(packageReferences);

        List<XmlNode> allNodes = [.. packageReferences, .. projectReferences];

        foreach (XmlElement node in allNodes.OfType<XmlElement>())
        {
            string includeName = node.GetAttribute("Include");

            if (string.IsNullOrEmpty(includeName))
            {
                WriteProgress("= Skipping malformed include");

                continue;
            }

            if (config.IsDoNotRemovePackage(packageId: includeName, allPackageIds: allPackageIds))
            {
                WriteProgress($"= Skipping {includeName} as it is marked as do not remove");

                continue;
            }

            if (node["PrivateAssets"] is not null)
            {
                WriteProgress($"= Skipping {includeName} as it uses private assets");

                continue;
            }

            WriteProgress($"Checking: {includeName}");

            XmlNode? previousNode = node.PreviousSibling;
            XmlNode? parentNode = node.ParentNode;
            parentNode?.RemoveChild(node);

            bool needToBuild = true;
            xml.Save(project);

            XmlNode? versionNode = node["Version"];

            if (versionNode is not null)
            {
                PackageReference? existingChildInclude = FindChildPackage(childPackageReferences: childPackageReferences, includeName: includeName, version: versionNode.InnerText);

                if (existingChildInclude is not null)
                {
                    WriteProgress($"= {project} references package {includeName} ({versionNode.InnerText}) also in child project {existingChildInclude.File}");
                    needToBuild = false;
                }
                else
                {
                    WriteProgress($"* Building {project} without package {includeName} ({versionNode.InnerText})...");
                }
            }
            else
            {
                ProjectReference? existingChildInclude = FindChildProject(childProjectReferences: childProjectReferences, includeName: includeName);

                if (existingChildInclude is not null)
                {
                    WriteProgress($"= {project} references project {includeName} also in child project {existingChildInclude.File}");
                    needToBuild = false;
                }
                else
                {
                    WriteProgress($"* Building {project} without project {includeName}...");
                }
            }

            bool buildOk = !needToBuild || await this.BuildProjectAsync(projectFileName: project,
                                                                        basePath: sourceDirectory,
                                                                        buildSettings: buildSettings,
                                                                        buildOverride: buildOverride,
                                                                        cancellationToken: cancellationToken);
            bool restore = true;

            if (buildOk)
            {
                WriteProgress("  - Building succeeded.");

                tracking.AddObsolete(versionNode is not null
                                         ? new(ProjectFileName: project, Type: ReferenceType.Package, Name: includeName, Version: versionNode.InnerText)
                                         : new(ProjectFileName: project, Type: ReferenceType.Project, Name: includeName));

                if (await this.BuildSolutionAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken))
                {
                    restore = false;
                }
            }
            else
            {
                WriteProgress("  = Building failed.");

                if (versionNode is not null)
                {
                    if (await ShouldHaveNarrowerPackageReferenceAsync(projectFolder: projectDirectory, packageId: includeName, cancellationToken: cancellationToken))
                    {
                        tracking.AddReduceReferences(new(ProjectFileName: project, Type: ReferenceType.Package, Name: includeName, Version: versionNode.InnerText));
                    }
                }
                else
                {
                    string? packageId = ExtractProjectFromReference(includeName);

                    if (!string.IsNullOrEmpty(packageId) && await ShouldHaveNarrowerPackageReferenceAsync(projectFolder: projectDirectory, packageId: packageId, cancellationToken: cancellationToken))
                    {
                        tracking.AddReduceReferences(new(ProjectFileName: project, Type: ReferenceType.Project, Name: includeName));
                    }
                }
            }

            if (restore)
            {
                if (previousNode is null)
                {
                    parentNode?.PrependChild(node);
                }
                else
                {
                    parentNode?.InsertAfter(newChild: node, refChild: previousNode);
                }

                xml.Save(project);
            }
            else
            {
                rawFileContent = await File.ReadAllBytesAsync(path: project, cancellationToken: cancellationToken);
            }
        }

        await File.WriteAllBytesAsync(path: project, bytes: rawFileContent, cancellationToken: cancellationToken);

        if (!await this.BuildProjectAsync(projectFileName: project, basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken))
        {
            WriteError($"### Failed to build {project} after restore.");

            throw new DotNetBuildErrorException("Failed to build project after restore");
        }

        WriteSectionEnd($"({projectInstance}/{projectCount}): Testing project: {project}");
    }

    private async ValueTask<byte[]> CheckProjectSdkAsync(string sourceDirectory,
                                                         string projectDirectory,
                                                         string project,
                                                         BuildSettings buildSettings,
                                                         BuildOverride buildOverride,
                                                         DependencyTracking tracking,
                                                         XmlDocument xml,
                                                         byte[] rawFileContent,
                                                         CancellationToken cancellationToken)
    {
        XmlNode? projectNode = xml.SelectSingleNode("/Project");

        if (projectNode?.Attributes is not null)
        {
            XmlAttribute? sdkAttr = projectNode.Attributes["Sdk"];

            if (sdkAttr is not null)
            {
                string sdk = sdkAttr.Value;

                if (ShouldCheckSdk(sdk: sdk, projectFolder: projectDirectory, xml: xml))
                {
                    sdkAttr.Value = MINIMAL_SDK;
                    xml.Save(project);

                    this._logger.BuildingProjectWithMinimalSdk(project: project, minimalSdk: MINIMAL_SDK, currentSdk: sdk);
                    bool buildOk = await this.BuildProjectAsync(projectFileName: project,
                                                                basePath: sourceDirectory,
                                                                buildSettings: buildSettings,
                                                                buildOverride: buildOverride,
                                                                cancellationToken: cancellationToken);
                    bool restore1 = true;

                    if (buildOk)
                    {
                        WriteProgress("  - Building succeeded.");
                        tracking.AddChangeSdk(new(ProjectFileName: project, Type: ReferenceType.Sdk, Name: sdk));

                        if (await this.BuildSolutionAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken))
                        {
                            restore1 = false;
                        }
                    }
                    else
                    {
                        WriteProgress("  = Building failed.");
                    }

                    bool restore = restore1;

                    if (restore)
                    {
                        sdkAttr.Value = sdk;
                        xml.Save(project);
                    }
                    else
                    {
                        return await File.ReadAllBytesAsync(path: project, cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    WriteProgress($"= SDK does not need changing. Currently {MINIMAL_SDK}.");
                }
            }
        }

        return rawFileContent;
    }

    private static async ValueTask<XmlDocument> LoadProjectXmlAsync(byte[] rawFileContent, CancellationToken cancellationToken)
    {
        XmlDocument xml = new();

        await using (MemoryStream memoryStream = new(buffer: rawFileContent, writable: false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            xml.Load(memoryStream);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return xml;
    }

    private async ValueTask<IReadOnlyList<string>> GetProjectsAsync(string sourceDirectory, ReferenceConfig config, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> projects = await this._projectFinder.FindProjectsAsync(basePath: sourceDirectory, cancellationToken: cancellationToken);

        return
        [
            ..projects.Where(config.IsIgnoreProject)
        ];
    }

    private static ProjectReference? FindChildProject(List<ProjectReference> childProjectReferences, string includeName)
    {
        return childProjectReferences.Find(p => IsMatchingProjectName(p: p, includeName: includeName));
    }

    private static PackageReference? FindChildPackage(List<PackageReference> childPackageReferences, string includeName, string version)
    {
        return childPackageReferences.Find(packageReference => IsMatching(p: packageReference, includeName: includeName, version: version));

        static bool IsMatching(PackageReference p, string includeName, string version)
        {
            return IsMatchingPackageName(p: p, includeName: includeName) && IsMatchingPackageVersion(p: p, version: version);
        }
    }

    private static bool IsMatchingPackageVersion(PackageReference p, string version)
    {
        return StringComparer.Ordinal.Equals(x: p.Version, y: version);
    }

    private static bool IsMatchingPackageName(PackageReference p, string includeName)
    {
        return StringComparer.Ordinal.Equals(x: p.Name, y: includeName);
    }

    private static bool IsMatchingProjectName(ProjectReference p, string includeName)
    {
        return StringComparer.Ordinal.Equals(x: p.Name, y: includeName);
    }

    private static List<string> ExtractPackageIds(IReadOnlyList<XmlNode> packageReferences)
    {
        List<string> allPackageIds = [];

        foreach (XmlElement node in packageReferences.OfType<XmlElement>())
        {
            string include = node.GetAttribute("Include");

            if (!string.IsNullOrEmpty(include))
            {
                allPackageIds.Add(include);
            }
        }

        return allPackageIds;
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

    private static void PrintResults(string header, IReadOnlyList<ReferenceCheckResult> items)
    {
        Console.WriteLine($"\n{header}");
        string? previousFile = null;

        foreach (ReferenceCheckResult item in items)
        {
            if (!StringComparer.Ordinal.Equals(x: previousFile, y: item.ProjectFileName))
            {
                Console.WriteLine($"\nProject: {item.ProjectFileName}");
            }

            if (item.Type == ReferenceType.Package)
            {
                Console.WriteLine($"* Package reference: {item.Name} ({item.Version})");
            }
            else
            {
                Console.WriteLine($"* Project reference: {item.Name}");
            }

            previousFile = item.ProjectFileName;
        }
    }

    private async ValueTask<bool> BuildProjectAsync(string projectFileName, string basePath, BuildSettings buildSettings, BuildOverride buildOverride, CancellationToken cancellationToken)
    {
        try
        {
            // $results = dotnet build $FileName -warnAsError -nodeReuse:False /p:SolutionDir=$solutionDirectory
            await this._dotNetBuild.BuildAsync(projectFileName: projectFileName, basePath: basePath, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);

            return true;
        }
        catch (DotNetBuildErrorException)
        {
            return false;
        }
    }

    private async ValueTask<bool> BuildSolutionAsync(string basePath, BuildSettings buildSettings, BuildOverride buildOverride, CancellationToken cancellationToken)
    {
        try
        {
            await this._dotNetBuild.BuildAsync(basePath: basePath, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);

            return true;
        }
        catch (DotNetBuildErrorException)
        {
            return false;
        }
    }

    private static void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }

    private static void WriteProgress(string message)
    {
        Console.WriteLine(message);
    }

    private static void WriteSectionEnd(string message)
    {
        Console.WriteLine("=== End " + message + " ===");
    }

    private static void WriteStatistics(string section, int value)
    {
        Console.WriteLine($"{section}: {value}");
    }

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

    private static List<PackageReference> GetPackageReferences(string fileName, bool includeReferences, bool includeChildReferences, ReferenceConfig config)
    {
        string? baseDir = Path.GetDirectoryName(fileName);

        if (string.IsNullOrEmpty(baseDir))
        {
            throw new FileNotFoundException(message: "Unable to find project file.", fileName: fileName);
        }

        List<PackageReference> references = [];
        List<string> allPackageIds = [];

        XmlDocument doc = LoadProjectXmlFromFile(fileName);

        if (includeReferences)
        {
            XmlNodeList? packageReferenceNodes = doc.SelectNodes("//Project/ItemGroup/PackageReference");

            if (packageReferenceNodes is not null)
            {
                IncludeReferencedPackages(allPackageIds: allPackageIds, packageReferenceNodes: packageReferenceNodes);

                references.AddRange(packageReferenceNodes.OfType<XmlElement>()
                                                         .Select(node => ExtractPackageReference(config: config, node: node, allPackageIds: allPackageIds, baseDir: baseDir))
                                                         .RemoveNulls());
            }
        }

        if (includeChildReferences)
        {
            XmlNodeList? projectReferenceNodes = doc.SelectNodes("//Project/ItemGroup/ProjectReference");

            if (projectReferenceNodes is not null)
            {
                foreach (XmlElement node in projectReferenceNodes.OfType<XmlElement>())
                {
                    string includeAttr = node.GetAttribute("Include");

                    if (string.IsNullOrEmpty(includeAttr))
                    {
                        continue;
                    }

                    string childPath = Path.Combine(path1: baseDir, path2: includeAttr);
                    references.AddRange(GetPackageReferences(fileName: childPath, includeReferences: true, includeChildReferences: true, config: config));
                }
            }
        }

        return references;
    }

    private static XmlDocument LoadProjectXmlFromFile(string fileName)
    {
        XmlDocument doc = new();
        doc.Load(fileName);

        return doc;
    }

    private static PackageReference? ExtractPackageReference(ReferenceConfig config, XmlElement node, List<string> allPackageIds, string baseDir)
    {
        string includeAttr = node.GetAttribute("Include");

        if (string.IsNullOrEmpty(includeAttr))
        {
            return null;
        }

        XmlNode? privateAssetsNode = node.SelectSingleNode("PrivateAssets");

        if (privateAssetsNode is not null)
        {
            return null;
        }

        if (config.IsDoNotRemovePackage(packageId: includeAttr, allPackageIds: allPackageIds))
        {
            return null;
        }

        XmlNode? versionNode = node.SelectSingleNode("Version");

        if (versionNode is null)
        {
            return null;
        }

        PackageReference packageReference = new(File: baseDir, Name: includeAttr, Version: versionNode.InnerText);

        return packageReference;
    }

    private static void IncludeReferencedPackages(List<string> allPackageIds, XmlNodeList packageReferenceNodes)
    {
        allPackageIds.AddRange(packageReferenceNodes.OfType<XmlElement>()
                                                    .Select(node => node.GetAttribute("Include"))
                                                    .Where(include => !string.IsNullOrEmpty(include) && !allPackageIds.Contains(value: include, comparer: StringComparer.OrdinalIgnoreCase)));
    }

    private static List<ProjectReference> GetProjectReferences(string fileName, bool includeReferences, bool includeChildReferences)
    {
        string? baseDir = Path.GetDirectoryName(fileName);

        if (string.IsNullOrEmpty(baseDir))
        {
            throw new FileNotFoundException(message: "Unable to find project file.", fileName: fileName);
        }

        List<ProjectReference> references = [];

        XmlDocument doc = new();
        doc.Load(fileName);

        if (includeReferences)
        {
            XmlNodeList? nodes = doc.SelectNodes("//Project/ItemGroup/ProjectReference");

            if (nodes is not null)
            {
                foreach (XmlElement node in nodes.OfType<XmlElement>())
                {
                    string includeAttr = node.GetAttribute(localName: "Include", namespaceURI: "");

                    if (!string.IsNullOrEmpty(includeAttr))
                    {
                        references.Add(new(File: baseDir, Name: includeAttr));
                    }
                }
            }
        }

        if (includeChildReferences)
        {
            XmlNodeList? nodes = doc.SelectNodes("//Project/ItemGroup/ProjectReference");

            if (nodes is not null)
            {
                foreach (XmlElement node in nodes.OfType<XmlElement>())
                {
                    string includeAttr = node.GetAttribute("Include");

                    if (!string.IsNullOrEmpty(includeAttr))
                    {
                        string childPath = Path.Combine(path1: baseDir, path2: includeAttr);

                        references.AddRange(GetProjectReferences(fileName: childPath, includeReferences: true, includeChildReferences: true));
                    }
                }
            }
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

    public static bool ShouldCheckSdk(string sdk, string projectFolder, XmlDocument xml)
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