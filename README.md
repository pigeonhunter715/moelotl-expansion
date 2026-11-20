# 萌螈扩展 · MoeLotl Expansion

适用于 RimWorld 1.6 的萌螈与螈僵炼丹扩展。

## 安装

当前发行版为 **v0.2.1**。下载 ZIP，将解压后的 `MoeLotlExpansion` 文件夹放入游戏的 `Mods` 目录。安装包内含编译好的模组程序集。[下载 RimWorld 1.6 安装包](https://github.com/pigeonhunter715/moelotl-expansion/releases/download/v0.2.1/moelotl-expansion-v0.2.1-rimworld1.6.zip)

前置模组包括 Harmony、MoeLotl Race、MoeLotl: Rigor Mortis 及它们依赖的 HAR 和 Custom Quest Framework。[完整安装与升级说明](docs/INSTALL.md)

## 模组内容

- 十二种丹药，涵盖御寒、清暑、回气、战斗、劳作与螈僵控煞。捣药台提供单份和五倍配方，普通丹炉每炉使用五份丹剂。
- 太阴丹需要在通天仪中炼制一年。符合资格的苏醒飞僵服用后可蜕变为太阴仙体。[太阴丹配方与服用资格](docs/TAIYIN_PILL.md)
- 药渡玉佩可收纳六类丹药，仓内丹药免负重。玉佩按品质提供减伤，符合条件的活体萌螈还能获得螈气护盾。自动服药、续药、应急和补货默认关闭。

## 开发与资料

将 `build.example.json` 复制为 `build.local.json` 并填写依赖路径。使用 `./build.ps1 -RunTests` 构建和执行规则测试，使用 `python tools/validate.py` 校验 Def 和配方。[构建、打包与实机测试](docs/RUNTIME_TESTS.md)

- [丹药设计与配方](萌螈丹药扩展设计_v0.1.md#当前配方主表)
- [更新记录](CHANGELOG.md) · [发行检查](docs/RELEASE_0.2.1.md) · [实现进度](docs/IMPLEMENTATION.md)
- [贴图总览](docs/art/art-preview.png) · [制作记录](docs/ART_DIRECTION.md) · [素材与依赖说明](THIRD_PARTY_NOTICES.md)
