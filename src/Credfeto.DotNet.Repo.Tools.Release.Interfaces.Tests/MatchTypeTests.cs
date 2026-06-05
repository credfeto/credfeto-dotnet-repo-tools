using System.Diagnostics;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces.Tests;

public sealed class MatchTypeTests : TestBase
{
    [Fact]
    public void MustBeAnEnum()
    {
        Assert.True(typeof(MatchType).IsEnum, userMessage: $"{nameof(MatchType)} must be an enum");
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(MatchType).IsPublic, userMessage: $"{nameof(MatchType)} must be public");
    }

    [Fact]
    public void MustDefineExactValue()
    {
        Assert.True(
            System.Enum.IsDefined(MatchType.EXACT),
            userMessage: $"{nameof(MatchType)}.{nameof(MatchType.EXACT)} must be defined"
        );
    }

    [Fact]
    public void MustDefineContainsValue()
    {
        Assert.True(
            System.Enum.IsDefined(MatchType.CONTAINS),
            userMessage: $"{nameof(MatchType)}.{nameof(MatchType.CONTAINS)} must be defined"
        );
    }

    [Fact]
    public void ExactAndContainsMustHaveDifferentValues()
    {
        Assert.NotEqual(MatchType.EXACT, MatchType.CONTAINS);
    }

    [Fact]
    public void GetName_Exact_ReturnsExact()
    {
        Assert.Equal(expected: "EXACT", actual: MatchType.EXACT.GetName());
    }

    [Fact]
    public void GetName_Contains_ReturnsContains()
    {
        Assert.Equal(expected: "CONTAINS", actual: MatchType.CONTAINS.GetName());
    }

    [Fact]
    public void GetName_InvalidValue_ThrowsUnreachableException()
    {
        const MatchType invalid = (MatchType)999;

        Assert.Throws<UnreachableException>(() => invalid.GetName());
    }

    [Fact]
    public void GetDescription_Exact_ReturnsExact()
    {
        Assert.Equal(expected: "EXACT", actual: MatchType.EXACT.GetDescription());
    }

    [Fact]
    public void GetDescription_Contains_ReturnsContains()
    {
        Assert.Equal(expected: "CONTAINS", actual: MatchType.CONTAINS.GetDescription());
    }

    [Fact]
    public void IsDefinedExtension_Exact_ReturnsTrue()
    {
        Assert.True(MatchType.EXACT.IsDefined(), userMessage: $"{nameof(MatchType.EXACT)} must be defined");
    }

    [Fact]
    public void IsDefinedExtension_Contains_ReturnsTrue()
    {
        Assert.True(MatchType.CONTAINS.IsDefined(), userMessage: $"{nameof(MatchType.CONTAINS)} must be defined");
    }

    [Fact]
    public void IsDefinedExtension_InvalidValue_ReturnsFalse()
    {
        const MatchType invalid = (MatchType)999;

        Assert.False(invalid.IsDefined(), userMessage: "Invalid value must not be defined");
    }
}
