# 萌螈扩展 · MoeLotl Expansion

面向 RimWorld 萌螈（MoeLotl）及螈僵扩展的独立设计与开发项目。

当前重点是十一种新增丹药、药渡玉佩，以及五只活体萌螈加一只螈僵的制药产能和原料供给平衡。

## 当前状态

`dev` 分支已实现暖脉、清暑、百工、回元、临战十种丹药（另含纳煞、涤煞、聚煞、归真、安魂）。前期三药在捣药台直接制成；回元、临战先配丹剂，再由本体通天鼎/通天仪炼成每批12枚。回元回复40%上限、每枚至多200，冷却6小时；临战8小时后进入6小时疲劳，均持久化保存。

已针对 RimWorld 1.6.4871 编译并运行规则与静态数据验证；尚未完成游戏加载、存读档和行为实测。五种螈僵丹药及120 ticks贴身喂药已完成源码；镇煞攻击归因和药渡玉佩仍在开发；所有自动开关后续默认关闭、补货目标默认0。

开发和阶段提交仅推送 `dev`；未经用户明确同意，不合并或推送到 `main`。完整状态见 [实现进度](docs/IMPLEMENTATION.md)。

## 构建与验证

本阶段针对 **RimWorld 1.6**，不声明1.7兼容。需要自行准备游戏1.6、萌螈本体与螈僵扩展的1.6程序集、.NET Framework 4.8参考程序集和Roslyn编译器；依赖文件不会复制进构建输出或提交仓库。

1. 将 `build.example.json` 复制为 `build.local.json`，填写本机路径。
2. 在 Windows PowerShell 中运行 `./build.ps1 -RunTests`。编译失败或任何规则测试失败都会终止。
3. 运行 `python tools/validate.py`，验证XML、配方与设计一致性。若有RimSearcher，可追加 `--rimsearcher <rimsearcher.exe路径>` 核对外部Def。
4. 输出为 `1.6/Assemblies/MoeLotlExpansion.dll`。仓库忽略本机构建配置及输出；克隆后需先构建，不能把纯源码目录当作已打包发行版。

也可用已安装的 .NET SDK 构建 `Source/MoeLotlExpansion.csproj`，通过 `RimWorldManagedDir`、`AxolotlDll`、`RigorMortisDll`、`HarmonyDll` 属性配置依赖；当前机器实际验证的是上述Roslyn脚本路径。

丹药现阶段引用依赖模组原有丹药纹理并着色作为开发占位，不复制或重新分发该素材；正式独立图标尚待制作。

## 文档

- [萌螈丹药扩展设计 v0.1](萌螈丹药扩展设计_v0.1.md)：当前设计主文档，含有效参数入口、历史方案区分及第十三节核查结果。

## 依赖与研究资料

设计以 MoeLotl Race 和 MoeLotl: Rigor Mortis 为依赖范围。基础模组、游戏程序集、存档和运行时 Def 快照不包含在本仓库中。主文档中的本地绝对路径是研究环境记录，其他环境需自行配置。

当前工作区共用的 RimSearcher 工具和数据库仍位于父目录的 `.rimsearcher/`，从本目录访问时为 `../.rimsearcher/`；旧诊断资料保留在父目录 `.diagnostics/`。这些是本机资料，不是本仓库的嵌套依赖，单独克隆仓库后不会自动拥有。

## 仓库范围

本目录 `moelotl-expansion/` 是独立 Git 根目录，后续 GitHub 仓库以此目录为范围。RimTalk 预设、SolarFlareShield 和其他同级项目均在仓库范围之外。
