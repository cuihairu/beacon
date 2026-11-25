using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 样式推荐（SET-UX-05，2026-11-24 原型落地批2）：按类型数据能力（WidgetTypeDescriptor.DataKinds）
/// 给出模板候选的推荐排序——兼容项在前（注册表序，首个=推荐档，画廊默认选中位同源），不兼容项
/// 殿后但不再静默剔除：每行带 CanApply 的中文原因（「该组件无百分比数据」），画廊把不兼容行置灰
/// 直显原因。判定与画廊/组件中心共用同一真源（CanApply），推荐不再各写各的。
/// </summary>
public static class WidgetStyleAdvice
{
    /// <summary>单条建议：模板投影 + Ok（可套用）/ Recommended（推荐档=首个兼容项，画廊默认位）/
    /// Reason（不可套用原因；可套用为 null）。</summary>
    public sealed record StyleAdvice(
        string TemplateId,
        string Name,
        string Desc,
        bool Ok,
        bool Recommended,
        string? Reason);

    /// <summary>按能力给全量模板的建议表（兼容优先、不兼容殿后）。<paramref name="surface"/> 默认 tile，
    /// 与画廊主列表口径一致；传 row/detail 即按该面适用性过滤（SET-UX-ROW-2：切位置=清单跟着变）。</summary>
    public static IReadOnlyList<StyleAdvice> List(
        IReadOnlySet<string>? dataKinds,
        WidgetState? state,
        string surface = "tile")
    {
        List<StyleAdvice> applicable = [];
        List<StyleAdvice> blocked = [];
        var recommended = true;
        foreach (var template in DisplayTemplates.All)
        {
            var verdict = DisplayTemplates.CanApply(template, dataKinds, surface, state);
            if (verdict.Ok)
            {
                applicable.Add(new StyleAdvice(template.Id, template.Name, template.Desc, Ok: true, recommended, null));
                recommended = false; // 只标首个兼容项
            }
            else
            {
                blocked.Add(new StyleAdvice(template.Id, template.Name, template.Desc, Ok: false, Recommended: false, verdict.Reason));
            }
        }
        var all = new List<StyleAdvice>(applicable.Count + blocked.Count);
        all.AddRange(applicable);
        all.AddRange(blocked);
        return all;
    }

    /// <summary>能力摘要（画廊目录副题用）：按声明能力给一句中文，未声明回通用句。</summary>
    public static string CapabilityNote(IReadOnlySet<string>? dataKinds)
    {
        if (dataKinds is null || dataKinds.Count == 0)
        {
            return "按类型数据能力筛选：无百分比数据的样式会置灰并给原因。";
        }
        if (dataKinds.Contains(DisplayTemplateKinds.Progress) && dataKinds.Contains(DisplayTemplateKinds.Windows))
        {
            return "该组件产百分比，且含多窗口读数——组合样式可展开各窗口副行。";
        }
        if (dataKinds.Contains(DisplayTemplateKinds.Progress))
        {
            return "该组件产百分比——横条为默认档，圆环/数字卡/水位同样可用。";
        }
        if (dataKinds.Contains(DisplayTemplateKinds.Windows))
        {
            return "该组件含带标签的并列指标（暂无主值百分比）——状态行可用，进度样式置灰。";
        }
        if (dataKinds.Contains(DisplayTemplateKinds.Amount))
        {
            return "该组件只有余额金额，无百分比——只列状态/摘要样式（不显虚假的确定进度）。";
        }
        return "该组件为状态型（状态灯+摘要）——状态行为默认档。";
    }
}
