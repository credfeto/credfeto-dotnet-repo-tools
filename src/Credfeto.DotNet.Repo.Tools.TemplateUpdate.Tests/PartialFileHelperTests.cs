using System;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests;

public sealed class PartialFileHelperTests : TestBase
{
    [Fact]
    public void BuildContent_WhenNoTargetExists_ProducesExpectedOutput()
    {
        const string globalContent = "Some global content";
        const string expected = "<!-- Globally Maintained -->\nSome global content\n<!-- Locally Maintained -->\n";

        string result = PartialFileHelper.BuildContent(globalContent: globalContent, existingTargetContent: null);

        Assert.Equal(expected: expected, actual: result);
    }

    [Fact]
    public void BuildContent_WhenNoTargetExists_GlobalContentComesBeforeLocalMarker()
    {
        const string globalContent = "Some global content";

        string result = PartialFileHelper.BuildContent(globalContent: globalContent, existingTargetContent: null);

        int globalMarkerPos = result.IndexOf(PartialFileHelper.DefaultGloballyMaintainedMarker, StringComparison.Ordinal);
        int globalContentPos = result.IndexOf(globalContent, StringComparison.Ordinal);
        int localMarkerPos = result.IndexOf(PartialFileHelper.DefaultLocallyMaintainedMarker, StringComparison.Ordinal);

        Assert.True(globalMarkerPos < globalContentPos, userMessage: "Global marker should precede global content");
        Assert.True(globalContentPos < localMarkerPos, userMessage: "Global content should precede local marker");
    }

    [Fact]
    public void BuildContent_WhenTargetHasLocalContent_PreservesLocalContent()
    {
        const string globalContent = "New global content";
        const string localContent = "My local notes";
        const string existingTarget = "<!-- Globally Maintained -->\n" +
                                     "Old global content\n" +
                                     "<!-- Locally Maintained -->\n" +
                                     localContent;

        string result = PartialFileHelper.BuildContent(globalContent: globalContent, existingTargetContent: existingTarget);

        Assert.Contains(localContent, result, StringComparison.Ordinal);
        Assert.Contains(globalContent, result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildContent_WhenTargetHasLocalContent_OldGlobalContentIsReplaced()
    {
        const string globalContent = "New global content";
        const string oldGlobalContent = "Old global content";
        const string existingTarget = "<!-- Globally Maintained -->\n" +
                                     "Old global content\n" +
                                     "<!-- Locally Maintained -->\n" +
                                     "My local notes";

        string result = PartialFileHelper.BuildContent(globalContent: globalContent, existingTargetContent: existingTarget);

        Assert.DoesNotContain(oldGlobalContent, result, StringComparison.Ordinal);
        Assert.Contains(globalContent, result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildContent_IsIdempotent_WhenGlobalContentUnchanged()
    {
        const string globalContent = "Some global content";
        string firstResult = PartialFileHelper.BuildContent(globalContent: globalContent, existingTargetContent: null);

        string secondResult = PartialFileHelper.BuildContent(globalContent: globalContent, existingTargetContent: firstResult);

        Assert.Equal(expected: firstResult, actual: secondResult);
    }

    [Fact]
    public void BuildContent_WhenTargetLacksLocalMarker_ProducesExpectedOutput()
    {
        const string globalContent = "Some global content";
        const string existingTarget = "Some unstructured content without markers";
        const string expected = "<!-- Globally Maintained -->\nSome global content\n<!-- Locally Maintained -->\n";

        string result = PartialFileHelper.BuildContent(globalContent: globalContent, existingTargetContent: existingTarget);

        Assert.Equal(expected: expected, actual: result);
    }

    [Fact]
    public void BuildContent_WithCustomMarkers_WhenNoTargetExists_ProducesExpectedOutput()
    {
        const string globalContent = "Some global content";
        const string beginMarker = "<!-- BEGIN GLOBAL -->";
        const string endMarker = "<!-- END GLOBAL -->";
        const string expected = "<!-- BEGIN GLOBAL -->\nSome global content\n<!-- END GLOBAL -->\n";

        string result = PartialFileHelper.BuildContent(globalContent: globalContent,
                                                       existingTargetContent: null,
                                                       globallyMaintainedMarker: beginMarker,
                                                       locallyMaintainedMarker: endMarker);

        Assert.Equal(expected: expected, actual: result);
    }

    [Fact]
    public void BuildContent_WithCustomMarkers_PreservesLocalContent()
    {
        const string globalContent = "New global content";
        const string localContent = "My local notes";
        const string beginMarker = "<!-- BEGIN GLOBAL -->";
        const string endMarker = "<!-- END GLOBAL -->";
        const string existingTarget = "<!-- BEGIN GLOBAL -->\n" +
                                     "Old global content\n" +
                                     "<!-- END GLOBAL -->\n" +
                                     localContent;

        string result = PartialFileHelper.BuildContent(globalContent: globalContent,
                                                       existingTargetContent: existingTarget,
                                                       globallyMaintainedMarker: beginMarker,
                                                       locallyMaintainedMarker: endMarker);

        Assert.Contains(localContent, result, StringComparison.Ordinal);
        Assert.Contains(globalContent, result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildContent_WithCustomMarkers_IsIdempotent()
    {
        const string globalContent = "Some global content";
        const string beginMarker = "<!-- BEGIN GLOBAL -->";
        const string endMarker = "<!-- END GLOBAL -->";
        string firstResult = PartialFileHelper.BuildContent(globalContent: globalContent,
                                                            existingTargetContent: null,
                                                            globallyMaintainedMarker: beginMarker,
                                                            locallyMaintainedMarker: endMarker);

        string secondResult = PartialFileHelper.BuildContent(globalContent: globalContent,
                                                             existingTargetContent: firstResult,
                                                             globallyMaintainedMarker: beginMarker,
                                                             locallyMaintainedMarker: endMarker);

        Assert.Equal(expected: firstResult, actual: secondResult);
    }

    [Fact]
    public void BuildContent_WhenSourceContainsMarkers_DoesNotDuplicateMarkers()
    {
        const string sourceContent = "<!-- Globally Maintained -->\n" +
                                     "# Local instructions\n" +
                                     "<!-- Locally Maintained -->\n";

        string result = PartialFileHelper.BuildContent(globalContent: sourceContent, existingTargetContent: null);

        Assert.Equal(expected: 1, actual: CountOccurrences(result: result, marker: PartialFileHelper.DefaultGloballyMaintainedMarker));
        Assert.Equal(expected: 1, actual: CountOccurrences(result: result, marker: PartialFileHelper.DefaultLocallyMaintainedMarker));
        Assert.Contains("# Local instructions", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildContent_WhenTargetHasRepeatedLocalMarkers_StripsDuplicateLocalMarkers()
    {
        const string globalContent = "New global content";
        const string existingTarget = "<!-- Globally Maintained -->\n" +
                                      "Old global content\n" +
                                      "<!-- Locally Maintained -->\n" +
                                      "<!-- Locally Maintained -->\n" +
                                      "<!-- Locally Maintained -->\n";

        string result = PartialFileHelper.BuildContent(globalContent: globalContent, existingTargetContent: existingTarget);

        Assert.Equal(expected: 1, actual: CountOccurrences(result: result, marker: PartialFileHelper.DefaultLocallyMaintainedMarker));
    }

    private static int CountOccurrences(string result, string marker)
    {
        int count = 0;
        int startIndex = 0;

        while (true)
        {
            int markerIndex = result.IndexOf(value: marker, startIndex: startIndex, comparisonType: StringComparison.Ordinal);

            if (markerIndex < 0)
            {
                return count;
            }

            ++count;
            startIndex = markerIndex + marker.Length;
        }
    }
}
