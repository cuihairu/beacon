[English](declarative-vertical-sample.en.md) | [中文](declarative-vertical-sample.md)

# 可导入样例：GitHub Actions 纵向面（P3-04）

`declarative-vertical-sample.json` 是一份**配置包**（与 设置 → 高级 → 导入/导出配置 同格式），演示声明式组件的一条纵向闭环：同一个 `github.actions.runs` 状态在 L0 tile、L2 面板行、L3 详情三面共用同源事实，并在 L3 详情聚合第二个源（`http.quota` 额度）演示跨源绑定与缺源显示。

## 里面有什么

| 内容 | 说明 |
|---|---|
| `w-sample-ci` | GitHub Actions 运行卡：tile / row / detail 三面全部声明式，detail 聚合额度源 |
| `w-sample-quota` | `http.quota` 额度卡：指向 GitHub 公共 `rate_limit` 端点，无需凭据即可出数 |
| `conn-sample-github` | GitHub 连接：包内只有 credentialRef，导入后在连接里录入 PAT |
| `conn-sample-quota` | HTTP 连接：可替换成任意返回 JSON 的额度接口 |

## 怎么用

1. **先备份**：导入会整体替换五份配置（含动作库）——先在 设置 → 高级 → 导出配置 存一份。
2. 设置 → 高级 → 导入配置，选择本文件。
3. 连接页给 `conn-sample-github` 录入 PAT；把 `w-sample-ci` 的 `repository` / `workflow` 改成你的仓库。
4. 钉选后走三面：tile（L0）→ 面板行（L2）→ 详情（L3），同一状态三处一致。

## 验什么

- [ ] 不改 C#：只编辑 JSON 组件树，保存后对应面立即变化（保存链只重绘，不重新取数）。
- [ ] 三面同源：L0 / L2 / L3 显示同一份 `w-sample-ci` 状态。
- [ ] 两源独立：额度源刷新不带动 CI 源取数；detail 的 QUOTA 区随额度源独立更新。
- [ ] 缺源显示：停用/删除 `w-sample-quota` 后，detail 的 QUOTA 区出现「⚠ 缺少数据源：quota」，不冒充空值、不显示健康旧值；恢复后自动回填。
- [ ] 清空回退：删掉 `components` / `sources` 键保存，三面全部回传统渲染。
- [ ] 断网 / 恢复：断网期间 tile 与详情显示陈旧时间，恢复后自愈。

以上运行时条目需在 Windows 真机验收。CI 结构门由 `DeclarativeSampleTests` 常青保证：树/源校验通过、附加源闭环指向包内组件、全文无密钥语义词。
