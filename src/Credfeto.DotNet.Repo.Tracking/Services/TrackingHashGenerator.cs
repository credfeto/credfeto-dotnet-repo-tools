using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tracking.Interfaces;

namespace Credfeto.DotNet.Repo.Tracking.Services;

public sealed class TrackingHashGenerator : ITrackingHashGenerator
{
    private static readonly IReadOnlyList<string> FileMasks =
    [
        "*.sln",
        "*.slnx",
        "*.csproj",
        "global.json",
        "*.props",
        "*.ruleset",
    ];

    private static readonly HashSet<string> ExcludedDirectoryNames = new(
        ["obj", "bin", ".git"],
        StringComparer.OrdinalIgnoreCase
    );

    // Matches the case sensitivity of the previous Directory.EnumerateFiles(path, searchPattern)
    // matching, which uses MatchCasing.PlatformDefault: case-insensitive on Windows/macOS,
    // case-sensitive on Linux.
    private static readonly bool IgnoreCase = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public async ValueTask<string> GenerateTrackingHashAsync(
        RepoContext repoContext,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> sourceFiles = GetFileList(repoContext.WorkingDirectory);

        using (HashAlgorithm hashAlgorithm = SHA256.Create())
        {
            await HashFileListAsync(
                hashAlgorithm: hashAlgorithm,
                sourceFiles: sourceFiles,
                cancellationToken: cancellationToken
            );

            return hashAlgorithm.Hash is null
                ? repoContext.Repository.HeadRev
                : Convert.ToBase64String(hashAlgorithm.Hash);
        }
    }

    private static async ValueTask HashFileListAsync(
        HashAlgorithm hashAlgorithm,
        IReadOnlyList<string> sourceFiles,
        CancellationToken cancellationToken
    )
    {
        await using (CryptoStream cryptoStream = new(Stream.Null, hashAlgorithm, CryptoStreamMode.Write))
        {
            foreach (string fileName in sourceFiles)
            {
                await HashOneFileAsync(
                    cryptoStream: cryptoStream,
                    fileName: fileName,
                    cancellationToken: cancellationToken
                );
            }

            await cryptoStream.FlushFinalBlockAsync(cancellationToken);
        }
    }

    private static async ValueTask HashOneFileAsync(
        CryptoStream cryptoStream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        await using (FileStream fileStream = File.OpenRead(fileName))
        {
            await fileStream.CopyToAsync(cryptoStream, cancellationToken);
        }
    }

    private static IReadOnlyList<string> GetFileList(string sourceFolder)
    {
        return [.. EnumerateSourceFiles(sourceFolder).Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static IEnumerable<string> EnumerateSourceFiles(string folder)
    {
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            string fileName = Path.GetFileName(file);

            if (FileMasks.Any(mask => FileSystemName.MatchesSimpleExpression(mask, fileName, ignoreCase: IgnoreCase)))
            {
                yield return file;
            }
        }

        foreach (string subDirectory in Directory.EnumerateDirectories(folder))
        {
            if (ExcludedDirectoryNames.Contains(Path.GetFileName(subDirectory)))
            {
                continue;
            }

            foreach (string file in EnumerateSourceFiles(subDirectory))
            {
                yield return file;
            }
        }
    }
}
