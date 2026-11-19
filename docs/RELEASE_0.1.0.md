# 萌螈丹药扩展 v0.1.0 发行检查

适用 RimWorld 1.6。安装文件为 `moelotl-expansion-v0.1.0-rimworld1.6.zip`，大小 1,620,253 字节，SHA256 为 `aec315f97463fb677ef2b6e5a0ef15f51690219bc8c0629609178ca09dac504f`。解压后，`MoeLotlExpansion` 文件夹可直接放进 `Mods`。[安装说明](INSTALL.md)

| 检查 | 验证结果 |
|---|---|
| 构建与规则 | 使用 RimWorld 1.6 和当前依赖程序集编译，52 项规则断言通过。 |
| 数据 | 11 种丹药、7 种丹剂、配方与效果、XML 及中英界面键通过静态检查。 |
| 发行文件 | 29 个文件解压后逐一核对 SHA256。唯一的 DLL 是本扩展程序集。 |
| 生产排程 | 五螈一螈僵、四座各配四柱的通天仪、80% 开工率及持续供料下，360 天压力模型各药缺货次数均为零。 |

游戏内验收使用独立的 RimWorld 1.6.4871 rev591、五个官方 DLC、Harmony、HAR、Custom Quest Framework、萌螈本体与螈僵扩展。游戏从上述 ZIP 的解压目录加载本扩展，测试模组单独编译。

详见[游戏内测试方法](RUNTIME_TESTS.md)和[生产与兼容矩阵](PRODUCTION_COMPAT_REPORT.md)。无 DLC 组合的 72 项回归来自此前的兼容测试。长期殖民地实际搬运与交易供给仍待验证。
