# 仓库开发入口

本目录就是 Unity 工程根目录。先读 `README.md`、`CONTRIBUTING.md`、`docs/ARCHITECTURE.md`、`docs/COMPATIBILITY.md` 和 `docs/BUILDING.md`。当前基线 1.0.10，包含 FlickTailFix；历史版本号命名的 C# 文件仍可能是当前调用链的一部分。

- 默认作者格式对应原版语义。任何包含非官方壳子功能的 commit/PR 标题必须带 `[非官方壳子]`，注明目标壳子、版本、语法与官方兼容性。`timinggroup` 目前未实现，不是官方原有参数，也不是 SkyArea 的 `groupid`。
- 保留手动 Save、四难度草稿、稳定 SourceId、原 token、未知/icp_event、BOM/EOL、冲突检查与失败回滚。不要在打开时迁移、重新导入或自动排序；已识别时间语句在显式保存/导出时稳定排序。
- 新建/续接/切分/默认粘贴 Sky 省略可选 groupid，旧 -1 读兼容；显式自动分组才赋非负 ID。首尾时间容差 ±2ms，仍要求横向接触，不改源时间。长音符段选必须完整包含头尾。
- 采样切分/续接使用最多 128 份的整数坐标比；普通作者编码和原始读取保留既有范围，不能读入即全谱重写。
- IMGUI Layout、输入、Repaint 保持一致控件树；事务经已有队列提交。保护焦点、命中阻挡、撤销边界和输入草稿。
- 相机、StageSpace、舞台分区、Flick 曲线、原图集/音效与渲染裁切只在任务明确涉及它们时修改。保持 99999×1ms 滚速、NoteSkin106 和保调播放规则。
- `StudioBuild08.cs`、`CoreSelfTests.cs`、`LosslessJsonPatch.cs`、`SongMetadataEdit.cs` 包含现有本地修补，不从旧包覆盖。若任务直接涉及它们，基于当前文件修改并验证。
- 不提交 `Library`、`Logs`、音频导入缓存、个人歌曲、历史 Archive 或构建目录。保留 `.meta`、资源字节及源码换行；不顺手升级依赖或批量格式化。
- 维护版本与最新三版公告，不改变产品/公司名称。执行与改动相关的检查；不能把编译、静态图或已有历史报告说成新做的 Player 验收。
- 仅在用户明确要求时使用 Computer Use；默认由用户验证原生界面。开发范围以当次用户要求为准。
