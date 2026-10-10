using Beacon.Core.Models;
using Beacon.Storage;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Services;

/// <summary>动效档位（config.json appearance.motion.mode）。</summary>
internal enum MotionMode
{
    Off,
    Reduced,
    Full,
}

/// <summary>
/// L0 动效引擎（B-707，RFC §6.2.8）：Composition 属性动画五族，只作用于对应 tile
/// 的视觉层，不重建整窗——
/// ① 变色过渡（换色 + 亮度渐入 ~200ms×intensity）② 提醒闪烁（状态变化后短促 2 次，full）
/// ③ 呼吸灯（full 常驻）④ Critical 脉冲（reduced 默认，周期 ≥1s 即 ≤1Hz）
/// ⑤ tile 滑入（full，首次出现/收起展开）。
/// mode/intensity 每次调用实时读 config.json appearance.Motion，设置改档即刻生效。
/// 铁律不变（RFC §6.2.7）：动效永不抢焦点、不发声、不产生弹窗；收起态由宿主暂停循环。
/// 动画失败静默降级为瞬时着色——动效故障绝不影响常驻应用。
/// </summary>
internal sealed class MotionEngine
{
    private readonly JsonConfigurationStore _config;

    public MotionEngine(JsonConfigurationStore config) => _config = config;

    public MotionMode Mode => (_config.App.Appearance.Motion.Mode)?.Trim().ToLowerInvariant() switch
    {
        "full" => MotionMode.Full,
        "off" => MotionMode.Off,
        _ => MotionMode.Reduced, // 缺省/非法值落 reduced（RFC §6.2.8 默认档）
    };

    /// <summary>0.5–2.0，缩放动效时长与幅度；非法值回退 1.0。</summary>
    public double Intensity => double.IsFinite(_config.App.Appearance.Motion.Intensity)
        ? Math.Clamp(_config.App.Appearance.Motion.Intensity, 0.5, 2.0)
        : 1.0;

    /// <summary>① 变色过渡：换色后亮度渐入（8px 灯上视觉等价交叉淡变）。off 档瞬时。</summary>
    public void TransitionFill(Shape shape, global::Windows.UI.Color to)
    {
        var mode = Mode;
        shape.Fill = new SolidColorBrush(to);
        if (mode == MotionMode.Off)
        {
            return;
        }
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(shape);
            var compositor = visual.Compositor;
            var fadeIn = compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0f, 0f);
            fadeIn.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.1f, 0.9f), new System.Numerics.Vector2(0.2f, 1f)));
            fadeIn.Duration = TimeSpan.FromMilliseconds(200 * Intensity);
            visual.StartAnimation("Opacity", fadeIn);
        }
        catch
        {
            // 动效不可用（如无 Composition 支持）：保留瞬时着色结果
        }
    }

    /// <summary>② 提醒闪烁：状态变化后短促 2 次（full 档）。</summary>
    public void Flash(UIElement element)
    {
        if (Mode != MotionMode.Full)
        {
            return;
        }
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var blink = compositor.CreateScalarKeyFrameAnimation();
            blink.InsertKeyFrame(0.00f, 1f);
            blink.InsertKeyFrame(0.25f, (float)BlinkFloor());
            blink.InsertKeyFrame(0.50f, 1f);
            blink.InsertKeyFrame(0.75f, (float)BlinkFloor());
            blink.InsertKeyFrame(1.00f, 1f);
            blink.Duration = TimeSpan.FromMilliseconds(400 * Intensity);
            visual.StartAnimation("Opacity", blink);
        }
        catch
        {
        }
    }

    /// <summary>③ 呼吸灯：明暗缓慢循环，full 档常驻（其余档不启动）。</summary>
    public void StartBreathing(UIElement element)
    {
        if (Mode != MotionMode.Full)
        {
            return;
        }
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var breathe = compositor.CreateScalarKeyFrameAnimation();
            breathe.InsertKeyFrame(0.0f, 1f);
            breathe.InsertKeyFrame(0.5f, (float)BreathFloor());
            breathe.InsertKeyFrame(1.0f, 1f, compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.4f, 0f), new System.Numerics.Vector2(0.6f, 1f)));
            breathe.IterationBehavior = AnimationIterationBehavior.Forever; // 常驻循环；收起态由宿主 StopLoops 暂停
            breathe.Duration = TimeSpan.FromMilliseconds(2400 * Intensity);
            visual.StartAnimation("Opacity", breathe);
        }
        catch
        {
        }
    }

    /// <summary>④ Critical 脉冲：缩放 ≤1Hz×intensity（周期钳制 ≥1s，幅度随 intensity）。</summary>
    public void StartPulse(UIElement element, double baseSizeDips)
    {
        if (Mode == MotionMode.Off)
        {
            return;
        }
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.CenterPoint = new System.Numerics.Vector3((float)(baseSizeDips / 2), (float)(baseSizeDips / 2), 0);
            var compositor = visual.Compositor;
            var pulse = compositor.CreateScalarKeyFrameAnimation();
            var peak = (float)(1.0 + 0.35 * Intensity);
            pulse.InsertKeyFrame(0.0f, 1f);
            pulse.InsertKeyFrame(0.5f, peak);
            pulse.InsertKeyFrame(1.0f, 1f, compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.4f, 0f), new System.Numerics.Vector2(0.6f, 1f)));
            pulse.IterationBehavior = AnimationIterationBehavior.Forever;
            pulse.Duration = TimeSpan.FromMilliseconds(1000 * Math.Max(1.0, Intensity)); // ≤1Hz 铁律
            visual.StartAnimation("Scale", pulse);
        }
        catch
        {
        }
    }

    /// <summary>⑤ tile 滑入：首次出现自上 24DIP 落位（full 档）。</summary>
    public void SlideIn(UIElement element)
    {
        if (Mode != MotionMode.Full)
        {
            return;
        }
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var slide = compositor.CreateVector3KeyFrameAnimation();
            slide.InsertExpressionKeyFrame(0f, "Vector3(Offset.X, Offset.Y + 24.0, 0.0)");
            slide.InsertKeyFrame(1f, System.Numerics.Vector3.Zero, compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.1f, 0.9f), new System.Numerics.Vector2(0.2f, 1f)));
            slide.Duration = TimeSpan.FromMilliseconds(220 * Intensity);
            visual.StartAnimation("Offset", slide);
        }
        catch
        {
        }
    }

    /// <summary>⑥ 水波纹（2026-10-10 用户令：进度条反复修不显，换形态）：单环缩放+淡出 forever 循环，
    /// phase ∈ [0,1) 把波形在周期内平移（双环错相即成扩散涟漪）。Composition 线程驱动（60fps 不占 UI 线程）；
    /// off 档不启动（全静止铁律），reduced/full 均可用（状态指示动效，同 Critical 脉冲档位口径）。
    /// 动画失败静默——动效故障不影响静态百分比可读。</summary>
    public void StartWave(UIElement element, double sizeDips, double phase, double periodMs)
    {
        if (Mode == MotionMode.Off)
        {
            return;
        }
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.CenterPoint = new System.Numerics.Vector3((float)(sizeDips / 2), (float)(sizeDips / 2), 0);
            var compositor = visual.Compositor;
            var easing = compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.2f, 0.6f), new System.Numerics.Vector2(0.4f, 1f));

            var scale = compositor.CreateScalarKeyFrameAnimation();
            scale.InsertKeyFrame(0.0f, 0.4f);
            if (phase > 0.01)
            {
                scale.InsertKeyFrame((float)phase, 0.4f); // 相位空闲段：保持收拢不可见
            }
            scale.InsertKeyFrame(1.0f, 1.8f, easing);
            scale.IterationBehavior = AnimationIterationBehavior.Forever;

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0.0f, 0f);
            if (phase > 0.01)
            {
                opacity.InsertKeyFrame((float)phase, 0f);
            }
            var peak = phase + (1.0 - phase) * 0.35;
            opacity.InsertKeyFrame((float)peak, 0.9f);
            opacity.InsertKeyFrame(1.0f, 0f, easing);
            opacity.IterationBehavior = AnimationIterationBehavior.Forever;

            var duration = TimeSpan.FromMilliseconds(Math.Clamp(periodMs, 600, 3000));
            scale.Duration = duration;
            opacity.Duration = duration;
            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Opacity", opacity);
        }
        catch
        {
        }
    }

    /// <summary>停掉某元素上的全部循环（收起态暂停/降级复位用）；一次性过渡自然结束。</summary>
    public void StopLoops(UIElement element)
    {
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.StopAnimation("Opacity");
            visual.StopAnimation("Scale");
            visual.Opacity = 1f;
            visual.Scale = new System.Numerics.Vector3(1f);
        }
        catch
        {
        }
    }

    private double BlinkFloor() => Math.Clamp(0.25 - (Intensity - 1.0) * 0.1, 0.1, 0.35);

    private double BreathFloor() => Math.Clamp(0.55 - (Intensity - 1.0) * 0.2, 0.35, 0.7);
}
