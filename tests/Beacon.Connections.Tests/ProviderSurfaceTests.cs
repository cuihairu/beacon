using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// Provider/Connection 元数据面收口：无参构造、Descriptor、ConnectionType、类型级描述符不变量。
/// 这些面是组件注册表（App 侧声明式注册）与连接类型分发的契约键，逐一钉住防回归。
/// </summary>
public sealed class ProviderSurfaceTests
{
    // ---------- 类型级描述符（registry 契约键 + 字段表 + 档位） ----------

    [Fact]
    public void ArkDescriptors_TypeAndFieldsArePinned()
    {
        Assert.Equal("ark.usage", ArkWidgetDescriptors.UsageType);
        Assert.Equal("ark.usage", ArkWidgetDescriptors.Usage.Type);
        Assert.Equal("方舟 Coding Plan 额度（火山）", ArkWidgetDescriptors.Usage.DisplayName);
        Assert.True(ArkWidgetDescriptors.Usage.PinSupported);
        Assert.True(ArkWidgetDescriptors.Usage.FloatingOptIn);
        Assert.Equal(RefreshTiers.Ci, ArkWidgetDescriptors.Usage.SuggestedTier);
        Assert.Equal(["label", "warn_percent", "error_percent"],
            [.. ArkWidgetDescriptors.Usage.Fields.Select(f => f.Key)]);
        Assert.Equal([ArkWidgetDescriptors.Usage], [.. ArkWidgetDescriptors.All]);
        Assert.Same(ArkWidgetDescriptors.Usage, new ArkUsageProvider().Descriptor);
    }

    [Fact]
    public void CodexDescriptors_TypeAndFieldsArePinned()
    {
        Assert.Equal("codex.usage", CodexWidgetDescriptors.Usage.Type);
        Assert.Equal("OpenAI Codex 用量（本机）", CodexWidgetDescriptors.Usage.DisplayName);
        Assert.True(CodexWidgetDescriptors.Usage.PinSupported);
        Assert.True(CodexWidgetDescriptors.Usage.FloatingOptIn);
        Assert.Equal(RefreshTiers.Static, CodexWidgetDescriptors.Usage.SuggestedTier);
        Assert.Equal(["label", "days"], [.. CodexWidgetDescriptors.Usage.Fields.Select(f => f.Key)]);
        Assert.Equal([CodexWidgetDescriptors.Usage], [.. CodexWidgetDescriptors.All]);
        Assert.Same(CodexWidgetDescriptors.Usage, new CodexUsageProvider().Descriptor);
    }

    [Fact]
    public void CopilotDescriptors_TypeAndFieldsArePinned()
    {
        Assert.Equal("copilot.usage", CopilotWidgetDescriptors.Usage.Type);
        Assert.Equal("GitHub Copilot 用量", CopilotWidgetDescriptors.Usage.DisplayName);
        Assert.True(CopilotWidgetDescriptors.Usage.PinSupported);
        Assert.True(CopilotWidgetDescriptors.Usage.FloatingOptIn);
        Assert.Equal(RefreshTiers.Default, CopilotWidgetDescriptors.Usage.SuggestedTier);
        Assert.Equal(["label", "warn_percent", "error_percent"],
            [.. CopilotWidgetDescriptors.Usage.Fields.Select(f => f.Key)]);
        Assert.Equal([CopilotWidgetDescriptors.Usage], [.. CopilotWidgetDescriptors.All]);
        Assert.Same(CopilotWidgetDescriptors.Usage, new CopilotUsageProvider().Descriptor);
    }

    [Fact]
    public void MiMoDescriptors_TypeAndFieldsArePinned()
    {
        Assert.Equal("mimo.usage", MiMoWidgetDescriptors.Usage.Type);
        Assert.Equal("小米 MiMo 用量（开放平台）", MiMoWidgetDescriptors.Usage.DisplayName);
        Assert.True(MiMoWidgetDescriptors.Usage.PinSupported);
        Assert.True(MiMoWidgetDescriptors.Usage.FloatingOptIn);
        Assert.Equal(RefreshTiers.Ci, MiMoWidgetDescriptors.Usage.SuggestedTier);
        Assert.Equal(["label"], [.. MiMoWidgetDescriptors.Usage.Fields.Select(f => f.Key)]);
        Assert.Equal([MiMoWidgetDescriptors.Usage], [.. MiMoWidgetDescriptors.All]);
        Assert.Same(MiMoWidgetDescriptors.Usage, new MiMoUsageProvider().Descriptor);
    }

    [Fact]
    public void OpenCodeDescriptors_TypeAndFieldsArePinned()
    {
        Assert.Equal("opencode.usage", OpenCodeWidgetDescriptors.Usage.Type);
        Assert.Equal("OpenCode Go 用量", OpenCodeWidgetDescriptors.Usage.DisplayName);
        Assert.True(OpenCodeWidgetDescriptors.Usage.PinSupported);
        Assert.True(OpenCodeWidgetDescriptors.Usage.FloatingOptIn);
        Assert.Equal(RefreshTiers.Default, OpenCodeWidgetDescriptors.Usage.SuggestedTier);
        Assert.Equal(["label", "warn_percent", "error_percent"],
            [.. OpenCodeWidgetDescriptors.Usage.Fields.Select(f => f.Key)]);
        Assert.Equal([OpenCodeWidgetDescriptors.Usage], [.. OpenCodeWidgetDescriptors.All]);
        Assert.Same(OpenCodeWidgetDescriptors.Usage, new OpenCodeUsageProvider().Descriptor);
    }

    [Fact]
    public void QwenDescriptors_TypeAndFieldsArePinned()
    {
        Assert.Equal("qwen.usage", QwenWidgetDescriptors.UsageType);
        Assert.Equal("qwen.usage", QwenWidgetDescriptors.Usage.Type);
        Assert.Equal("阿里千问用量（百炼 Token Plan）", QwenWidgetDescriptors.Usage.DisplayName);
        Assert.True(QwenWidgetDescriptors.Usage.PinSupported);
        Assert.True(QwenWidgetDescriptors.Usage.FloatingOptIn);
        Assert.Equal(RefreshTiers.Ci, QwenWidgetDescriptors.Usage.SuggestedTier);
        Assert.Equal(["label", "warn_percent", "error_percent"],
            [.. QwenWidgetDescriptors.Usage.Fields.Select(f => f.Key)]);
        Assert.Equal([QwenWidgetDescriptors.Usage], [.. QwenWidgetDescriptors.All]);
        Assert.Same(QwenWidgetDescriptors.Usage, new QwenUsageProvider().Descriptor);
    }

    [Fact]
    public void SenseNovaDescriptors_TypeAndFieldsArePinned()
    {
        Assert.Equal("sensenova.tokenplan", SenseNovaWidgetDescriptors.Usage.Type);
        Assert.Equal("商汤套餐池额度（Token Plan）", SenseNovaWidgetDescriptors.Usage.DisplayName);
        Assert.True(SenseNovaWidgetDescriptors.Usage.PinSupported);
        Assert.True(SenseNovaWidgetDescriptors.Usage.FloatingOptIn);
        Assert.Equal(RefreshTiers.Ci, SenseNovaWidgetDescriptors.Usage.SuggestedTier);
        Assert.Equal(["label", "warn_percent", "error_percent"],
            [.. SenseNovaWidgetDescriptors.Usage.Fields.Select(f => f.Key)]);
        Assert.Equal([SenseNovaWidgetDescriptors.Usage], [.. SenseNovaWidgetDescriptors.All]);
        Assert.Same(SenseNovaWidgetDescriptors.Usage, new SenseNovaUsageProvider().Descriptor);
    }

    // ---------- 无参构造（生产注册路径）+ Descriptor 契约 ----------

    [Fact]
    public void UsageProviderParameterlessConstructors_BuildWithSharedPool()
    {
        Assert.NotNull(new ArkUsageProvider());
        Assert.NotNull(new BigModelUsageProvider());
        Assert.NotNull(new CodexUsageProvider());
        Assert.NotNull(new CopilotUsageProvider());
        Assert.NotNull(new DeepSeekBalanceProvider());
        Assert.NotNull(new HeraldIncidentsProvider());
        Assert.NotNull(new HeraldFeedProvider());
        Assert.NotNull(new HeraldDeliveriesProvider());
        Assert.NotNull(new HttpConnectionProvider());
        Assert.NotNull(new HttpQuotaProvider());
        Assert.NotNull(new KimiCodingUsageProvider());
        Assert.NotNull(new MiMoUsageProvider());
        Assert.NotNull(new MoonshotBalanceProvider());
        Assert.NotNull(new OpenCodeUsageProvider());
        Assert.NotNull(new QwenUsageProvider());
        Assert.NotNull(new SenseNovaUsageProvider());
    }

    [Fact]
    public void MachineProvidersParameterlessConstructors_BuildWithSystemSources()
    {
        Assert.NotNull(new MachineNetProvider());
        Assert.NotNull(new MachineLocalProvider());
    }

    // ---------- 连接类型分发键（connections.json type 字段的契约） ----------

    [Fact]
    public void ConnectionTypeKeys_ArePinned()
    {
        Assert.Equal("ark", new ArkConnectionProvider().ConnectionType);
        Assert.Equal("bigmodel", new BigModelConnectionProvider().ConnectionType);
        Assert.Equal("claude", new ClaudeConnectionProvider().ConnectionType);
        Assert.Equal("codex", new CodexConnectionProvider().ConnectionType);
        Assert.Equal("copilot", new CopilotConnectionProvider().ConnectionType);
        Assert.Equal("deepseek", new DeepSeekConnectionProvider().ConnectionType);
        Assert.Equal("github", new GitHubConnectionProvider().ConnectionType);
        Assert.Equal("herald", new HeraldConnectionProvider().ConnectionType);
        Assert.Equal("http", new HttpConnectionProvider().ConnectionType);
        Assert.Equal("jenkins", new JenkinsConnectionProvider().ConnectionType);
        Assert.Equal("kimi", new KimiConnectionProvider().ConnectionType);
        Assert.Equal("machine", new MachineConnectionProvider().ConnectionType);
        Assert.Equal("mimo", new MiMoConnectionProvider().ConnectionType);
        Assert.Equal("moonshot", new MoonshotConnectionProvider().ConnectionType);
        Assert.Equal("opencode", new OpenCodeConnectionProvider().ConnectionType);
        Assert.Equal("qwen", new QwenConnectionProvider().ConnectionType);
        Assert.Equal("sensenova", new SenseNovaConnectionProvider().ConnectionType);
    }

    [Fact]
    public void OpenCodeBudget_RecordShapeIsPinned()
    {
        var budget = new OpenCodeBudget(Email: null, LimitMicroCents: null,
            SpentMicroCents: 1_500_000, Exceeded: false, ResetsAt: "2026-12-01T00:00:00Z");

        Assert.Null(budget.Email);
        Assert.Null(budget.LimitMicroCents);
        Assert.Equal(1_500_000, budget.SpentMicroCents);
        Assert.False(budget.Exceeded);
        Assert.Equal("2026-12-01T00:00:00Z", budget.ResetsAt);
    }

    [Fact]
    public void GitWorkspaceProvider_ParameterlessConstructorUsesCliSource()
    {
        Assert.NotNull(new GitWorkspaceProvider());
    }
}
