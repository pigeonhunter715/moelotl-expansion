# 实现进度

开发分支：`dev`。未经用户同意，不合并或推送到 `main`。

## 阶段安排

1. 可构建的 RimWorld 1.6 模组骨架；暖脉丹、清暑丹、百工丹及其捣药台配方；持久药效、目标校验与验证工具。
2. 回元、临战和螈僵煞气相关丹药；丹剂/炼丹设备接入；耗气攻击散煞的完整事件归因。
3. 药渡玉佩真实储药、手动使用与补货、默认关闭的自动策略，以及狂暴应急和远行持久化。
4. 独立游戏环境中的加载、存读档、行为与产能验证；完善美术、翻译和兼容。

## 当前阶段：阶段1源码完成，游戏验证待做

实现时间：2026-11-19。

| 功能 | 实现状态 |
|---|---|
| 1.6模组结构与依赖 | About、LoadFolders、源码项目、可配置离线构建脚本完成 |
| 暖脉丹 | 桃木5＋草药2→5枚；工作量1200、手工2；最低舒适温度－15℃、2天 |
| 清暑丹 | 阴沉木5＋草药2→5枚；工作量1200、手工2；最高舒适温度＋15℃、2天 |
| 百工丹 | 精神叶6＋草药2＋玉琼粉10→5枚；工作量6000、手工4；全局工作×1.2、研究额外×1.1、1天 |
| 制作接入 | 三种配方均在萌螈捣药台，需镶金翠竹研究，使用手工技能 |
| 使用与数量 | UseItem等待100 ticks，完成时复核活体萌螈和库存；失败不扣药，成功只销毁1枚；无独立DestroySelf造成双重消耗 |
| 效果时长 | 使用游戏绝对ticks和Scribe字段；重复服用刷新到当前时间＋完整时长，强度不叠加 |
| 图标 | 临时引用萌螈本体现有丹药纹理并着色，未复制第三方素材 |
| 后续内容 | 剩余8药、炉炼、喂药和玉佩尚未实现，不生成无效果的占位物品 |

## 已执行检查

- 使用本地RimWorld **1.6.4871 rev590**的Assembly-CSharp及UnityEngine.CoreModule，配合已安装本体/螈僵的1.6程序集，编译MoeLotlExpansion.dll；警告按错误处理。
- `build.ps1 -RunTests`：13项规则检查通过，包括错误种族/死亡/螈僵拒用、到期边界、跨时间跳跃、重复刷新不累加和无效时长拒绝。
- `tools/validate.py --rimsearcher ...`：3种物品、3种药效、3种配方与设计一致；XML解析、翻译键及17项运行时Def引用通过。
- 核对已有纹理路径存在，且源码输出不携带依赖DLL。

以上不是游戏实测。原Steam游戏目录已是 **1.7.5013**，不能用它代替1.6验证；独立1.6程序集用于本阶段编译。玩家正式存档、模组启用列表和创意工坊文件未被修改。

## 接口依据

- RimSearcher快照：`Axolotl_GrindingTable`、`Axolotl_Research_BasePlant`、`MakeAxolotlJadeEssencePowder`、`Axolotl_LotlqiPellet`、`RM_AxoPeachWood`、`RM_OvercastWood`、`ComfyTemperatureMin/Max`、`WorkSpeedGlobal`、`ResearchSpeed`。
- 1.6游戏反编译：`CompUsable.CanBeUsedBy/UsedBy`、`JobDriver_UseItem`、`Hediff.ExposeData/ShouldRemove/TryMergeWith`、`Pawn_HealthTracker.AddHediff/Notify_HediffChanged`、`HediffStage.statOffsets/statFactors`。
- 依赖反编译：`AxolotlExtension.IsMoeLotl`、`RMUtility.IsZombie`；前者以种族身体定义识别，后者读取mutant扩展，而非猜测角色名称。
- 研究速度的运行时 `statFactors` 包含 `WorkSpeedGlobal`，故百工的综合研究倍率为1.2×1.1。

## 待游戏验证

1. 在独立1.6测试配置加载依赖与扩展，检查XML交叉引用/类型加载日志、捣药台账单与研究门槛。
2. 未开脉活体萌螈服用三种药，核对舒适温度和工作/研究属性；普通人、死亡目标、螈僵不能误用。
3. 单颗及25枚堆叠使用、动作中断、重复刷新和同时冷热药，确认数量与强度。
4. 保存读取、旅行、离图和重新加载验证时长；纯规则测试不替代这些运行时集成测试。
5. 观察售卖、劣化和财富；现行市值沿用设计，交易平衡仍待验证。
