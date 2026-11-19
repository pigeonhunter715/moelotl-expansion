# 丹药纹样方案：乳白丹体与中国传统纹样

更新：2026-11-19。用户确定全部丹药改用乳白色丹体，暖脉丹参考火焰纹、清暑丹参考水纹。本文件整理其余配套纹样及绘制约束，优先于 ART_DIRECTION.md 中首版的彩色丹体方案。

本轮完成馆藏文字资料核对与纹样选型；没有下载、描摹馆藏图片，没有生成第二版贴图。现行游戏 PNG 和生成脚本仍是首版，需要后续按此表重绘。下列英文只描述可见形状，不把药名、纹样专名或游戏机制交给绘图模型猜测。

## 统一画法

- 丹体：乳白底色 `#F3ECDD`，亮部 `#FFF9EE`，暖灰阴影 `#C8BDAA`；阴影用量克制，不能变成整颗金黄或灰色。
- 表面：光滑、略带蜡质，保留粗深色外轮廓与右上高光；纹样是贴合球面的彩色药纹，不画成金属镶嵌或贴上的标签。
- 色彩：整颗仍以乳白为主，纹样约占可见丹体面积的15%—25%；每药一种主纹色，最多少量同色明暗变化。
- 构图：保留首版原创折纸底托与视角。一个主纹样即可，不能叠满文字、八卦、符箓、花边；丹体右上留出高光空白。
- 可读性：先按64px最终物品图检查，主笔画目标约2—3px、间隙至少1px，再反推大图尺寸。32px检查轮廓和主色，不要求盘长的穿插细节全部可见。这是待验证目标，不能视为已经通过验收。
- 轮廓分工：暖脉尖焰、清暑横波、百工方结、回气舒云、临战尖纹、镇煞直剑、纳煞瓶腹、涤煞闭环、聚煞内卷、归真双旋、安魂展开花瓣。颜色相近的丹药必须靠形状区分。

## 按实际药效选型

“传统依据”只说明资料确证的纹样或含义；与具体药效的对应属于本模组设计，并非古代药方或纹样固有功效。

| 丹药与用途 | 首选纹样 | 传统依据与游戏转译 | 纹色／简化构图 |
| --- | --- | --- | --- |
| 暖脉丹：耐寒，舒适温度下限降低15°C，2天 | 火焰纹 | 按用户指定，以火焰表达温暖。本轮未取得能直接支持该具体画法的馆藏说明，不附会十二章、明德或治疗寒伤等含义 | 朱红 `#B84A32`；一高两低、向上收尖的三瓣火焰，内部留一道乳白弯隙 |
| 清暑丹：耐热，舒适温度上限提高15°C，2天 | 水波纹 | 马家窑彩陶水波纹是河水的图案化表现[S1]；流动清凉是本作功能联想 | 靛青 `#286B8B`；两至三道横向缓波，避免画成飞溅巨浪 |
| 百工丹：工作速度+20%、研究额外+10%，1天 | 简化盘长纹 | 盘长属于八吉祥；故宫释义含“回环贯彻，一切通明”[S2]，另有玉器实物[S3]。借连续穿插表达思路贯通、手工熟练，不宣称传统纹样代表生产加速 | 赭金 `#95652B`；圆角方形回环带，保留两处明确上下穿插，减少外圈结耳 |
| 回气丹：恢复40%最大螈气，至多200，冷却6小时 | 云纹，横向舒展 | 玉云纹珌提供传统云纹的实物依据[S8]；以舒展流动的云气表达补充螈气，是本作转译，不宣称云纹具有传统补气功效 | 青绿 `#397559`；一组横向舒展的圆润云头，接两条短飘带，保留乳白空隙；区别于聚煞丹的三股向心勾云 |
| 临战丹：战斗增强8小时，随后疲劳6小时 | 虎纹，提炼为虎额与颊侧纹 | 布老虎有驱邪避灾、护佑平安的民俗依据[S5]；猛兽的力量感用于本作战斗识别。战斗加成及疲劳不是传统寓意 | 赤褐 `#814735`；三道宽而尖的对称虎斑，中央向下，侧面斜展；不画虎脸、眼睛或“王”字 |
| 镇煞丹：耗气攻击降低目标螈僵煞气，12小时 | 暗八仙中的宝剑，配极少短云线 | 暗八仙以器物代人，宝剑在其中，资料提及逢凶化吉[S6]；转译为攻击时镇煞。不画成服药者自身自动降煞 | 铁青 `#395E69`；一柄短直剑、短横护手、两侧短弧；优先保留剑形 |
| 纳煞丹：稳定煞气容量×1.5，10天，当前量不变 | 宝瓶纹 | 宝瓶属于八吉祥，资料释义“福智圆满，具完无漏”[S2]；由容器的大腹与收口转译为增加容纳能力，不宣称古义为储存煞气 | 深紫 `#6D5289`；窄颈、宽腹、收口的小瓶轮廓，腹部留乳白，不画液面或向内吸气 |
| 涤煞丹：10天内超过实时容量80%的部分自动削减 | 回纹环带 | 回纹边饰有馆藏实物[S7]；把它改编成闭合约束环，是本作表达上限的设计。传统回纹不等于“80%封顶” | 深蓝 `#3E587D`；闭合圆环内置四处短直角回折，中心留白；不画80%进度条，也不画清空符号 |
| 聚煞丹：增加至多300煞气，受90%／涤煞80%阈值限制 | 勾云纹，向心重组 | 玉云纹珌有阴刻勾云纹[S8]；三股朝内汇聚是新编构图，不把祥云解释成邪恶或煞气的传统象征 | 梅紫 `#814B78`；三枚粗逗点状卷云围绕中心，尖端向内，留小块乳白中心 |
| 归真丹：解除涤煞丹和夜叉珍珠效果，保留当前煞气与纳煞 | 太极纹 | 太极图有绘画实物依据[S9]；以相接回转表达解除人为约束、回到自然流转，是本作解释。药效并非平衡阴阳数值，也不清零煞气 | 墨青 `#3D6262`；一深一乳白的双旋形，用S形分界，保留两小眼；不加八卦圈 |
| 安魂丹：己方狂暴螈僵清空煞气、结束狂暴、恢复控制 | 展开莲瓣纹 | 莲花为传统吉祥纹饰，八宝解释含“出五浊世无所染”[S2]；以舒展莲瓣表现安定与清净。恢复游戏控制属于新设定 | 藕紫 `#856082`；三至五枚向上展开的尖圆花瓣，中央最高、两侧低，不加莲台圈或繁复花蕊 |

纳煞选择宝瓶而非葫芦：馆藏金葫芦说明着重瓜瓞绵绵、子孙繁衍[S10]，与容量的直接联系较弱。葫芦可以作为备用容器轮廓，但不能写成其传统寓意就是“吸纳煞气”。

## 给绘图模型的形状描述

共同前缀：`A single milky ivory round medicinal pellet, smooth waxy surface, heavy charcoal outer contour, broad flat shading, small white highlight on the upper right, resting on a folded beige paper square, slightly elevated view, simple hand-drawn 2D inventory icon.`

共同约束：`The colored motif is a simple flat surface marking that follows the sphere. Keep most of the pellet ivory. No lettering, no metal inlay, no face, no scenery, no fine ornamental clutter.` 这些是意图描述，不能代替实际缩略图检查。结合原创几何底稿约束形状，再用低强度图生图统一质感。

| 丹药（只作索引，不进入提示词） | 添加的可见形状描述 |
| --- | --- |
| 暖脉 | `A vermilion flame marking with one tall pointed curved tongue and two shorter side tongues, one ivory curved slit inside.` |
| 清暑 | `Two broad indigo-blue horizontal flowing wave lines, smooth parallel curves, generous ivory gaps, no splashes.` |
| 百工 | `An ochre rounded-square ribbon loop with two clearly separated over-under crossings, a compact open center, few thick strokes.` |
| 回气 | `A muted green horizontally flowing cloud marking, three rounded connected lobes with two short trailing curved strokes, generous ivory gaps, no inward spiral or radial arrangement.` |
| 临战 | `Three dark red-brown tapered tiger-stripe markings, one downward central wedge and two outward angled side strokes, no animal face or lettering.` |
| 镇煞 | `A small dark teal straight sword-shaped marking, one pointed blade, one short crossguard, two short curved strokes beside it.` |
| 纳煞 | `A dark violet outline of a closed narrow-necked vessel with a broad rounded belly and a small rim, ivory interior, no liquid level.` |
| 涤煞 | `A closed dark blue circular boundary band with four short right-angle inward meanders, a large empty ivory center, no numbers or gauge.` |
| 聚煞 | `Three plum-purple hooked comma-shaped cloud curls arranged around a small ivory center, all tips curving inward, thick separated strokes.` |
| 归真 | `A compact circular motif split by a smooth S curve into two interlocking teardrop shapes, one dark teal and one ivory, each with a small contrasting dot, no outer symbols.` |
| 安魂 | `A muted mauve lotus-shaped marking with three broad pointed-round upright petals and two shorter side petals, open symmetrical silhouette, no elaborate stamens or circular frame.` |

## 资料索引与适用边界

访问日期：2026-11-19。以下为故宫博物院藏品页面及其术语解释，已核对页面文字；没有将网站照片作为新图像的像素来源。朝代和器物名称仅供追溯，不要求最终纹样复原某件文物。

| 编号 | 来源 | 本方案采用的证据 |
| --- | --- | --- |
| S1 | [马家窑文化彩陶水波纹钵](https://www.dpm.org.cn/collection/ceramic/227459.html) | 水波纹是日常所见河水的图案化表现 |
| S2 | [剔红莲托八吉祥纹圆盒](https://www.dpm.org.cn/collection/lacquerware/232889.html) | 八吉祥种类、盘长／宝瓶／莲花的解释，以及缠枝的形式定义 |
| S3 | [白玉镂雕盘长纹扁方](https://www.dpm.org.cn/collection/jade/232338.html) | 盘长纹在玉器上的实际应用；不据此额外推导研究效率含义 |
| S4（备选留档） | [青花缠枝莲纹碗](https://www.dpm.org.cn/collection/ceramic/227625.html) | 原枝叶方案的来源，回气丹已按用户要求改为云纹，不再采用 |
| S5 | [布老虎](https://www.dpm.org.cn/collection/utensil/232027.html) | 虎形民俗物品驱邪避灾、祈求平安健康；简化虎斑为本作再设计 |
| S6 | [粉彩暗八仙纹双耳转心瓶](https://www.dpm.org.cn/collection/ceramic/227331.html) | 宝剑等八件器物及以物代人、逢凶化吉的解释 |
| S7 | [定窑白釉刻回纹盏托](https://www.dpm.org.cn/collection/ceramic/226985.html) | 回纹环绕边饰的器物实例；闭合圆形药纹为新编 |
| S8 | [玉云纹珌](https://www.dpm.org.cn/collection/jade/229308.html) | 阴刻勾云纹与线条组合；回气丹的横向舒展和聚煞丹的向心聚集均为新编 |
| S9 | [吴伟太极图轴](https://www.dpm.org.cn/collection/paint/231892.html) | 太极图像的绘画实例；不据此宣称解除药效是传统寓意 |
| S10 | [金葫芦](https://www.dpm.org.cn/collection/tinware/230717.html) | 藤蔓、瓜果、种子与子孙繁衍的关联，用于比较备用方案 |

暂不采用：完整海水江崖（构图复杂且不能只解释为清凉）、全套八吉祥或暗八仙（小图拥挤）、完整虎头（容易像动物头像）、方胜代表战斗胜利（仅凭名字附会）、把雷纹画成现代闪电并称其古义为镇煞（依据不足）。火焰图案按用户指定的自然物象绘制，本轮没有确证的特定历史式样来源，不能标称某朝某器火纹复刻。

## 后续接入检查

1. 更新生成脚本的丹体配色、纹样底稿和英文描述；药渡玉佩不受本次丹体调整影响。
2. 先试暖脉、清暑、百工、涤煞四种，验证尖焰、横波、交织、环带在64px仍可区分，再生成其余七种。
3. 检查乳白丹体与浅色药纸的边界、高光是否遮纹、线条是否黏连；成图要同时看深浅背景及128／64／32px。
4. 更新预览和生成溯源记录，确认XML引用、透明边缘与512×512 RGBA；最后进行游戏内显示验收。本文件不表示这些步骤已经完成。
