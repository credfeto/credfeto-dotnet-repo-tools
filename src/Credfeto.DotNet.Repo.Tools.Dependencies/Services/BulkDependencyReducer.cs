using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services;

public sealed class BulkDependencyReducer : IBulkDependencyReducer
{
    public async ValueTask BulkUpdateAsync(string templateRepository,
                                           string trackingFileName,
                                           string packagesFileName,
                                           string workFolder,
                                           string releaseConfigFileName,
                                           IReadOnlyList<string> repositories,
                                           CancellationToken cancellationToken)
    {
        ReferenceConfig config = new();

        int result = await StaticMethods.CheckReferencesAsync(sourceDirectory: workFolder, config: config, cancellationToken: cancellationToken);

        Debug.WriteLine(result);
    }

    private static class StaticMethods
    {
        private const string MinimalSdk = "Microsoft.NET.Sdk";

        public static string? ExtractProjectFromReference(string reference)
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

        public static List<PackageReference> GetPackageReferences(string fileName, bool includeReferences, bool includeChildReferences, ReferenceConfig config)
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
            XPathNavigator navigator = doc.CreateNavigator() ?? throw new InvalidDataException("Could not create navigator");

            if (includeReferences)
            {
                XPathNodeIterator packageReferenceNodes = navigator.Select("//Project/ItemGroup/PackageReference");

                while (packageReferenceNodes.MoveNext())
                {
                    XPathNavigator? node = packageReferenceNodes.Current;

                    if (node is null)
                    {
                        continue;
                    }

                    string includeAttr = node.GetAttribute(localName: "Include", namespaceURI: "");

                    if (!string.IsNullOrEmpty(includeAttr))
                    {
                        if (!allPackageIds.Contains(includeAttr))
                        {
                            allPackageIds.Add(includeAttr);
                        }
                    }
                }

                packageReferenceNodes = navigator.Select("//Project/ItemGroup/PackageReference");

                while (packageReferenceNodes.MoveNext())
                {
                    XPathNavigator? node = packageReferenceNodes.Current;

                    if (node is null)
                    {
                        continue;
                    }

                    string includeAttr = node.GetAttribute(localName: "Include", namespaceURI: "");

                    if (string.IsNullOrEmpty(includeAttr))
                    {
                        continue;
                    }

                    XPathNavigator? privateAssetsNode = node.SelectSingleNode("PrivateAssets");

                    if (privateAssetsNode is not null)
                    {
                        continue;
                    }

                    if (config.IsDoNotRemovePackage(packageId: includeAttr, allPackageIds: allPackageIds))
                    {
                        continue;
                    }

                    XPathNavigator? versionNode = node.SelectSingleNode("Version");

                    if (versionNode is not null)
                    {
                        references.Add(new(File: baseDir, Name: includeAttr, Version: versionNode.Value));
                    }
                }
            }

            if (includeChildReferences)
            {
                XPathNodeIterator projectReferenceNodes = navigator.Select("//Project/ItemGroup/ProjectReference");

                while (projectReferenceNodes.MoveNext())
                {
                    XPathNavigator? node = projectReferenceNodes.Current;

                    if (node is null)
                    {
                        continue;
                    }

                    string includeAttr = node.GetAttribute(localName: "Include", namespaceURI: "");

                    if (!string.IsNullOrEmpty(includeAttr))
                    {
                        string childPath = Path.Combine(path1: baseDir, path2: includeAttr);
                        List<PackageReference> childReferences = GetPackageReferences(fileName: childPath, includeReferences: true, includeChildReferences: true, config);
                        references.AddRange(childReferences);
                    }
                }
            }

            return references;
        }

        public static List<ProjectReference> GetProjectReferences(string fileName, bool includeReferences, bool includeChildReferences)
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

        public static bool ShouldHaveNarrowerPackageReference(string projectFolder, string packageId)
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
                string content = File.ReadAllText(file);

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

        public static List<FileInfo> GetProjects(string sourceDirectory, ReferenceConfig config)
        {
            List<FileInfo> projects = new();

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

        public static async ValueTask<int> CheckReferencesAsync(string sourceDirectory, ReferenceConfig config, CancellationToken cancellationToken)
        {
            List<FileInfo> files = GetProjects(sourceDirectory, config);
            Console.WriteLine($"Number of projects to check: {files.Count}");

            WriteSectionStart("Checking Projects");

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<ReferenceCheckResult> obsoletes = new();
            List<ReferenceCheckResult> reduceReferences = new();
            List<ReferenceCheckResult> changeSdk = new();

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

                WriteSectionStart($"({projectInstance}/{projectCount}): Testing project: {file.Name}");

                byte[] rawFileContent = await File.ReadAllBytesAsync(file.FullName, cancellationToken);

                if (!BuildProject(fileName: file.FullName, fullError: true))
                {
                    WriteProgress("* Does not build without changes");

                    throw new("Failed to build a project");
                }

                List<PackageReference> childPackageReferences = GetPackageReferences(fileName: file.FullName, includeReferences: false, includeChildReferences: true, config);
                List<ProjectReference> childProjectReferences = GetProjectReferences(fileName: file.FullName, includeReferences: false, includeChildReferences: true);

                XmlDocument xml = new();
                xml.Load(file.FullName);

                XmlNode? projectNode = xml.SelectSingleNode("/Project");

                if (projectNode?.Attributes is not null)
                {
                    XmlAttribute? sdkAttr = projectNode.Attributes["Sdk"];

                    if (sdkAttr is not null)
                    {
                        string sdk = sdkAttr?.Value;

                        if (ShouldCheckSdk(sdk: sdk, projectFolder: file.DirectoryName, xml: xml))
                        {
                            sdkAttr.Value = MinimalSdk;
                            xml.Save(file.FullName);

                            WriteProgress($"* Building {file.Name} using {MinimalSdk} instead of {sdk}...");
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
                            WriteProgress($"= SDK does not need changing. Currently {MinimalSdk}.");
                        }
                    }
                }

                List<string> allPackageIds = new();
                XmlNodeList packageReferences = xml.SelectNodes("/Project/ItemGroup/PackageReference");
                XmlNodeList projectReferences = xml.SelectNodes("/Project/ItemGroup/ProjectReference");

                foreach (XmlNode node in packageReferences)
                {
                    if (node.Attributes?["Include"] != null)
                    {
                        allPackageIds.Add(node.Attributes["Include"].Value);
                    }
                }

                List<XmlNode> allNodes = new();

                foreach (XmlNode node in packageReferences)
                {
                    allNodes.Add(node);
                }

                foreach (XmlNode node in projectReferences)
                {
                    allNodes.Add(node);
                }

                foreach (XmlNode node in allNodes)
                {
                    if (node.Attributes?["Include"] == null)
                    {
                        WriteProgress("= Skipping malformed include");

                        continue;
                    }

                    string includeName = node.Attributes["Include"].Value;

                    if (config.IsDoNotRemovePackage(packageId: includeName, allPackageIds: allPackageIds))
                    {
                        WriteProgress($"= Skipping {includeName} as it is marked as do not remove");

                        continue;
                    }

                    if (node["PrivateAssets"] != null)
                    {
                        WriteProgress($"= Skipping {includeName} as it uses private assets");

                        continue;
                    }

                    WriteProgress($"Checking: {includeName}");

                    XmlNode previousNode = node.PreviousSibling;
                    XmlNode parentNode = node.ParentNode;
                    parentNode?.RemoveChild(node);

                    bool needToBuild = true;
                    xml.Save(file.FullName);

                    XmlNode versionNode = node["Version"];

                    if (versionNode != null)
                    {
                        PackageReference? existingChildInclude = childPackageReferences.FirstOrDefault(p => p.Name == includeName && p.Version == versionNode.InnerText);

                        if (existingChildInclude != null)
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
                        ProjectReference? existingChildInclude = childProjectReferences.FirstOrDefault(p => p.Name == includeName);

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

                        if (versionNode != null)
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

                        if (versionNode != null)
                        {
                            if (ShouldHaveNarrowerPackageReference(projectFolder: file.DirectoryName, packageId: includeName))
                            {
                                reduceReferences.Add(new(File: file, Type: ReferenceType.Package, Name: includeName, Version: versionNode.InnerText));
                            }
                        }
                        else
                        {
                            string? packageId = ExtractProjectFromReference(includeName);

                            if (!string.IsNullOrEmpty(packageId) && ShouldHaveNarrowerPackageReference(projectFolder: file.DirectoryName, packageId: packageId))
                            {
                                reduceReferences.Add(new(File: file, Type: ReferenceType.Project, Name: includeName));
                            }
                        }
                    }

                    if (restore)
                    {
                        if (previousNode == null)
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
                    Console.Error.WriteLine($"### Failed to build {file.FullName} after restore.");

                    throw new("Failed to build project after restore");
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

            return obsoletes.Count + changeSdk.Count + reduceReferences.Count;
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
    }
}