import { defineConfig } from 'vitepress'

const englishNav = [
  { text: 'Overview', link: '/' },
  {
    text: 'Product',
    items: [
      { text: 'Mission', link: '/mission' },
      { text: 'Positioning', link: '/positioning' },
      { text: 'Master Plan', link: '/master-plan' },
      { text: 'Solution Structure', link: '/solution-structure' },
    ],
  },
  {
    text: 'Technical',
    items: [
      { text: 'RFC-001 Technical Design', link: '/rfc/RFC-001-technical-design' },
      { text: 'MVP Issues', link: '/mvp-issues' },
      { text: 'Configuration Reference', link: '/configuration' },
      { text: 'Troubleshooting', link: '/troubleshooting' },
    ],
  },
  {
    text: 'Delivery',
    items: [
      { text: 'Store Submission', link: '/store-submission' },
      { text: 'Privacy Policy', link: '/privacy-policy' },
      { text: 'Acceptance Checklist', link: '/acceptance-checklist' },
    ],
  },
  {
    text: 'Quality',
    items: [
      { text: 'Documentation Consistency Audit', link: '/审计-文档一致性' },
      { text: 'B-705 L0 Behavior Audit', link: '/acceptance-B705-L0-behavior' },
      { text: 'B-803 End-to-End Walkthrough', link: '/acceptance-B803-e2e-walkthrough' },
    ],
  },
]

const chineseNav = [
  { text: '概览', link: '/zh/' },
  {
    text: '产品',
    items: [
      { text: '任务书', link: '/zh/mission' },
      { text: '定位与首批场景', link: '/zh/positioning' },
      { text: '产品与技术实施总方案', link: '/zh/master-plan' },
      { text: 'Solution 与项目目录', link: '/zh/solution-structure' },
    ],
  },
  {
    text: '技术',
    items: [
      { text: 'RFC-001 技术方案', link: '/zh/rfc/RFC-001-technical-design' },
      { text: 'MVP Issue 列表', link: '/zh/mvp-issues' },
      { text: '配置项参考', link: '/zh/configuration' },
      { text: '故障排查', link: '/zh/troubleshooting' },
    ],
  },
  {
    text: '交付',
    items: [
      { text: '商店提交前置清单', link: '/zh/store-submission' },
      { text: '隐私政策', link: '/zh/privacy-policy' },
      { text: '装机验收执行清单', link: '/zh/acceptance-checklist' },
    ],
  },
  {
    text: '质量',
    items: [
      { text: '文档一致性审计', link: '/zh/审计-文档一致性' },
      { text: 'B-705 L0 行为审计', link: '/zh/acceptance-B705-L0-behavior' },
      { text: 'B-803 端到端走查', link: '/zh/acceptance-B803-e2e-walkthrough' },
    ],
  },
]

const englishSidebar = [
  {
    text: 'Overview',
    items: [{ text: 'Home', link: '/' }],
  },
  {
    text: 'Product',
    items: [
      { text: 'Mission', link: '/mission' },
      { text: 'Positioning and First Scenarios', link: '/positioning' },
      { text: 'Product and Technical Master Plan', link: '/master-plan' },
      { text: 'Solution and Project Layout', link: '/solution-structure' },
    ],
  },
  {
    text: 'Technical',
    items: [
      { text: 'RFC-001 Technical Design', link: '/rfc/RFC-001-technical-design' },
      { text: 'MVP Issue List', link: '/mvp-issues' },
      { text: 'Configuration Reference', link: '/configuration' },
      { text: 'Troubleshooting', link: '/troubleshooting' },
    ],
  },
  {
    text: 'Delivery',
    items: [
      { text: 'Store Submission Checklist', link: '/store-submission' },
      { text: 'Privacy Policy', link: '/privacy-policy' },
      { text: 'Acceptance Execution Checklist', link: '/acceptance-checklist' },
    ],
  },
  {
    text: 'Quality',
    items: [
      { text: 'Documentation Consistency Audit', link: '/审计-文档一致性' },
      { text: 'B-705 L0 Behavior Audit', link: '/acceptance-B705-L0-behavior' },
      { text: 'B-803 End-to-End Walkthrough', link: '/acceptance-B803-e2e-walkthrough' },
    ],
  },
]

const chineseSidebar = [
  {
    text: '概览',
    items: [{ text: '首页', link: '/zh/' }],
  },
  {
    text: '产品',
    items: [
      { text: '任务书', link: '/zh/mission' },
      { text: '定位与首批场景', link: '/zh/positioning' },
      { text: '产品与技术实施总方案', link: '/zh/master-plan' },
      { text: 'Solution 与项目目录', link: '/zh/solution-structure' },
    ],
  },
  {
    text: '技术',
    items: [
      { text: 'RFC-001 技术方案', link: '/zh/rfc/RFC-001-technical-design' },
      { text: 'MVP Issue 列表', link: '/zh/mvp-issues' },
      { text: '配置项参考', link: '/zh/configuration' },
      { text: '故障排查', link: '/zh/troubleshooting' },
    ],
  },
  {
    text: '交付',
    items: [
      { text: '商店提交前置清单', link: '/zh/store-submission' },
      { text: '隐私政策', link: '/zh/privacy-policy' },
      { text: '装机验收执行清单', link: '/zh/acceptance-checklist' },
    ],
  },
  {
    text: '质量',
    items: [
      { text: '文档一致性审计', link: '/zh/审计-文档一致性' },
      { text: 'B-705 L0 行为审计', link: '/zh/acceptance-B705-L0-behavior' },
      { text: 'B-803 端到端走查', link: '/zh/acceptance-B803-e2e-walkthrough' },
    ],
  },
]

export default defineConfig({
  base: '/beacon/',
  title: 'Beacon',
  description: 'Windows developer status and action center — Signal + Action, not a dashboard',
  ignoreDeadLinks: false,
  head: [['link', { rel: 'icon', type: 'image/svg+xml', href: '/beacon/img/logo.svg' }]],
  locales: {
    root: {
      label: 'English',
      lang: 'en-US',
      title: 'Beacon',
      description: 'Windows developer status and action center — Signal + Action, not a dashboard',
      themeConfig: {
        nav: englishNav,
        sidebar: englishSidebar,
        outline: { level: [2, 3], label: 'On this page' },
        docFooter: { prev: 'Previous page', next: 'Next page' },
        lastUpdated: { text: 'Last updated' },
      },
    },
    zh: {
      label: '简体中文',
      lang: 'zh-CN',
      link: '/zh/',
      title: 'Beacon',
      description: 'Windows 开发者状态与操作中心——Signal + Action，不是 Dashboard',
      themeConfig: {
        nav: chineseNav,
        sidebar: chineseSidebar,
        outline: { level: [2, 3], label: '本页目录' },
        docFooter: { prev: '上一页', next: '下一页' },
        lastUpdated: { text: '最后更新' },
      },
    },
  },
  themeConfig: {
    logo: '/img/logo.svg',
    siteTitle: 'Beacon',
    socialLinks: [{ icon: 'github', link: 'https://github.com/cuihairu/beacon' }],
    search: { provider: 'local' },
  },
})
