using Beacon.Core.Services;
using Microsoft.UI.Xaml;

namespace Beacon.App.Services;

/// <summary>
/// 设计令牌层（组件化重构 Phase 2 第 1 条，redesign-plan §6）：字号 / 圆角 / 间距三类本类承载，
/// 值全部取自既有 inline 字面量（迁移即统一，不改任何视觉值——G 系 CI 像素断言锚现值）。
/// 其余维度委托既有单一来源：色彩与状态色 UiPalette / ThemeColors（Core：PaletteResolver / HighContrastPalette）、
/// 动效 MotionEngine（三档模式 + 时长）、尺寸 DpiLayoutMath（Core，DPI 唯一口径）；阴影当前无使用面，预留。
/// XAML 侧圆角镜像见 App.xaml 资源字典（WinUI 3 无 x:Static，两处须同步改）。
/// 间距令牌是刻度表：各窗口 bespoke Padding/Margin 组合暂不迁移，组件化（Phase 2 组件库）时逐面收编。
/// </summary>
internal static class DesignTokens
{
    // 字号刻度（Value → 值；取自 Detail/PinnedHost/QuickPanel/Settings 四窗 inline 值集合）
    public const double FontFootnote = 10.5;       // 辅助脚注（设置页提示）
    public const double FontCaption = 11;          // 次要文案（时间戳/子标签/chip）
    public const double FontCaptionEmphasis = 11.5; // 次要文案强调（设置页单点）
    public const double FontBody = 12;             // 正文（标签/值位/行）
    public const double FontSubtitle = 13;         // 小节副题
    public const double FontTitle = 14;            // tile 标题
    public const double FontHeader = 15;           // 区块头
    public const double FontDisplay = 16;          // 详情主标题

    // 圆角（C# 构造面；XAML 面在 App.xaml 资源字典同名镜像）
    public static readonly CornerRadius RadiusCard = new(6);      // 设置卡片
    public static readonly CornerRadius RadiusControl = new(7);   // 设置输入控件
    public static readonly CornerRadius RadiusChip = new(9);      // L2 过滤 chip
    public static readonly CornerRadius RadiusInner = new(8);     // 内嵌条（胶囊横幅/面板内层）
    public static readonly CornerRadius RadiusPanel = new(12);    // 面板根
    public static readonly CornerRadius RadiusPill = new(16);     // 胶囊

    // 间距刻度（bespoke Thickness 组合的组成单位）
    public const double Space2 = 2;
    public const double Space4 = 4;
    public const double Space6 = 6;
    public const double Space8 = 8;
    public const double Space10 = 10;
    public const double Space12 = 12;
    public const double Space16 = 16;
    public const double Space18 = 18;

    /// <summary>
    /// 密度档收编口（Phase 5 行 5）：间距值经 Core UiDensity 按当前档缩放（regular 恒等——G 系像素断言锚的值不动；
    /// compact ×0.75 四舍五入 AwayFromZero）。各窗构造时传当前 Appearance.Density，「构造期实时读」口径与
    /// MotionEngine.ProgressStyle 一致：改档后下次开窗生效。未知档 Core 侧回 regular（fail-open）。
    /// </summary>
    public static double Spacing(double space, string? density)
        => UiDensity.Spacing(space, UiDensity.Parse(density));
}
