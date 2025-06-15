using LCAnomalyCore.Comp;
using Verse;

namespace LCAnomalyCore.DamageWorker
{
    public class DamageWorker_LC_Red : Verse.DamageWorker
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Pawn pawn2 = victim as Pawn;
            DamageResult damageResult = new DamageResult();

            //受害者不存在或者死亡就不计算了
            if (pawn2 == null || pawn2.Dead)
            {
                return damageResult;
            }

            //根据受害者穿着的EGO来进行伤害修正
            if (pawn2.apparel != null)
            {
                bool flag1 = false;
                foreach (var apparel in pawn2.apparel.WornApparel)
                {
                    //如穿着EGO就进行伤害修正
                    var comp = apparel.GetComp<CompEgoSuit>();
                    if (comp != null)
                    {
                        flag1 = true;
                        comp.ApplyEGOResist(ref dinfo);
                        break;
                    }
                }

                if (flag1)
                {
                    Log.Warning("DamageWorker：被攻击对象穿着EGO装备");
                }
                else
                {
                    Log.Warning("DamageWorker：被攻击对象没有穿着EGO装备");
                }
            }

            return damageResult;
        }
    }
}