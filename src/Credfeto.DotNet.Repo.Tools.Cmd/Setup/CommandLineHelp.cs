using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

// Renders help in Cocona's layout, so the text callers and people see is unchanged.
internal static class CommandLineHelp
{
    private const string COLUMN_GAP = "    ";
    private const string INDENT = "  ";
    private const string VALUE = " <String>";

    private static readonly (string Name, string Description) HelpOption = ("-h, --help", "Show help message");
    private static readonly (string Name, string Description) VersionOption = ("--version", "Show version");

    public static string RenderCommandList(string application, IReadOnlyList<CommandLineCommand> commands)
    {
        StringBuilder help = new StringBuilder()
            .Append("Usage: ")
            .Append(application)
            .AppendLine(" [command]")
            .AppendLine()
            .AppendLine(application)
            .AppendLine()
            .AppendLine("Commands:");
        AppendTable(help: help, rows: [.. commands.Select(command => (command.Name, command.Description))]);

        help.AppendLine().AppendLine("Options:");
        AppendTable(help: help, rows: [HelpOption, VersionOption]);

        return help.ToString().TrimEnd();
    }

    public static string RenderCommand(string application, CommandLineCommand command)
    {
        StringBuilder help = new StringBuilder().Append("Usage: ").Append(application).Append(' ').Append(command.Name);

        foreach (CommandLineOption option in command.Options)
        {
            help.Append(" [").Append(option.LongForm).Append(ValueText(option)).Append(']');
        }

        help.Append(" [--help]");

        help.AppendLine().AppendLine().AppendLine(command.Description).AppendLine();

        help.AppendLine("Options:");
        AppendTable(help: help, rows: [.. OptionRows(command)]);

        return help.ToString().TrimEnd();
    }

    private static IEnumerable<(string Name, string Description)> OptionRows(CommandLineCommand command)
    {
        foreach (CommandLineOption option in command.Options)
        {
            string name = option.ShortForm is { } shortForm ? shortForm + ", " + option.LongForm : option.LongForm;

            yield return (
                name + ValueText(option),
                option.IsRequired ? option.Description + " (Required)" : option.Description
            );
        }

        yield return HelpOption;
    }

    private static string ValueText(CommandLineOption option)
    {
        return option.IsRepeatable ? VALUE + "..." : VALUE;
    }

    private static void AppendTable(StringBuilder help, IReadOnlyList<(string Name, string Description)> rows)
    {
        int width = rows.Max(row => row.Name.Length);

        foreach ((string name, string description) in rows)
        {
            help.Append(INDENT).Append(name.PadRight(width)).Append(COLUMN_GAP).AppendLine(description);
        }
    }
}
