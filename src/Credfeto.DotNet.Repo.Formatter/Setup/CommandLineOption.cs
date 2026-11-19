using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Formatter.Setup;

[DebuggerDisplay("--{Name}")]
internal sealed record CommandLineOption(
    string Name,
    char? ShortName,
    string Description,
    bool IsRequired,
    bool IsFlag,
    bool IsRepeatable
)
{
    public static CommandLineOption Required(string name, char shortName, string description)
    {
        return new(
            Name: name,
            ShortName: shortName,
            Description: description,
            IsRequired: true,
            IsFlag: false,
            IsRepeatable: false
        );
    }

    public static CommandLineOption Optional(string name, char? shortName, string description)
    {
        return new(
            Name: name,
            ShortName: shortName,
            Description: description,
            IsRequired: false,
            IsFlag: false,
            IsRepeatable: false
        );
    }

    public static CommandLineOption Repeatable(string name, char shortName, string description)
    {
        return new(
            Name: name,
            ShortName: shortName,
            Description: description,
            IsRequired: false,
            IsFlag: false,
            IsRepeatable: true
        );
    }

    public static CommandLineOption Flag(string name, string description)
    {
        return new(
            Name: name,
            ShortName: null,
            Description: description,
            IsRequired: false,
            IsFlag: true,
            IsRepeatable: false
        );
    }

    public string LongForm => "--" + this.Name;

    public string? ShortForm => this.ShortName is { } shortName ? "-" + shortName : null;
}
