﻿using RimWorld;

namespace LCAnomalyLibrary.Comp
{
    public class LC_CompProperties_StudyUnlocks : CompProperties_StudyUnlocks
    {
        public LC_CompProperties_StudyUnlocks()
        {
            compClass = typeof(LC_CompStudyUnlocks);
        }
    }
}