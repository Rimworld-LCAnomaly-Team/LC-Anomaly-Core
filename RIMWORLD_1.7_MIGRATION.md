# LC 系列 RimWorld 1.7 适配

目标版本：本机 RimWorld **1.7.5005 rev1082** 测试版，运行库为 .NET 10 / Waylith。

**1.7 系列现在不再要求安装或启用 Anomaly DLC。** Core 的强制依赖只有 Harmony。HAR Patch 和背景扩展仍分别需要其原有第三方框架。

## 覆盖范围

以下 12 个仓库统一只维护和发布 `1.7` 加载配置及内容目录。有 C# 代码的 10 个模组使用 `Source/1.7` 项目，并生成各自的 1.7 程序集。

- LC Anomaly Backgrounds
- LC Anomaly BurrowingHeaven
- LC Anomaly Core
- LC Anomaly HAR Patch
- LC Anomaly Laetitia
- LC Anomaly MeatLantern
- LC Anomaly OneSin
- LC Anomaly Portrait of Another World
- LC SevenSin Entity
- LC-Anomaly-AnimationStudio
- LC-Anomaly-Ordeals
- LC-Anomaly-Story

已停止支持 1.5、1.6，并从当前发布树移除旧版运行内容、源码项目及专用扩展。历史版本可从 Git 历史找回；共享 `Cont` 资源、美术源文件和资产工程继续保留。所有 About 元数据与 LoadFolders 只声明 1.7。

1.7 的项目直接引用游戏根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll` 和 `Waylith.UnityCompat.dll`，不再引用旧的 `RimWorldWin64_Data/Managed`。子模组通过项目引用使用 Core / Story，且不在自己的 Assemblies 中复制这些依赖 DLL。

## 主要修改

- Core 的警报覆盖层改用游戏原生 GUI，移除对 Unity Canvas、CanvasGroup、GraphicRaycaster 的运行时依赖；覆盖层状态由当前游戏组件持有。
- 旧版 UGUI 调试入口改用现有的原生图鉴窗口。旧资产包复制到 1.7 自己的 Assets 目录，源码中的加载路径同步更新。
- 穿刺乐园和大罪生物迁移到现行 Core 异想体、收容和研究接口；穿刺乐园保留工作结果对应的逆卡巴拉增量，并接入当前 PeBox 结算。
- 肉灯的出逃形态在没有研究解锁组件时返回零观察加成，避免工作成功率查询时空引用。
- 研究标签直接引用完整 ITab 类型名，不再依赖 Story 提供旧 ITabDef 别名。
- 修正失效 XML 字段、异想体分类扩展、收容组件、声音名称和多行 defName。
- 实体材质、肢体、尖刺、生命阶段、敌对派系、AI 和工作速度使用 LC 自有定义；移除 DLC 感染路径、疾病集合、原版图鉴隐藏条件及必需引用。
- 生成、死亡、咬击和 Cogito 转化使用基础游戏粒子与血迹，不复制 DLC 付费资源；原有 LC 动画和外观继续保留。
- 图鉴发现状态由 LC 游戏组件保存；蛋携带独立生物特征，收容、死亡返卵和存档读回均保留身份。
- 工作速度饰品接入 LC 自有速度属性。穿刺乐园使用独立的 24 格进度条；缺少平台名称图片时绘制本地化文字。
- 修复未生成到地图上的世界生物错误触发逃脱，以及无部位 Hediff 使用部位锚点的渲染错误。
- 考验生物显式设置 `trainability=None`，修复存档重新载入时训练追踪器报错。
- 修复中文翻译的自定义 Def 完整类型名、组件句柄和已改名工作定义；移除只指向已删除内容的注入项。
- 异世的肖像统一依赖 Core，移除旧版 Library 依赖及版本化元数据。其原有 C# 项目仍为空壳；本次迁移不新增尚未实现的玩法。
- 维护规则默认版本改为 1.7。
- 动画编辑器的 XML 导入映射、导出、项目验证和预览输出统一使用 1.7 路径。Story、Ordeals 与 Studio 的工具脚本和开发说明同步到 .NET 10 / 1.7 工作流。
- 动画校验识别基础游戏的渲染节点标签，并允许用于隐藏节点的有限零缩放关键帧，避免误拒绝蕾蒂希娅的现有出生动画。
- 系列构建脚本拒绝旧版本支持声明、加载配置及遗留版本目录，防止后续误打包。

## 构建与验证

在本模组目录运行：

```powershell
.\Tools\1.7\BuildFamily.ps1
.\Tools\1.7\RuntimeSmokeTest.ps1 -DlcProfile BaseGame -Language English
.\Tools\1.7\RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language ChineseSimplified
.\Tools\1.7\RuntimeSmokeTest.ps1 -DlcProfile All -Language English
```

构建需要 .NET 10 SDK、同级目录中的上述系列模组和 Harmony 1.7。非 Steam 默认安装可显式传入 `-RimWorldRoot`、`-HarmonyAssembliesDir`。

运行测试使用 `.runtime-test-1.7/<配置>-<语言>` 中的独立配置与存档，同时加载上述 12 个模组、Harmony、HAR 和 Vanilla Backgrounds Expanded。`BaseGame` 不启用任何官方 DLC，`WithoutAnomaly` 仅禁用 Anomaly，`All` 启用所有已安装官方 DLC。默认测试配置为 `WithoutAnomaly`。

脚本写入完整 knownExpansions 列表以防游戏自动启用已安装 DLC，且运行时断言实际 AnomalyActive 状态。测试脚本临时注入验证程序集，并在结束或失败后删除；不更改玩家的正常模组列表和存档。测试世界使用固定种子和玩家、必要隐藏派系，隔离 1.7 测试版非玩家首领生成的不稳定因素。玩法检查使用独立生成、智力技能可用的成年基础人类员工，并固定工作随机种子；不依赖随机开局员工的背景或技能禁用状态。

验证内容包括：

- 12 个模组的版本和加载目录检查、10 个程序集编译、283 个 XML 文件解析。
- 系列 ThingDef 的 ConfigErrors 检查，以及中文和英文翻译加载检查。
- 34 个系列 PawnKind 的生成、入图、渲染初始化与 Gizmo 构造。
- 异想体分类、收容和研究组件及四种工作成功率检查。
- 68 项物品与建筑定义的对象、Gizmo 和材质初始化。
- 一罪、蕾蒂希娅、肉灯、穿刺乐园的蛋转收容、工作回调、PeBox 产出、可用 EGO 提取、释放和死亡返卵。
- 蕾蒂希娅怪物的独立生成、派系、攻击能力、伤害与特效。
- 原生警报覆盖层触发、120 tick 运行、保存、重新载入及再运行 120 tick；验证图鉴发现与四种返卵身份保存。

各配置子目录保存 `<语言>-map.log`、`<语言>-load.log` 和 `translation-errors.txt`；独立版构建日志位于 `.runtime-test-1.7/standalone-build.log`。

2026-11-15 的 1.7 专版整理已通过全系列构建、Studio 四项回归、蕾蒂希娅动画校验和上述三种 DLC 配置的地图及读档检查。本轮日志为 `single-version-build.log`、`single-version-base-final.log`、`single-version-chinese-final.log` 和 `single-version-all-final.log`。运行测试的临时验证程序集已清理，不随 Mod 发布。

本机第三方 Vanilla Backgrounds Expanded 缺少可选模组图标 `UI/VELogo`，快速开局也可能触发原版初始化阶段的 `TicksAbs` 日志。这两项有明确来源，测试脚本单独列明；其余 ERROR 和翻译加载错误会使验证失败。其他已安装但未启用模组的 About 元数据警告来自游戏扫描全部模组目录。

当前 1.7.5005 原版中文语言数据在禁用 Anomaly 时会产生 28 条指向 BloodWarmer、Hellcat 唯一武器和 BioferriteBurner 等不存在定义的翻译警告。已在不启用任何第三方 Mod 的原版对照环境复现；对照日志位于 `.runtime-test-1.7/VanillaWithoutAnomaly-ChineseSimplified/baseline.log`。验证脚本只接受 `Vanilla17ChineseWithoutAnomaly.errors.txt` 记录的精确诊断集合，任何新增错误仍会失败。本系列没有通过创建伪 DLC 定义或修改游戏文件来掩盖这些原版问题。

## 验证边界

HAR 主框架和背景扩展的已安装版本均声明支持 1.7。HAR Patch 的条件目录和种族服装定义已迁移，但本机 Kiiro、Milira、Miho 模组只声明支持到 1.6，因此尚未进行这三种族的 1.7 实际联动测试。

地图测试覆盖初始化、上述玩法调用、短时运行和本次 1.7 存档的读回，不代替长时间战斗、全部考验阶段、完整剧情进度或含官方 DLC 内容的旧存档移除 DLC 测试。测试版后续更新仍需重新编译并运行验证。

本轮全 DLC 地图初始化曾出现一次 180 秒超时，日志停在 `Game.InitNewGame`，根因尚未定位；后续复测完成地图玩法和读档。原始日志保留在 `.runtime-test-1.7/All-English-timeout-*`。这次短时验证不构成对测试版地图生成稳定性的保证。
