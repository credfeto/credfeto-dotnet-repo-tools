# CLI Command Reference

All commands are invoked through the `updaterepo` tool (`Credfeto.DotNet.Repo.Tools.Cmd`). Every command except `check-dependencies` operates on a list of git repositories loaded from a `repos.lst` file (see [Configuration Reference](configuration.md)).

## update-packages

Update all packages specified in a `packages.json` file across every repository in `repos.lst`.

```bash
dotnet updaterepo \
    update-packages \
    --repositories ~/work/personal/auto-update-config/personal/repos.lst \
    --work ~/temp \
    --tracking ~/temp/tracking.json \
    --cache ~/temp/cache.json \
    --packages ~/work/personal/auto-update-config/packages.json \
    --template git@github.com:credfeto/cs-template.git \
    --release ~/work/personal/auto-update-config/release.json
```

| Option | Short | Required | Description |
| --- | --- | --- | --- |
| `--repositories` | `-r` | Yes | `repos.lst` file containing the list of repositories |
| `--template` | `-m` | Yes | Template repository to clone; excluded from `repos.lst` if present there |
| `--work` | `-w` | Yes | Folder where repositories are cloned |
| `--tracking` | `-t` | Yes | Folder where to write the `tracking.json` file |
| `--packages` | `-p` | Yes | `packages.json` file to load |
| `--release` | `-l` | Yes | `release.json` config file to load |
| `--cache` | `-c` | No | Package cache file |
| `--source` | `-s` | No | Additional NuGet feed URLs to load (repeatable) |

## update-template

Update common files in each repository to match the files defined in the template repository specified by `--template`.

```bash
dotnet updaterepo \
    update-template \
    --repositories ~/work/personal/auto-update-config/personal/repos.lst \
    --work ~/temp \
    --tracking ~/temp/tracking.json \
    --packages ~/work/personal/auto-update-config/packages.json \
    --template git@github.com:credfeto/cs-template.git \
    --template-config ~/work/personal/auto-update-config/template.json \
    --release ~/work/personal/auto-update-config/release.json
```

| Option | Short | Required | Description |
| --- | --- | --- | --- |
| `--repositories` | `-r` | Yes | `repos.lst` file containing the list of repositories |
| `--template` | `-m` | Yes | Template repository to clone; excluded from `repos.lst` if present there |
| `--template-config` | `-c` | Yes | `template.json` file to load (see [Configuration Reference](configuration.md)) |
| `--work` | `-w` | Yes | Folder where repositories are cloned |
| `--tracking` | `-t` | Yes | Folder where to write the `tracking.json` file |
| `--packages` | `-p` | Yes | `packages.json` file to load |
| `--release` | `-l` | Yes | `release.json` config file to load |

## code-cleanup

Perform code cleanup in all repositories.

```bash
dotnet updaterepo \
    code-cleanup \
    --repositories ~/work/personal/auto-update-config/personal/repos.lst \
    --work ~/temp \
    --tracking ~/temp/tracking.json \
    --packages ~/work/personal/auto-update-config/packages.json \
    --template git@github.com:credfeto/cs-template.git \
    --release ~/work/personal/auto-update-config/release.json
```

| Option | Short | Required | Description |
| --- | --- | --- | --- |
| `--repositories` | `-r` | Yes | `repos.lst` file containing the list of repositories |
| `--template` | `-m` | Yes | Template repository to clone; excluded from `repos.lst` if present there |
| `--work` | `-w` | Yes | Folder where repositories are cloned |
| `--tracking` | `-t` | Yes | Folder where to write the `tracking.json` file |
| `--packages` | `-p` | Yes | Accepted but currently has no effect ([#319](https://github.com/credfeto/credfeto-dotnet-repo-tools/issues/319)) |
| `--release` | `-l` | Yes | Accepted but currently has no effect ([#320](https://github.com/credfeto/credfeto-dotnet-repo-tools/issues/320)) |

## reduce-dependencies

Reduce dependencies in all repositories by removing project/package references that are not actually required.

```bash
dotnet updaterepo \
    reduce-dependencies \
    --repositories ~/work/personal/auto-update-config/personal/repos.lst \
    --work ~/temp \
    --tracking ~/temp/tracking.json \
    --template git@github.com:credfeto/cs-template.git
```

| Option | Short | Required | Description |
| --- | --- | --- | --- |
| `--repositories` | `-r` | Yes | `repos.lst` file containing the list of repositories |
| `--template` | `-m` | Yes | Template repository to clone; excluded from `repos.lst` if present there |
| `--work` | `-w` | Yes | Folder where repositories are cloned |
| `--tracking` | `-t` | Yes | Folder where to write the `tracking.json` file |

## check-dependencies

Check for unnecessary dependencies in a single already-checked-out folder, without cloning or committing anything.

```bash
dotnet updaterepo \
    check-dependencies \
    --source-folder ~/work/personal/my-repo
```

| Option | Short | Required | Description |
| --- | --- | --- | --- |
| `--source-folder` | `-s` | Yes | Folder where the dotnet source is |
