using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services;

public sealed class DependencyReducer : IDependencyReducer
{
    private const string MINIMAL_SDK = "Microsoft.NET.Sdk";
    private readonly ILogger<DependencyReducer> _logger;

    public DependencyReducer(ILogger<DependencyReducer> logger)
    {
        this._logger = logger;
    }

    public async ValueTask<bool> CheckReferencesAsync(string sourceDirectory, ReferenceConfig config, CancellationToken cancellationToken)
    {
        List<FileInfo> files = GetProjects(sourceDirectory: sourceDirectory, config: config);
        Console.WriteLine($"Number of projects to check: {files.Count}");

        WriteSectionStart("Checking Projects");

        Stopwatch stopwatch = Stopwatch.StartNew();
        List<ReferenceCheckResult> obsoletes = [];
        List<ReferenceCheckResult> reduceReferences = [];
        List<ReferenceCheckResult> changeSdk = [];

        int projectCount = files.Count;
        int projectInstance = 0;

        foreach (FileInfo file in files)
        {
            projectInstance++;

            if (config.IsIgnoreProject(file.Name))
            {
                WriteProgress($"Ignoring {file.Name}");

                continue;
            }

            string? projectDirectory = file.DirectoryName;

            if (string.IsNullOrEmpty(projectDirectory))
            {
                WriteProgress($"Ignoring {file.Name} as directory could not be found");

                continue;
            }

            WriteSectionStart($"({projectInstance}/{projectCount}): Testing project: {file.Name}");

            byte[] rawFileContent = await File.ReadAllBytesAsync(path: file.FullName, cancellationToken: cancellationToken);

            if (!BuildProject(fileName: file.FullName, fullError: true))
            {
                WriteError("* Does not build without changes");

                throw new DotNetBuildErrorException("Failed to build a project");
            }

            List<PackageReference> childPackageReferences = GetPackageReferences(fileName: file.FullName, includeReferences: false, includeChildReferences: true, config: config);
            List<ProjectReference> childProjectReferences = GetProjectReferences(fileName: file.FullName, includeReferences: false, includeChildReferences: true);

            XmlDocument xml = new();
            xml.Load(file.FullName);

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
                        xml.Save(file.FullName);

                        WriteProgress($"* Building {file.Name} using {MINIMAL_SDK} instead of {sdk}...");
                        bool buildOk = BuildProject(fileName: file.FullName, fullError: false);
                        bool restore = true;

                        if (buildOk)
                        {
                            WriteProgress("  - Building succeeded.");
                            changeSdk.Add(new(File: file, Type: ReferenceType.Sdk, Name: sdk));

                            if (BuildSolution())
                            {
                                restore = false;
                            }
                        }
                        else
                        {
                            WriteProgress("  = Building failed.");
                        }

                        if (restore)
                        {
                            sdkAttr.Value = sdk;
                            xml.Save(file.FullName);
                        }
                        else
                        {
                            rawFileContent = await File.ReadAllBytesAsync(path: file.FullName, cancellationToken: cancellationToken);
                        }
                    }
                    else
                    {
                        WriteProgress($"= SDK does not need changing. Currently {MINIMAL_SDK}.");
                    }
                }
            }

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
                xml.Save(file.FullName);

                XmlNode? versionNode = node["Version"];

                if (versionNode is not null)
                {
                    PackageReference? existingChildInclude = FindChildPackage(childPackageReferences: childPackageReferences, includeName: includeName, version: versionNode.InnerText);

                    if (existingChildInclude is not null)
                    {
                        WriteProgress($"= {file.Name} references package {includeName} ({versionNode.InnerText}) also in child project {existingChildInclude.File}");
                        needToBuild = false;
                    }
                    else
                    {
                        WriteProgress($"* Building {file.Name} without package {includeName} ({versionNode.InnerText})...");
                    }
                }
                else
                {
                    ProjectReference? existingChildInclude = FindChildProject(childProjectReferences: childProjectReferences, includeName: includeName);

                    if (existingChildInclude is not null)
                    {
                        WriteProgress($"= {file.Name} references project {includeName} also in child project {existingChildInclude.File}");
                        needToBuild = false;
                    }
                    else
                    {
                        WriteProgress($"* Building {file.Name} without project {includeName}...");
                    }
                }

                bool buildOk = needToBuild
                    ? BuildProject(fileName: file.FullName, fullError: false)
                    : true;
                bool restore = true;

                if (buildOk)
                {
                    WriteProgress("  - Building succeeded.");

                    if (versionNode is not null)
                    {
                        obsoletes.Add(new(File: file, Type: ReferenceType.Package, Name: includeName, Version: versionNode.InnerText));
                    }
                    else
                    {
                        obsoletes.Add(new(File: file, Type: ReferenceType.Project, Name: includeName));
                    }

                    if (BuildSolution())
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
                            reduceReferences.Add(new(File: file, Type: ReferenceType.Package, Name: includeName, Version: versionNode.InnerText));
                        }
                    }
                    else
                    {
                        string? packageId = ExtractProjectFromReference(includeName);

                        if (!string.IsNullOrEmpty(packageId) &&
                            await ShouldHaveNarrowerPackageReferenceAsync(projectFolder: projectDirectory, packageId: packageId, cancellationToken: cancellationToken))
                        {
                            reduceReferences.Add(new(File: file, Type: ReferenceType.Project, Name: includeName));
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

                    xml.Save(file.FullName);
                }
                else
                {
                    rawFileContent = await File.ReadAllBytesAsync(path: file.FullName, cancellationToken: cancellationToken);
                }
            }

            await File.WriteAllBytesAsync(path: file.FullName, bytes: rawFileContent, cancellationToken: cancellationToken);

            if (!BuildProject(fileName: file.FullName, fullError: true))
            {
                WriteError($"### Failed to build {file.FullName} after restore.");

                throw new DotNetBuildErrorException("Failed to build project after restore");
            }

            WriteSectionEnd($"({projectInstance}/{projectCount}): Testing project: {file.Name}");
        }

        WriteSectionEnd("Checking Projects");

        WriteProgress("");
        WriteProgress("-------------------------------------------------------------------------");
        WriteProgress($"Analyse completed in {stopwatch.Elapsed.TotalSeconds} seconds");
        WriteProgress($"{changeSdk.Count} SDK reference(s) could potentially be narrowed.");
        WriteProgress($"{obsoletes.Count} reference(s) could potentially be removed.");
        WriteProgress($"{reduceReferences.Count} reference(s) could potentially be switched.");

        PrintResults(header: "SDK:", items: changeSdk);
        PrintResults(header: "Obsolete:", items: obsoletes);
        PrintResults(header: "Reduce Scope:", items: reduceReferences);

        WriteStatistics(section: "SDK", value: changeSdk.Count);
        WriteStatistics(section: "Obsolete", value: obsoletes.Count);
        WriteStatistics(section: "Reduce", value: reduceReferences.Count);

        return obsoletes.Count + changeSdk.Count + reduceReferences.Count > 0;
    }

    private static List<FileInfo> GetProjects(string sourceDirectory, ReferenceConfig config)
    {
        List<FileInfo> projects = [];

        FileInfo[] files = new DirectoryInfo(sourceDirectory).GetFiles(searchPattern: "*.csproj", searchOption: SearchOption.AllDirectories);

        Console.WriteLine($"Number of projects: {files.Length}");

        foreach (FileInfo file in files)
        {
            bool ignoreProject = config.IsIgnoreProject(file.Name);
            Console.WriteLine($"Found * {file.FullName} (Ignore: {ignoreProject})");

            if (!ignoreProject)
            {
                projects.Add(file);
            }
        }

        return projects;
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

    private static void PrintResults(string header, List<ReferenceCheckResult> items)
    {
        Console.WriteLine($"\n{header}");
        FileInfo? previousFile = null;

        foreach (ReferenceCheckResult item in items)
        {
            if (previousFile != item.File)
            {
                Console.WriteLine($"\nProject: {item.File.Name}");
            }

            if (item.Type == ReferenceType.Package)
            {
                Console.WriteLine($"* Package reference: {item.Name} ({item.Version})");
            }
            else
            {
                Console.WriteLine($"* Project reference: {item.Name}");
            }

            previousFile = item.File;
        }
    }

    private static bool BuildProject(string fileName, bool fullError)
    {
        // Implement your build logic, e.g., invoking MSBuild or dotnet CLI
        throw new NotImplementedException();
    }

    private static bool BuildSolution()
    {
        // Implement solution build logic
        throw new NotImplementedException();
    }

    private static void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }

    private static void WriteProgress(string message)
    {
        Console.WriteLine(message);
    }

    private static void WriteSectionStart(string message)
    {
        Console.WriteLine("=== " + message + " ===");
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

        XmlDocument doc = new();
        doc.Load(fileName);

        if (includeReferences)
        {
            XmlNodeList? packageReferenceNodes = doc.SelectNodes("//Project/ItemGroup/PackageReference");

            if (packageReferenceNodes is not null)
            {
                allPackageIds.AddRange(packageReferenceNodes.OfType<XmlElement>()
                                                            .Select(node => node.GetAttribute("Include"))
                                                            .Where(include => !string.IsNullOrEmpty(include) && !allPackageIds.Contains(value: include, comparer: StringComparer.OrdinalIgnoreCase)));

                foreach (XmlElement node in packageReferenceNodes.OfType<XmlElement>())
                {
                    string includeAttr = node.GetAttribute(localName: "Include", namespaceURI: "");

                    if (string.IsNullOrEmpty(includeAttr))
                    {
                        continue;
                    }

                    XmlNode? privateAssetsNode = node.SelectSingleNode("PrivateAssets");

                    if (privateAssetsNode is not null)
                    {
                        continue;
                    }

                    if (config.IsDoNotRemovePackage(packageId: includeAttr, allPackageIds: allPackageIds))
                    {
                        continue;
                    }

                    XmlNode? versionNode = node.SelectSingleNode("Version");

                    if (versionNode is not null)
                    {
                        references.Add(new(File: baseDir, Name: includeAttr, Version: versionNode.InnerText));
                    }
                }
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

    private static List<ProjectReference> GetProjectReferences(string fileName, bool includeReferences, bool includeChildReferences)
    {
        string? baseDir = Path.GetDirectoryName(fileName);

        if (string.IsNullOrEmpty(baseDir))
        {
            throw new FileNotFoundException(message: "Unable to find project file.", fileName: fileName);
        }

        List<ProjectReference> references = [];

        XmlDocument xml = new();
        xml.Load(fileName);

        XmlNamespaceManager namespaceManager = new(xml.NameTable);
        namespaceManager.AddNamespace(prefix: "msb", uri: "http://schemas.microsoft.com/developer/msbuild/2003");

        XPathNavigator navigator = xml.CreateNavigator() ?? throw new InvalidDataException("Could not create navigator");

        if (includeReferences)
        {
            XPathNodeIterator nodes = navigator.Select(xpath: "//msb:Project/msb:ItemGroup/msb:ProjectReference", resolver: namespaceManager);

            while (nodes.MoveNext())
            {
                XPathNavigator? node = nodes.Current;

                if (node is null)
                {
                    continue;
                }

                string includeAttr = node.GetAttribute(localName: "Include", namespaceURI: "");

                if (!string.IsNullOrEmpty(includeAttr))
                {
                    references.Add(new(File: baseDir, Name: includeAttr));
                }
            }
        }

        if (includeChildReferences)
        {
            XPathNodeIterator nodes = navigator.Select(xpath: "//msb:Project/msb:ItemGroup/msb:ProjectReference", resolver: namespaceManager);

            while (nodes.MoveNext())
            {
                XPathNavigator? node = nodes.Current;

                if (node is null)
                {
                    continue;
                }

                string includeAttr = node.GetAttribute(localName: "Include", namespaceURI: "");

                if (!string.IsNullOrEmpty(includeAttr))
                {
                    string childPath = Path.Combine(path1: baseDir, path2: includeAttr);

                    List<ProjectReference> childReferences = GetProjectReferences(fileName: childPath, includeReferences: true, includeChildReferences: true);
                    references.AddRange(childReferences);
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
}