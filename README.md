# 萌螈扩展 · MoeLotl Expansion

面向 RimWorld 萌螈（MoeLotl）及螈僵扩展的独立设计与开发项目。

当前重点是十一种新增丹药、药渡玉佩，以及五只活体萌螈加一只螈僵的制药产能和原料供给平衡。

## 安装发行版

当前版本 **v0.1.0，RimWorld 1.6**。从 [GitHub Releases](https://github.com/pigeonhunter715/moelotl-expansion/releases) 下载 `moelotl-expansion-v0.1.0-rimworld1.6.zip`，解压到游戏 `Mods` 目录。发行ZIP包含已编译DLL；GitHub自动提供的Source code包只包含源码，需要自行构建。

依赖Harmony、MoeLotl Race、MoeLotl: Rigor Mortis及其前置模组。完整步骤和首次用药设置见 [安装说明](docs/INSTALL.md)。本扩展不支持RimWorld 1.7。

## 当前功能

十一种丹药与七种丹剂、捣药台／炉炼流程，以及药渡玉佩的六格实物药仓。所有自动服药、续药、应急和补货功能默认关闭，补货目标默认为0。十一药与玉佩已使用第二版独立贴图，七种丹剂引用前置模组现有纹理，暂无人物腰间穿戴图。

已执行隔离RimWorld 1.6.4871测试，覆盖规则、真实取药与喂药、补货竞争、镇煞攻击及商队使用等场景。版本记录、实际覆盖和局限见 [发行检查](docs/RELEASE_0.1.0.md) 与 [实现进度](docs/IMPLEMENTATION.md)。

## 构建与验证

本阶段针对 **RimWorld 1.6**，不声明1.7兼容。需要自行准备游戏1.6、萌螈本体与螈僵扩展的1.6程序集、.NET Framework 4.8参考程序集和Roslyn编译器；依赖文件不会复制进构建输出或提交仓库。

1. 将 `build.example.json` 复制为 `build.local.json`，填写本机路径。
2. 在 Windows PowerShell 中运行 `./build.ps1 -RunTests`。编译失败或任何规则测试失败都会终止。
3. 运行 `python tools/validate.py`，验证XML、配方与设计一致性。若有RimSearcher，可追加 `--rimsearcher <rimsearcher.exe路径>` 核对外部Def。
4. 输出为 `1.6/Assemblies/MoeLotlExpansion.dll`。仓库忽略本机构建配置及输出；克隆后需先构建，不能把纯源码目录当作已打包发行版。

也可用已安装的 .NET SDK 构建 `Source/MoeLotlExpansion.csproj`，通过 `RimWorldManagedDir`、`AxolotlDll`、`RigorMortisDll`、`HarmonyDll` 属性配置依赖；当前机器实际验证的是上述Roslyn脚本路径。

十一药与药渡玉佩使用512×512 RGBA贴图。制作过程、传统纹样参考与AI辅助参数见 [美术记录](docs/ART_DIRECTION.md)、[贴图总览](docs/art/art-preview.png)及 [素材说明](THIRD_PARTY_NOTICES.md)。

运行 `./tools/package-release.ps1` 可构建、执行规则／静态校验并生成发行ZIP、SHA256和逐文件清单。发布文件采用白名单，不包含游戏依赖DLL、本机配置、测试模组或存档。输出位于忽略的 `artifacts/releases/`。

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
