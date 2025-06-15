using LCAnomalyCore.Settings;
using LCAnomalyCore.Singleton;
using RimWorld;
using System.ComponentModel;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 音乐工具
    /// </summary>
    public static class MusicUtils
    {
        public static void PlayMusic_LC(int points)
        {
            //如果未启用警报音乐，就不播放音乐
            if (!Setting_LCAnomalyCore_Main.Settings.If_EnableLCWarningMusic)
                return;

            MusicManagerPlay musicManager = Find.MusicManagerPlay;

            if (points >= 80)
            {
                if (musicManager.CurrentSong != Defs.SongDefOf.ThirdWarning)
                {
                    LogUtil.Warning("WaringMusic：Play tier3 music");
                    musicManager.ForcePlaySong(Defs.SongDefOf.ThirdWarning, false);
                    LCCanvasSingleton.Instance.ShowWarningUI(EWarningLevel.Third);
                }
                return;
            }

            if (points >= 50)
            {
                if (musicManager.CurrentSong != Defs.SongDefOf.SecondWarning)
                {
                    //播放比自己高的警报后，不会向下自动递减警报音乐
                    if (musicManager.CurrentSong == Defs.SongDefOf.ThirdWarning)
                        return;

                    LogUtil.Warning("WaringMusic：Play tier2 music");
                    musicManager.ForcePlaySong(Defs.SongDefOf.SecondWarning, false);
                    LCCanvasSingleton.Instance.ShowWarningUI(EWarningLevel.Second);
                }

                return;
            }

            if (points >= 10)
            {
                if (musicManager.CurrentSong != Defs.SongDefOf.FirstWarning)
                {
                    //播放比自己高的警报后，不会向下自动递减警报音乐
                    if (musicManager.CurrentSong == Defs.SongDefOf.ThirdWarning || musicManager.CurrentSong == Defs.SongDefOf.SecondWarning)
                        return;

                    LogUtil.Warning("WaringMusic：Play tier1 music");
                    musicManager.ForcePlaySong(Defs.SongDefOf.FirstWarning, false);
                    LCCanvasSingleton.Instance.ShowWarningUI(EWarningLevel.First);
                }

                return;
            }

            //警报点数小于10时，重置音乐
            if (musicManager.CurrentSong == Defs.SongDefOf.FirstWarning
                || musicManager.CurrentSong == Defs.SongDefOf.SecondWarning
                || musicManager.CurrentSong == Defs.SongDefOf.ThirdWarning)
            {
                LogUtil.Warning("WaringMusic：Reset");
                musicManager.StartNewSong();
            }
        }

        public static int LevelTag2Points(string tag)
        {
            switch (tag)
            {
                case "ZAYIN":
                    return 5;

                case "TETH":
                    return 20;

                case "HE":
                    return 40;

                case "WAW":
                    return 60;

                case "ALEPH":
                    return 75;

                default:
                    throw new InvalidEnumArgumentException($"未知的异想体等级：{tag}");
            }
        }

        public enum EWarningLevel
        {
            First,
            Second,
            Third
        }
    }
}