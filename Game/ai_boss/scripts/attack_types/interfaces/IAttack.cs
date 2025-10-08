using Godot;
using System;

public interface IAttack
{
    void Execute(WeaponBase weapon, Vector2 target, bool facingLeft);
    void Interrupt(WeaponBase weapon);
}