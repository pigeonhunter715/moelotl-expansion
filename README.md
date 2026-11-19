# 萌螈扩展 · MoeLotl Expansion

面向 RimWorld 萌螈（MoeLotl）及螈僵扩展的独立设计与开发项目。

当前重点是十一种新增丹药、药渡玉佩，以及五只活体萌螈加一只螈僵的制药产能和原料供给平衡。

## 当前状态

`dev` 分支已实现十一种丹药：暖脉、清暑、百工、回元、临战、镇煞、纳煞、涤煞、聚煞、归真、安魂。前期三药和归真在捣药台直接制成；其余七药先配丹剂，再由本体通天鼎/通天仪炼制。四种短效炉炼药每批12枚，纳煞、涤煞和安魂每批3枚，配方及工时遵循设计主表。

药渡玉佩具备真实药仓、六格配置、手动用药、独立补货和逐药自动规则。最多6种／60枚，每种25枚；**所有自动开关默认关闭，补货目标默认0**。纳煞／涤煞等维持模式按到期衔接，安魂应急须玩家主动开启。运气丹、回脉丹、夜叉珍珠使用专门适配器。

已在独立 RimWorld **1.6.4871 rev591** 环境执行加载和真实地图集成测试，确认的范围及未验证项目见实现进度。编译、52项纯规则检查和静态配方核验另行运行；不声明完整模组兼容或长期供给平衡已通过游戏实测。

开发和阶段提交仅推送 `dev`；未经用户明确同意，不合并或推送到 `main`。完整状态见 [实现进度](docs/IMPLEMENTATION.md)。

## 构建与验证

本阶段针对 **RimWorld 1.6**，不声明1.7兼容。需要自行准备游戏1.6、萌螈本体与螈僵扩展的1.6程序集、.NET Framework 4.8参考程序集和Roslyn编译器；依赖文件不会复制进构建输出或提交仓库。

1. 将 `build.example.json` 复制为 `build.local.json`，填写本机路径。
2. 在 Windows PowerShell 中运行 `./build.ps1 -RunTests`。编译失败或任何规则测试失败都会终止。
3. 运行 `python tools/validate.py`，验证XML、配方与设计一致性。若有RimSearcher，可追加 `--rimsearcher <rimsearcher.exe路径>` 核对外部Def。
4. 输出为 `1.6/Assemblies/MoeLotlExpansion.dll`。仓库忽略本机构建配置及输出；克隆后需先构建，不能把纯源码目录当作已打包发行版。

也可用已安装的 .NET SDK 构建 `Source/MoeLotlExpansion.csproj`，通过 `RimWorldManagedDir`、`AxolotlDll`、`RigorMortisDll`、`HarmonyDll` 属性配置依赖；当前机器实际验证的是上述Roslyn脚本路径。

丹药现阶段引用依赖模组原有丹药纹理并着色作为开发占位，不复制或重新分发该素材；正式独立图标尚待制作。
药渡玉佩现用原版翡翠图标作为开发占位，无独立穿戴贴图。

## 游戏集成测试

`Tests/RuntimeSmoke.cs` 是单独编译的测试模组，不进入正式扩展DLL。`tools/runtime-smoke.ps1` 使用 `artifacts/runtime/game` 中准备好的隔离1.6运行环境和独立 `artifacts/runtime/save`，运行完成后自动退出。测试所需的游戏文件和依赖由用户自行提供；不要将启动脚本指向正式存档或生产模组目录。详见 [测试说明](docs/RUNTIME_TESTS.md)。

加 `-Functional` 可运行 `Tests/RuntimeFunctional.cs`，覆盖真实取药、喂药、补货、缠风剑与连弩攻击、世界商队等操作。本轮发现的补货目标变更问题已修复，具体结果和覆盖范围见 [功能测试记录](docs/FUNCTIONAL_TEST_REPORT.md)。

## 文档

- [萌螈丹药扩展设计 v0.1](萌螈丹药扩展设计_v0.1.md)：当前设计主文档，含有效参数入口、历史方案区分及第十三节核查结果。

## 依赖与研究资料

设计以 MoeLotl Race 和 MoeLotl: Rigor Mortis 为依赖范围。基础模组、游戏程序集、存档和运行时 Def 快照不包含在本仓库中。主文档中的本地绝对路径是研究环境记录，其他环境需自行配置。

当前工作区共用的 RimSearcher 工具和数据库仍位于父目录的 `.rimsearcher/`，从本目录访问时为 `../.rimsearcher/`；旧诊断资料保留在父目录 `.diagnostics/`。这些是本机资料，不是本仓库的嵌套依赖，单独克隆仓库后不会自动拥有。

## 仓库范围

本目录 `moelotl-expansion/` 是独立 Git 根目录，后续 GitHub 仓库以此目录为范围。RimTalk 预设、SolarFlareShield 和其他同级项目均在仓库范围之外。
