# GitHub Labels Reference

The `update-template` command generates two GitHub configuration files for each managed repository (when `gitHub.labels.generate` is enabled in `template.json`, see [Configuration Reference](configuration.md)):

* **`labels.yml`**: defines all labels (name, colour, description) used in the repository
* **`labeler.yml`**: maps file path patterns to labels so pull requests are labelled automatically

## Static labels

These labels are applied to every managed repository regardless of content. This table mirrors the generated source of truth in [`.github/labels.yml`](../.github/labels.yml); update that file (via `LabelsBuilder`) first, then keep this table in sync:

| Label | Colour | Description |
| ----- | ------ | ----------- |
| `.NET update` | `a870c9` | Update to .net global.json |
| `AI-Work` | `ffa500` | Work for an AI Agent |
| `auto-pr` | `0000aa` | Pull request created automatically |
| `Blocked` | `ff0000` | Blocked by a dependency or external factor |
| `Bug` | `d73a4a` | Generic bug fix |
| `C#` | `db6baa` | C# Source Files |
| `C# Project` | `db6baa` | C# Project Files |
| `C# Solution` | `db6baa` | C# Solutions |
| `Change Log` | `53fcd4` | Changelog tracking file |
| `Changelog Not Required` | `08f5f8` | No changelog entry required for this pull request |
| `Config Change` | `d8bb50` | Configuration files changes |
| `dependencies` | `0366d6` | Updates to dependencies |
| `DO NOT MERGE` | `ff0000` | This pull request should not be merged yet |
| `dotnet` | `db6baa` | Dotnet package updates |
| `Editorconfig` | `00dead` | Editor config file change |
| `Enhancement` | `a2eeef` | Enhancement to project |
| `github-actions` | `e09cf4` | Github actions workflow files |
| `High` | `ffa500` | High Priority |
| `Low` | `cc8899` | Low Priority |
| `Markdown` | `5319e7` | Markdown files |
| `Medium` | `ffff00` | Medium Priority |
| `Migration Script` | `b680e5` | SQL Migration scripts |
| `never-close` | `1d76db` | This issue should never be closed; it is a permanent tracking issue |
| `no-pr-activity` | `ffff00` | Pull Request has had no activity for a long time |
| `npm` | `e99695` | npm package update |
| `On Hold` | `ff0000` | Do not work on this |
| `Performance` | `0075ca` | Performance enhancement or issue |
| `Powershell` | `23bc12` | Powershell Source Files |
| `Read Me` | `5319e7` | Repository readme file |
| `Refactor` | `30027a` | Code refactoring |
| `Security` | `ee0701` | Security issue, e.g. use of insecure packages, or security fix |
| `Setup` | `5319e7` | Setup instructions |
| `Solidity` | `413cd1` | Solidity Source Files |
| `SQL` | `413cd1` | SQL Source Files |
| `Static Code Analysis Rules` | `00dead` | Ruleset for static code analysis files |
| `Tech Debt` | `30027a` | Technical debt |
| `Unit Tests` | `0e8a16` | Unit test and integration test projects |
| `Urgent` | `ff0000` | Urgent Priority |

## Dynamic labels (per project)

For each `.csproj` found in the repository, a label is created named after the project (dots and spaces replaced with hyphens, lowercased). The colour indicates the project type:

| Pattern | Colour | Meaning |
| ------- | ------ | ------- |
| `*.Tests` / `*.Tests.*` | `0e8a16` | Test project |
| `*.Mocks` | `0e8a16` | Mock/test-helper project |
| Everything else | `96f7d2` | Production source project |
