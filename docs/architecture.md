# Architecture

## Solution Layout

The solution is split by concern; each area typically has a `.Interfaces` project (public contracts) and a matching implementation project, plus a `.Tests` project.

```text
src/
├── Credfeto.DotNet.Repo.Tools.Cmd/            # updaterepo CLI entry point (Cocona commands)
├── Credfeto.DotNet.Repo.Formatter/             # cscleanup standalone CLI entry point
├── Credfeto.DotNet.Repo.Tools.Models/          # Shared data models (e.g. packages.json shape)
├── Credfeto.DotNet.Repo.Tools.Build/           # dotnet build orchestration
│   └── Credfeto.DotNet.Repo.Tools.Build.Interfaces/
├── Credfeto.DotNet.Repo.Tools.CleanUp/         # Code cleanup pipeline (used by code-cleanup and cscleanup)
│   └── Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces/
├── Credfeto.DotNet.Repo.Tools.Dependencies/    # Dependency reduction (reduce-dependencies, check-dependencies)
│   └── Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces/
├── Credfeto.DotNet.Repo.Tools.DotNet/          # dotnet SDK/version detection and command execution
│   └── Credfeto.DotNet.Repo.Tools.DotNet.Interfaces/
├── Credfeto.DotNet.Repo.Tools.Extensions/      # Shared extension methods and process running helpers
├── Credfeto.DotNet.Repo.Tools.Git/             # Git repository cloning, commits, and pushes
│   └── Credfeto.DotNet.Repo.Tools.Git.Interfaces/
├── Credfeto.DotNet.Repo.Tools.Packages/        # Package update logic (update-packages)
│   └── Credfeto.DotNet.Repo.Tools.Packages.Interfaces/
├── Credfeto.DotNet.Repo.Tools.Release/         # Release configuration and release-note generation
│   └── Credfeto.DotNet.Repo.Tools.Release.Interfaces/
├── Credfeto.DotNet.Repo.Tools.TemplateUpdate/  # Template sync logic (update-template), including labels/labeler generation
│   └── Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces/
└── Credfeto.DotNet.Repo.Tracking/              # tracking.json / cache.json persistence
    └── Credfeto.DotNet.Repo.Tracking.Interfaces/
```

Every project above has a corresponding `*.Tests` (and, where relevant, `*.Integration.Tests`) project alongside it.

## Command Flow

`Credfeto.DotNet.Repo.Tools.Cmd` (`updaterepo`) is the composition root: it wires up the interface projects via dependency injection and exposes each area's bulk operation as a CLI command (see [CLI Command Reference](commands.md)). `Credfeto.DotNet.Repo.Formatter` (`cscleanup`) is a separate, smaller composition root that reuses the `CleanUp` and `Build` projects directly against a single working tree, without the git/tracking/release machinery.
