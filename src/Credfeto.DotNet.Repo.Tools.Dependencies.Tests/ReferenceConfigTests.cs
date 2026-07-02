using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests;

public sealed class ReferenceConfigTests : TestBase
{
    private static readonly ValueTask NoOpRemoval = ValueTask.CompletedTask;

    private static ReferenceConfig BuildConfig()
    {
        return new ReferenceConfig(onSuccessfulRemoval: static (_, _, _) => NoOpRemoval);
    }

    [Theory]
    [InlineData("FunFair.Test.Common")]
    [InlineData("funfair.test.common")]
    [InlineData("FUNFAIR.TEST.COMMON")]
    [InlineData("Microsoft.NET.Test.Sdk")]
    [InlineData("NSubstitute")]
    [InlineData("TeamCity.VSTest.TestAdapter")]
    [InlineData("xunit")]
    [InlineData("xunit.v3")]
    [InlineData("xunit.v3.extensibility.core")]
    [InlineData("xunit.runner.visualstudio")]
    [InlineData("xunit.runner.visualstudio.v3")]
    [InlineData("Secp256k1.Native")]
    [InlineData("Castle.Core")]
    [InlineData("AutoBogus.NSubstitute")]
    [InlineData("Bogus")]
    [InlineData("CountryData.Bogus")]
    [InlineData("BenchmarkDotNet")]
    [InlineData("BenchmarkDotNet.Diagnostics.dotTrace")]
    public void PackageInDoNotRemoveListShouldReturnTrue(string packageId)
    {
        ReferenceConfig config = BuildConfig();
        bool result = config.IsDoNotRemovePackage(packageId: packageId, allPackageIds: []);
        Assert.True(condition: result, userMessage: "Package in do-not-remove list should return true");
    }

    [Fact]
    public void RegularPackageNotInListShouldReturnFalse()
    {
        ReferenceConfig config = BuildConfig();
        bool result = config.IsDoNotRemovePackage(packageId: "SomeOtherPackage", allPackageIds: []);
        Assert.False(condition: result, userMessage: "Regular package should not be in do-not-remove list");
    }

    [Fact]
    public void SystemIdentityModelTokensJwtWithJwtBearerShouldReturnTrue()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["Microsoft.AspNetCore.Authentication.JwtBearer"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "System.IdentityModel.Tokens.Jwt",
            allPackageIds: allPackageIds
        );
        Assert.True(
            condition: result,
            userMessage: "System.IdentityModel.Tokens.Jwt should not be removed when JwtBearer is present"
        );
    }

    [Fact]
    public void SystemIdentityModelTokensJwtWithIdentityModelTokensShouldReturnTrue()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["Microsoft.IdentityModel.Tokens"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "System.IdentityModel.Tokens.Jwt",
            allPackageIds: allPackageIds
        );
        Assert.True(
            condition: result,
            userMessage: "System.IdentityModel.Tokens.Jwt should not be removed when IdentityModel.Tokens is present"
        );
    }

    [Fact]
    public void SystemIdentityModelTokensJwtWithoutRelatedPackagesShouldReturnFalse()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["SomeOtherPackage"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "System.IdentityModel.Tokens.Jwt",
            allPackageIds: allPackageIds
        );
        Assert.False(
            condition: result,
            userMessage: "System.IdentityModel.Tokens.Jwt without related packages should be removable"
        );
    }

    [Fact]
    public void JwtBearerWithIdentityModelTokensShouldReturnTrue()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["Microsoft.IdentityModel.Tokens"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "Microsoft.AspNetCore.Authentication.JwtBearer",
            allPackageIds: allPackageIds
        );
        Assert.True(
            condition: result,
            userMessage: "JwtBearer should not be removed when IdentityModel.Tokens is present"
        );
    }

    [Fact]
    public void JwtBearerWithSystemIdentityModelTokensJwtShouldReturnTrue()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["System.IdentityModel.Tokens.Jwt"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "Microsoft.AspNetCore.Authentication.JwtBearer",
            allPackageIds: allPackageIds
        );
        Assert.True(
            condition: result,
            userMessage: "JwtBearer should not be removed when System.IdentityModel.Tokens.Jwt is present"
        );
    }

    [Fact]
    public void JwtBearerAloneWithoutRelatedPackagesShouldReturnFalse()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["Microsoft.AspNetCore.Authentication.JwtBearer"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "Microsoft.AspNetCore.Authentication.JwtBearer",
            allPackageIds: allPackageIds
        );
        Assert.False(
            condition: result,
            userMessage: "JwtBearer alone (without related identity packages) should be removable"
        );
    }

    [Fact]
    public void JwtBearerWithoutRelatedPackagesShouldReturnFalse()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["SomeOtherPackage"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "Microsoft.AspNetCore.Authentication.JwtBearer",
            allPackageIds: allPackageIds
        );
        Assert.False(condition: result, userMessage: "JwtBearer without related packages should be removable");
    }

    [Fact]
    public void IdentityModelTokensWithJwtShouldReturnTrue()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["System.IdentityModel.Tokens.Jwt"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "Microsoft.IdentityModel.Tokens",
            allPackageIds: allPackageIds
        );
        Assert.True(condition: result, userMessage: "IdentityModel.Tokens should not be removed when Jwt is present");
    }

    [Fact]
    public void IdentityModelTokensWithJwtBearerShouldReturnTrue()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["Microsoft.AspNetCore.Authentication.JwtBearer"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "Microsoft.IdentityModel.Tokens",
            allPackageIds: allPackageIds
        );
        Assert.True(
            condition: result,
            userMessage: "IdentityModel.Tokens should not be removed when JwtBearer is present"
        );
    }

    [Fact]
    public void IdentityModelTokensWithoutRelatedPackagesShouldReturnFalse()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["SomeOtherPackage"];
        bool result = config.IsDoNotRemovePackage(
            packageId: "Microsoft.IdentityModel.Tokens",
            allPackageIds: allPackageIds
        );
        Assert.False(
            condition: result,
            userMessage: "IdentityModel.Tokens without related packages should be removable"
        );
    }

    [Theory]
    [InlineData("LibSassHost.Native.linux-x64")]
    [InlineData("LIBSASSHOST.NATIVE.linux-x64")]
    [InlineData("LibSassHost.Native.win-x64")]
    public void LibSassHostNativePackageShouldReturnTrue(string packageId)
    {
        ReferenceConfig config = BuildConfig();
        bool result = config.IsDoNotRemovePackage(packageId: packageId, allPackageIds: []);
        Assert.True(condition: result, userMessage: "LibSassHost.Native.* packages should not be removed");
    }

    [Fact]
    public void SerilogWithSerilogSinksShouldReturnTrue()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["Serilog.Sinks.Console"];
        bool result = config.IsDoNotRemovePackage(packageId: "Serilog", allPackageIds: allPackageIds);
        Assert.True(condition: result, userMessage: "Serilog should not be removed when Serilog sinks are present");
    }

    [Fact]
    public void SerilogWithoutSerilogSinksShouldReturnFalse()
    {
        ReferenceConfig config = BuildConfig();
        IReadOnlyList<string> allPackageIds = ["SomeOtherPackage"];
        bool result = config.IsDoNotRemovePackage(packageId: "Serilog", allPackageIds: allPackageIds);
        Assert.False(condition: result, userMessage: "Serilog without sinks should be removable");
    }

    [Fact]
    public void BenchmarkDotNetAutoGeneratedProjectShouldBeIgnored()
    {
        ReferenceConfig config = BuildConfig();
        bool result = config.IsIgnoreProject("/repo/src/Benchmarks/BenchmarkDotNet.AutoGenerated.csproj");
        Assert.True(condition: result, userMessage: "BenchmarkDotNet.AutoGenerated.csproj should be ignored");
    }

    [Fact]
    public void BenchmarkDotNetAutoGeneratedProjectCaseInsensitiveShouldBeIgnored()
    {
        ReferenceConfig config = BuildConfig();
        bool result = config.IsIgnoreProject("/repo/src/Benchmarks/benchmarkdotnet.autogenerated.csproj");
        Assert.True(
            condition: result,
            userMessage: "BenchmarkDotNet.AutoGenerated.csproj (case insensitive) should be ignored"
        );
    }

    [Fact]
    public void ProjectEndingWithAllCsprojShouldBeIgnored()
    {
        ReferenceConfig config = BuildConfig();
        bool result = config.IsIgnoreProject("/path/to/MyMeta.All.csproj");
        Assert.True(condition: result, userMessage: "Meta project ending in .All.csproj should be ignored");
    }

    [Fact]
    public void ProjectEndingWithAllCsprojCaseInsensitiveShouldBeIgnored()
    {
        ReferenceConfig config = BuildConfig();
        bool result = config.IsIgnoreProject("/path/to/MyMeta.ALL.CSPROJ");
        Assert.True(
            condition: result,
            userMessage: "Meta project ending in .ALL.CSPROJ (case insensitive) should be ignored"
        );
    }

    [Fact]
    public void RegularProjectShouldNotBeIgnored()
    {
        ReferenceConfig config = BuildConfig();
        bool result = config.IsIgnoreProject("/path/to/MyProject.csproj");
        Assert.False(condition: result, userMessage: "Regular project should not be ignored");
    }

    [Fact]
    public void OnSuccessfulRemovalPropertyShouldBeSet()
    {
        static ValueTask OnRemovalAsync(string projectFileName, string message, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        ReferenceConfig config = new(onSuccessfulRemoval: OnRemovalAsync);
        Assert.Equal(expected: OnRemovalAsync, actual: config.OnSuccessfulRemoval);
    }
}
