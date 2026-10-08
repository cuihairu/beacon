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
}
