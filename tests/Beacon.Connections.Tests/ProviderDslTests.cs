using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// provider DSL（批3-补42）：YamlLite 极小子集解析（缩进映射 / - 列表 / 引号 / 行内注释 + fail-loud 全表）、
/// ProviderDsl.Load 校验（顶层键 / mapping 键白名单 / action 必填与类型 / confirm 默认与取值 / retry 范围）、
/// ToActionConfig 进既有执行器链路、LoadFile mtime+size 缓存与缺文件 Degraded、
/// MappingValue 声明层取值次序（widget.Config 实例值优先，dsl_path 未配返回 null = 默认路径零变化）。
/// </summary>
public sealed class ProviderDslTests
{
    private const string FullDoc = """
        provider: my-service
        mapping:
          status_path: $.job.status
          success_values: success,ok
          unit: ms
        actions:
          - id: restart
            type: http
            name: 重启服务
            url: https://api.example.com/jobs/1/restart
            method: POST
            body: '{"force": true}  # body 内 # 不算注释'
            confirm: true
            retry:
              max_attempts: 3
              backoff_seconds: 2
          - id: dashboard
            type: open.url
            url: https://status.example.com
        """;

    // —— YamlLite 解析 ——

    [Fact]
    public void Parse_NestedMapping_ListOfMaps_Quotes_Comments()
    {
        var root = YamlLite.Parse(FullDoc);
        Assert.Equal("my-service", root.Text("provider"));
        var mapping = root.Child("mapping");
        Assert.NotNull(mapping);
        Assert.Equal("$.job.status", mapping.Text("status_path"));
        Assert.Equal("success,ok", mapping.Text("success_values"));

        var actions = root.Child("actions");
        Assert.NotNull(actions);
        Assert.Equal(2, actions.List.Count);
        var first = actions.List[0];
        Assert.Equal("restart", first.Text("id"));
        Assert.Equal("https://api.example.com/jobs/1/restart", first.Text("url"));
        // 引号内的 # 保留、引号剥除
        Assert.Equal("{\"force\": true}  # body 内 # 不算注释", first.Text("body"));
        var retry = first.Child("retry");
        Assert.NotNull(retry);
        Assert.Equal("3", retry.Text("max_attempts"));
        Assert.Equal("2", retry.Text("backoff_seconds"));
        var second = actions.List[1];
        Assert.Equal("dashboard", second.Text("id"));
        Assert.Equal("https://status.example.com", second.Text("url"));
        // 未写的键：null（不是空串）
        Assert.Null(second.Text("confirm"));
        Assert.Null(second.Child("retry"));
    }

    [Theory]
    [InlineData("provider: x\n  bad: 1", "缩进层级不一致")]
    [InlineData("\tprovider: x", "Tab")]
    [InlineData("provider: {a: b}", "flow")]
    [InlineData("provider: [1]", "flow")]
    [InlineData("body: |\n  line1", "块标量")]
    [InlineData("---\nprovider: x", "多文档")]
    [InlineData("", "空文档")]
    [InlineData("# 只有注释\n", "空文档")]
    [InlineData("provider: x\nprovider: y", "重复键")]
    [InlineData("provider: \"unterminated", "引号未闭合")]
    [InlineData("just_text", "冒号")]
    [InlineData("actions:\n  - id: a\n    - id: b", "缩进层级不一致")]
    [InlineData("actions:\n - just scalar", "列表项必须")]
    [InlineData("actions:\n  -  id: a", "恰好一个空格")]
    public void Parse_UnsupportedSyntax_FailsLoudWithLine(string text, string expectedFragment)
    {
        var exception = Assert.Throws<YamlLiteException>(() => YamlLite.Parse(text));
        Assert.Contains(expectedFragment, exception.Message);
        Assert.Matches(@"第 \d+ 行", exception.Message);
    }

    [Fact]
    public void Parse_ScalarForms_Bare_Quoted_TrailingSpaces()
    {
        var root = YamlLite.Parse(
            "a: bare\nb: \"quoted # not comment\"\nc: 'single'\nd:   spaced   # trailing comment");
        Assert.Equal("bare", root.Text("a"));
        Assert.Equal("quoted # not comment", root.Text("b"));
        Assert.Equal("single", root.Text("c"));
        Assert.Equal("spaced", root.Text("d"));
    }

    [Fact]
    public void Parse_QuotedJsonBody_ParsedLiterally_UnquotedFlowRejected()
    {
        // 引号包住的 JSON 是字面量（webhook body 常见）；裸 { 开头 fail-loud 并提示加引号
        var root = YamlLite.Parse("body: '{\"event\": \"restart\"}'");
        Assert.Equal("{\"event\": \"restart\"}", root.Text("body"));

        var exception = Assert.Throws<YamlLiteException>(() => YamlLite.Parse("body: {\"event\": 1}"));
        Assert.Contains("flow", exception.Message);
    }

    // —— ProviderDsl.Load 校验 ——

    [Fact]
    public void Load_FullDocument_MapsActionsWithConfirmAndRetryDefaults()
    {
        var document = ProviderDsl.Load(FullDoc);
        Assert.Equal("my-service", document.Provider);
        Assert.Equal(3, document.Mapping.Count);
        Assert.Equal(2, document.Actions.Count);

        var restart = document.Actions[0];
        Assert.True(restart.RequireConfirmation); // 显式 confirm: true
        Assert.Equal(3, restart.RetryMaxAttempts);
        Assert.Equal(2, restart.RetryBackoffSeconds);

        // open.url 默认免确认（无副作用）；http/webhook 默认要确认；retry 取默认 1 次 / 5 秒
        var dashboard = document.Actions[1];
        Assert.False(dashboard.RequireConfirmation);
        Assert.Equal(1, dashboard.RetryMaxAttempts);
        Assert.Equal(5, dashboard.RetryBackoffSeconds);
    }

    [Theory]
    [InlineData("other: 1\nprovider: x", "未知顶层键")]
    [InlineData("mapping:\n  status_path: $.a", "缺少 provider")]
    [InlineData("provider: x\nmapping:\n  bogus_key: 1", "未知 mapping 键")]
    [InlineData("provider: x\nmapping: scalar", "mapping 必须是映射")]
    [InlineData("provider: x\nactions: nope", "actions 必须是列表")]
    [InlineData("provider: x\nactions:\n  - type: http\n    url: https://a", "缺少 id")]
    [InlineData("provider: x\nactions:\n  - id: a\n    url: https://a", "缺少 type")]
    [InlineData("provider: x\nactions:\n  - id: a\n    type: http", "缺少 url")]
    [InlineData("provider: x\nactions:\n  - id: a\n    type: gh.workflow_dispatch\n    url: https://a", "不支持")]
    [InlineData("provider: x\nactions:\n  - id: a\n    type: http\n    url: https://a\n    confirm: yes", "confirm 只接受")]
    [InlineData("provider: x\nactions:\n  - id: a\n    type: http\n    url: https://a\n    retry:\n      max_attempts: 0", "max_attempts 必须")]
    [InlineData("provider: x\nactions:\n  - id: a\n    type: http\n    url: https://a\n    retry:\n      max_attempts: 11", "max_attempts 必须")]
    [InlineData("provider: x\nactions:\n  - id: a\n    type: http\n    url: https://a\n    retry:\n      backoff_seconds: -1", "backoff_seconds 必须")]
    [InlineData("provider: x\nactions:\n  - id: a\n    type: http\n    url: https://a\n    retry:\n      backoff_seconds: 301", "backoff_seconds 必须")]
    [InlineData("provider: x\nactions:\n  - scalar", "列表项必须")]
    public void Load_InvalidDocument_FailsLoud(string text, string expectedFragment)
    {
        var exception = Assert.Throws<YamlLiteException>(() => ProviderDsl.Load(text));
        Assert.Contains(expectedFragment, exception.Message);
    }

    [Theory]
    [InlineData("webhook", true)]
    [InlineData("http", true)]
    [InlineData("open.url", false)]
    public void Load_ConfirmDefaultsByType(string type, bool expected)
    {
        var document = ProviderDsl.Load($$"""
            provider: x
            actions:
              - id: a
                type: {{type}}
                url: https://a
            """);
        var action = Assert.Single(document.Actions);
        Assert.Equal(expected, action.RequireConfirmation);
    }

    // —— ToActionConfig：进既有执行器链路 ——

    [Fact]
    public void ToActionConfig_BuildsRunnerCompatibleParameters()
    {
        var document = ProviderDsl.Load(FullDoc);
        var config = document.Actions[0].ToActionConfig("conn-1");
        Assert.Equal("dsl.conn-1.restart", config.Id);
        Assert.Equal("http", config.Type);
        Assert.Equal("重启服务", config.Name);
        Assert.True(config.RequireConfirmation);
        Assert.Equal("https://api.example.com/jobs/1/restart", config.Parameters["url"]);
        Assert.Equal("POST", config.Parameters["method"]);
        Assert.Equal("{\"force\": true}  # body 内 # 不算注释", config.Parameters["body"]);
        Assert.Equal("conn-1", config.Parameters["connection_id"]);
        Assert.Equal("3", config.Parameters["retry_max_attempts"]);
        Assert.Equal("2", config.Parameters["retry_backoff_seconds"]);

        // open.url 动作不写 method/body 键（执行器默认值生效）
        var open = document.Actions[1].ToActionConfig("conn-1");
        Assert.False(open.Parameters.ContainsKey("method"));
        Assert.False(open.Parameters.ContainsKey("body"));
    }

    // —— LoadFile：缓存与缺文件 ——

    [Fact]
    public void LoadFile_MissingFile_ThrowsDegradedConnectionException()
    {
        var exception = Assert.Throws<ConnectionException>(
            () => ProviderDsl.LoadFile("/nonexistent/beacon-dsl.yaml"));
        Assert.Contains("DSL 文件不存在", exception.Message);
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public void LoadFile_ReusesCacheUntilMtimeOrSizeChanges()
    {
        var path = Path.Combine(Path.GetTempPath(), $"beacon-dsl-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(path, "provider: one\nmapping:\n  unit: ms");
            var first = ProviderDsl.LoadFile(path);
            Assert.Equal("one", first.Provider);

            // mtime+size 都没变 → 命中缓存（同一实例）
            Assert.Same(first, ProviderDsl.LoadFile(path));

            // 内容变了（size 变）→ 重新解析
            File.WriteAllText(path, "provider: two-changed\nmapping:\n  unit: ms\n");
            var second = ProviderDsl.LoadFile(path);
            Assert.Equal("two-changed", second.Provider);

            // size 一样但 mtime 变 → 也重解析
            File.WriteAllText(path, "provider: three-mtime");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(1));
            var third = ProviderDsl.LoadFile(path);
            Assert.Equal("three-mtime", third.Provider);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFile_BrokenFile_WrapsParseErrorAsDegraded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"beacon-dsl-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(path, "provider: x\n\ttab: 1");
            var exception = Assert.Throws<ConnectionException>(() => ProviderDsl.LoadFile(path));
            Assert.Contains("DSL 文件解析失败", exception.Message);
            Assert.Contains("Tab", exception.Message);
            Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // —— MappingValue：声明层取值次序 ——

    [Fact]
    public void MappingValue_WidgetConfigWinsOverDsl()
    {
        var (widget, connection, path) = DslFixture(
            """
            provider: x
            mapping:
              status_path: $.from.dsl
              unit: ms
            """);
        try
        {
            widget.Config["status_path"] = "$.from.widget";
            Assert.Equal("$.from.widget", ProviderDsl.MappingValue(widget, connection, "status_path"));
            // 实例没配的键落到 DSL
            Assert.Equal("ms", ProviderDsl.MappingValue(widget, connection, "unit"));
            // 双方都没有 → null
            Assert.Null(ProviderDsl.MappingValue(widget, connection, "total_path"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MappingValue_NoDslPath_ReturnsNull_EvenWithoutOwnValue()
    {
        var widget = new WidgetConfig { Id = "w1", Type = "http.status" };
        var connection = new ConnectionConfig
        {
            Id = "c1",
            Type = "http",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
        Assert.Null(ProviderDsl.MappingValue(widget, connection, "status_path"));

        connection.Settings[ProviderDsl.SettingKey] = "   "; // 空白视同未配置
        Assert.Null(ProviderDsl.MappingValue(widget, connection, "status_path"));
    }

    [Fact]
    public void MappingValue_MissingDslFile_ThrowsDegraded()
    {
        var widget = new WidgetConfig { Id = "w1", Type = "http.status" };
        var connection = new ConnectionConfig
        {
            Id = "c1",
            Type = "http",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderDsl.SettingKey] = "/nonexistent/beacon-dsl.yaml",
            },
        };
        // 双方都没配该键才会走到声明层 → 缺文件暴露为 Degraded（fail-loud，不静默）
        Assert.Throws<ConnectionException>(
            () => ProviderDsl.MappingValue(widget, connection, "status_path"));
    }

    private static (WidgetConfig Widget, ConnectionConfig Connection, string Path) DslFixture(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"beacon-dsl-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        var widget = new WidgetConfig { Id = "w1", Type = "http.status" };
        var connection = new ConnectionConfig
        {
            Id = "c1",
            Type = "http",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderDsl.SettingKey] = path,
            },
        };
        return (widget, connection, path);
    }

    // —— Provider 集成：DSL 作为 http.status / http.quota 的可选声明层 ——

    private static string WriteDsl(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"beacon-dsl-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static ConnectionConfig DslConnection(string path) => new()
    {
        Id = "c1",
        Type = "http",
        Endpoint = "http://ci.local/api",
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProviderDsl.SettingKey] = path,
        },
    };

    private static ConnectionContext Ctx() => new() { Secrets = new SecretStoreStub() };

    [Fact]
    public async Task HttpStatusProvider_DslMappingSuppliesDefaults_WidgetConfigWins()
    {
        var path = WriteDsl("""
            provider: my-ci
            mapping:
              status_path: $.job.state
              success_values: green
              warning_values: yellow
            """);
        try
        {
            var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"job":{"state":"green"}}""") };
            var provider = new HttpStatusProvider(http);
            var widget = new WidgetConfig
            {
                Id = "w1",
                Type = "http.status",
                Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            };
            var connection = DslConnection(path);

            // DSL 提供 status_path 与词表：green 按 DSL 词表映射成功
            var state = await provider.GetStateAsync(widget, connection, Ctx(), CancellationToken.None);
            Assert.NotNull(state);
            Assert.Equal(Severity.Success, state.Severity);
            Assert.Equal("green", state.Payload["status"]);

            // 实例 Config 覆盖 DSL：status_path 改指自定义键
            widget.Config["status_path"] = "$.custom";
            http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"custom":"yellow"}""");
            var overridden = await provider.GetStateAsync(widget, connection, Ctx(), CancellationToken.None);
            Assert.NotNull(overridden);
            Assert.Equal("yellow", overridden.Payload["status"]);
            Assert.Equal(Severity.Warning, overridden.Severity);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task HttpStatusProvider_MissingDslFile_SurfacesDegraded()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"status":"success"}""") };
        var provider = new HttpStatusProvider(http);
        var widget = new WidgetConfig
        {
            Id = "w1",
            Type = "http.status",
            Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
        var connection = new ConnectionConfig
        {
            Id = "c1",
            Type = "http",
            Endpoint = "http://ci.local/api",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderDsl.SettingKey] = "/nonexistent/beacon-dsl.yaml",
            },
        };

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(widget, connection, Ctx(), CancellationToken.None));
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("DSL 文件不存在", exception.Message);
    }

    [Fact]
    public async Task HttpQuotaProvider_DslMappingSuppliesPathsAndUnit()
    {
        var path = WriteDsl("""
            provider: quota
            mapping:
              total_path: $.plan.total
              used_path: $.plan.used
              unit: tokens
            """);
        try
        {
            var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"plan":{"total":1000,"used":750}}""") };
            var provider = new HttpQuotaProvider(http);
            var widget = new WidgetConfig
            {
                Id = "w1",
                Type = "http.quota",
                Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            };

            var state = await provider.GetStateAsync(widget, DslConnection(path), Ctx(), CancellationToken.None);
            Assert.NotNull(state);
            Assert.Equal(0.75, state.Progress!.Value, 3);
            Assert.Equal("tokens", state.Payload["unit"]);
            Assert.Equal("750", state.Payload["used"]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
