using Beacon.Core.Models;

namespace Beacon.Core.Tests;

/// <summary>
/// 数量悬浮窗总闸（拍板 2026-10-08 ×4）：数值/额度类（FloatingOptIn）默认关 = 桌面零残留。
/// 回归锚：只闸悬浮窗不闸钉选面板时，GLM 绑定流一键钉选的数值卡照样上桌面（「改了还弹」实证）。
/// </summary>
public sealed class WidgetDisplayPolicyTests
{
    private static WidgetTypeDescriptor Descriptor(bool floatingOptIn = false, bool floatingSupported = false)
        => new() { Type = "test.type", DisplayName = "t", PinSupported = true, FloatingOptIn = floatingOptIn, FloatingSupported = floatingSupported };

    [Fact]
    public void NumericOptIn_SwitchOff_Suppressed()
        => Assert.True(WidgetDisplayPolicy.IsNumericSuppressed(Descriptor(floatingOptIn: true), numericFloatingEnabled: false));

    [Fact]
    public void NumericOptIn_SwitchOn_Shows()
        => Assert.False(WidgetDisplayPolicy.IsNumericSuppressed(Descriptor(floatingOptIn: true), numericFloatingEnabled: true));

    [Theory]
    [InlineData(false)] // 开关关
    [InlineData(true)]  // 开关开：非数值类不受闸
    public void DenseWidget_NeverSuppressed(bool switchOn)
        => Assert.False(WidgetDisplayPolicy.IsNumericSuppressed(Descriptor(floatingSupported: true), switchOn));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownDescriptor_NeverSuppressed(bool switchOn)
        => Assert.False(WidgetDisplayPolicy.IsNumericSuppressed(null, switchOn));

    // —— PanelCarries：面板承载判定（启动建板/设置后重建/空板自关复活三处合一，2026-10-09 白板空壳收口） ——

    [Fact]
    public void PanelCarries_PanelMode_KeepsNumericTileWhenSwitchOn()
        => Assert.True(WidgetDisplayPolicy.PanelCarries(Descriptor(floatingOptIn: true), numericFloatingEnabled: true, floatingMode: false, floatingEligible: true));

    [Fact]
    public void PanelCarries_NumericSwitchOff_Suppresses()
        => Assert.False(WidgetDisplayPolicy.PanelCarries(Descriptor(floatingOptIn: true), numericFloatingEnabled: false, floatingMode: false, floatingEligible: false));

    [Fact]
    public void PanelCarries_FloatingMode_ExcludesEligibleTile()
        => Assert.False(WidgetDisplayPolicy.PanelCarries(Descriptor(floatingOptIn: true), numericFloatingEnabled: true, floatingMode: true, floatingEligible: true));

    [Fact]
    public void PanelCarries_FloatingMode_KeepsIneligibleTile()
        => Assert.True(WidgetDisplayPolicy.PanelCarries(Descriptor(), numericFloatingEnabled: false, floatingMode: true, floatingEligible: false));

    [Fact]
    public void PanelCarries_NonPinSupported_NeverCarried()
        => Assert.False(WidgetDisplayPolicy.PanelCarries(
            new WidgetTypeDescriptor { Type = "test.type", DisplayName = "t", PinSupported = false },
            numericFloatingEnabled: true, floatingMode: false, floatingEligible: false));
}
