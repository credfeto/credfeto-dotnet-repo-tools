# cscleanup: Standalone C# Formatter

`cscleanup` is a standalone dotnet tool that formats C# source files (`.cs`) and project files (`.csproj`) without requiring a git repository or making any commits. It is suitable for use as a pre-commit hook or in CI pipelines.

## Usage

```text
cscleanup [--remove-suppressions] [--build-root <path>] <inputs...>
```

| Argument / Option | Required | Description |
| --- | --- | --- |
| `inputs` | Yes | One or more files, glob patterns, or folders to process |
| `--remove-suppressions` | No | Remove redundant `[SuppressMessage]` attributes (requires `--build-root`) |
| `--build-root <path>` | Conditional | Root directory used for `dotnet build` when `--remove-suppressions` is enabled |

## Input resolution

* **File path**: processed directly; must be a `.cs` or `.csproj` file
* **Glob pattern**: expanded relative to the current working directory (e.g. `"src/**/*.cs"`)
* **Directory**: scanned recursively for all `.cs` and `.csproj` files, excluding generated files (paths containing `/obj/`, `/generated/`, or `.generated.` in the filename)

Passing any other file type is an error.

## Examples

Format a single file:

```bash
cscleanup src/MyProject/Foo.cs
```

Format all C# files in a folder:

```bash
cscleanup src/MyProject/
```

Format files matching a glob:

```bash
cscleanup "src/**/*.cs"
```

Format multiple inputs at once:

```bash
cscleanup src/MyProject/ src/MyProject.Tests/
```

Remove suppression attributes (requires a successful build to verify each removal is safe):

```bash
cscleanup --remove-suppressions --build-root /path/to/solution src/MyProject/
```

## What it does

For each `.cs` file:

1. Converts Resharper suppression comments to `[SuppressMessage]` attributes
2. Removes XML doc comments
3. Reformats the file using CSharpier
4. Optionally removes redundant `[SuppressMessage]` attributes (only when `--remove-suppressions` and `--build-root` are set)

For each `.csproj` file:

1. Reorders `<PropertyGroup>` elements into a canonical order
2. Reorders `<ItemGroup>` includes into a canonical order

## Use as a pre-commit hook

Add to `.git/hooks/pre-commit` (or via a hook manager such as [pre-commit][pre-commit]):

```bash
#!/bin/sh
git diff --cached --name-only --diff-filter=ACM | grep -E '\.(cs|csproj)$' | xargs cscleanup
```

[pre-commit]: https://pre-commit.com/
