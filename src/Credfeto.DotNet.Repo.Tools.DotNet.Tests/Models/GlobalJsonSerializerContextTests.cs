using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Credfeto.DotNet.Repo.Tools.DotNet.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Tests.Models;

public sealed class GlobalJsonSerializerContextTests : TestBase
{
    [Fact]
    public void ShouldProvideTypeInfoForGlobalJsonPacket()
    {
        Assert.NotNull(GlobalJsonJsonSerializerContext.Default.GlobalJsonPacket);
    }

    [Fact]
    public void ShouldProvideTypeInfoForGlobalJsonSdk()
    {
        Assert.NotNull(GlobalJsonJsonSerializerContext.Default.GlobalJsonSdk);
    }

    [Fact]
    public void ShouldSerializeGlobalJsonPacketWithSdk()
    {
        GlobalJsonPacket packet = new(
            sdk: new GlobalJsonSdk(version: "9.0.100", rollForward: "latestPatch", allowPrerelease: false)
        );

        string json = JsonSerializer.Serialize(
            value: packet,
            jsonTypeInfo: GlobalJsonJsonSerializerContext.Default.GlobalJsonPacket
        );

        Assert.NotEmpty(json);
        Assert.Contains("9.0.100", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldSerializeGlobalJsonPacketWithNullSdk()
    {
        GlobalJsonPacket packet = new(sdk: null);

        string json = JsonSerializer.Serialize(
            value: packet,
            jsonTypeInfo: GlobalJsonJsonSerializerContext.Default.GlobalJsonPacket
        );

        Assert.NotEmpty(json);
    }

    [Fact]
    public void ShouldSerializeGlobalJsonSdkWithAllFields()
    {
        GlobalJsonSdk sdk = new(version: "9.0.100", rollForward: "latestPatch", allowPrerelease: true);

        string json = JsonSerializer.Serialize(
            value: sdk,
            jsonTypeInfo: GlobalJsonJsonSerializerContext.Default.GlobalJsonSdk
        );

        Assert.NotEmpty(json);
        Assert.Contains("9.0.100", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldResolveTypeInfoByType()
    {
        JsonTypeInfo? typeInfo = GlobalJsonJsonSerializerContext.Default.GetTypeInfo(typeof(GlobalJsonPacket));

        Assert.NotNull(typeInfo);
    }

    [Fact]
    public void ShouldReturnNullForTypeNotInContext()
    {
        JsonTypeInfo? typeInfo = GlobalJsonJsonSerializerContext.Default.GetTypeInfo(typeof(int));

        Assert.Null(typeInfo);
    }

    [Fact]
    public void ShouldProvideTypeInfoForString()
    {
        Assert.NotNull(GlobalJsonJsonSerializerContext.Default.String);
    }

    [Fact]
    public void ShouldProvideTypeInfoForBoolean()
    {
        Assert.NotNull(GlobalJsonJsonSerializerContext.Default.Boolean);
    }

    [Fact]
    public void ShouldProvideTypeInfoForNullableBoolean()
    {
        Assert.NotNull(GlobalJsonJsonSerializerContext.Default.NullableBoolean);
    }
}
