# 萌螈丹药扩展 v0.2.2

适用 RimWorld 1.6。需要 Harmony、MoeLotl Race、MoeLotl: Rigor Mortis，以及它们的前置模组 HAR 和 Custom Quest Framework。

## 安装

1. 下载包含编译程序集的安装包。[下载 v0.2.2 ZIP](https://github.com/pigeonhunter715/moelotl-expansion/releases/download/v0.2.2/moelotl-expansion-v0.2.2-rimworld1.6.zip)
2. 解压后把完整 `MoeLotlExpansion` 文件夹放入游戏的 `Mods` 目录。正确路径是 `Mods/MoeLotlExpansion/About/About.xml`，不要多套一层同名目录。
3. 安装前置模组，在游戏模组菜单中将本扩展排在Harmony、萌螈本体与螈僵扩展之后；依赖本身的先后顺序遵循各自说明。
4. 启用后重启游戏。已有旧版时用新文件夹完整替换旧扩展目录，避免同时启用两份相同packageId。

模组的 `packageId` 为 `pigeonhunter715.moelotl.expansion`。

## 使用

暖脉、清暑、百工和归真在捣药台直接制成，其他常规丹药先配丹剂，再用通天鼎或通天仪炼制。普通炉炼每批需要五份丹剂。太阴丹解锁于传世至宝，使用通天仪和太阴丹胚炼制。服用前请检查游戏内的飞僵蜕变资格，条件不足者服用后会死亡。

药渡玉佩在艺术工作台制作，具备六类、合计60枚、单类25枚的实物药仓。装备后打开其管理面板设置药物与数量。装药优先由佩戴者完成。佩戴者无法补货时，可由其他搬运者补位。仓内丹药免负重。所有自动服药、续药、应急和补货开关初始关闭，补货目标初始为0；仅设数量不会自动开启服药。安魂应急需要主动启用。

旧存档的炉内丹剂和已有炼制进度会保留，可补足五份后接着炼。旧版回气冷却会自动清理。太阴丹所需的螈僵击杀数从安装带有太阴丹的版本后开始统计。升级前可备份老存档。

## 更新与反馈

提交问题时请提供游戏版本、模组列表、复现步骤与 `Player.log`。[查看发行版本](https://github.com/pigeonhunter715/moelotl-expansion/releases) · [提交问题](https://github.com/pigeonhunter715/moelotl-expansion/issues)
