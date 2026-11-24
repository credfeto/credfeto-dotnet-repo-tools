using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

[DebuggerDisplay("--{Name}")]
internal sealed record CommandLineOption(
    string Name,
    char? ShortName,
    string Description,
    bool IsRequired,
    bool IsRepeatable
)
{
    public static CommandLineOption Required(string name, char shortName, string description)
    {
        return new(Name: name, ShortName: shortName, Description: description, IsRequired: true, IsRepeatable: false);
    }

    public static CommandLineOption Optional(string name, char? shortName, string description)
    {
        return new(Name: name, ShortName: shortName, Description: description, IsRequired: false, IsRepeatable: false);
    }

    public static CommandLineOption Repeatable(string name, char shortName, string description)
    {
        return new(Name: name, ShortName: shortName, Description: description, IsRequired: false, IsRepeatable: true);
    }

    public string LongForm => "--" + this.Name;

    public string? ShortForm => this.ShortName is { } shortName ? "-" + shortName : null;
}
