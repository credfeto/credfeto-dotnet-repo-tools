namespace Credfeto.DotNet.Repo.Formatter.Setup;

// The help text of the root command. The option names must match the parameter names of CleanupCommand.CleanupAsync,
// which is what ConsoleAppFramework binds (camelCase becomes --kebab-case).
internal static class CommandLineSpecification
{
    public static CommandLineCommand Cleanup { get; } =
        new(
            Name: string.Empty,
            Description: "Format C# source files and project files",
            Options:
            [
                CommandLineOption.Flag(
                    name: "remove-suppressions",
                    description: "Remove redundant [SuppressMessage] attributes (requires --build-root)"
                ),
                CommandLineOption.Optional(
                    name: "build-root",
                    shortName: null,
                    description: "Root directory used for dotnet build when --remove-suppressions is enabled"
                ),
                CommandLineOption.Flag(
                    name: "self-test",
                    description: "Check the tool can start, then exit without processing any files"
                ),
            ],
            Argument: new(Name: "inputs", Description: "Files, globs, or folders to process")
        );
}
