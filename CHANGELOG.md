# Changelog
All notable changes to this project will be documented in this file.

<!--
Please ADD ALL Changes to the UNRELEASED SECTION and not a specific release
-->

## [Unreleased]
### Added
### Fixed
### Changed
### Removed
### Deployment Changes

<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->
## [1.0.22] - 2024-05-16
### Changed
- Dependencies - Updated Roslynator.Analyzers to 4.12.1
- Dependencies - Updated xunit.analyzers to 1.12.0
- Dependencies - Updated xunit.runner.visualstudio to 2.5.8
- Dependencies - Updated xunit to 2.7.1
- Dependencies - Updated Credfeto.Enumeration to 1.1.6.354
- Dependencies - Updated SmartAnalyzers.CSharpExtensions.Annotations to 4.2.11
- Dependencies - Updated FunFair.CodeAnalysis to 7.0.14.369
- Dependencies - Updated FunFair.Test.Common to 6.1.51.455
- Dependencies - Updated SonarAnalyzer.CSharp to 9.24.0.89429
- Dependencies - Updated FunFair.BuildCheck to 474.0.20.429
- SDK - Updated DotNet SDK to 8.0.300

## [1.0.21] - 2024-04-10
### Changed
- Dependencies - Updated Credfeto.Enumeration to 1.1.5.315
- Dependencies - Updated Meziantou.Analyzer to 2.0.146
- Dependencies - Updated Roslynator.Analyzers to 4.12.0
- Dependencies - Updated LibGit2Sharp to 0.30.0
- Dependencies - Updated TeamCity.VSTest.TestAdapter to 1.0.40
- Dependencies - Updated SonarAnalyzer.CSharp to 9.23.1.88495
- Dependencies - Updated Microsoft.Extensions to 8.0.4
- SDK - Updated DotNet SDK to 8.0.204
- Dependencies - Updated FunFair.CodeAnalysis to 7.0.13.341

## [1.0.20] - 2024-03-08
### Added
- Cloning of Directory.build.props and other files in the src folder

## [1.0.19] - 2024-02-26
### Fixed
- Regex for Dotnet SDK updates

## [1.0.18] - 2024-02-24
### Fixed
- DotNet SDK version update

## [1.0.17] - 2024-02-19
### Fixed
- Corrected dependabot for nuget

## [1.0.16] - 2024-02-16
### Added
- Support for dependabot

## [1.0.15] - 2024-02-15
### Changed

- Dependencies - Updated Credfeto.Enumeration.Source.Generation to 1.1.3.296
- Dependencies - Updated Microsoft.VisualStudio.Threading.Analyzers to 17.9.28
- Dependencies - Updated Credfeto.Extensions.Linq to 1.0.16.122
- Dependencies - Updated FunFair.CodeAnalysis to 7.0.8.274
- Dependencies - Updated FunFair.Test.Common to 6.1.41.357
- Dependencies - Updated Meziantou.Analyzer to 2.0.141
- Dependencies - Updated NSubstitute.Analyzers.CSharp to 1.0.17
- Dependencies - Updated Credfeto.Date to 1.1.13.250

## [1.0.14] - 2024-02-07
### Changed
- Dependencies - Updated SonarAnalyzer.CSharp to 9.19.0.84025
- Dependencies - Updated Microsoft.NET.Test.Sdk to 17.9.0
- Dependencies - Updated Meziantou.Analyzer to 2.0.140

## [1.0.13] - 2024-01-29
### Fixed
- Search of files for actions to be recursive
### Changed
- Dependencies - Updated SonarAnalyzer.CSharp to 9.18.0.83559
- Dependencies - Updated Meziantou.Analyzer to 2.0.139

## [1.0.12] - 2024-01-26
### Fixed
- File updates with modification

## [1.0.11] - 2024-01-25
### Changed
- Dependencies - Updated Roslynator.Analyzers to 4.10.0
- Updated logging

## [1.0.10] - 2024-01-24
### Changed
- Dependencies - Updated SonarAnalyzer.CSharp to 9.17.0.82934
- Dependencies - Updated Meziantou.Analyzer to 2.0.138
- Conditionally change self hosted runners to github hosted runners when updating workflows

## [1.0.9] - 2024-01-22
### Added
- Copying simple items from template if missing or unchanged
- Copying resharper settings from template if missing or unchanged
### Changed
- Dependencies - Updated Meziantou.Analyzer to 2.0.136

## [1.0.8] - 2024-01-15
### Added
- Basic template update support for .net repos (SDK version)
### Changed
- Dependencies - Updated Roslynator.Analyzers to 4.9.0
- Dependencies - Updated Meziantou.Analyzer to 2.0.136
- Dependencies - Updated xunit.analyzers to 1.10.0
- Dependencies - Updated xunit to 2.6.6
- Dependencies - Updated FunFair.Test.Common to 6.1.39.333

## [1.0.7] - 2024-01-10
### Added

- Detection of installed dotnet sdk versions
- Explicit check for Dotnet SDK being installed by the template
### Changed
- Dependencies - Updated Roslynator.Analyzers to 4.8.0
- Dependencies - Updated xunit.analyzers to 1.9.0
- Dependencies - Updated xunit to 2.6.5
- Dependencies - Updated FunFair.CodeAnalysis to 7.0.6.239
- Dependencies - Updated FunFair.Test.Common to 6.1.33.320
- Dependencies - Updated Meziantou.Analyzer to 2.0.135
- Dependencies - Updated Microsoft.Extensions to 8.0.1

## [1.0.6] - 2024-01-05
### Fixed
- Rebuild cached packages list before processing to ensure the latest of all packages is available
### Changed
- Dependencies - Updated Credfeto.Package to 1.10.45.303

## [1.0.5] - 2024-01-04
### Changed
- Better logging

## [1.0.4] - 2024-01-02
### Fixed
- Unhandled exception when resetting to main branch
### Changed
- Dependencies - Updated Meziantou.Analyzer to 2.0.132

## [1.0.3] - 2023-12-30
### Changed
- Dependencies - Updated Meziantou.Analyzer to 2.0.128
- Dependencies - Updated FunFair.Test.Common to 6.1.27.296
- Switched to use Cocona instead of CommandLineParser

## [1.0.2] - 2023-12-28
### Added
- Ability to load repositories list from a URL
- Ability to load package config list from a URL
### Changed
- Dependencies - Updated Nullable.Extended.Analyzer to 1.15.6169
- Dependencies - Updated FunFair.Test.Common to 6.1.27.296

## [1.0.1] - 2023-12-24
### Added
- Pre-loading and updating cached packages before processing repo to avoid lots of additional updates
### Changed
- Dependencies - Updated Credfeto.ChangeLog to 1.10.19.111

## [1.0.0] - 2023-12-24
### Added
- Initial bulk package update
### Changed
- Dependencies - Updated Meziantou.Analyzer to 2.0.127
- Dependencies - Updated Philips.CodeAnalysis.MaintainabilityAnalyzers to 1.5.0
- Dependencies - Updated SmartAnalyzers.CSharpExtensions.Annotations to 4.2.9
- Dependencies - Updated SonarAnalyzer.CSharp to 9.16.0.82469
- Dependencies - Updated xunit.analyzers to 1.8.0
- Dependencies - Updated FunFair.Test.Common to 6.1.23.276
- Dependencies - Updated TeamCity.VSTest.TestAdapter to 1.0.39
- Dependencies - Updated xunit to 2.6.4
- Dependencies - Updated xunit.runner.visualstudio to 2.5.6
- Dependencies - Updated Credfeto.Package to 1.10.44.293
- Dependencies - Updated FunFair.BuildCheck to 474.0.17.246
- Dependencies - Updated FunFair.BuildVersion to 6.2.15.250

## [0.0.0] - Project created