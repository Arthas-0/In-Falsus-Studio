# 贡献与 commit 规范

欢迎提交错误修复、谱面工具和兼容性改进。当前基线为 1.0.10（含 Flick 尾部修正）。先阅读 [README](README.md)、[架构说明](docs/ARCHITECTURE.md)与[格式兼容性](docs/COMPATIBILITY.md)。

## 提交流程

1. 从最新 `main` 建立用途清楚的分支，例如 `fix/sky-join`、`feat/chart-check`；没有写权限时使用 fork 和 Pull Request。
2. 一次 PR 解决一个明确问题。修复、批量格式化、依赖升级和无关美术调整分开提交。
3. 运行与改动相关的检查，记录 Unity 版本、测试步骤与结果。参考谱面使用最小复现，避免提交整首音乐、个人工作谱或机器日志。
4. 更新 `CHANGELOG.md` 的“未发布”条目；影响使用方法或文件格式时同步相关文档。
5. 按 PR 模板描述行为、兼容性和验证情况。若采用 squash，合并后的标题和正文也必须保留非官方扩展标记及说明。

不要提交 `Library`、`Temp`、`Logs`、`UserSettings`、IDE 生成文件、构建输出或 `Assets/InFalsusStudioUser` 音频缓存。新增/移动 Unity 资源必须一起提交对应 `.meta`；不要重新生成已有 GUID。

## Commit 标题

格式：`type(scope): 简短说明`。中文或英文均可，尽量在一行内写清楚具体行为。

| type | 用途 |
| --- | --- |
| `feat` | 新功能 |
| `fix` | 错误修复 |
| `docs` | 文档与公告 |
| `refactor` | 保持行为的内部调整 |
| `perf` | 性能改进 |
| `test` | 检查与回归用例 |
| `build` | 构建与依赖 |
| `ci` | 自动化流程 |
| `chore` | 仓库维护 |

scope 描述模块，如 `sky`、`flick`、`audio`、`save`、`ui`、`chart-check`、`timinggroup`、`repo`。破坏兼容性的提交用 `type(scope)!:`，正文增加 `BREAKING CHANGE:`，说明迁移方法与旧文件处理方式。

```text
fix(sky): 修复重叠 2ms 的首尾连接判定
docs(readme): 补充 Windows 构建步骤
test(save): 覆盖同刻语句稳定排序
```

正文建议写明问题触发方式、修改后的行为、验证命令/步骤及已知限制。不要用“已修复”“更新一下”等无法判断内容的标题。

## 非官方壳子扩展（必填）

以下规则适用于新增、修改、修复或删除非官方壳子专属功能的 **每一个 commit**，不只首次添加功能的提交：

- **标题必须包含 `[非官方壳子]`。** 英文正文可以使用 `Compatibility: nonofficial`，但不能代替这个可直接识别的标题标记。
- 正文必须列出目标壳子名称、可定位的仓库/发行版与已验证版本，不能只写“最新版”。
- 列出新增/改变的语句、参数、单位、默认值和语义，以及保存位置。说明与原版的区别。
- 明确官方本体是否支持、在编辑器中如何显式启用，以及导出为官方格式时的行为；不得静默丢掉扩展语义或自动把未知语句解释为官方功能。
- 给出适用壳子的验证结果和官方模式的回归结果。未验证的部分明确写“未验证”。
- PR 标题、兼容性栏与 `CHANGELOG.md` 同样标注。合并或整理历史时保留这些信息。

示例模板（`timinggroup` 仅为未来扩展示例，不表示当前已支持）：

```text
feat(timinggroup): [非官方壳子] 支持某壳子的独立时间组

Compatibility: nonofficial
Shell: <壳子名称、仓库链接、tag/commit>
Format: <timinggroup 的完整语法、作用域、默认值、时间/滚速语义>
Official compatibility: <不支持哪些语义；原版打开时的明确限制>
Activation: <扩展模式/能力选项；默认关闭>
Official export: <阻止、显式转换或经用户确认的有损导出>
Validation: <壳子版本、复现步骤、官方模式回归；未完成项>
```

**SkyArea 的 `groupid` 与 `timinggroup` 是两种概念。** 不得复用组号偷偷承载时间组语义。规则详见[兼容性说明](docs/COMPATIBILITY.md)。

## 验证要求

按照[构建与检查指南](docs/BUILDING.md)运行相关菜单或批处理。格式、撤销、保存和时间轴变化需要覆盖原始语句保留、重开一致性和失败路径；UI 变化需要人工检查输入焦点、Layout、点击阻挡和窗口缩放；音频变化需要记录实际听感检查。

仅修改文档时核对路径、链接、版本与操作步骤即可。不要把几何数据图、脚本断言或编译成功写成“游戏实测通过”。个人完整日志留在本地，PR 提供必要结果与脱敏片段。

## 版本与公告

维护者发布时同步 `ReleaseIdentity.cs`、`ProjectSettings/ProjectSettings.asset` 中的版本号、`ReleaseNotesCatalog.cs`、`CHANGELOG.md` 和对应 `docs/releases/` 公告。普通功能 PR 先写“未发布”，不要各自抢占版本号。应用公告显示最新三版；保持 `productName` / `companyName`，避免改变用户偏好存储位置。

构建和提交使用相同的源码版本。发布前记录检查、实际验收范围和完整构建目录；非官方扩展在公告中单独注明目标壳子。引入第三方素材/代码时保留原声明，并更新素材与依赖来源记录。
