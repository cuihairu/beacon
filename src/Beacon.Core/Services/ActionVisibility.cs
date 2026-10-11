namespace Beacon.Core.Services;

/// <summary>
/// Action 在某组件详情页的可见性（纯函数供单测，B-603 + 批3-补42 DSL 动作）：
/// `widgetType` 作用域支持逗号分隔多值（留空 = 全组件可见）；
/// `connection_id` 作用域限定连接（DSL 声明动作只出现在所属连接的组件上）。
/// </summary>
public static class ActionVisibility
{
    public static bool IsVisible(IReadOnlyDictionary<string, string>? parameters, string widgetType, string connectionId)
    {
        if (parameters is null)
        {
            return true;
        }
        if (parameters.TryGetValue("connection_id", out var scopeConnection) && scopeConnection.Length > 0
            && !string.Equals(scopeConnection, connectionId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (parameters.TryGetValue("widgetType", out var scopeTypes) && scopeTypes.Length > 0)
        {
            foreach (var scope in scopeTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(scope, widgetType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        return true;
    }
}
