namespace Credfeto.DotNet.Repo.Tools.Cmd;

internal static class ExecutableVersionInformation
{
    public static string ProgramName()
    {
        return typeof(Program).Namespace ?? "Credfeto.DotNet.Repo.Tools.Cmd";
    }

    public static string ProgramVersion()
    {
        return ThisAssembly.Info.FileVersion;
    }
}