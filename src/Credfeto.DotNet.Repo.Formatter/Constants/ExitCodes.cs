namespace Credfeto.DotNet.Repo.Formatter.Constants;

public static class ExitCodes
{
    public const int Success = 0;
    public const int Error = 1;

    // Cocona exited with 129 after showing help for -h/--help; kept so callers see the same exit code.
    public const int HelpShown = 129;
}
