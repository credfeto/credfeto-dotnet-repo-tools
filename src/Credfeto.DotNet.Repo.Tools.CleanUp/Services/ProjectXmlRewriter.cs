using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class ProjectXmlRewriter : IProjectXmlRewriter
{
    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "TODO just comments")]
    public void ReOrderPropertyGroups(XmlDocument projectDocument, string filename)
    {
        if (projectDocument.SelectSingleNode("Project") is not XmlElement project)
        {
            return;
        }

        XmlNodeList? propertyGroups = project.SelectNodes("PropertyGroup");

        if (propertyGroups is null)
        {
            return;
        }

        MergeProprtiesOfMultipleGroups(propertyGroups: propertyGroups);

        ReOrderPropertyGroupWithAttributesOrComments(filename: filename, propertyGroups: propertyGroups);
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "TODO just comments")]
    public void ReOrderIncludes(XmlDocument projectDocument)
    {
        /*
             $itemGroups = $project.SelectNodes("ItemGroup")

           $normalItems = @{}
           $privateItems = @{}
           $projectItems = @{}

           foreach($itemGroup in $itemGroups) {
               if($itemGroup.HasAttributes) {
                   # Skip groups that have attributes
                   Continue
               }

               $toRemove = @()

               # Extract Package References
               $includes = $itemGroup.SelectNodes("PackageReference")
               if($includes.Count -ne 0) {

                   foreach($include in $includes) {

                       [string]$packageId = $include.GetAttribute("Include")
                       [string]$private = $include.GetAttribute("PrivateAssets")
                       $toRemove += $include

                       if([string]::IsNullOrEmpty($private)) {
                           if(!$normalItems.Contains($packageId.ToUpper())) {
                               $normalItems.Add($packageId.ToUpper(), $include)
                           }
                       }
                       else {
                           if(!$privateItems.Contains($packageId.ToUpper())) {
                               $privateItems.Add($packageId.ToUpper(), $include)
                           }
                       }
                   }
               }

               # Extract Project References
               $includes = $itemGroup.SelectNodes("ProjectReference")
               if($includes.Count -ne 0) {

                   foreach($include in $includes) {

                       [string]$projectPath = $include.GetAttribute("Include")

                       $toRemove += $include
                       if(!$projectItems.Contains($projectPath.ToUpper())) {
                           $projectItems.Add($projectPath.ToUpper(), $include)
                       }
                   }
               }

               # Folder Includes
               $includes = $itemGroup.SelectNodes("Folder")
               if($includes.Count -ne 0) {
                   foreach($include in $includes) {
                       Log -message "* Found Folder to remove $( $include.Include )"
                       $toRemove += $include
                   }
               }

               # Remove items marked for deletion
               foreach($include in $toRemove) {
                   [void]$itemGroup.RemoveChild($include)
               }

               # Remove Empty item Groups
               if($itemGroup.ChildNodes.Count -eq 0) {
                   [void]$project.RemoveChild($itemGroup)
               }
           }

           # Write References to projects
           if($projectItems.Count -ne 0) {
               $itemGroup = $data.CreateElement("ItemGroup")
               foreach($includeKey in $projectItems.Keys | Sort-Object -CaseSensitive ) {
                   $include = $projectItems[$includeKey]
                   $itemGroup.AppendChild($include)
               }
               $project.AppendChild($itemGroup)
           }

           # Write References that are not dev only dependencies
           if($normalItems.Count -ne 0) {
               $itemGroup = $data.CreateElement("ItemGroup")
               foreach($includeKey in $normalItems.Keys | Sort-Object -CaseSensitive ) {
                   $include = $normalItems[$includeKey]
                   $itemGroup.AppendChild($include)
               }
               $project.AppendChild($itemGroup)
           }

           # Write References that are dev only dependencies
           if($privateItems.Count -ne 0) {
               $itemGroup = $data.CreateElement("ItemGroup")
               foreach($includeKey in $privateItems.Keys | Sort-Object -CaseSensitive ) {
                   $include = $privateItems[$includeKey]
                   $itemGroup.AppendChild($include)
               }
               $project.AppendChild($itemGroup)
           }
         */
        throw new NotSupportedException("Needs to be written");
    }

    private static void RemoveNodes(List<XmlElement> toRemove)
    {
        foreach (XmlElement item in toRemove)
        {
            // ! Should always have a parent here
            XmlNode parent = item.ParentNode!;

            parent.RemoveChild(item);
        }
    }

    private static void MergeProprtiesOfMultipleGroups(XmlNodeList propertyGroups)
    {
        IReadOnlyList<XmlElement> combinablePropertyGroups =
        [
            ..propertyGroups.OfType<XmlElement>()
                            .Where(IsCombinableGroup)
        ];

        XmlElement? targetPropertyGroup = combinablePropertyGroups.FirstOrDefault();

        if (targetPropertyGroup is null)
        {
            return;
        }

        List<XmlElement> toRemove = [];
        Dictionary<string, XmlNode> orderedChildren = new(StringComparer.Ordinal);

        foreach (XmlElement propertyGroup in combinablePropertyGroups)
        {
            XmlNodeList children = propertyGroup.ChildNodes;

            foreach (XmlElement child in children)
            {
                orderedChildren.Add(key: child.Name, value: child);
            }

            if (targetPropertyGroup != propertyGroup)
            {
                toRemove.Add(propertyGroup);
            }
        }

        // Empty the target property group
        targetPropertyGroup.RemoveAll();

        // Add the children we've added to the target property group
        foreach (string entryKey in orderedChildren.Keys.OrderBy(keySelector: x => x, comparer: StringComparer.Ordinal))
        {
            XmlNode item = orderedChildren[entryKey];
            targetPropertyGroup.AppendChild(item);
        }

        // remove the old groups
        RemoveNodes(toRemove);
    }

    private static bool IsCombinableGroup(XmlElement propertyGroup)
    {
        if (propertyGroup.HasAttributes)
        {
            return false;
        }

        XmlNodeList children = propertyGroup.ChildNodes;

        HashSet<string> childNames = new(StringComparer.Ordinal);

        foreach (XmlNode child in children)
        {
            if (IsComment(child))
            {
                return false;
            }

            if (IsDefineConstants(child))
            {
                return false;
            }

            if (!childNames.Add(child.Name))
            {
                // Has a duplicate name
                return false;
            }
        }

        return true;
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "TODO just comments")]
    private static void ReOrderPropertyGroupWithAttributesOrComments(string filename, XmlNodeList propertyGroups)
    {
        IReadOnlyList<XmlElement> nonCombinablePropertyGroups =
        [
            ..propertyGroups.OfType<XmlElement>()
                            .Where(ph => !IsCombinableGroup(ph))
        ];

        foreach (XmlElement propertyGroup in nonCombinablePropertyGroups)
        {
            Dictionary<string, string> attributes = new(StringComparer.Ordinal);

            foreach (XmlAttribute attribute in propertyGroup.Attributes)
            {
                string attValue = propertyGroup.GetAttribute(attribute.Name);
                attributes[attribute.Name] = attValue;
            }

            XmlNodeList children = propertyGroup.ChildNodes;

            if (children.OfType<XmlNode>()
                        .Any(IsComment))
            {
                Log(message: $"{filename} SKIPPING GROUP AS Found Comment");

                continue;
            }

            Dictionary<string, XmlNode> orderedChildren = new(StringComparer.Ordinal);
            bool replace = true;

            foreach (XmlElement child in children)
            {
                if (IsDefineConstants(child))
                {
                    // Skip DefineConstants as they can be added many times
                    replace = false;

                    break;
                }

                string name = child.Name;

                if (!orderedChildren.TryAdd(key: name, value: child))
                {
                    replace = false;

                    Log(message: $"{filename} SKIPPING GROUP AS Found Duplicate item {name}");

                    break;
                }
            }

            if (replace)
            {
                propertyGroup.RemoveAll();

                foreach (string entryKey in orderedChildren.Keys.OrderBy(keySelector: x => x, comparer: StringComparer.Ordinal))
                {
                    XmlNode item = orderedChildren[entryKey];
                    propertyGroup.AppendChild(item);
                }

                foreach (KeyValuePair<string, string> attribute in attributes)
                {
                    propertyGroup.SetAttribute(name: attribute.Key, value: attribute.Value);
                }
            }
        }
    }

    private static bool IsDefineConstants(XmlNode node)
    {
        return StringComparer.Ordinal.Equals(x: node.Name, y: "DefineConstants");
    }

    private static bool IsComment(XmlNode node)
    {
        return node.NodeType == XmlNodeType.Comment;
    }

    private static void Log(string message)
    {
        Debug.WriteLine(message);
    }
}