# 四色伤害 XML 接入说明

LC Anomaly Core 1.6 提供三种只需 XML 的接入方式。数值均为最终伤害倍率：`0` 为免疫，`0.5` 为承受 50%，`1` 为正常，`2` 为弱点。

## 给武器添加四色伤害

在武器 `ThingDef` 中加入 `modExtensions`。该方式会把武器造成的近战和远程伤害统一转换为指定颜色，无需修改弹丸：

```xml
<modExtensions>
  <li Class="LCAnomalyCore.ModExtensions.FourColorWeaponExtension">
    <damageType>Black</damageType>
    <riskLevel>HE</riskLevel>
    <damageMultiplier>1</damageMultiplier>
  </li>
</modExtensions>
```

`damageType` 可填 `Red`、`White`、`Black`、`Pale`；`riskLevel` 可填 `ZAYIN`、`TETH`、`HE`、`WAW`、`ALEPH`。

旧式 Comp 定义仍然可用，并增加了可选的 `damageType` 与 `damageMultiplier` 字段：

```xml
<li Class="LCAnomalyCore.Comp.CompProperties_EgoWeapon">
  <level>WAW</level>
  <damageType>White</damageType>
  <damageMultiplier>1</damageMultiplier>
</li>
```

## 给服装添加四色抗性

在服装 `ThingDef` 中加入：

```xml
<modExtensions>
  <li Class="LCAnomalyCore.ModExtensions.FourColorApparelExtension">
    <riskLevel>WAW</riskLevel>
    <redResistance>0.5</redResistance>
    <whiteResistance>0.8</whiteResistance>
    <blackResistance>1</blackResistance>
    <paleResistance>1.5</paleResistance>
  </li>
</modExtensions>
```

若同时穿着多件带四色抗性的服装，系统采用对当前伤害防护效果最好的一件，不叠乘，避免普通服装分层造成极端免伤。

旧式 `LCAnomalyCore.Comp.CompPoperties_EgoSuit` 仍兼容；新 Def 可使用拼写正确的别名 `LCAnomalyCore.Comp.CompProperties_EgoSuit`。其字段名仍为 `level`、`redResist`、`whiteResist`、`blackResist`、`paleResist`。

## 定义新的四色 DamageDef

需要独立 `DamageDef` 时，可继承公共父 Def。务必同时提供颜色扩展：

```xml
<DamageDef ParentName="LC_FourColorDamageBase">
  <defName>MyMod_CustomWhiteDamage</defName>
  <label>custom WHITE damage</label>
  <harmsHealth>false</harmsHealth>
  <modExtensions>
    <li Class="LCAnomalyCore.ModExtensions.FourColorDamageExtension">
      <damageType>White</damageType>
      <riskLevel>TETH</riskLevel>
    </li>
  </modExtensions>
</DamageDef>
```

若武器本身已有 `FourColorWeaponExtension`，武器的风险等级优先于 `DamageDef` 的后备等级。

## 给其他 Mod 种族指定结算身份

默认将普通人形视为员工式目标，将 LC 异想体与非人形视为异想体式目标。兼容补丁可在种族 `ThingDef` 上明确覆盖：

```xml
<modExtensions>
  <li Class="LCAnomalyCore.ModExtensions.FourColorTargetExtension">
    <targetKind>Abnormality</targetKind>
  </li>
</modExtensions>
```

`targetKind` 可填 `Employee` 或 `Abnormality`。这会决定白伤是否进入精神值，以及苍白伤使用百分比还是原始点数。

## 结算规则

- 红色：造成普通肉体伤害。
- 白色：对普通人形扣除精神值；员工精神上限取“谨慎”，其他人形为 100。归零后按最高员工属性触发最接近原作行为的 RimWorld 恐慌状态；最高属性并列时随机选择。对异想体和非人形目标则作为普通肉体伤害。
- 黑色：以相同点数同时造成肉体与精神伤害。
- 苍白：每 1 点对普通人形造成核心部位最大生命 1% 的伤害；对异想体、非人形 Pawn 与建筑按原始点数结算。
- 恐慌中的 Pawn 受到白色或黑色伤害时会恢复精神值；恢复满后结束恐慌。未恐慌者脱离受击 10 秒后会逐渐恢复精神值。
- 风险等级压制和服装四色抗性在上述效果之前结算；原版护甲不抵挡四色伤害。
