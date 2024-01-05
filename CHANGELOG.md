# Changelog
All notable changes to this project will be documented in this file.

<!--
Please ADD ALL Changes to the UNRELEASED SECTION and not a specific release
-->

## [Unreleased]
### Added
### Fixed
- Rebuild cached packages list before processing to ensure the latest of all packages is available
### Changed
- Dependencies - Updated Credfeto.Package to 1.10.45.303
### Removed
### Deployment Changes

<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->
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