# 萌螈丹药扩展 v0.1.0

适用 RimWorld 1.6。需要 Harmony、MoeLotl Race、MoeLotl: Rigor Mortis，以及它们要求的前置模组（含 HAR、Custom Quest Framework）。不支持直接用于 RimWorld 1.7。

## 安装

1. 下载 `moelotl-expansion-v0.1.0-rimworld1.6.zip`，不要用GitHub自动生成的Source code压缩包代替：源码包没有编译DLL。
2. 解压后把完整 `MoeLotlExpansion` 文件夹放入游戏的 `Mods` 目录。正确路径是 `Mods/MoeLotlExpansion/About/About.xml`，不要多套一层同名目录。
3. 安装前置模组，在游戏模组菜单中将本扩展排在Harmony、萌螈本体与螈僵扩展之后；依赖本身的先后顺序遵循各自说明。
4. 启用后重启游戏。已有旧版时用新文件夹完整替换旧扩展目录，避免同时启用两份相同packageId。

本扩展packageId：`pigeonhunter715.moelotl.expansion`。

## 使用

十一种丹药：暖脉、清暑、百工、回气、临战、镇煞、纳煞、涤煞、聚煞、归真、安魂。前期三药与归真在捣药台直接制作，其余七药先配丹剂，再用通天鼎／通天仪炼制。使用受种族、脉络、研究及状态条件限制，详情见物品说明。

药渡玉佩在艺术工作台制作，具备六类、合计60枚、单类25枚的实物药仓。装备后打开其管理面板设置药物与数量。所有自动服药、续药、应急和补货开关初始关闭，补货目标初始为0；仅设数量不会自动开启服药。安魂应急需要主动启用。

回气丹沿用旧版 `MX_Recovery*` 内部标识，改名不会主动替换存档中的Def引用。长期存档或复杂Mod组合请保留升级前存档备份；已有功能的保存／读取覆盖不等于所有组合都经过验证。

## 发行范围

包含本扩展编译DLL、Def、中文及英文界面键、十一药与玉佩独立PNG。物品说明目前以中文为主；七种丹剂引用前置模组纹理，玉佩没有人物腰间穿戴图。无需安装Python、ComfyUI或SDXL即可游玩。

已在隔离1.6环境执行规则、功能和回归检查；具体版本及覆盖范围见仓库发行记录。未承诺RimWorld 1.7、全部第三方组合、完整殖民地长期供给或商业交易平衡。

项目与更新：https://github.com/pigeonhunter715/moelotl-expansion
反馈请提供游戏版本、模组列表、复现步骤和Player.log。
