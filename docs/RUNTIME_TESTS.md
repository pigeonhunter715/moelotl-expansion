# 构建、打包与实机测试

## 构建与打包

源码构建需要 RimWorld 1.6、Harmony、萌螈本体和螈僵扩展的程序集，以及 .NET Framework 4.8 参考程序集和 Roslyn。将 `build.example.json` 复制为 `build.local.json`，填写构建依赖的路径。

- 在 PowerShell 中运行 `./build.ps1 -RunTests`，输出 `1.6/Assemblies/MoeLotlExpansion.dll`。
- 运行 `python tools/validate.py` 检查 Def、配方与设计一致性。可追加 `--rimsearcher <rimsearcher.exe 路径>`，核对外部 Def。
- 运行 `./tools/package-release.ps1` 生成安装 ZIP、SHA-256 校验文件与逐文件清单。输出位于 `artifacts/releases/`。

也可用 .NET SDK 构建 `Source/MoeLotlExpansion.csproj`，通过 `RimWorldManagedDir`、`AxolotlDll`、`RigorMortisDll`、`HarmonyDll` 配置依赖。

## 准备隔离测试

`-mx-smoke` 和 `-mx-functional` 会启动独立编译的测试模组，创建测试角色、物品和存档。请在隔离游戏目录运行。

1. 配好 `build.local.json`，运行 `./build.ps1 -RunTests`。
2. 在忽略目录 `artifacts/runtime/game` 放置合法的 RimWorld 1.6 可执行文件及必要根目录文件。`Data`、`RimWorldWin64_Data`、`MonoBleedingEdge` 可连接到独立1.6游戏安装。不可误连Steam自动升级后的1.7。
3. 在该目录的 `Mods` 中只连接 Harmony、Humanoid Alien Races、Custom Quest Framework、萌螈本体、螈僵扩展和本项目。不复制或提交依赖DLL。
4. 在 `artifacts/runtime/save/Config/ModsConfig.xml` 创建专用启用列表，顺序为Harmony、Core及所需DLC、HAR、CQF、本体、螈僵、本扩展。测试脚本会追加测试模组。
5. 运行 `./tools/runtime-smoke.ps1`。脚本默认使用仓库的 `artifacts/runtime`，设置该测试配置允许后台运行、静音，并隐藏启动窗口。测试使用正常图形设备；`-nographics` 会引起原版图集初始化错误，不作为加载通过依据。

发行包验收可加 `-ReleasePackage`。在 `artifacts/runtime-release` 按上述结构准备独立环境，把实际 ZIP 解压到 `game/Mods`，使用其中的 DLL 编译测试模组。功能测试与常规回归依次启动，等前一个进程退出后再启动下一个。

无 DLC 回归可运行 `./tools/runtime-smoke.ps1 -CoreOnly`。它从 `save/Config` 复制配置到独立的 `save-core-only/Config`，只修改副本中的 DLC 启用列表和已知扩展，不改主测试配置。

结果文件为 `artifacts/runtime/save/mx-smoke.txt`，完整引擎日志为 `artifacts/runtime/smoke.log`。成功必须出现 `COMPLETE`，不能仅凭进程启动或日志中没有扩展名称判断通过。测试退出后才可重新编译，否则Unity映射DLL会使编译器无法覆盖输出。

目录含junction；**不要直接递归删除整个隔离目录**，应先明确确认并移除连接本身，避免误操作原安装。测试存档和日志全部被Git忽略。

## 覆盖范围

- 真正加载Def与Harmony补丁，生成活体萌螈和己方绿僵。
- 服药精确消耗一枚、温度属性、回气数值与连续服用。
- 动态纳煞容量、80%涤煞、归真去涤煞与珍珠且保留当前煞气、聚煞实际补量。
- 用测试攻击范围调用真实的耗气和受击接口，区分未耗气／已耗气命中、同动作重复伤害与角色对冷却。**不等于所有本体武器、弹丸和第三方技能都已经完成实战验收。**
- 两种原炉的真实丹剂扣料和成品生成；功能轮次还在地图上连接四根精粹之柱，通过本体进度方法按60 ticks间隔推进一炉完整回气丹批次，记录单批耗时与燃料余量。该炉速检查不推进地图时钟，也不验证四炉长期不断供。
- 实物药仓、单类 25 枚上限、六类预留限制、仓内丹药免负重及市值。
- 写入真实 `.rws` 后重载，检查角色与装备引用、药仓实物、目标设置、时钟和待结算请求；完成后只扣一枚。
- 狂暴时已授权的安魂送药与即时清煞、恢复控制、后续聚煞／珍珠暂停。清煞在结算瞬间观察；本体随后自然增长不视为未清零。
- 纳煞维持模式按旧药效准确到期续服；实际搬运工作补至目标；界面截图。

## 仍需手工或专项验收

四炉360天离散批次供给模型见 `tools/production-sim.py` 和 `docs/PRODUCTION_COMPAT_REPORT.md`；这是给定供料、燃料和80%开工率下的模拟，不是完整殖民地实战。仍需验证其他武器／技能／飞僵倍率、弹丸跨存档、追逐喂药、缺料及被预定库存、更多多人竞争组合、尸体与运输载具、原药全部适配条件、不同分辨率和中英界面，以及临战完整18小时周期、纳煞10天、长期四炉实际供料与交易经济。

规则可执行程序、XML校验和游戏集成日志是三种独立证据。提交说明应写出本次实际通过的项，不将待验收清单描述为已经通过。

## 扩展功能测试

运行 `./tools/runtime-smoke.ps1 -Functional`，启用独立的 `Tests/RuntimeFunctional.cs`。该模式在真正的游戏地图与tick循环中下达 UseItem、喂药和补货工作，并使用本体缠风剑近战、濯枝连弩连射；不注入镇煞攻击范围。它还建立真实世界商队检查离图用药。

该模式成功标志是 `FUNCTIONAL_COMPLETE`；任一断言失败写入 `FUNCTIONAL_FAIL` 后退出。每次启动保留上一轮结果和引擎日志，避免覆盖失败证据。测试角色的等级、武器与物品由测试装置准备，测试场地转为无屋顶的普通土地；螈僵日晒伤害仅对测试目标禁用，以免与待验证机制混淆。攻击仍走本体命中、伤害、消耗及弹丸流程。

补货目标变更场景使用玩家下达的工作，专门覆盖 `TryTakeOrderedJob` 自动设定 `playerForced=true` 的行为。“手动装一枚”使用独立 `MX_LoadPendantOne` JobDef；普通目标补货即使由玩家下达，也必须在交付时按最新目标重算，不能将强制工作误认为不受数量目标约束。

安魂专项使用本体 `Axolotl_MoveFlyer`：自动请求建立后起跳，该次测试飞行延长到240 ticks；检查递送期限没有重置、空中只消费一枚并清煞、落地仍受控。另验证普通离图仍取消待送药。冒烟模式保留递送开始／清除诊断，便于区分真正失败与随机跳跃场景。
