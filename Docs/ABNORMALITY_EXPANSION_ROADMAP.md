# 异想体 20 项扩充目标计划与进度跟踪

> 本文档用于规划并持续跟踪《脑叶公司》RimWorld 1.7（.NET 10，无强制 Anomaly DLC 依赖）异想体合集包 `LC Abnormalities` 的分期扩充目标与 `LC Anomaly Core` 基础框架支撑能力。

---

## 一、当前基线（已完成 10 项图鉴）

| 危险等级 | 已实装图鉴 | 类型 | E.G.O 武器 / 四色特征 |
| :--- | :--- | :--- | :--- |
| ZAYIN | 一罪与百善（`OneSin`） | 标准 | 白伤近战短杖 |
| ZAYIN | 七宗罪 / 罪孽实体（`SevenSinEntity_ZAYIN`，6 种） | 衍生 | 四色近战实体 |
| ZAYIN | 我们可以改变一切（`WeCanChangeAnything`） | 持续封闭工具 | — |
| ZAYIN | 古老的信念与承诺（`OldBeliefAndPromise`） | 单次交互工具 | 武器永久增伤赐福 |
| TETH | 肉食提灯（`MeatLantern`） | 标准 | 黑伤近战 |
| HE | 蕾蒂希娅（`Laetitia`） | 标准 | 黑伤远程步枪 |
| HE | 异界肖像（`PortraitOfAnotherWorld`） | 单次交互工具 | 替身伤害转嫁 |
| WAW | 穿刺乐园（`BurrowingHeaven`） | 标准 | 红伤近战长矛 |
| WAW | 血肉偶像（`FleshIdol`） | 持续祈祷工具 | 全殖民地治疗脉冲 |
| ALEPH | 一无所有（`NothingThere`） | 标准 | 红伤吸血大剑 |

---

## 二、Core 核心框架前置支撑清单

- [x] `AbnormalityToolMode.Equippable` 完整生命周期（佩戴、持续 Tick、安全归还/违规反噬、宿主死亡/丢失清理、收容平台空置贴图切换）
- [x] `CompAbnormality.GetPlatformGizmos()` 与 `GetPlatformInspectString()` 收容平台按钮/状态文本转发接口
- [x] 全局轻量事件钩子：`CompAbnormality.OnAnyAbnormalityStudyStarted`、`CompAbnormality.OnAnyAbnormalityStudyCompleted`、`Patch_Thing_TakeDamage.OnPawnDealtDamage`
- [x] `RuntimeValidation` 自动化集成测试覆盖（1、2 期全量测试 + 存档读档持久化校验）

---

## 三、20 个新增异想体分期目标（总计 4 期，每期 5 个）

### 第 1 期：补齐前期池 & 打通佩戴型工具框架（已实装）

| 状态 | `defName` | 异想体名称 | 编号 | 危险等级 | 类型 | E.G.O 武器 / 护甲 / 饰品摘要 | 实现要点 |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| [x] | `FairyFestival` | 妖精盛宴 | `F-04-83` | ZAYIN | 标准 | 翅振（Z 红伤近战吸血）/ 红黑抗甲 / 护手饰品 | 工作后附带妖精守护持续回血；带状态操作其他异想体即遭啃噬暴毙 |
| [x] | `WellCheers` | 韦尔奇乐汽水 | `F-05-52` | ZAYIN | 标准 | 苏打（Z 红伤手枪，概率回血理智）/ 蓝红汽水甲 / 嘴部饰品 | 良/优工作随机器官回血或回理智；差工作昏迷低温被丢去远洋捕虾 |
| [x] | `PunishingBird` | 惩戒鸟 | `O-02-56` | TETH | 标准 | 喙（T 红伤速射手枪）/ 高红抗甲 / 丝带饰品 | 常态出逃四处啄人并在约 45 秒后自行回卵；若出逃期间遭还击则暴怒并重咬反杀 |
| [x] | `VoidDream` | 虚无之梦 | `T-02-99` | TETH | 标准 | 沉沦之梦（T 白伤二连发远程枪）/ 黑白抗甲 / 羊角帽饰品 | 自律 I 降计数器；出逃变身黑公鸡释放范围催眠嚎叫，需队友唤醒梦游者 |
| [x] | `HeartOfAspiration` | 渴望之心 | `T-09-77` | TETH | 佩戴型工具 | 无（佩戴加成近战/射速/移速/承伤） | 佩戴后 60 秒内未参战或未战斗即归还则失控狂暴并造成红白反噬 |

### 第 2 期：人气爆发 & 远程 / 双色 / 压迫体系成型（已实装）

| 状态 | `defName` | 异想体名称 | 编号 | 危险等级 | 类型 | E.G.O 武器 / 护甲 / 饰品摘要 | 实现要点 |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| [x] | `ScorchedGirl` | 焦化少女 | `F-01-02` | TETH | 标准 | 第四根火柴（T 红伤 AOE 榴弹炮）/ 防火高红抗甲 / 火柴口饰 | 出逃点燃 20 秒引信直线冲锋接近目标并引爆自毁，击杀阻止则不爆炸直接化卵 |
| [x] | `DerFreischutz` | 魔弹射手 | `F-01-69` | HE | 标准 | 魔弹（**WAW** 超远贯穿黑伤步枪，不分敌我）/ 全抗均衡甲 / 烟斗口饰 | 全图付费请求魔弹贯穿射击；第 7 发必定诅咒贯穿随机殖民者；优质压迫训练位 |
| [x] | `FuneralOfDeadButterflies` | 亡蝶葬仪 | `T-01-68` | HE | 标准 | 圣宣（**WAW** 白+黑双色双枪）/ 黑抗甲 / 蝶翼背饰 | 高勇气/低正义降计数器；出逃中远程蝴蝶弹幕与开棺范围爆发 |
| [x] | `SnowQueen` | 雪皇后 | `F-01-37` | HE | 标准 | 霜之碎片（H 纯白伤减速长矛）/ 极寒高白抗甲 / 冰之吻面饰 | 首次亲吻标记，二次亲吻冰封收容；派队友决斗营救成功则双人获赠「冰之吻」 |
| [x] | `NotesFromCrazedResearcher` | 狂徒笔记 | `T-09-78` | HE | 佩戴型工具 | 无（大幅提升工作速度、研究速度与外交） | 佩戴期间累计受伤 ≥100、或未满 30 秒/未完成 1 次研究工作即归还时剧烈爆炸 |

### 第 3 期：WAW 中后期核心 & 蓝伤破冰 & 集齐黑森林三鸟（规划中）

| 状态 | `defName` | 异想体名称 | 编号 | 危险等级 | 类型 | E.G.O 武器 / 护甲 / 饰品摘要 | 实现要点 |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| [ ] | `AllAroundHelper` | 小帮手 | `T-05-41` | HE | 标准 | 研磨机 Mk4（H 高频红伤锯）/ — | 非人型清洁机器人，高速直线冲撞连斩 |
| [ ] | `JudgementBird` | 审判鸟 | `O-02-62` | WAW | 标准 | 正义裁决者（**ALEPH** 蓝伤长刃）/ 0.5 蓝抗核心甲 / 蒙眼饰品 | 全库首个蓝伤与高蓝抗来源；百分比灵魂审判；三鸟联动前置 ② |
| [ ] | `BigBird` | 大鸟 | `O-02-40` | WAW | 标准 | 目灯（W 黑伤重锤）/ 0.4 顶级黑抗甲 / 提灯头饰 | 熄灯魅惑牵引与咬头处决；三鸟联动前置 ③ |
| [ ] | `LittleRedRidingHoodedMercenary` | 小红帽雇佣兵 | `F-01-57` | WAW | 标准 | 赤红疤痕（W 红伤远近双修，低血增伤）/ 猎装护甲 / 狼皮饰品 | 消耗 PE-Box 雇佣出战追杀袭击者与出逃怪 |
| [ ] | `BackwardClock` | 回溯之钟 | `D-09-104` | WAW | 充能工具 | 无（终局战略复位按钮） | 充能 4 次后献祭高阶员工瞬间镇压常规出逃并清空危机 |

### 第 4 期：顶流人气 & 四色 ALEPH 终局收官（规划中）

| 状态 | `defName` | 异想体名称 | 编号 | 危险等级 | 类型 | E.G.O 武器 / 护甲 / 饰品摘要 | 实现要点 |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| [ ] | `QueenOfHatred` | 憎恶女王 | `O-01-04` | WAW | 标准 | 以爱与恨之名（W 红白黑三色治疗魔杖）/ — | 平时可协助防御战斗，失控化龙喷射贯穿激光 |
| [ ] | `BlueStar` | 蓝星 | `O-03-93` | ALEPH | 标准 | 星之声（A 全图白伤大炮+回理智）/ 白抗终局甲 | 出逃原地发射全图精神吸力脉冲秒杀低谨慎/恐慌者 |
| [ ] | `MountainOfSmilingBodies` | 笑魇 | `T-01-75` | ALEPH | 标准 | 微笑（A 黑伤重锤，减速+吸血）/ 黑抗终局甲 | 搜寻吞噬战后尸体实现三阶段体型与战斗力进化 |
| [ ] | `SilentOrchestra` | 静默乐团 | `T-01-31` | ALEPH | 标准 | 返始（A 超高速白伤镰刀）/ 白伤免疫甲 | 复用 `ILCFourColorDynamicResistance` 实现四乐章动态抗性轮换 |
| [ ] | `WhiteNight` | 白夜（含前身疫医） | `T-03-46` | ALEPH | 标准 | 失乐园（唯一 **ALEPH** 蓝伤神装与全能蓝抗甲） | 祝福 12 名员工后破茧变身，与 `OneSin` 联动实现第 12 使徒忏悔处决（备选：复用 `ImageEffect_Mosaic` 的 `CENSORED` 审查） |

---

## 四、验证与里程碑记录

- **2026-11-23（第 1、2 期交付里程碑）**：
  - 全系列构建验证：`Tools/1.7/BuildFamily.ps1` 退出码 `0`（`1.7 family validation passed: 7 mods, 5 assemblies, 424 XML files`，全部程序集 0 警告 0 错误）。
  - 英文运行时冒烟测试：`Tools/1.7/RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language English` 退出码 `0`（`English map passed` 与 `English load passed`，0 错误；覆盖全图鉴、仪式提取、第 1–2 期全部 10 项核心机制、佩戴型工具及雪皇后 `IThingHolder` 存读档校验）。
  - 简体中文运行时冒烟测试：`Tools/1.7/RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language ChineseSimplified` 退出码 `0`（`ChineseSimplified map passed` 与 `ChineseSimplified load passed`，仅命中基线已知的 28 条原版 1.7 中文提示，系列零翻译与运行时错误）。
  - 验证顺带修复的跨模组兼容性：`LC Anomaly Core` `Patch_ApparelProperties` 现已覆盖全部 14 个 `LC_Accessory*` E.G.O. 饰品层以统一无体型后缀贴图加载；`LC-Anomaly-Ordeals` 已消除 `WithoutAnomaly` 配置文件下对 `[MayRequireAnomaly]` `DamageDefOf.Psychic` 的未判空依赖（回退至 `DamageDefOf.LC_WhiteDamage`）。
