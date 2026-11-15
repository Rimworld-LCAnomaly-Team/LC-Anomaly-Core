# LC 系列 RimWorld 1.7 适配

目标版本：本机 RimWorld **1.7.5005 rev1082** 测试版，运行库为 .NET 10 / Waylith。

## 覆盖范围

以下 12 个仓库均新增 `1.7` 加载配置和内容目录。有 C# 代码的 10 个模组同时新增 `Source/1.7` 项目，并生成各自的 1.7 程序集。

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

`1.5`、`1.6` 及其源码目录保留。1.7 的项目直接引用游戏根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll` 和 `Waylith.UnityCompat.dll`，不再引用旧的 `RimWorldWin64_Data/Managed`。子模组通过项目引用使用 Core / Story，且不在自己的 Assemblies 中复制这些依赖 DLL。

## 主要修改

- Core 的警报覆盖层改用游戏原生 GUI，移除对 Unity Canvas、CanvasGroup、GraphicRaycaster 的运行时依赖；覆盖层状态由当前游戏组件持有。
- 旧版 UGUI 调试入口改用现有的原生图鉴窗口。旧资产包复制到 1.7 自己的 Assets 目录，源码中的加载路径同步更新。
- 穿刺乐园和大罪生物迁移到现行 Core 异想体、收容和研究接口；穿刺乐园保留工作结果对应的逆卡巴拉增量，并接入当前 PeBox 结算。
- 肉灯的出逃形态在没有研究解锁组件时返回零观察加成，避免工作成功率查询时空引用。
- 研究标签直接引用完整 ITab 类型名，不再依赖 Story 提供旧 ITabDef 别名。
- 修正失效 XML 字段、异想体分类扩展、收容组件、声音名称和多行 defName；蕾蒂希娅与一罪使用 EntityMechanical 实体材质类型。
- 考验生物显式设置 `trainability=None`，修复存档重新载入时训练追踪器报错。
- 修复中文翻译的自定义 Def 完整类型名、组件句柄和已改名工作定义；移除只指向已删除内容的注入项。
- 异世的肖像在 1.7 下依赖 Core，旧版本的 Library 依赖通过版本化元数据保留。其原有 C# 项目仍为空壳；本次迁移不新增尚未实现的玩法。
- 维护规则默认版本改为 1.7。

## 构建与验证

在本模组目录运行：

```powershell
.\Tools\1.7\BuildFamily.ps1
.\Tools\1.7\RuntimeSmokeTest.ps1 -Language English
.\Tools\1.7\RuntimeSmokeTest.ps1 -Language ChineseSimplified
```

构建需要 .NET 10 SDK、同级目录中的上述系列模组和 Harmony 1.7。非 Steam 默认安装可显式传入 `-RimWorldRoot`、`-HarmonyAssembliesDir`。

运行测试使用 `.runtime-test-1.7` 中的独立配置与存档，同时加载上述 12 个模组、全部已安装官方 DLC、Harmony、HAR 和 Vanilla Backgrounds Expanded。测试脚本临时注入验证程序集，并在结束或失败后删除；不更改玩家的正常模组列表和存档。

验证内容包括：

- 12 个模组的版本和加载目录检查、10 个程序集编译、276 个 XML 文件解析。
- 系列 ThingDef 的 ConfigErrors 检查，以及中文和英文翻译加载检查。
- 34 个系列 PawnKind 的生成、入图、渲染初始化与 Gizmo 构造。
- 异想体分类、收容和研究组件及四种工作成功率检查。
- 62 项物品与建筑定义的对象、Gizmo 和材质初始化。
- 原生警报覆盖层触发、120 tick 运行、保存、重新载入及再运行 120 tick。

日志位于 `.runtime-test-1.7/English-map.log`、`English-load.log`、`ChineseSimplified-map.log`、`ChineseSimplified-load.log`，构建日志位于 `family-build.log`。

本机第三方 Vanilla Backgrounds Expanded 缺少可选模组图标 `UI/VELogo`，快速开局也可能触发原版初始化阶段的 `TicksAbs` 日志。这两项有明确来源，测试脚本单独列明；其余 ERROR 和翻译加载错误会使验证失败。其他已安装但未启用模组的 About 元数据警告来自游戏扫描全部模组目录。

## 验证边界

HAR 主框架和背景扩展的已安装版本均声明支持 1.7。HAR Patch 的条件目录和种族服装定义已迁移，但本机 Kiiro、Milira、Miho 模组只声明支持到 1.6，因此尚未进行这三种族的 1.7 实际联动测试。

地图测试覆盖初始化、短时运行和本次 1.7 存档的读回，不代替长时间战斗、全部考验阶段、完整剧情进度或旧 1.6 存档迁移测试。测试版后续更新仍需重新编译并运行验证。
