"""
Player configuration for PlayerMimic instances.

Defines the equipment, behaviors, and characteristics of a player opponent
that will be spawned for training episodes.
"""

from __future__ import annotations
from dataclasses import dataclass
from typing import Optional
import random


@dataclass
class PlayerConfig:
    """Configuration for a single PlayerMimic instance."""
    
    # Equipment (affects combat stats and gameplay)
    weapon: str  # sword, dagger, bow, staff
    armor: str   # light_armor, medium_armor, heavy_armor, shirt
    consumable: str  # none, medkit, potion
    
    # Behaviors (affects decision-making and playstyle)
    attack_behavior: str  # AttackCowardly, AttackEdgelord, AttackSpam, AttackTactical
    dodge_behavior: str   # DodgeNever, DodgePreemptive, DodgeRandom, DodgeReactive
    consumable_behavior: str  # ConsumableNone, ConsumableRandom, ConsumableScaredyCat, ConsumableTactical, ConsumableThreshold
    wander_behavior: str  # WanderHide, WanderImmovable, WanderRandomWalk
    aggro_behavior: str  # AggroFollowTarget, AggroKeepDistance, AggroKeepDistanceStrafe (NOT AggroFollowGaze)
    
    # Metadata
    seed: int = 0  # For reproducibility
    name: str = ""  # Display name
    
    def to_godot_args(self) -> list[str]:
        """
        Convert config to Godot command-line arguments.
        
        Returns:
            List of --key=value arguments for Godot
        """
        return [
            f"--player-weapon={self.weapon}",
            f"--player-armor={self.armor}",
            f"--player-consumable={self.consumable}",
            f"--player-attack-behavior={self.attack_behavior}",
            f"--player-dodge-behavior={self.dodge_behavior}",
            f"--player-consumable-behavior={self.consumable_behavior}",
            f"--player-wander-behavior={self.wander_behavior}",
            f"--player-aggro-behavior={self.aggro_behavior}",
        ]
    
    def __str__(self) -> str:
        """Human-readable description of config."""
        return (
            f"{self.name or f'Player_{self.seed}'}: "
            f"{self.weapon}/{self.armor}/{self.consumable} | "
            f"Attack:{self.attack_behavior} Dodge:{self.dodge_behavior} "
            f"Consumable:{self.consumable_behavior}"
        )


class PlayerFactory:
    """Factory for generating diverse PlayerMimic configurations."""

    # Available options for each category
    WEAPONS = ["sword", "dagger", "bow", "staff"]
    ARMORS = ["light_armor", "medium_armor", "heavy_armor", "shirt"]
    CONSUMABLES = ["none", "medkit", "potion"]

    ATTACK_BEHAVIORS = ["AttackCowardly", "AttackEdgelord", "AttackSpam", "AttackTactical"]
    DODGE_BEHAVIORS = ["DodgeNever", "DodgePreemptive", "DodgeRandom", "DodgeReactive"]
    CONSUMABLE_BEHAVIORS = ["ConsumableNone", "ConsumableRandom", "ConsumableScaredyCat",
                            "ConsumableTactical", "ConsumableThreshold"]
    WANDER_BEHAVIORS = ["WanderHide", "WanderImmovable", "WanderRandomWalk"]
    AGGRO_BEHAVIORS = ["AggroFollowTarget", "AggroKeepDistance", "AggroKeepDistanceStrafe", "AggroStandStill"]  # Exclude AggroFollowGaze

    # The first 8 slots are always these named archetypes; extras are randomly generated.
    ARCHETYPES: list[PlayerConfig] = [
        PlayerConfig("sword",  "medium_armor", "potion", "AttackTactical",  "DodgePreemptive", "ConsumableTactical",   "WanderRandomWalk", "AggroFollowTarget",         name="Adventurer"),
        PlayerConfig("dagger", "light_armor",  "medkit", "AttackSpam",      "DodgeReactive",   "ConsumableThreshold",  "WanderHide",       "AggroFollowTarget",         name="Rogue"),
        PlayerConfig("bow",    "shirt",        "medkit", "AttackTactical",  "DodgeReactive",   "ConsumableTactical",   "WanderRandomWalk", "AggroKeepDistanceStrafe",   name="Ranger"),
        PlayerConfig("sword",  "heavy_armor",  "none",   "AttackEdgelord",  "DodgeNever",      "ConsumableNone",       "WanderImmovable",  "AggroStandStill",           name="Edgelord"),
        PlayerConfig("staff",  "light_armor",  "potion", "AttackEdgelord",  "DodgeNever",      "ConsumableScaredyCat", "WanderHide",       "AggroKeepDistance",         name="Wizard"),
        PlayerConfig("dagger", "shirt",        "none",   "AttackCowardly",  "DodgeRandom",     "ConsumableNone",       "WanderImmovable",  "AggroFollowTarget",         name="Noob"),
        PlayerConfig("staff",  "heavy_armor",  "potion", "AttackCowardly",  "DodgeRandom",     "ConsumableScaredyCat", "WanderHide",       "AggroKeepDistanceStrafe",   name="Coward"),
        PlayerConfig("bow",    "medium_armor", "medkit", "AttackSpam",      "DodgePreemptive", "ConsumableThreshold",  "WanderImmovable",  "AggroKeepDistance",         name="Sniper"),
    ]
    
    def __init__(self, seed: int = 42):
        """
        Initialize the factory with a random seed.
        
        Args:
            seed: Random seed for reproducible generation
        """
        self.seed = seed
        self.rng = random.Random(seed)
    
    def create_random_config(self, seed: int | None = None, name: str = "") -> PlayerConfig:
        """
        Generate a random player configuration.
        
        Args:
            seed: Optional override for this specific config
            name: Optional display name
        
        Returns:
            PlayerConfig with randomly selected values
        """
        if seed is None:
            seed = self.rng.randint(0, 2**31 - 1)
        
        local_rng = random.Random(seed)
        
        return PlayerConfig(
            weapon=local_rng.choice(self.WEAPONS),
            armor=local_rng.choice(self.ARMORS),
            consumable=local_rng.choice(self.CONSUMABLES),
            attack_behavior=local_rng.choice(self.ATTACK_BEHAVIORS),
            dodge_behavior=local_rng.choice(self.DODGE_BEHAVIORS),
            consumable_behavior=local_rng.choice(self.CONSUMABLE_BEHAVIORS),
            wander_behavior=local_rng.choice(self.WANDER_BEHAVIORS),
            aggro_behavior=local_rng.choice(self.AGGRO_BEHAVIORS),
            seed=seed,
            name=name or f"P{seed % 10000}",
        )
    
    def create_config(self, 
                      weapon: str = "random",
                      armor: str = "random",
                      consumable: str = "random",
                      attack_behavior: str = "random",
                      dodge_behavior: str = "random",
                      consumable_behavior: str = "random",
                      wander_behavior: str = "random",
                      aggro_behavior: str = "random",
                      seed: int = 0,
                      name: str = "") -> PlayerConfig:
        """
        Create a player config with specific or random values.
        
        Any parameter set to "random" will be randomly selected.
        
        Args:
            weapon: Weapon type (or "random")
            armor: Armor type (or "random")
            consumable: Consumable type (or "random")
            attack_behavior: Attack behavior (or "random")
            dodge_behavior: Dodge behavior (or "random")
            consumable_behavior: Consumable behavior (or "random")
            wander_behavior: Wander behavior (or "random")
            aggro_behavior: Aggro behavior (or "random")
            seed: Seed for reproducibility
            name: Display name
        
        Returns:
            PlayerConfig
        """
        local_rng = random.Random(seed)
        
        return PlayerConfig(
            weapon=weapon if weapon != "random" else local_rng.choice(self.WEAPONS),
            armor=armor if armor != "random" else local_rng.choice(self.ARMORS),
            consumable=consumable if consumable != "random" else local_rng.choice(self.CONSUMABLES),
            attack_behavior=attack_behavior if attack_behavior != "random" else local_rng.choice(self.ATTACK_BEHAVIORS),
            dodge_behavior=dodge_behavior if dodge_behavior != "random" else local_rng.choice(self.DODGE_BEHAVIORS),
            consumable_behavior=consumable_behavior if consumable_behavior != "random" else local_rng.choice(self.CONSUMABLE_BEHAVIORS),
            wander_behavior=wander_behavior if wander_behavior != "random" else local_rng.choice(self.WANDER_BEHAVIORS),
            aggro_behavior=aggro_behavior if aggro_behavior != "random" else local_rng.choice(self.AGGRO_BEHAVIORS),
            seed=seed,
            name=name or f"Player_{seed}",
        )
    
    def create_batch(self, count: int, base_seed: int = 0) -> list[PlayerConfig]:
        """
        Generate a batch of player configurations.

        The first 8 slots are always the named archetypes (Adventurer, Rogue, …).
        Any additional slots beyond 8 are randomly generated.

        Args:
            count: Number of configs to generate
            base_seed: Starting seed for random configs beyond the first 8

        Returns:
            List of PlayerConfig instances
        """
        configs = list(self.ARCHETYPES[:count])
        for i in range(len(configs), count):
            config = self.create_random_config(
                seed=base_seed + i,
                name=f"P{i+1}"
            )
            configs.append(config)
        return configs
    
    @staticmethod
    def validate_config(config: PlayerConfig) -> tuple[bool, str]:
        """
        Validate that a config has valid values.
        
        Args:
            config: PlayerConfig to validate
        
        Returns:
            Tuple of (is_valid, error_message)
        """
        errors = []
        
        if config.weapon not in PlayerFactory.WEAPONS:
            errors.append(f"Invalid weapon: {config.weapon}")
        if config.armor not in PlayerFactory.ARMORS:
            errors.append(f"Invalid armor: {config.armor}")
        if config.consumable not in PlayerFactory.CONSUMABLES:
            errors.append(f"Invalid consumable: {config.consumable}")
        if config.attack_behavior not in PlayerFactory.ATTACK_BEHAVIORS:
            errors.append(f"Invalid attack behavior: {config.attack_behavior}")
        if config.dodge_behavior not in PlayerFactory.DODGE_BEHAVIORS:
            errors.append(f"Invalid dodge behavior: {config.dodge_behavior}")
        if config.consumable_behavior not in PlayerFactory.CONSUMABLE_BEHAVIORS:
            errors.append(f"Invalid consumable behavior: {config.consumable_behavior}")
        if config.wander_behavior not in PlayerFactory.WANDER_BEHAVIORS:
            errors.append(f"Invalid wander behavior: {config.wander_behavior}")
        if config.aggro_behavior not in PlayerFactory.AGGRO_BEHAVIORS:
            errors.append(f"Invalid aggro behavior: {config.aggro_behavior}")
        
        if errors:
            return False, "; ".join(errors)
        return True, ""
