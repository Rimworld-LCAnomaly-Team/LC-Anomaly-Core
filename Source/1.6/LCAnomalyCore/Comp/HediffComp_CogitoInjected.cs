using LCAnomalyCore.Defs;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// Cogito被注射状态的Hediff
    /// </summary>
    public class HediffComp_CogitoInjected : HediffComp
    {
        /// <summary>
        /// CompProperties
        /// </summary>
        public HediffCompProperties_CogitoInjected Props => (HediffCompProperties_CogitoInjected)props;

        #region 生命周期

        /// <summary>
        /// 移除前调用
        /// </summary>
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            TurnIntoAnomaly();
        }

        #endregion 生命周期

        #region 工具方法

        /// <summary>
        /// 注射提取后变成异常的方法
        /// </summary>
        private void TurnIntoAnomaly()
        {
            EffecterDefOf.MeatExplosionSmall.SpawnMaintained(Pawn.Position, Pawn.MapHeld);

            if (Rand.Chance(Props.deadChance))
            {
                Pawn.Kill(null);
                //Log.Message("Cogito注射：判定死亡");
                return;
            }

            //判定生成大罪生物
            if (ExpansionActive.SevenSinEntityActive)
            {
                var list1 = ExtractUtil.Get_AnomlayLvl2DefList_SevenSin("ZAYIN").
                    Union(ExtractUtil.Get_AnomlayLvl2DefList_SevenSin("TETH")).ToList();

                if (list1.Count > 0)
                {
                    if (Rand.Chance(Props.sevenSinEnitityChance))
                    {
                        Pawn pawn = PawnGenerator.GeneratePawn(list1.RandomElement(), Faction.OfEntities);

                        if (Pawn.Name != null)
                        {
                            pawn.Name = Pawn.Name;
                            //Log.Message($"Congito注射：{Pawn.Name}变成了大罪生物{pawn.def.defName.Translate()}");
                        }
                        //else
                        //    Log.Message($"Congito注射：???变成了大罪生物{pawn.def.defName.Translate()}");

                        GenSpawn.Spawn(pawn, Pawn.Position, Pawn.MapHeld);

                        Pawn.DeSpawn();
                        return;
                    }
                    //else
                    //    Log.Message("Congito注射：概率判定不生成大罪生物");
                }
                //else
                //    Log.Warning("Congito注射：ZAYIN和TETH的大罪生物列表为空");
            }

            //判定生成ZAYIN级别异常
            var list2 = ExtractUtil.Get_AnomlayLvl2DefList_Cogito("ZAYIN").ToList();
            if (SpawnEntity(list2, Props.zayinChance))
            {
                Pawn.DeSpawn();
                return;
            }
            //else
            //    Log.Message("Congito注射：概率判定/生成失败不生成ZAYIN级别异常");

            //判定生成ZAYIN级别异常
            list2 = ExtractUtil.Get_AnomlayLvl2DefList_Cogito("TETH").ToList();
            if (SpawnEntity(list2, Props.tethChance))
            {
                Pawn.DeSpawn();
                return;
            }
            //else
            //    Log.Message("Congito注射：概率判定/生成失败不生成TETH级别异常");

            //Log.Message("Congito注射：所有判定条件都不满足，无事发生");
        }

        /// <summary>
        /// 生成实体
        /// </summary>
        /// <param name="list">实体列表</param>
        protected bool SpawnEntity(List<ThingDef_LCAnomalyBase> list, float chance)
        {
            if (list.Count > 0 && Rand.Chance(chance))
            {
                var thing = ((LC_FX_Standard)GenSpawn.Spawn(list.RandomElement(), Pawn.Position, Pawn.MapHeld));
                if (thing != null)
                {
                    thing.InitWith(null);
                    return true;
                }
                else
                {
                    //Log.Error("Cogito注射：生成异常实体失败，thing is null");
                    return false;
                }
            }

            return false;
        }

        #endregion 工具方法
    }
}