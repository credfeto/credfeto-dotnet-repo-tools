using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.DotNet.Repo.Tools.Cmd.Constants;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

// The help text and short aliases of every command. The long option names must match the parameter names of the
// methods on Commands, which is what ConsoleAppFramework binds (camelCase becomes --kebab-case).
internal static class CommandLineSpecification
{
    private static readonly CommandLineOption Repositories = CommandLineOption.Required(
        name: "repositories",
        shortName: 'r',
        description: "repos.lst file containing list of repositories"
    );

    private static readonly CommandLineOption Template = CommandLineOption.Required(
        name: "template",
        shortName: 'm',
        description: "Template repository to clone"
    );

    private static readonly CommandLineOption Tracking = CommandLineOption.Required(
        name: "tracking",
        shortName: 't',
        description: "folder where to write tracking.json file"
    );

    private static readonly CommandLineOption Packages = CommandLineOption.Required(
        name: "packages",
        shortName: 'p',
        description: "Packages.json file to load"
    );

    private static readonly CommandLineOption Work = CommandLineOption.Required(
        name: "work",
        shortName: 'w',
        description: "folder where to clone repositories"
    );

    private static readonly CommandLineOption Release = CommandLineOption.Required(
        name: "release",
        shortName: 'l',
        description: "release.config file to load"
    );

    public static IReadOnlyList<CommandLineCommand> Commands { get; } =
    [
        new(
            Name: CommandNames.UpdatePackages,
            Description: "Update all packages in all repositories",
            Options:
            [
                Repositories,
                Template,
                CommandLineOption.Optional(name: "cache", shortName: 'c', description: "package cache file"),
                Tracking,
                Packages,
                Work,
                Release,
                CommandLineOption.Repeatable(
                    name: "source",
                    shortName: 's',
                    description: "Urls to additional NuGet feeds to load"
                ),
            ]
        ),
        new(
            Name: CommandNames.UpdateTemplate,
            Description: "Update repos from template in all repositories",
            Options:
            [
                Repositories,
                Template,
                CommandLineOption.Required(
                    name: "template-config",
                    shortName: 'c',
                    description: "template.config file to load"
                ),
                Tracking,
                Packages,
                Work,
                Release,
            ]
        ),
        new(
            Name: CommandNames.CodeCleanup,
            Description: "Perform code cleanup in all repositories",
            Options: [Repositories, Template, Tracking, Work]
        ),
        new(
            Name: CommandNames.ReduceDependencies,
            Description: "Reduce dependencies in all repositories",
            Options: [Repositories, Template, Tracking, Work]
        ),
        new(
            Name: CommandNames.CheckDependencies,
            Description: "Reduce dependencies one folder",
            Options:
            [
                CommandLineOption.Required(
                    name: "source-folder",
                    shortName: 's',
                    description: "folder where the dotnet source is"
                ),
            ]
        ),
    ];

    public static CommandLineCommand? Find(string name)
    {
        return Commands.FirstOrDefault(command => StringComparer.Ordinal.Equals(x: command.Name, y: name));
    }
}
