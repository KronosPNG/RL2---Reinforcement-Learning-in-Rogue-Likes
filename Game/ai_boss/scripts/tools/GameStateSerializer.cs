using System.IO;
using Godot;

/// <summary>
/// Hand-rolled binary serialization for StaticState and DynamicsState.
/// Field write/read order MUST match exactly between Write* and Read* methods.
/// If you add/remove a field on any of the data classes, update both sides here,
/// and update the corresponding *Size constant.
/// </summary>
public static class GameStateSerializer
{
    // ===================== Size constants =====================

    public const int EquipmentSize = (14 * 4) + 1;              // 14 floats + 1 bool = 61
    public const int RoomBoundsSize = 4 * 4;                     // 4 floats = 16
    public const int StaticStateSize = EquipmentSize + RoomBoundsSize; // 73

    public const int PlayerStateSize = (4 * 4) + 4 + (9 * 4);    // 2 Vector2 + 1 int + 9 floats = 56
    public const int BossStateSize = (2 * 4) + 4 + 4 + (4 * 4) + 4 + (4 * 4) + 2 + 1 + (4 * 4); // 71
    public const int ProjectileStateSize = 4 + 4 + 4 + (2 * 4) + 4; // 24
    public const int DynamicsStateSize = PlayerStateSize + BossStateSize + ProjectileStateSize; // 151

    // ===================== StaticState =====================

    public static byte[] SerializeStatic(GlobalState.StaticState state)
    {
        using var ms = new MemoryStream(StaticStateSize);
        using var w = new BinaryWriter(ms);

        WriteEquipment(w, state.Equipment);
        WriteRoomBounds(w, state.RoomData);

        return ms.ToArray();
    }

    public static GlobalState.StaticState DeserializeStatic(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);

        return new GlobalState.StaticState
        {
            Equipment = ReadEquipment(r),
            RoomData = ReadRoomBounds(r)
        };
    }

    // ===================== DynamicsState =====================

    public static byte[] SerializeDynamics(GlobalState.DynamicsState state)
    {
        using var ms = new MemoryStream(DynamicsStateSize);
        using var w = new BinaryWriter(ms);

        WritePlayerState(w, state.Player);
        WriteBossState(w, state.Boss);
        WriteProjectileState(w, state.Projectiles);

        return ms.ToArray();
    }

    public static GlobalState.DynamicsState DeserializeDynamics(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);

        return new GlobalState.DynamicsState
        {
            Player = ReadPlayerState(r),
            Boss = ReadBossState(r),
            Projectiles = ReadProjectileState(r)
        };
    }

    // ===================== Helpers =====================

    private static void WriteVector2(BinaryWriter w, Vector2 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
    }

    private static Vector2 ReadVector2(BinaryReader r)
    {
        return new Vector2(r.ReadSingle(), r.ReadSingle());
    }

    // ===================== EquipmentData =====================

    private static void WriteEquipment(BinaryWriter w, GlobalState.EquipmentData e)
    {
        w.Write(e.WeaponDamageLight);
        w.Write(e.WeaponDamageHeavy);
        w.Write(e.WeaponRangeLight);
        w.Write(e.WeaponRangeHeavy);
        w.Write(e.WeaponChargeTimeLight);
        w.Write(e.WeaponChargeTimeHeavy);
        w.Write(e.WeaponCooldownLight);
        w.Write(e.WeaponCooldownHeavy);

        w.Write(e.ArmorDamageModifier);
        w.Write(e.ArmorKnockbackModifier);
        w.Write(e.ArmorSpeedModifier);

        w.Write(e.HasConsumable);
        w.Write(e.ConsumableEffectDuration);
        w.Write(e.ConsumableHealAmount);
        w.Write(e.ConsumableChargeAmount);
    }

    private static GlobalState.EquipmentData ReadEquipment(BinaryReader r)
    {
        return new GlobalState.EquipmentData
        {
            WeaponDamageLight = r.ReadSingle(),
            WeaponDamageHeavy = r.ReadSingle(),
            WeaponRangeLight = r.ReadSingle(),
            WeaponRangeHeavy = r.ReadSingle(),
            WeaponChargeTimeLight = r.ReadSingle(),
            WeaponChargeTimeHeavy = r.ReadSingle(),
            WeaponCooldownLight = r.ReadSingle(),
            WeaponCooldownHeavy = r.ReadSingle(),

            ArmorDamageModifier = r.ReadSingle(),
            ArmorKnockbackModifier = r.ReadSingle(),
            ArmorSpeedModifier = r.ReadSingle(),

            HasConsumable = r.ReadBoolean(),
            ConsumableEffectDuration = r.ReadSingle(),
            ConsumableHealAmount = r.ReadSingle(),
            ConsumableChargeAmount = r.ReadSingle()
        };
    }

    // ===================== RoomBounds =====================

    private static void WriteRoomBounds(BinaryWriter w, GlobalState.RoomBounds rb)
    {
        w.Write(rb.MinX);
        w.Write(rb.MaxX);
        w.Write(rb.MinY);
        w.Write(rb.MaxY);
        // Width/Height are derived (computed properties) — not serialized.
    }

    private static GlobalState.RoomBounds ReadRoomBounds(BinaryReader r)
    {
        return new GlobalState.RoomBounds
        {
            MinX = r.ReadSingle(),
            MaxX = r.ReadSingle(),
            MinY = r.ReadSingle(),
            MaxY = r.ReadSingle()
        };
    }

    // ===================== PlayerState =====================

    private static void WritePlayerState(BinaryWriter w, GlobalState.PlayerState p)
    {
        WriteVector2(w, p.Position);
        WriteVector2(w, p.MovementDirection);
        w.Write(p.CurrentAction);

        w.Write(p.Health);
        w.Write(p.MaxHealth);

        w.Write(p.CurrentLightAttackCooldown);
        w.Write(p.CurrentHeavyAttackCooldown);
        w.Write(p.CurrentLightAttackChargeTime);
        w.Write(p.CurrentHeavyAttackChargeTime);

        w.Write(p.InvulnerabilityTimeRemaining);
        w.Write(p.DamageTaken);

        w.Write(p.CurrentConsumableChargeTime);
    }

    private static GlobalState.PlayerState ReadPlayerState(BinaryReader r)
    {
        return new GlobalState.PlayerState
        {
            Position = ReadVector2(r),
            MovementDirection = ReadVector2(r),
            CurrentAction = r.ReadInt32(),

            Health = r.ReadSingle(),
            MaxHealth = r.ReadSingle(),

            CurrentLightAttackCooldown = r.ReadSingle(),
            CurrentHeavyAttackCooldown = r.ReadSingle(),
            CurrentLightAttackChargeTime = r.ReadSingle(),
            CurrentHeavyAttackChargeTime = r.ReadSingle(),

            InvulnerabilityTimeRemaining = r.ReadSingle(),
            DamageTaken = r.ReadSingle(),

            CurrentConsumableChargeTime = r.ReadSingle()
        };
    }

    // ===================== BossState =====================

    private static void WriteBossState(BinaryWriter w, GlobalState.BossActionState b)
    {
        WriteVector2(w, b.Position);
        w.Write(b.Health);
        w.Write(b.MaxHealth);

        for (int i = 0; i < 4; i++) w.Write(b.AttackCooldowns[i]);
        for (int i = 0; i < 4; i++) w.Write(b.AttackCooldownTimers[i]);
        w.Write(b.DashCooldownTimer);

        w.Write(b.CurrentAttackId);
        w.Write(b.IsLocked);

        w.Write(b.InvulnerabilityTimeRemaining);
        w.Write(b.DamageTaken);

        w.Write(b.DistanceToPlayer);
        w.Write(b.AngleToPlayer);
    }

    private static GlobalState.BossActionState ReadBossState(BinaryReader r)
    {
        var b = new GlobalState.BossActionState
        {
            Position = ReadVector2(r),
            Health = r.ReadSingle(),
            MaxHealth = r.ReadSingle()
        };

        for (int i = 0; i < 4; i++) b.AttackCooldowns[i] = r.ReadSingle();
        for (int i = 0; i < 4; i++) b.AttackCooldownTimers[i] = r.ReadSingle();
        b.DashCooldownTimer = r.ReadSingle();

        b.CurrentAttackId = r.ReadInt16();
        b.IsLocked = r.ReadBoolean();

        b.InvulnerabilityTimeRemaining = r.ReadSingle();
        b.DamageTaken = r.ReadSingle();

        b.DistanceToPlayer = r.ReadSingle();
        b.AngleToPlayer = r.ReadSingle();

        return b;
    }

    // ===================== ProjectileState =====================

    private static void WriteProjectileState(BinaryWriter w, GlobalState.ProjectileState p)
    {
        w.Write(p.ActiveProjectileCount);
        w.Write(p.NearestProjectileDistance);
        w.Write(p.NearestProjectileAngle);
        WriteVector2(w, p.NearestProjectileVelocity);
        w.Write(p.NearestProjectileDamage);
    }

    private static GlobalState.ProjectileState ReadProjectileState(BinaryReader r)
    {
        return new GlobalState.ProjectileState
        {
            ActiveProjectileCount = r.ReadInt32(),
            NearestProjectileDistance = r.ReadSingle(),
            NearestProjectileAngle = r.ReadSingle(),
            NearestProjectileVelocity = ReadVector2(r),
            NearestProjectileDamage = r.ReadSingle()
        };
    }
}