using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class ProjectXmlRewriter : IProjectXmlRewriter
{
    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "TODO just comments")]
    public void ReOrderPropertyGroups(XmlDocument project, string filename)
    {
/*
    $toRemove = @()

   $propertyGroups = $project.SelectNodes("PropertyGroup")
   foreach($propertyGroup in $propertyGroups) {
       $children = $propertyGroup.SelectNodes("*")
       $attributes = [ordered]@{}
       foreach($attribute in $propertyGroup.Attributes) {
           $attValue = $propertyGroup.GetAttribute($attribute.Name)
           $attributes[$attribute.Name] = $attValue
       }
       $orderedChildren = @{}
       [bool]$replace = $true
       foreach($child in $children) {
           [string]$name = ($child.Name).ToString().ToUpper()
           if($name -eq "#COMMENT") {
               $replace = $false;
               Log -message "$filename SKIPPING GROUP AS Found Comment"
               Break
           }

           if($orderedChildren.Contains($name)) {
               $replace = $false;
               if($name -eq "DEFINECONSTANTS") {
                   # Skip DefineConstants as they can be added many times
                   Break
               }
               Log -message "$filename SKIPPING GROUP AS Found Duplicate item $name"
               Break
           }
           $orderedChildren.Add($name, $child)
       }

       if($replace) {
           if($orderedChildren) {
               $propertyGroup.RemoveAll()
               foreach($entryKey in $orderedChildren.Keys | Sort-Object -CaseSensitive) {
                   $item = $orderedChildren[$entryKey]
                   $propertyGroup.AppendChild($item)
               }

               foreach($attribute in $attributes.Keys) {
                   $propertyGroup.SetAttribute($attribute, $attributes[$attribute])
               }
           }
           else {
               $toRemove.Add($propertyGroup)
           }
       }
   }

   # remove any empty groups
   foreach($item in $toRemove) {
       [void]$project.RemoveChild($item)
   }
 */

        throw new NotSupportedException("Needs to be written");
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "TODO just comments")]
    public void ReOrderIncludes(XmlDocument project)
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
}