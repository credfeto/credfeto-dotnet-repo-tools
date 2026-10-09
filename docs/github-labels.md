# GitHub Labels Reference

The `update-template` command generates two GitHub configuration files for each managed repository (when `gitHub.labels.generate` is enabled in `template.json`, see [Configuration Reference](configuration.md)):

* **`labels.yml`**: defines all labels (name, colour, description) used in the repository
* **`labeler.yml`**: maps file path patterns to labels so pull requests are labelled automatically

## Static labels

These labels are applied to every managed repository regardless of content. This table mirrors the generated source of truth in [`.github/labels.yml`](../.github/labels.yml); update that file (via `LabelsBuilder`) first, then keep this table in sync:

| Label | Colour | Description |
| ----- | ------ | ----------- |
| `.NET update` | `a870c9` | Update to .NET SDK version in global.json |
| `AI-Work` | `ffa500` | Work for an AI Agent |
| `auto-pr` | `0000aa` | Pull request created automatically |
| `Blocked` | `ff0000` | Blocked by a dependency or external factor |
| `Bug` | `d73a4a` | Generic bug fix |
| `C#` | `db6baa` | C# source files |
| `C# Project` | `db6baa` | C# project files (`.csproj`) |
| `C# Solution` | `db6baa` | C# solution files (`.sln` / `.slnx`) |
| `Change Log` | `53fcd4` | Changelog tracking file |
| `Changelog Not Required` | `08f5f8` | No changelog entry required for this pull request |
| `Config Change` | `d8bb50` | Configuration file changes |
| `dependencies` | `0366d6` | Updates to dependencies |
| `DO NOT MERGE` | `ff0000` | This pull request should not be merged yet |
| `dotnet` | `db6baa` | .NET package updates |
| `Editorconfig` | `00dead` | Editor config file change |
| `Enhancement` | `a2eeef` | Enhancement to project |
| `github-actions` | `e09cf4` | GitHub Actions workflow files |
| `High` | `ffa500` | High priority |
| `Low` | `cc8899` | Low priority |
| `Markdown` | `5319e7` | Markdown files |
| `Medium` | `ffff00` | Medium priority |
| `Migration Script` | `b680e5` | SQL migration scripts |
| `never-close` | `1d76db` | This issue should never be closed - it is a permanent tracking issue |
| `no-pr-activity` | `ffff00` | Pull request has had no activity for a long time |
| `npm` | `e99695` | npm package update |
| `On Hold` | `ff0000` | Do not work on this |
| `Performance` | `0075ca` | Performance enhancement or issue |
| `Powershell` | `23bc12` | PowerShell source files |
| `Read Me` | `5319e7` | Repository readme file |
| `Refactor` | `30027a` | Code refactoring |
| `Security` | `ee0701` | Security issue, e.g. use of insecure packages, or security fix |
| `Setup` | `5319e7` | Setup instructions |
| `Solidity` | `413cd1` | Solidity source files |
| `SQL` | `413cd1` | SQL source files |
| `Static Code Analysis Rules` | `00dead` | Static code analysis ruleset files |
| `Tech Debt` | `30027a` | Technical debt |
| `Unit Tests` | `0e8a16` | Unit test and integration test projects |
| `Urgent` | `ff0000` | Urgent priority |

## Dynamic labels (per project)

For each `.csproj` found in the repository, a label is created named after the project (dots and spaces replaced with hyphens, lowercased). The colour indicates the project type:

| Pattern | Colour | Meaning |
| ------- | ------ | ------- |
| `*.Tests` / `*.Tests.*` | `0e8a16` | Test project |
| `*.Mocks` | `0e8a16` | Mock/test-helper project |
| Everything else | `96f7d2` | Production source project |
