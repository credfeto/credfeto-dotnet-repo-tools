using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
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
                        List<PackageReference> childReferences = GetPackageReferences(fileName: childPath, includeReferences: true, includeChildReferences: true);
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
            List<FileInfo> files = GetProjects(sourceDirectory: sourceDirectory, config: config);
            Console.WriteLine($"Number of projects to check: {files.Count}");

            WriteSectionStart("Checking Projects");

            Stopwatch stopWatch = Stopwatch.StartNew();

            List<ReferenceCheckResult> obsoletes = [];
            List<ReferenceCheckResult> reduceReferences = [];
            List<ReferenceCheckResult> changeSdk = [];

            int projectCount = files.Count;
            int projectInstance = 0;

            foreach (FileInfo file in files)
            {
                projectInstance++;

                bool ignoreProject = config.IsIgnoreProject(file.Name);

                if (ignoreProject)
                {
                    WriteProgress($"Ignoring {file.Name}");

                    continue;
                }

                WriteSectionStart($"({projectInstance}/{projectCount}): Testing project: {file.Name}");

                byte[] rawFileContent = await File.ReadAllBytesAsync(path: file.FullName, cancellationToken: cancellationToken);

                bool buildOk = BuildProject(fileName: file.FullName, fullError: true);
                Console.WriteLine($"Build OK: {buildOk}");

                if (!buildOk)
                {
                    WriteProgress("* Does not build without changes");

                    throw new("Failed to build a project");
                }

                List<PackageReference> childPackageReferences = GetPackageReferences(fileName: file.FullName, includeReferences: false, includeChildReferences: true, config: config);
                List<ProjectReference> childProjectReferences = GetProjectReferences(fileName: file.FullName, includeReferences: false, includeChildReferences: true, config);

                XDocument xml = XDocument.Load(file.FullName);
                XElement? projectElement = xml.Root; // Assumes root is <Project>

                if (projectElement is not null)
                {
                    WriteProgress("SDK");
                    XAttribute? sdkAttribute = projectElement.Attribute("Sdk");

                    if (sdkAttribute is not null)
                    {
                        string sdk = sdkAttribute.Value;

                        bool shouldReplaceSdk = ShouldCheckSdk(sdk: sdk, projectFolder: file.DirectoryName, xml: xml);

                        if (shouldReplaceSdk)
                        {
                            sdkAttribute.Value = MinimalSdk;
                            xml.Save(file.FullName);

                            WriteProgress($"* Building {file.Name} using {MinimalSdk} instead of {sdk}...");
                            buildOk = BuildProject(fileName: file.FullName, fullError: false);
                            Console.WriteLine($"Build OK: {buildOk}");

                            bool restore = true;

                            if (buildOk)
                            {
                                WriteProgress("  - Building succeeded.");
                                WriteProgress($"{file.Name} references SDK {sdk} that could be reduced to {MinimalSdk}.");
                                changeSdk.Add(new(File: file, Type: ReferenceType.Sdk, Name: sdk, Version: null));

                                buildOk = BuildSolution();

                                if (buildOk)
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
                                sdkAttribute.Value = sdk;
                                xml.Save(file.FullName);
                            }
                            else
                            {
                                rawFileContent = File.ReadAllBytes(file.FullName);
                            }
                        }
                        else
                        {
                            WriteProgress($"= SDK does not need changing. Currently {MinimalSdk}.");
                        }
                    }
                }

                List<string> allPackageIds = [];

                List<XElement> packageReferences = projectElement.Descendants("PackageReference")
                                                                 .ToList();

                List<XElement> projectReferences = projectElement.Descendants("ProjectReference")
                                                                 .ToList();

                foreach (XElement node in packageReferences)
                {
                    XAttribute? includeAttr = node.Attribute("Include");

                    if (includeAttr != null)
                    {
                        allPackageIds.Add(includeAttr.Value);
                    }
                }

                List<XElement> nodes = packageReferences.Concat(projectReferences)
                                                        .ToList();

                foreach (XElement node in nodes)
                {
                    XAttribute? includeAttr = node.Attribute("Include");

                    if (includeAttr is null)
                    {
                        WriteProgress("= Skipping malformed include");

                        continue;
                    }

                    if (config.IsDoNotRemovePackage(packageId: includeAttr.Value, allPackageIds: allPackageIds))
                    {
                        WriteProgress($"= Skipping {includeAttr.Value} as it is marked as do not remove");

                        continue;
                    }

                    XElement? privateAssets = node.Element("PrivateAssets");

                    if (privateAssets is not null)
                    {
                        WriteProgress($"= Skipping {includeAttr.Value} as it uses private assets");

                        continue;
                    }

                    string includeName = includeAttr.Value;
                    WriteProgress($"Checking: {includeName}");

                    XNode? previousNode = node.PreviousNode;
                    XElement? parentNode = node.Parent;

                    node.Remove();

                    bool needToBuild = true;

                    xml.Save(file.FullName);

                    XElement? versionElement = node.Element("Version");

                    if (versionElement is not null)
                    {
                        PackageReference? existingChildInclude = childPackageReferences.FirstOrDefault(p => p.Name == includeAttr.Value && p.Version == versionElement.Value);

                        if (existingChildInclude is not null)
                        {
                            WriteProgress($"= {file.Name} references package {includeAttr.Value} ({versionElement.Value}) that is also referenced in child project {existingChildInclude.File}.");
                            needToBuild = false;
                        }
                        else
                        {
                            WriteProgress($"* Building {file.Name} without package {includeAttr.Value} ({versionElement.Value})... ");
                        }
                    }
                    else
                    {
                        ProjectReference? existingChildInclude = childProjectReferences.FirstOrDefault(p => p.Name == includeAttr.Value);

                        if (existingChildInclude is not null)
                        {
                            WriteProgress($"= {file.Name} references project {includeAttr.Value} that is also referenced in child project {existingChildInclude.File}.");
                            needToBuild = false;
                        }
                        else
                        {
                            WriteProgress($"* Building {file.Name} without project {includeAttr.Value}... ");
                        }
                    }

                    if (needToBuild)
                    {
                        buildOk = BuildProject(fileName: file.FullName, fullError: false);
                    }
                    else
                    {
                        buildOk = true;
                    }

                    bool restore = true;

                    if (buildOk)
                    {
                        WriteProgress("  - Building succeeded.");

                        if (versionElement is not null)
                        {
                            obsoletes.Add(new(File: file, Type: ReferenceType.Package, Name: includeAttr.Value, Version: versionElement.Value));
                        }
                        else
                        {
                            obsoletes.Add(new(File: file, Type: ReferenceType.Project, Name: includeAttr.Value, Version: null));
                        }

                        buildOk = BuildSolution();

                        if (buildOk)
                        {
                            restore = false;
                        }
                    }
                    else
                    {
                        WriteProgress("  = Building failed.");

                        if (versionElement is not null)
                        {
                            bool narrower = ShouldHaveNarrowerPackageReference(projectFolder: file.DirectoryName, packageId: includeAttr.Value);

                            if (narrower)
                            {
                                reduceReferences.Add(new(File: file, Type: ReferenceType.Package, Name: includeAttr.Value, Version: versionElement.Value));
                            }
                        }
                        else
                        {
                            string? packageId = ExtractProjectFromReference(includeAttr.Value);

                            if (!string.IsNullOrEmpty(packageId))
                            {
                                bool narrower = ShouldHaveNarrowerPackageReference(file.DirectoryName!, packageId: packageId);

                                if (narrower)
                                {
                                    reduceReferences.Add(new(File: file, Type: ReferenceType.Project, Name: includeAttr.Value, Version: null));
                                }
                            }
                        }
                    }

                    if (restore)
                    {
                        if (previousNode is null)
                        {
                            parentNode.AddFirst(node);
                        }
                        else
                        {
                            previousNode.AddAfterSelf(node);
                        }

                        xml.Save(file.FullName);
                    }
                    else
                    {
                        rawFileContent = File.ReadAllBytes(file.FullName);
                    }
                }

                File.WriteAllBytes(path: file.FullName, bytes: rawFileContent);

                buildOk = BuildProject(fileName: file.FullName, fullError: true);

                if (!buildOk)
                {
                    Console.Error.WriteLine($"### Failed to build {file.FullName} after project file restore. Project built successfully before.");

                    throw new("Failed to build project after restore");
                }

                WriteSectionEnd($"({projectInstance}/{projectCount}): Testing project: {file.Name}");
            }

            WriteSectionEnd("Checking Projects");

            WriteProgress("");
            WriteProgress("-------------------------------------------------------------------------");
            WriteProgress($"Analyse completed in {stopWatch.Elapsed.TotalSeconds} seconds");
            WriteProgress($"{changeSdk.Count} SDK reference(s) could potentially be narrowed.");
            WriteProgress($"{obsoletes.Count} reference(s) could potentially be removed.");
            WriteProgress($"{reduceReferences.Count} reference(s) could potentially be switched to different packages.");

            WriteStatistics(section: "SDK", value: changeSdk.Count);
            WriteStatistics(section: "Obsolete", value: obsoletes.Count);
            WriteStatistics(section: "Reduce", value: reduceReferences.Count);

            WriteProgress("SDK:");
            FileInfo? previousFile = null;

            foreach (ReferenceCheckResult sdkRef in changeSdk)
            {
                if (!Equals(objA: previousFile, objB: sdkRef.File))
                {
                    WriteProgress("");
                    WriteProgress($"Project: {sdkRef.File.Name}");
                }

                WriteProgress($"* Project reference: {sdkRef.Name}");
                previousFile = sdkRef.File;
            }

            WriteProgress("Obsolete:");
            previousFile = null;

            foreach (ReferenceCheckResult obsolete in obsoletes)
            {
                if (!Equals(objA: previousFile, objB: obsolete.File))
                {
                    WriteProgress("");
                    WriteProgress($"Project: {obsolete.File.Name}");
                }

                if (StringComparer.Ordinal.Equals(x: obsolete.Type, y: "Package"))
                {
                    WriteProgress($"* Package reference: {obsolete.Name} ({obsolete.Version})");
                }
                else
                {
                    WriteProgress($"* Project reference: {obsolete.Name}");
                }

                previousFile = obsolete.File;
            }

            WriteProgress("");
            WriteProgress("Reduce Scope:");
            previousFile = null;

            foreach (ReferenceCheckResult reduce in reduceReferences)
            {
                if (!Equals(objA: previousFile, objB: reduce.File))
                {
                    WriteProgress("");
                    WriteProgress($"Project: {reduce.File.Name}");
                }

                if (reduce.Type == ReferenceType.Package)
                {
                    WriteProgress($"* Package reference: {reduce.Name} ({reduce.Version})");
                }
                else
                {
                    WriteProgress($"* Project reference: {reduce.Name}");
                }

                previousFile = reduce.File;
            }

            int totalOptimisations = obsoletes.Count + changeSdk.Count + reduceReferences.Count;

            return totalOptimisations;
        }

        // Placeholder for helper methods, you will need to implement these yourself

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