namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompProperties_CogitoBucketSpawner</c> 类型。</summary>
    public class CompProperties_CogitoBucketSpawner : LC_CompProperties_RequireThingSpawner
    {
        /// <summary>初始化 <c>CompProperties_CogitoBucketSpawner</c> 类的新实例。</summary>
        public CompProperties_CogitoBucketSpawner()
        {
            compClass = typeof(CompCogitoBucketSpawner);
        }
    }
}