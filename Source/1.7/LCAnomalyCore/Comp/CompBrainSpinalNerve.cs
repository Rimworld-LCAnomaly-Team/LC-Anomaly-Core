using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompBrainSpinalNerve</c> 类型。</summary>
    public class CompBrainSpinalNerve : ThingComp
    {
        /// <summary>
        /// CompProperties
        /// </summary>
        public CompProperties_BrainSpinalNerve Props => (CompProperties_BrainSpinalNerve)props;

        /// <summary>表示 <c>name</c>。</summary>
        protected Name name;
        /// <summary>表示 <c>skill</c>。</summary>
        protected List<NerveSkill> skill;

        /// <summary>执行 <c>Init</c> 定义的操作。</summary>
        public virtual void Init(Pawn pawn)
        {
            if (pawn.Name != null)
                name = pawn.Name;
            if (pawn.skills.skills.Count > 0)
            {
                skill = new List<NerveSkill>();
                foreach (var s in pawn.skills.skills)
                {
                    skill.Add(new NerveSkill
                    {
                        def = s.def,
                        level = s.Level
                    });
                }
            }
        }

        /// <inheritdoc />
        public override void PostExposeData()
        {
            Scribe_Deep.Look(ref name, "name");
            //Scribe_Deep.Look(ref skill, "skill");
            Scribe_Collections.Look(ref skill, "skill", LookMode.Deep);
        }

        /// <inheritdoc />
        public override string CompInspectStringExtra()
        {
            //return base.CompInspectStringExtra();

            StringBuilder sb = new StringBuilder();

            if (name != null)
            {
                sb.Append("Name: " + name);
            }
            else
            {
                sb.Append("Name: Null");
            }

            if (skill != null)
            {
                foreach (var s in skill)
                {
                    sb.Append("\n" + s.def.label.Translate() + ": " + s.level);
                }
            }

            return sb.ToString();
        }

        //public override IEnumerable<Verse.Gizmo> CompGetGizmosExtra()
        //{
        //    if (DebugSettings.ShowDevGizmos)
        //    {
        //        yield return new Command_Action
        //        {
        //            defaultLabel = "PrintInfo",
        //            action = delegate
        //            {
        //                StringBuilder sb = new StringBuilder();

        //                if (name != null)
        //                {
        //                    sb.Append(name + "\n");
        //                }
        //                if (skill != null)
        //                {
        //                    foreach (var s in skill)
        //                    {
        //                        sb.Append(s.def.label.Translate() + ": " + s.level + "\n");
        //                    }
        //                    Log.Message(sb);
        //                }
        //            }
        //        };
        //    }
        //}
    }

    /// <summary>表示 <c>NerveSkill</c> 类型。</summary>
    public struct NerveSkill : IExposable
    {
        /// <summary>表示 <c>def</c>。</summary>
        public SkillDef def;
        /// <summary>表示 <c>level</c>。</summary>
        public int level;

        /// <summary>执行 <c>ExposeData</c> 定义的操作。</summary>
        public void ExposeData()
        {
            Scribe_Defs.Look<SkillDef>(ref def, "def");
            Scribe_Values.Look<int>(ref level, "level");
        }
    }
}