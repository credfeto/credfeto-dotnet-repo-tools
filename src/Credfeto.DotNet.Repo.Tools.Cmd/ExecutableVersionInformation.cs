namespace Credfeto.DotNet.Repo.Tools.Cmd;

internal static class ExecutableVersionInformation
{
    public static string ProgramVersion()
    {
        return ThisAssembly.Info.FileVersion;
    }
}