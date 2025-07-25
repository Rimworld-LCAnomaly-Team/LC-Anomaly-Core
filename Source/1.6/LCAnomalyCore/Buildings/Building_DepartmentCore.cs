﻿using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
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

        private GenDraw.FillableBarRequest fillableBarRequest;
        private int healTimer = 0;

        protected override void Tick()
        {
            base.Tick();

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

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            //清理所有已分配员工
            CompDepartmentCore.UnassignAllPawn();
            base.DeSpawn(mode);
        }

        /// <summary>
        /// 房间内的员工治疗
        /// </summary>
        protected void TryHeal()
        {
            //不在房间内则不进行治疗
            var room = InteractionCell.GetRoom(this.MapHeld);
            if (room == null)
                return;

            //获取房间内所有的Pawn
            int healTimes = 0;
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

                healTimes = CompDepartmentCore.DoHeal(employee, CompRefuelable.Fuel);
            }

            //按照治疗次数消耗燃料
            if (healTimes > 0)
            {
                float amount = CompDepartmentCore.Props.healConsumeAmount * healTimes;
                CompRefuelable.ConsumeFuel(amount);

                LogUtil.Message($"{def.defName} consumed {amount} KCorpAmpoule.");
            }
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