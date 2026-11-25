using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Render.Weapon
{
    /// <summary>表示武器动画在某个时刻施加给持握者身体的位移与倾斜。</summary>
    public readonly struct EgoWeaponBodyPose
    {
        public readonly Vector3 Offset;
        public readonly float LeanAngleDeg;
        public readonly bool HasOverride;

        public static readonly EgoWeaponBodyPose None = new EgoWeaponBodyPose(Vector3.zero, 0f, false);

        public EgoWeaponBodyPose(Vector3 offset, float leanAngleDeg, bool hasOverride = true)
        {
            Offset = offset;
            LeanAngleDeg = leanAngleDeg;
            HasOverride = hasOverride;
        }
    }

    /// <summary>规范化（面向右侧 +X、上方 +Z、顺时针为正角度）的武器关键帧。</summary>
    public readonly struct WeaponKeyframe
    {
        public readonly float T;
        public readonly float PosX;
        public readonly float PosZ;
        public readonly float AngleDeg;
        public readonly float Scale;

        public WeaponKeyframe(float t, float posX, float posZ, float angleDeg, float scale = 1f)
        {
            T = t;
            PosX = posX;
            PosZ = posZ;
            AngleDeg = angleDeg;
            Scale = scale;
        }
    }

    /// <summary>规范化（面向右侧 +X、上方 +Z、顺时针为正角度）的身体关键帧。</summary>
    public readonly struct BodyKeyframe
    {
        public readonly float T;
        public readonly float OffsetX;
        public readonly float OffsetZ;
        public readonly float LeanDeg;

        public BodyKeyframe(float t, float offsetX, float offsetZ, float leanDeg)
        {
            T = t;
            OffsetX = offsetX;
            OffsetZ = offsetZ;
            LeanDeg = leanDeg;
        }
    }

    /// <summary>单个 Pawn 上正在播放的 E.G.O. 武器动画实例状态。</summary>
    public sealed class EgoWeaponAnimPlayback
    {
        public int PawnId;
        public string WeaponDefName;
        public string ClipId;
        public int StartTick;
        public int DurationTicks;
        public float AimAngleDeg;
        public bool FacingLeft;
        public int Variant;
        public Verb RequiredWarmupVerb;
        public volatile bool Cancelled;

        // 供 ParallelPreDraw 工作线程无锁读取的预计算身体姿势快照
        public float CachedBodyOffsetX;
        public float CachedBodyOffsetZ;
        public float CachedBodyLeanDeg;
        public bool CachedHasBodyPose;
    }

    /// <summary>传递给各 E.G.O. 武器绘制处理器的帧绘制上下文。</summary>
    public readonly struct EgoWeaponDrawContext
    {
        public readonly Pawn Pawn;
        public readonly ThingWithComps Weapon;
        public readonly Vector3 BaseDrawPos;
        public readonly Rot4 Facing;
        public readonly PawnRenderFlags Flags;
        public readonly float AimAngleDeg;
        public readonly bool FacingLeft;
        public readonly bool IsAiming;
        public readonly bool CarryOpenly;
        public readonly EgoWeaponAnimPlayback ActiveAnim;
        public readonly float ElapsedTicks;
        public readonly float NormalizedProgress;

        public EgoWeaponDrawContext(
            Pawn pawn,
            ThingWithComps weapon,
            Vector3 baseDrawPos,
            Rot4 facing,
            PawnRenderFlags flags,
            float aimAngleDeg,
            bool facingLeft,
            bool isAiming,
            bool carryOpenly,
            EgoWeaponAnimPlayback activeAnim,
            float elapsedTicks,
            float normalizedProgress)
        {
            Pawn = pawn;
            Weapon = weapon;
            BaseDrawPos = baseDrawPos;
            Facing = facing;
            Flags = flags;
            AimAngleDeg = aimAngleDeg;
            FacingLeft = facingLeft;
            IsAiming = isAiming;
            CarryOpenly = carryOpenly;
            ActiveAnim = activeAnim;
            ElapsedTicks = elapsedTicks;
            NormalizedProgress = normalizedProgress;
        }
    }

    /// <summary>特定 E.G.O. 武器专属绘制与身体姿势求值处理器接口。</summary>
    public interface IEgoWeaponAnimationHandler
    {
        /// <summary>在主线程安全求值当前动画时刻的身体位移与倾角。</summary>
        EgoWeaponBodyPose EvaluateBodyPose(EgoWeaponAnimPlayback playback, float elapsedTicks);

        /// <summary>绘制武器本体及附加部件或残影；返回 true 表示由本处理器接管渲染，跳过原版默认武器绘制。</summary>
        bool TryDrawWeapon(in EgoWeaponDrawContext ctx);

        /// <summary>当持握者以该 E.G.O. 武器发动普通近战攻击（含命中、未命中、闪避、护盾吸收与致命一击）时调用，确保必然触发普攻动画。</summary>
        void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb);
    }

    /// <summary>全局 E.G.O. 武器动画注册与播放调度中心。</summary>
    public static class EgoWeaponAnimationManager
    {
        private static readonly ConcurrentDictionary<string, IEgoWeaponAnimationHandler> handlersByWeaponDefName =
            new ConcurrentDictionary<string, IEgoWeaponAnimationHandler>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<int, EgoWeaponAnimPlayback> activeByPawnId =
            new ConcurrentDictionary<int, EgoWeaponAnimPlayback>();

        /// <summary>注册指定武器 defName 的专属武器动画处理器。</summary>
        public static void RegisterHandler(string weaponDefName, IEgoWeaponAnimationHandler handler)
        {
            if (string.IsNullOrEmpty(weaponDefName) || handler == null)
            {
                return;
            }

            handlersByWeaponDefName[weaponDefName] = handler;
        }

        /// <summary>尝试获取已注册的武器动画处理器。</summary>
        public static bool TryGetHandler(string weaponDefName, out IEgoWeaponAnimationHandler handler)
        {
            if (string.IsNullOrEmpty(weaponDefName))
            {
                handler = null;
                return false;
            }

            return handlersByWeaponDefName.TryGetValue(weaponDefName, out handler);
        }

        /// <summary>开始播放指定 Pawn 的 E.G.O. 武器动作剪辑，并立即刷新第 0 Tick 身体姿势快照。</summary>
        public static void Play(
            Pawn pawn,
            ThingWithComps weapon,
            string clipId,
            int durationTicks,
            float aimAngleDeg,
            int variant = 0,
            Verb requiredWarmupVerb = null)
        {
            if (pawn == null || weapon?.def == null || string.IsNullOrEmpty(clipId))
            {
                return;
            }

            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            bool facingLeft = ResolveFacingLeft(pawn, aimAngleDeg);

            EgoWeaponAnimPlayback playback = new EgoWeaponAnimPlayback
            {
                PawnId = pawn.thingIDNumber,
                WeaponDefName = weapon.def.defName,
                ClipId = clipId,
                StartTick = ticksGame,
                DurationTicks = Mathf.Max(1, durationTicks),
                AimAngleDeg = aimAngleDeg,
                FacingLeft = facingLeft,
                Variant = variant,
                RequiredWarmupVerb = requiredWarmupVerb,
                Cancelled = false
            };

            if (handlersByWeaponDefName.TryGetValue(playback.WeaponDefName, out IEgoWeaponAnimationHandler handler))
            {
                EgoWeaponBodyPose pose = handler.EvaluateBodyPose(playback, 0f);
                playback.CachedBodyOffsetX = pose.Offset.x;
                playback.CachedBodyOffsetZ = pose.Offset.z;
                playback.CachedBodyLeanDeg = pose.LeanAngleDeg;
                playback.CachedHasBodyPose = pose.HasOverride;
            }

            activeByPawnId[pawn.thingIDNumber] = playback;
        }

        /// <summary>在多段连击过程中同步更新目标朝向角度。</summary>
        public static void UpdateAimAngle(Pawn pawn, float newAimAngleDeg)
        {
            if (pawn == null || !activeByPawnId.TryGetValue(pawn.thingIDNumber, out EgoWeaponAnimPlayback playback))
            {
                return;
            }

            playback.AimAngleDeg = newAimAngleDeg;
            playback.FacingLeft = ResolveFacingLeft(pawn, newAimAngleDeg);
        }

        /// <summary>查询 Pawn 当前是否正在播放指定动画（支持前缀或完整匹配；传 null 表示任意动画）。</summary>
        public static bool IsPlaying(Pawn pawn, string clipPrefixOrExact = null)
        {
            if (pawn == null || !TryGetActive(pawn, out EgoWeaponAnimPlayback playback))
            {
                return false;
            }

            if (string.IsNullOrEmpty(clipPrefixOrExact))
            {
                return true;
            }

            return string.Equals(playback.ClipId, clipPrefixOrExact, StringComparison.Ordinal)
                || (playback.ClipId != null && playback.ClipId.StartsWith(clipPrefixOrExact, StringComparison.Ordinal));
        }

        /// <summary>停止指定 Pawn 当前播放的 E.G.O. 武器动画。</summary>
        public static void Stop(Pawn pawn, string onlyIfClipId = null)
        {
            if (pawn == null)
            {
                return;
            }

            if (activeByPawnId.TryGetValue(pawn.thingIDNumber, out EgoWeaponAnimPlayback playback))
            {
                if (!string.IsNullOrEmpty(onlyIfClipId) && !string.Equals(playback.ClipId, onlyIfClipId, StringComparison.Ordinal))
                {
                    return;
                }

                playback.Cancelled = true;
                activeByPawnId.TryRemove(pawn.thingIDNumber, out _);
            }
        }

        /// <summary>获取 Pawn 当前未过期且未取消的活动武器动画。</summary>
        public static bool TryGetActive(Pawn pawn, out EgoWeaponAnimPlayback playback)
        {
            if (pawn == null || !activeByPawnId.TryGetValue(pawn.thingIDNumber, out playback) || playback == null || playback.Cancelled)
            {
                playback = null;
                return false;
            }

            int ticksGame = Find.TickManager?.TicksGame ?? playback.StartTick;
            if (ticksGame >= playback.StartTick + playback.DurationTicks)
            {
                playback = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 100% 无锁读取预计算身体变换，供 <see cref="PawnRenderTree.ParallelPreDraw"/> 多线程安全调用。
        /// </summary>
        public static bool TryGetBodyTransform(Pawn pawn, out Vector3 offset, out float leanDeg)
        {
            if (pawn == null || !activeByPawnId.TryGetValue(pawn.thingIDNumber, out EgoWeaponAnimPlayback playback) ||
                playback == null || playback.Cancelled || !playback.CachedHasBodyPose)
            {
                offset = Vector3.zero;
                leanDeg = 0f;
                return false;
            }

            offset = new Vector3(playback.CachedBodyOffsetX, 0f, playback.CachedBodyOffsetZ);
            leanDeg = playback.CachedBodyLeanDeg;
            return true;
        }

        /// <summary>每 Tick 在主线程刷新所有活动动画状态、清理过期或被打断的动画并缓存身体姿势。</summary>
        public static void TickAll()
        {
            if (activeByPawnId.IsEmpty)
            {
                return;
            }

            int ticksGame = Find.TickManager?.TicksGame ?? 0;

            foreach (var kv in activeByPawnId)
            {
                int pawnId = kv.Key;
                EgoWeaponAnimPlayback playback = kv.Value;
                if (playback == null || playback.Cancelled || ticksGame >= playback.StartTick + playback.DurationTicks)
                {
                    activeByPawnId.TryRemove(pawnId, out _);
                    continue;
                }

                Pawn pawn = FindSpawnedPawnById(pawnId);
                if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
                {
                    playback.Cancelled = true;
                    activeByPawnId.TryRemove(pawnId, out _);
                    continue;
                }

                ThingWithComps primary = pawn.equipment?.Primary;
                if (primary == null || primary.def == null || !string.Equals(primary.def.defName, playback.WeaponDefName, StringComparison.Ordinal))
                {
                    playback.Cancelled = true;
                    activeByPawnId.TryRemove(pawnId, out _);
                    continue;
                }

                if (playback.RequiredWarmupVerb != null)
                {
                    if (!(pawn.stances?.curStance is Stance_Warmup sw) || sw.verb != playback.RequiredWarmupVerb)
                    {
                        playback.Cancelled = true;
                        activeByPawnId.TryRemove(pawnId, out _);
                        continue;
                    }
                }

                if (handlersByWeaponDefName.TryGetValue(playback.WeaponDefName, out IEgoWeaponAnimationHandler handler))
                {
                    float elapsed = Mathf.Max(0f, ticksGame - playback.StartTick);
                    EgoWeaponBodyPose pose = handler.EvaluateBodyPose(playback, elapsed);
                    playback.CachedBodyOffsetX = pose.Offset.x;
                    playback.CachedBodyOffsetZ = pose.Offset.z;
                    playback.CachedBodyLeanDeg = pose.LeanAngleDeg;
                    playback.CachedHasBodyPose = pose.HasOverride;
                }
            }
        }

        /// <summary>在读档或新建游戏时清理残留的活动播放记录（保留已注册的武器处理器）。</summary>
        public static void ClearAll()
        {
            activeByPawnId.Clear();
        }

        private static bool ResolveFacingLeft(Pawn pawn, float aimAngleDeg)
        {
            if (Mathf.Abs(Mathf.Sin(aimAngleDeg * Mathf.Deg2Rad)) < 0.10f)
            {
                return pawn != null && pawn.Rotation == Rot4.West;
            }

            float norm = ((aimAngleDeg % 360f) + 360f) % 360f;
            return norm > 180f && norm < 360f;
        }

        private static Pawn FindSpawnedPawnById(int pawnId)
        {
            if (Find.Maps == null)
            {
                return null;
            }

            for (int m = 0; m < Find.Maps.Count; m++)
            {
                Map map = Find.Maps[m];
                var pawns = map?.mapPawns?.AllPawnsSpawned;
                if (pawns == null)
                {
                    continue;
                }

                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (p != null && p.thingIDNumber == pawnId)
                    {
                        return p;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>E.G.O. 武器动画通用关键帧插值、坐标镜像、精灵绘制与挥砍残影工具集。</summary>
    public static class EgoWeaponAnimationUtility
    {
        private static readonly Dictionary<Texture, Material> ghostMatCache = new Dictionary<Texture, Material>();

        /// <summary>为 Cutout 类武器材质获取支持 Alpha 混合的透明材质缓存（用于半透明残影）。</summary>
        public static Material GetGhostMaterial(Material sourceMat)
        {
            if (sourceMat == null)
            {
                return null;
            }

            if (sourceMat.shader != ShaderDatabase.Cutout
                && sourceMat.shader != ShaderDatabase.CutoutComplex
                && sourceMat.shader != ShaderDatabase.CutoutFlying
                && sourceMat.shader != ShaderDatabase.CutoutComplexBlend)
            {
                return sourceMat;
            }

            Texture tex = sourceMat.mainTexture;
            if (tex == null || ShaderDatabase.Transparent == null || !UnityData.IsInMainThread)
            {
                return sourceMat;
            }

            if (ghostMatCache.TryGetValue(tex, out Material cached) && cached != null)
            {
                return cached;
            }

            Material transparentMat = MaterialPool.MatFrom(new MaterialRequest(tex, ShaderDatabase.Transparent));
            if (transparentMat != null)
            {
                ghostMatCache[tex] = transparentMat;
                return transparentMat;
            }

            return sourceMat;
        }

        /// <summary>在关键帧序列中按规范化进度或 Tick 平滑插值武器位置、角度与缩放。</summary>
        public static WeaponKeyframe SampleWeapon(WeaponKeyframe[] keys, float t)
        {
            if (keys == null || keys.Length == 0)
            {
                return new WeaponKeyframe(t, 0f, 0f, 0f, 1f);
            }

            if (t <= keys[0].T)
            {
                return keys[0];
            }

            int last = keys.Length - 1;
            if (t >= keys[last].T)
            {
                return keys[last];
            }

            for (int i = 0; i < last; i++)
            {
                WeaponKeyframe a = keys[i];
                WeaponKeyframe b = keys[i + 1];
                if (t >= a.T && t <= b.T)
                {
                    float span = b.T - a.T;
                    float u = span > 0.0001f ? Mathf.Clamp01((t - a.T) / span) : 0f;
                    float posX = Mathf.Lerp(a.PosX, b.PosX, u);
                    float posZ = Mathf.Lerp(a.PosZ, b.PosZ, u);
                    float angle = a.AngleDeg + Mathf.DeltaAngle(a.AngleDeg, b.AngleDeg) * u;
                    float scale = Mathf.Lerp(a.Scale, b.Scale, u);
                    return new WeaponKeyframe(t, posX, posZ, angle, scale);
                }
            }

            return keys[last];
        }

        /// <summary>在关键帧序列中平滑插值持握者的身体位移与前倾/后仰角。</summary>
        public static BodyKeyframe SampleBody(BodyKeyframe[] keys, float t)
        {
            if (keys == null || keys.Length == 0)
            {
                return new BodyKeyframe(t, 0f, 0f, 0f);
            }

            if (t <= keys[0].T)
            {
                return keys[0];
            }

            int last = keys.Length - 1;
            if (t >= keys[last].T)
            {
                return keys[last];
            }

            for (int i = 0; i < last; i++)
            {
                BodyKeyframe a = keys[i];
                BodyKeyframe b = keys[i + 1];
                if (t >= a.T && t <= b.T)
                {
                    float span = b.T - a.T;
                    float u = span > 0.0001f ? Mathf.Clamp01((t - a.T) / span) : 0f;
                    float ox = Mathf.Lerp(a.OffsetX, b.OffsetX, u);
                    float oz = Mathf.Lerp(a.OffsetZ, b.OffsetZ, u);
                    float lean = a.LeanDeg + Mathf.DeltaAngle(a.LeanDeg, b.LeanDeg) * u;
                    return new BodyKeyframe(t, ox, oz, lean);
                }
            }

            return keys[last];
        }

        /// <summary>将规范化右侧朝向的身体位移与倾角转换至世界坐标系。</summary>
        public static EgoWeaponBodyPose ToWorldBodyPose(float localOffsetX, float localOffsetZ, float localLeanDeg, bool facingLeft)
        {
            float sign = facingLeft ? -1f : 1f;
            return new EgoWeaponBodyPose(new Vector3(localOffsetX * sign, 0f, localOffsetZ), localLeanDeg * sign, hasOverride: true);
        }

        /// <summary>以规范化本地偏移、旋转角与大小绘制单个武器精灵（自动处理朝左时的水平翻转）。</summary>
        public static void DrawWeaponSprite(
            Material mat,
            Vector3 basePos,
            float altitudeY,
            float localPosX,
            float localPosZ,
            float localAngleDeg,
            Vector2 drawSize,
            bool facingLeft,
            float scale = 1f,
            float alpha = 1f,
            Color? tint = null,
            MaterialPropertyBlock propBlock = null,
            bool spriteNativeFacingLeft = false)
        {
            if (mat == null || alpha <= 0.01f)
            {
                return;
            }

            float sign = facingLeft ? -1f : 1f;
            Vector3 worldPos = new Vector3(basePos.x + localPosX * sign, altitudeY, basePos.z + localPosZ);
            Quaternion rot = Quaternion.AngleAxis(localAngleDeg * sign, Vector3.up);
            Vector3 meshScale = new Vector3(drawSize.x * scale, 1f, drawSize.y * scale);
            Mesh mesh = (facingLeft ^ spriteNativeFacingLeft) ? MeshPool.plane10Flip : MeshPool.plane10;
            Matrix4x4 matrix = Matrix4x4.TRS(worldPos, rot, meshScale);

            Material drawMat = mat;
            bool hasAlphaOrTint = propBlock != null || tint.HasValue || alpha < 0.999f;
            if (alpha < 0.99f || (tint.HasValue && tint.Value.a < 0.99f))
            {
                drawMat = GetGhostMaterial(mat) ?? mat;
            }

            if (hasAlphaOrTint)
            {
                MaterialPropertyBlock block = propBlock ?? new MaterialPropertyBlock();
                Color c = tint ?? Color.white;
                c.a *= alpha;
                block.SetColor(ShaderPropertyIDs.Color, c);
                GenDraw.DrawMeshNowOrLater(mesh, matrix, drawMat, drawNow: false, properties: block);
            }
            else
            {
                GenDraw.DrawMeshNowOrLater(mesh, matrix, drawMat, drawNow: false);
            }
        }

        /// <summary>在高速挥砍窗口期间绘制半透明重影残影轨迹。</summary>
        public static void DrawWeaponAfterimages(
            Material mat,
            WeaponKeyframe[] keys,
            float elapsedTicks,
            Vector3 basePos,
            float altitudeY,
            Vector2 drawSize,
            bool facingLeft,
            Color ghostTint,
            MaterialPropertyBlock propBlock = null)
        {
            if (mat == null || keys == null || keys.Length == 0)
            {
                return;
            }

            float[] lags = { 1.5f, 3.2f, 5.0f };
            float[] alphas = { 0.42f, 0.26f, 0.14f };

            for (int i = 0; i < lags.Length; i++)
            {
                float sampleT = elapsedTicks - lags[i];
                if (sampleT < 0f)
                {
                    continue;
                }

                WeaponKeyframe kf = SampleWeapon(keys, sampleT);
                DrawWeaponSprite(
                    mat,
                    basePos,
                    altitudeY - 0.002f * (i + 1),
                    kf.PosX,
                    kf.PosZ,
                    kf.AngleDeg,
                    drawSize,
                    facingLeft,
                    kf.Scale,
                    alphas[i],
                    ghostTint,
                    propBlock);
            }
        }

        /// <summary>计算施放者朝向目标平面的 RimWorld 瞄准角（0=北, 90=东, 180=南, 270=西）。</summary>
        public static float ResolveAimAngle(Pawn pawn, Vector3 targetPos)
        {
            if (pawn != null && (targetPos - pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f)
            {
                return (targetPos - pawn.DrawPos).AngleFlat();
            }

            return pawn != null ? pawn.Rotation.AsAngle : 90f;
        }
    }
}
