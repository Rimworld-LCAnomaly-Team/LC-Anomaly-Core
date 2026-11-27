using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Buildings
{
    /// <summary>
    /// 部门核心建筑
    /// </summary>
    public class Building_DepartmentCore : Verse.Building
    {
        /// <summary>
        /// 部门comp
        /// </summary>
        public CompAssignableDepartmentCore CompDepartmentCore
        {
            get
            {
                compDepartmentCore ??= GetComp<CompAssignableDepartmentCore>();
                return compDepartmentCore;
            }
        }

        private CompAssignableDepartmentCore compDepartmentCore;

        /// <summary>
        /// 电力comp
        /// </summary>
        public CompPowerTrader CompPower
        {
            get
            {
                compPower ??= GetComp<CompPowerTrader>();
                return compPower;
            }
        }

        private CompPowerTrader compPower;

        /// <summary>
        /// 燃料Comp
        /// </summary>
        public CompRefuelable CompRefuelable
        {
            get
            {
                compRefuelable ??= GetComp<CompRefuelable>();
                return compRefuelable;
            }
        }

        private CompRefuelable compRefuelable;

        private const int HealPulseDurationTicks = 75;
        private static Material healWaveMat;
        private static MaterialPropertyBlock healWavePropBlock;

        private GenDraw.FillableBarRequest fillableBarRequest;
        private int healTimer = 0;
        private int healPulseTicksRemaining = 0;
        private bool healPulseWasActiveHeal = false;
        private float healPulseMaxRadius = 5f;
        private readonly List<IntVec3> cachedPulseCells = new List<IntVec3>();

        /// <inheritdoc />
        protected override void Tick()
        {
            base.Tick();

            if (healPulseTicksRemaining > 0)
            {
                healPulseTicksRemaining--;
            }

            //通电才能工作
            if (CompPower.PowerOn)
            {
                healTimer++;

                //按设定的时间间隔进行房间治疗
                if (healTimer >= CompDepartmentCore.Props.healDuration)
                {
                    healTimer = 0;
                    TryHeal();
                }
            }
        }

        /// <inheritdoc />
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            foreach (Gizmo bulletGizmo in ManagerBulletUtility.GetBulletGizmos(this))
            {
                yield return bulletGizmo;
            }
        }

        /// <inheritdoc />
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            //清理所有已分配员工
            CompDepartmentCore.UnassignAllPawn();
            base.DeSpawn(mode);
        }

        /// <summary>
        /// 房间内的员工治疗（供周期再生与运行时校验调用）
        /// </summary>
        public void TryHeal()
        {
            // 安保部核心抑制期间临时封锁部门核心再生器
            if (GameComponent.GameComponent_LC.Instance?.SuppressionHealLocked == true)
                return;

            //不在房间内则不进行治疗
            Room room = ResolveHealRoom();
            if (room == null)
                return;

            //获取房间内所有的Pawn并累计消耗燃料
            int healTimes = 0;
            float unitConsume = DepartmentCoreUtility.GetEffectiveHealFuelCost(CompDepartmentCore.Props.healConsumeAmount);
            float remainingFuel = CompRefuelable.Fuel;
            var roomPawns = room.ContainedThings<Pawn>();
            foreach (Pawn employee in roomPawns)
            {
                //不是玩家派系的pawn禁止恢复
                if (employee.Faction == null || employee.Faction != Faction.OfPlayer)
                    continue;

                //不存在或者没激活员工状态则禁止恢复
                var comp = employee.GetComp<CompPawnStatus>();
                if (comp == null || !comp.Enabled)
                    continue;

                if (remainingFuel < unitConsume)
                    break;

                int used = CompDepartmentCore.DoHeal(employee, remainingFuel);
                if (used > 0)
                {
                    healTimes += used;
                    remainingFuel -= unitConsume * used;
                }
            }

            //按照治疗次数消耗燃料
            if (healTimes > 0)
            {
                float amount = unitConsume * healTimes;
                CompRefuelable.ConsumeFuel(amount);

                LogUtil.Message($"{def.defName} consumed {amount} KCorpAmpoule.");
            }

            if (healTimes > 0 || CompRefuelable.Fuel >= unitConsume)
            {
                StartHealPulse(room, didHeal: healTimes > 0);
            }
        }

        private Room ResolveHealRoom()
        {
            Map map = MapHeld;
            if (map == null)
            {
                return null;
            }

            return InteractionCell.GetRoom(map) ?? this.GetRoom();
        }

        private Color GetHealOverlayColor(float alpha)
        {
            Color dept = CompDepartmentCore?.Props?.fillableBarColor ?? new Color(0.28f, 0.92f, 0.60f, 1f);
            Color blended = Color.Lerp(dept, new Color(0.28f, 0.96f, 0.62f, 1f), 0.55f);
            blended.a = alpha;
            return blended;
        }

        private void StartHealPulse(Room room, bool didHeal)
        {
            healPulseTicksRemaining = HealPulseDurationTicks;
            healPulseWasActiveHeal = didHeal;
            cachedPulseCells.Clear();
            if (room != null && !room.TouchesMapEdge && room.CellCount <= 1500)
            {
                cachedPulseCells.AddRange(room.Cells);
                healPulseMaxRadius = Mathf.Clamp(Mathf.Sqrt(room.CellCount / Mathf.PI) + 1.2f, 3.2f, 11f);
            }
            else
            {
                healPulseMaxRadius = 4.5f;
            }

            if (didHeal && Spawned && Map != null)
            {
                FleckMaker.Static(DrawPos, Map, FleckDefOf.PsycastAreaEffect, 2.4f);
            }
        }

        /// <inheritdoc />
        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            Room room = ResolveHealRoom();
            if (room == null || room.TouchesMapEdge || room.CellCount > 1500)
            {
                return;
            }

            cachedPulseCells.Clear();
            cachedPulseCells.AddRange(room.Cells);
            GenDraw.DrawFieldEdges(cachedPulseCells, GetHealOverlayColor(0.85f));
        }

        /// <summary>
        /// 绘制方法
        /// </summary>
        /// <param name="drawLoc">位置</param>
        /// <param name="flip">是否翻转</param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            //base.DrawAt(drawLoc, flip);
            var offsetPos = drawLoc + Vector3.up * 0.1f;

            GraphicUtil
                .DepartmentCore_GetCachedBaseGraphic(CompRefuelable.FuelPercentOfMax)
                .Draw(drawLoc + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);

            GraphicUtil
                .DepartmentCore_GetCachedTopGraphic()[CompDepartmentCore.Props.departmentType.ToString()]
                .Draw(offsetPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);

            ProgressBarDraw(offsetPos);
            DrawHealPulseOverlay(drawLoc);
        }

        private void DrawHealPulseOverlay(Vector3 drawLoc)
        {
            if (healPulseTicksRemaining <= 0)
            {
                return;
            }

            float t = 1f - (float)healPulseTicksRemaining / HealPulseDurationTicks;
            float alpha = Mathf.Sin(t * Mathf.PI);
            if (alpha <= 0.01f)
            {
                return;
            }

            float intensity = healPulseWasActiveHeal ? 1f : 0.42f;
            if (cachedPulseCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(cachedPulseCells, GetHealOverlayColor(0.90f * alpha * intensity));
            }

            if (!UnityData.IsInMainThread)
            {
                return;
            }

            healWaveMat ??= MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent);
            if (healWaveMat == null)
            {
                return;
            }

            healWavePropBlock ??= new MaterialPropertyBlock();
            Color waveColor = GetHealOverlayColor(0.48f * alpha * intensity);
            healWavePropBlock.SetColor(ShaderPropertyIDs.Color, waveColor);

            float diameter = Mathf.Lerp(2.4f, healPulseMaxRadius * 2f, t);
            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteLow.AltitudeFor();
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(diameter, 1f, diameter));
            GenDraw.DrawMeshNowOrLater(MeshPool.plane10, matrix, healWaveMat, drawNow: false, properties: healWavePropBlock);
        }

        /// <summary>
        /// 绘制再生进度条
        /// </summary>
        private void ProgressBarDraw(Vector3 drawLoc)
        {
            if (fillableBarRequest.filledMat == null)
            {
                LogUtil.Warning("部门核心建筑进度条为null，准备初始化");

                fillableBarRequest = default;
                fillableBarRequest.center = drawLoc + Vector3.back * 1.294f + Vector3.left * 0.12f;
                fillableBarRequest.size = new Vector2(1.94f, 0.29f);
                fillableBarRequest.filledMat = SolidColorMaterials.SimpleSolidColorMaterial(CompDepartmentCore.Props.fillableBarColor);
                fillableBarRequest.unfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.clear);
                fillableBarRequest.margin = 0.15f;
            }

            fillableBarRequest.fillPercent = 1.0f * healTimer / CompDepartmentCore.Props.healDuration;
            GenDraw.DrawFillableBar(fillableBarRequest);
        }
    }

    /// <summary>
    /// 部门类型枚举
    /// </summary>
    public enum EDepartmentType
    {
        /// <summary>
        /// null
        /// </summary>
        Null = 0,
        /// <summary>
        /// 控制部
        /// </summary>
        ControlTeam = 1,
        /// <summary>
        /// 情报部
        /// </summary>
        InformationTeam,
        /// <summary>
        /// 培训部
        /// </summary>
        TrainingTeam,
        /// <summary>
        /// 安保部
        /// </summary>
        SafetyTeam,
        /// <summary>
        /// 中央本部
        /// </summary>
        CentralCommandTeam,
        /// <summary>
        /// 福利部
        /// </summary>
        WelfareTeam,
        /// <summary>
        /// 惩戒部
        /// </summary>
        DisciplinaryTeam,
        /// <summary>
        /// 记录部
        /// </summary>
        RecordTeam,
        /// <summary>
        /// 研发部
        /// </summary>
        ExtractionTeam,
        /// <summary>
        /// 构筑部
        /// </summary>
        ArchitectureTeam
    }
}