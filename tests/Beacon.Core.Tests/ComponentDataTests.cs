using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 5「时间线、表格、图表与趋势组件」：三个组件件的数据面解析与空态（三诊口径
/// 「数据源走既有绑定引擎/payload，不引入新 Provider；各配 1 组 Core 侧解析与空态单测」）。
/// </summary>
public sealed class ComponentDataTests
{
    // —— TrendSeries：解析 ——

    [Fact]
    public void Parse_CommaSeparated_InvariantDoubles()
    {
        var points = TrendSeries.Parse(new Dictionary<string, string> { [TrendSeries.PayloadKey] = "12, 9.5,14,20" });

        Assert.Equal([12, 9.5, 14, 20], points);
    }

    [Fact]
    public void Parse_SemicolonAndSpaceSeparators_Too()
        => Assert.Equal([1, 2, 3], TrendSeries.Parse(new Dictionary<string, string> { [TrendSeries.PayloadKey] = "1;2 3" }));

    // —— TrendSeries：空态（fail-closed） ——

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_MissingOrBlankKey_ReturnsEmpty(string? raw)
    {
        var payload = raw is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { [TrendSeries.PayloadKey] = raw };

        Assert.Empty(TrendSeries.Parse(payload));
    }

    [Theory]
    [InlineData("1,2,x")]
    [InlineData("1,2,NaN")]
    [InlineData("1,2,Infinity")]
    [InlineData("42")]
    public void Parse_MalformedOrTooShort_ReturnsEmpty(string raw)
        => Assert.Empty(TrendSeries.Parse(new Dictionary<string, string> { [TrendSeries.PayloadKey] = raw }));

    [Fact]
    public void Parse_EmptySlotsTreatedAsSeparatorNoise()
        => Assert.Equal([1, 2, 3],
            TrendSeries.Parse(new Dictionary<string, string> { [TrendSeries.PayloadKey] = "1,2,,3" }));

    // —— Normalize ——

    [Fact]
    public void Normalize_MapsMinZeroMaxOne()
    {
        var normalized = TrendSeries.Normalize([10, 20, 15]);

        Assert.Equal([0, 1, 0.5], normalized);
    }

    [Fact]
    public void Normalize_AllEqual_ReturnsMidBandFlatLine()
        => Assert.Equal([0.5, 0.5, 0.5], TrendSeries.Normalize([7, 7, 7]));

    [Fact]
    public void Normalize_EmptyPoints_ReturnsEmpty()
        => Assert.Empty(TrendSeries.Normalize([]));

    // —— Direction ——

    public static IEnumerable<object[]> DirectionData()
    {
        yield return new object[] { new double[] { 1, 2, 3, 10 }, TrendDirection.Up };
        yield return new object[] { new double[] { 10, 9, 3, 1 }, TrendDirection.Down };
        yield return new object[] { new double[] { 5, 5, 5 }, TrendDirection.Flat };
        yield return new object[] { new double[] { 0, 10, 4.6, 0.3 }, TrendDirection.Flat }; // 末点落在首点 ±10% 死区内
        yield return new object[] { new double[0], TrendDirection.Flat };
        yield return new object[] { new double[] { 7 }, TrendDirection.Flat };
    }

    [Theory]
    [MemberData(nameof(DirectionData))]
    public void Direction_WithDeadBand(double[] points, TrendDirection expected)
        => Assert.Equal(expected, TrendSeries.Direction(points));

    // —— Summarize ——

    [Fact]
    public void Summarize_ComputesEndpointsExtremaDeltaOnce()
    {
        var summary = TrendSeries.Summarize([2, 8, 4]);

        Assert.NotNull(summary);
        Assert.Equal(2, summary!.First);
        Assert.Equal(4, summary.Last);
        Assert.Equal(2, summary.Min);
        Assert.Equal(8, summary.Max);
        Assert.Equal(2, summary.Delta);
        Assert.Equal(TrendDirection.Up, summary.Direction);
    }

    [Fact]
    public void Summarize_EmptyPoints_ReturnsNull()
        => Assert.Null(TrendSeries.Summarize([]));

    // —— TimelineEntries：映射 ——

    private static NotificationRecord Record(int minute, string title, bool read = false)
        => new()
        {
            Id = $"n-{minute}",
            SourceWidgetId = "w-1",
            Severity = Severity.Warning,
            Title = title,
            Timestamp = new DateTimeOffset(2026, 10, 16, 8, minute, 0, TimeSpan.Zero),
            Read = read,
        };

    [Fact]
    public void Of_NewestFirst_WithLocalMinuteStamps()
    {
        NotificationRecord[] records = [Record(1, "old"), Record(5, "mid"), Record(9, "new")];
        var stamps = records.Select(record => record.Timestamp.ToLocalTime().ToString("HH:mm")).ToList();

        var entries = TimelineEntries.Of(records);

        Assert.Equal(3, entries.Count);
        // 时刻按各箱本地时区格式化（本箱与 CI 时区不同，故与源戳同一口径比对而非写死）
        Assert.Equal(new[] { stamps[2], stamps[1], stamps[0] }, entries.Select(entry => entry.Time).ToList());
        Assert.Equal(new[] { "new", "mid", "old" }, entries.Select(entry => entry.Title).ToList());
        Assert.All(entries, entry => Assert.Matches(@"^\d{2}:\d{2}$", entry.Time));
    }

    [Fact]
    public void Of_CapsToMaxEntries_KeepingNewest()
    {
        var records = Enumerable.Range(0, 12).Select(minute => Record(minute, $"m{minute}")).ToList();

        var entries = TimelineEntries.Of(records, max: 3);

        Assert.Equal(3, entries.Count);
        Assert.Equal("m11", entries[0].Title);
        Assert.Equal("m9", entries[2].Title);
    }

    [Fact]
    public void Of_CarriesSeverityAndReadFlag()
    {
        var entries = TimelineEntries.Of([Record(1, "n", read: true)]);

        var entry = Assert.Single(entries);
        Assert.Equal(Severity.Warning, entry.Severity);
        Assert.True(entry.Read);
    }

    [Fact]
    public void Of_EmptyHistory_ReturnsEmpty()
        => Assert.Empty(TimelineEntries.Of([]));

    // —— WidgetFeedback.DetailRows（表格件数据面） ——

    [Fact]
    public void DetailRows_SkipsHintKeyAndBlankValues_KeepsEnumerationOrder()
    {
        var rows = WidgetFeedback.DetailRows(new Dictionary<string, string>
        {
            [WidgetFeedback.ValueHintKey] = "38%",
            ["repo"] = "owner/repo",
            ["blank"] = "  ",
            ["branch"] = "main",
        });

        Assert.Equal([("repo", "owner/repo"), ("branch", "main")], rows);
    }

    [Fact]
    public void DetailRows_EmptyPayload_ReturnsEmpty()
        => Assert.Empty(WidgetFeedback.DetailRows(new Dictionary<string, string>()));
}
