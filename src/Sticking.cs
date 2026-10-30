using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace StickyNades;

public partial class StickyNades
{
    // How far off the hit surface to rest the grenade so it doesn't clip inside.
    private const float SurfaceOffset = 2.0f;

    // Extra padding around player boxes so grazing hits (head, neck, shoulders)
    // still count as a hit before the engine bounces the grenade off.
    private const float PlayerHitPadding = 4.0f;

    // Half-size of the grenade used when its collision bounds aren't available.
    private const float DefaultNadeHalfExtent = 2.0f;

    // A change in velocity bigger than this in one tick (that gravity can't
    // explain) means the engine bounced the grenade off something.
    private const float BounceVelocityThreshold = 30.0f;

    // Ignore velocity changes for the first few ticks while the throw settles.
    private const int BounceCheckDelayTicks = 2;

    // If the hitbox trace misses, rest the grenade this far from the player's
    // centre line so it still sits on the model rather than floating.
    private const float FallbackBodyRadius = 7.0f;

    // Keep the grenade's resting height this far inside the top and bottom of
    // the player box, so it lands on the head or feet, not above or below them.
    private const float BodyHeightInset = 6.0f;

    private static readonly TraceOptions StickTrace = new()
    {
        InteractsWith = Masks.Shot,
    };

    private static readonly TraceOptions HitboxTrace = new()
    {
        InteractsWith = Contents.Hitbox,
    };

    private sealed class TrackedNade
    {
        public required CHEGrenadeProjectile Projectile;
        public required Vector LastPosition;
        public required Vector LastVelocity;

        public int LastBounces;
        public bool LastHitEnemy;

        public int Ticks;
    }

    private readonly record struct PlayerHit(CCSPlayerPawn Pawn, Vector Position, float Fraction);

    private bool ProcessNade(TrackedNade tracked, List<CCSPlayerPawn> pawns)
    {
        tracked.Ticks++;

        var nade = tracked.Projectile;
        var current = Copy(nade.AbsOrigin!);
        var velocity = Copy(nade.AbsVelocity!);
        var thrower = nade.Thrower.Value;
        var halfExtent = GetNadeHalfExtent(nade);

        var from = tracked.LastPosition;
        var ahead = current + velocity * Server.TickInterval;

        var worldHit = TraceWorld(nade, thrower, from, ahead, halfExtent);
        var playerHit = SweepPlayers(thrower, pawns, from, ahead, halfExtent);

        if (playerHit is { } p && (worldHit is null || p.Fraction <= worldHit.Value.Fraction))
        {
            Stick(nade, p.Position, p.Pawn);
            return true;
        }

        if (worldHit is { } w)
        {
            Stick(nade, w.EndPos + w.Normal * SurfaceOffset, GetHitPawn(w, thrower));
            return true;
        }

        var bounced = nade.Bounces > tracked.LastBounces
            || (nade.HasEverHitEnemy && !tracked.LastHitEnemy)
            || (tracked.Ticks > BounceCheckDelayTicks && DidBounce(tracked.LastVelocity, velocity));

        if (bounced)
        {
            var padding = halfExtent + PlayerHitPadding * 2;
            var victim = FindNearestPawn(thrower, pawns, from, padding);

            var bouncedOffIgnored = victim is null && IsNearIgnoredPawn(thrower, pawns, from, current, padding);

            if (!bouncedOffIgnored)
            {
                Stick(nade, victim is null ? current : from, victim);
                return true;
            }
        }

        tracked.LastPosition = current;
        tracked.LastVelocity = velocity;
        tracked.LastBounces = nade.Bounces;
        tracked.LastHitEnemy = nade.HasEverHitEnemy;
        return false;
    }

    private bool IsIgnored(CCSPlayerPawn? pawn, CCSPlayerPawn? thrower)
    {
        if (pawn is null || thrower is null)
            return false;

        if (pawn.Index == thrower.Index)
            return true;

        return !Config.StickToTeammates && !_teammatesAreEnemies && pawn.TeamNum == thrower.TeamNum;
    }

    private bool IsNearIgnoredPawn(CCSPlayerPawn? thrower, List<CCSPlayerPawn> pawns, Vector from, Vector current,
        float padding)
    {
        foreach (var pawn in pawns)
        {
            if (IsIgnored(pawn, thrower) && (IsNearPawn(from, pawn, padding) || IsNearPawn(current, pawn, padding)))
                return true;
        }

        return false;
    }

    private TraceResult? TraceWorld(CHEGrenadeProjectile nade, CCSPlayerPawn? thrower,
        Vector from, Vector to, float halfExtent)
    {
        var mins = new Vector(-halfExtent, -halfExtent, -halfExtent);
        var maxs = new Vector(halfExtent, halfExtent, halfExtent);

        var result = Trace.TraceHullShape(from, to, mins, maxs, nade, StickTrace);
        if (!result.DidHit() || IsIgnored(ResolvePawn(result.HitEntity()), thrower))
            return null;

        return result;
    }

    private PlayerHit? SweepPlayers(CCSPlayerPawn? thrower, List<CCSPlayerPawn> pawns,
        Vector from, Vector to, float halfExtent)
    {
        PlayerHit? best = null;

        foreach (var pawn in pawns)
        {
            if (IsIgnored(pawn, thrower))
                continue;

            var (min, max) = GetPawnBox(pawn, halfExtent + PlayerHitPadding);
            var fraction = SegmentBoxEntry(from, to, min, max);
            if (fraction is null || (best is not null && fraction >= best.Value.Fraction))
                continue;

            var position = from + (to - from) * fraction.Value;
            best = new PlayerHit(pawn, position, fraction.Value);
        }

        return best;
    }

    private CCSPlayerPawn? FindNearestPawn(CCSPlayerPawn? thrower,
        List<CCSPlayerPawn> pawns, Vector position, float padding)
    {
        CCSPlayerPawn? best = null;
        var bestDistance = float.MaxValue;

        foreach (var pawn in pawns)
        {
            if (IsIgnored(pawn, thrower) || !IsNearPawn(position, pawn, padding))
                continue;

            var distance = (pawn.AbsOrigin! - position).Length();
            if (distance < bestDistance)
            {
                best = pawn;
                bestDistance = distance;
            }
        }

        return best;
    }

    private CCSPlayerPawn? GetHitPawn(TraceResult result, CCSPlayerPawn? thrower)
    {
        var pawn = ResolvePawn(result.HitEntity());
        return IsIgnored(pawn, thrower) ? null : pawn;
    }

    private static bool IsNearPawn(Vector position, CCSPlayerPawn pawn, float padding)
    {
        var (min, max) = GetPawnBox(pawn, padding);
        return IsInsideBox(position, min, max);
    }

    private static bool DidBounce(Vector previous, Vector current)
    {
        var dx = current.X - previous.X;
        var dy = current.Y - previous.Y;
        var horizontalChange = MathF.Sqrt(dx * dx + dy * dy);
        var verticalGain = current.Z - previous.Z;

        return horizontalChange > BounceVelocityThreshold || verticalGain > BounceVelocityThreshold;
    }

    private static List<CCSPlayerPawn> GetAlivePawns()
    {
        var pawns = new List<CCSPlayerPawn>();
        foreach (var controller in Utilities.GetPlayers())
        {
            var pawn = controller.PlayerPawn.Value;
            if (pawn is { IsValid: true, AbsOrigin: not null } && pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                pawns.Add(pawn);
        }

        return pawns;
    }

    private static (Vector Min, Vector Max) GetPawnBox(CCSPlayerPawn pawn, float padding)
    {
        var origin = pawn.AbsOrigin!;
        var collision = pawn.Collision;

        var mins = collision?.Mins is { } cmin && cmin.Length() > 0 ? cmin : new Vector(-16, -16, 0);
        var maxs = collision?.Maxs is { } cmax && cmax.Length() > 0 ? cmax : new Vector(16, 16, 72);

        return (
            new Vector(origin.X + mins.X - padding, origin.Y + mins.Y - padding, origin.Z + mins.Z - padding),
            new Vector(origin.X + maxs.X + padding, origin.Y + maxs.Y + padding, origin.Z + maxs.Z + padding));
    }

    private static float GetNadeHalfExtent(CHEGrenadeProjectile nade)
    {
        var collision = nade.Collision;
        if (collision?.Maxs is null || collision.Mins is null)
            return DefaultNadeHalfExtent;

        var extent = MathF.Max(
            MathF.Max(collision.Maxs.X - collision.Mins.X, collision.Maxs.Y - collision.Mins.Y),
            collision.Maxs.Z - collision.Mins.Z) / 2;

        return extent > 0 ? extent : DefaultNadeHalfExtent;
    }

    /// <summary>
    /// Slab test: returns how far along the segment (0 to 1) it first enters the box, or null if it misses.
    /// </summary>
    private static float? SegmentBoxEntry(Vector from, Vector to, Vector min, Vector max)
    {
        float tMin = 0, tMax = 1;

        for (var axis = 0; axis < 3; axis++)
        {
            var start = Axis(from, axis);
            var delta = Axis(to, axis) - start;
            var lo = Axis(min, axis);
            var hi = Axis(max, axis);

            if (MathF.Abs(delta) < 1e-6f)
            {
                if (start < lo || start > hi)
                    return null;
                continue;
            }

            var t1 = (lo - start) / delta;
            var t2 = (hi - start) / delta;
            if (t1 > t2)
                (t1, t2) = (t2, t1);

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax)
                return null;
        }

        return tMin;
    }

    private static float Axis(Vector v, int axis) => axis switch
    {
        0 => v.X,
        1 => v.Y,
        _ => v.Z,
    };

    private static bool IsInsideBox(Vector p, Vector min, Vector max) =>
        p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y && p.Z >= min.Z && p.Z <= max.Z;

    private static CCSPlayerPawn? ResolvePawn(CEntityInstance? entity)
    {
        for (var depth = 0; depth < 3 && entity is not null && entity.IsValid; depth++)
        {
            if (entity.DesignerName == "player")
                return entity.As<CCSPlayerPawn>();

            entity = entity.As<CBaseEntity>().OwnerEntity.Value;
        }

        return null;
    }

    private static Vector SnapToPlayer(CHEGrenadeProjectile nade, CCSPlayerPawn pawn, Vector contact)
    {
        var (min, max) = GetPawnBox(pawn, 0);
        var origin = pawn.AbsOrigin!;

        var height = Math.Clamp(contact.Z, min.Z + BodyHeightInset, max.Z - BodyHeightInset);
        var centre = new Vector(origin.X, origin.Y, height);

        var result = Trace.TraceEndShape(contact, centre, nade, HitboxTrace);
        if (result.DidHit() && ResolvePawn(result.HitEntity())?.Handle == pawn.Handle)
            return result.EndPos;

        var dx = contact.X - centre.X;
        var dy = contact.Y - centre.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance < 1e-3f)
            return centre;

        var radius = MathF.Min(distance, FallbackBodyRadius);
        return new Vector(centre.X + dx / distance * radius, centre.Y + dy / distance * radius, height);
    }

    private void Stick(CHEGrenadeProjectile nade, Vector position, CCSPlayerPawn? victim)
    {
        if (victim is not null)
            position = SnapToPlayer(nade, victim, position);

        nade.Teleport(position, null, new Vector(0, 0, 0));

        nade.MoveType = MoveType_t.MOVETYPE_NONE;
        nade.ActualMoveType = MoveType_t.MOVETYPE_NONE;
        Utilities.SetStateChanged(nade, "CBaseEntity", "m_MoveType");

        if (victim is null)
            return;

        nade.AcceptInput("SetParent", victim, nade, "!activator");

        AnnounceStick(nade.Thrower.Value?.OriginalController.Value, victim.OriginalController.Value);
    }

    private static Vector Copy(Vector v) => new(v.X, v.Y, v.Z);
}
