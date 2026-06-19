"""
Training orchestrator for managing multiple Godot player instances.

Spawns and coordinates multiple PlayerMimic instances with varied configurations,
collecting experiences from each for distributed training.
"""

import asyncio
import subprocess
import os
import sys
import logging
from dataclasses import dataclass, field
from typing import Optional
from datetime import datetime, timedelta
from enum import Enum

from game.player_config import PlayerConfig, PlayerFactory

logger = logging.getLogger(__name__)


class InstanceState(Enum):
    """State of a Godot instance."""
    PENDING = "pending"  # Waiting to spawn
    STARTING = "starting"  # Spawning process
    RUNNING = "running"  # Process alive
    CONNECTED = "connected"  # WebSocket connection established
    EPISODE_ACTIVE = "episode_active"  # Episode in progress
    EPISODE_DONE = "episode_done"  # Episode complete, awaiting next
    TIMEOUT = "timeout"  # Instance exceeded max duration
    CRASHED = "crashed"  # Process died unexpectedly
    STOPPED = "stopped"  # Intentionally terminated


@dataclass
class InstanceMetrics:
    """Metrics for a single instance."""
    episodes_completed: int = 0
    total_transitions: int = 0
    avg_episode_length: float = 0.0
    last_episode_duration: float = 0.0
    boss_wins: int = 0
    player_wins: int = 0


@dataclass
class InstanceInfo:
    """Runtime information for a Godot instance."""
    instance_id: int
    config: PlayerConfig
    state: InstanceState = InstanceState.PENDING
    process: Optional[subprocess.Popen] = None
    process_start_time: Optional[datetime] = None
    episode_start_time: Optional[datetime] = None
    connected_at: Optional[datetime] = None
    last_heartbeat: Optional[datetime] = None
    metrics: InstanceMetrics = field(default_factory=InstanceMetrics)
    error_message: str = ""


class TrainingOrchestrator:
    """
    Manages multiple Godot instances for distributed training.
    
    Orchestrates:
    - Instance spawning with varied PlayerMimic configs
    - WebSocket connection lifecycle
    - Episode lifecycle per instance
    - Timeout detection and recovery
    - Batch coordination for training
    """
    
    def __init__(self, 
                 num_instances: int = 4,
                 godot_executable: str = "",
                 game_scene: str = "res://scenes/train.tscn",
                 max_instance_lifetime: float = 3600.0,  # 1 hour
                 max_episode_duration: float = 600.0,  # 10 minutes
                 headless: bool = True):
        """
        Initialize the orchestrator.
        
        Args:
            num_instances: Number of Godot instances to spawn
            godot_executable: Path to Godot executable (auto-detect if empty)
            game_scene: Scene to load in each instance
            max_instance_lifetime: Max time an instance can run (seconds)
            max_episode_duration: Max duration of a single episode (seconds)
            headless: Run Godot in headless mode (no display)
        """
        self.num_instances = num_instances
        self.godot_executable = godot_executable or self._find_godot_executable()
        self.game_scene = game_scene
        self.max_instance_lifetime = max_instance_lifetime
        self.max_episode_duration = max_episode_duration
        self.headless = headless
        
        self.instances: dict[int, InstanceInfo] = {}
        self.factory = PlayerFactory(seed=42)
        
        logger.info(f"TrainingOrchestrator initialized: {num_instances} instances, "
                   f"Godot: {self.godot_executable}")
    
    def _find_godot_executable(self) -> str:
        """
        Auto-detect Godot executable path.
        
        Checks common install locations on Windows.
        
        Returns:
            Path to Godot executable
        
        Raises:
            FileNotFoundError: If Godot not found
        """
        common_paths = [
            "C:\\Program Files\\Godot\\Godot.exe",
            "C:\\Program Files (x86)\\Godot\\Godot.exe",
            "C:\\Users\\turco\\AppData\\Local\\Godot\\Godot.exe",
        ]
        
        for path in common_paths:
            if os.path.exists(path):
                logger.info(f"Found Godot at {path}")
                return path
        
        raise FileNotFoundError(
            f"Could not find Godot executable. "
            f"Checked: {common_paths}. "
            f"Specify manually via godot_executable parameter."
        )
    
    def create_instance_configs(self, base_seed: int = 0) -> list[PlayerConfig]:
        """
        Generate configurations for all instances.
        
        Args:
            base_seed: Starting seed for reproducibility
        
        Returns:
            List of PlayerConfig instances
        """
        return self.factory.create_batch(
            count=self.num_instances,
            base_seed=base_seed
        )
    
    async def spawn_instances(self, configs: Optional[list[PlayerConfig]] = None) -> bool:
        """
        Spawn all Godot instances with provided configurations.
        
        Args:
            configs: Configurations for each instance (auto-generated if None)
        
        Returns:
            True if all instances spawned successfully
        """
        if configs is None:
            configs = self.create_instance_configs()
        
        if len(configs) != self.num_instances:
            logger.error(f"Expected {self.num_instances} configs, got {len(configs)}")
            return False
        
        logger.info(f"Spawning {self.num_instances} Godot instances...")
        
        for instance_id, config in enumerate(configs):
            success = await self._spawn_single_instance(instance_id, config)
            if not success:
                logger.error(f"Failed to spawn instance {instance_id}")
                await self.cleanup()
                return False
        
        logger.info("All instances spawned successfully")
        return True
    
    async def _spawn_single_instance(self, instance_id: int, config: PlayerConfig) -> bool:
        """
        Spawn a single Godot instance.
        
        Args:
            instance_id: Unique ID for this instance
            config: PlayerConfig for this instance
        
        Returns:
            True if spawn succeeded
        """
        instance_info = InstanceInfo(
            instance_id=instance_id,
            config=config,
            state=InstanceState.STARTING
        )
        
        # Build command line
        args = [
            self.godot_executable,
            self.game_scene,
            f"--instance-id={instance_id}",
            f"--instance-port={7000 + instance_id}",  # Unique port per instance
        ]
        
        # Add player config args
        args.extend(config.to_godot_args())
        
        # Add headless if requested
        if self.headless:
            args.append("--headless")
        
        try:
            # Spawn process with subprocess (not await, as it's sync)
            process = subprocess.Popen(
                args,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0
            )
            
            instance_info.process = process
            instance_info.process_start_time = datetime.now()
            instance_info.state = InstanceState.RUNNING
            
            self.instances[instance_id] = instance_info
            
            logger.info(f"Instance {instance_id} spawned (PID {process.pid}): {config}")
            
            # Give it a moment to start
            await asyncio.sleep(0.5)
            
            return True
        
        except Exception as e:
            logger.error(f"Failed to spawn instance {instance_id}: {e}")
            instance_info.state = InstanceState.CRASHED
            instance_info.error_message = str(e)
            self.instances[instance_id] = instance_info
            return False
    
    async def cleanup(self):
        """Terminate all instances."""
        logger.info("Cleaning up instances...")
        
        for instance_id, info in self.instances.items():
            if info.process and info.process.poll() is None:
                try:
                    info.process.terminate()
                    await asyncio.sleep(0.5)
                    
                    if info.process.poll() is None:
                        info.process.kill()
                    
                    info.state = InstanceState.STOPPED
                    logger.info(f"Instance {instance_id} terminated")
                
                except Exception as e:
                    logger.error(f"Error terminating instance {instance_id}: {e}")
    
    def mark_instance_connected(self, instance_id: int) -> bool:
        """
        Mark an instance as connected.
        
        Called by connection handler when WebSocket connects.
        
        Args:
            instance_id: Instance ID
        
        Returns:
            True if instance found and marked
        """
        if instance_id not in self.instances:
            logger.warning(f"Unknown instance_id: {instance_id}")
            return False
        
        info = self.instances[instance_id]
        info.state = InstanceState.CONNECTED
        info.connected_at = datetime.now()
        info.last_heartbeat = datetime.now()
        
        logger.info(f"Instance {instance_id} connected")
        return True
    
    def mark_episode_start(self, instance_id: int) -> bool:
        """Mark the start of an episode for an instance."""
        if instance_id not in self.instances:
            return False
        
        info = self.instances[instance_id]
        info.episode_start_time = datetime.now()
        info.state = InstanceState.EPISODE_ACTIVE
        return True
    
    def mark_episode_end(self, instance_id: int, 
                        boss_wins: bool, 
                        transitions_count: int) -> bool:
        """
        Mark the end of an episode for an instance.
        
        Args:
            instance_id: Instance ID
            boss_wins: Whether the boss won
            transitions_count: Number of transitions collected
        
        Returns:
            True if instance found and marked
        """
        if instance_id not in self.instances:
            return False
        
        info = self.instances[instance_id]
        
        if info.episode_start_time:
            duration = (datetime.now() - info.episode_start_time).total_seconds()
            info.metrics.last_episode_duration = duration
        
        info.metrics.episodes_completed += 1
        info.metrics.total_transitions += transitions_count
        
        if boss_wins:
            info.metrics.boss_wins += 1
        else:
            info.metrics.player_wins += 1
        
        info.state = InstanceState.EPISODE_DONE
        logger.info(f"Instance {instance_id} episode end: "
                   f"boss_wins={boss_wins}, transitions={transitions_count}")
        
        return True
    
    def get_instance_status(self, instance_id: int) -> Optional[dict]:
        """Get status of a single instance."""
        if instance_id not in self.instances:
            return None
        
        info = self.instances[instance_id]
        return {
            "instance_id": instance_id,
            "state": info.state.value,
            "config": str(info.config),
            "episodes_completed": info.metrics.episodes_completed,
            "boss_wins": info.metrics.boss_wins,
            "player_wins": info.metrics.player_wins,
            "total_transitions": info.metrics.total_transitions,
            "connected_at": info.connected_at.isoformat() if info.connected_at else None,
            "last_heartbeat": info.last_heartbeat.isoformat() if info.last_heartbeat else None,
        }
    
    def get_all_status(self) -> dict:
        """Get status of all instances."""
        statuses = {}
        for instance_id in self.instances:
            status = self.get_instance_status(instance_id)
            if status:
                statuses[instance_id] = status
        
        return {
            "orchestrator": {
                "num_instances": self.num_instances,
                "godot_executable": self.godot_executable,
                "timestamp": datetime.now().isoformat(),
            },
            "instances": statuses,
        }
    
    async def monitor_instances(self):
        """
        Periodically monitor instance health and detect timeouts.
        
        This should be run as a background task.
        """
        while True:
            await asyncio.sleep(10)  # Check every 10 seconds
            
            for instance_id, info in list(self.instances.items()):
                # Check if process is still alive
                if info.process and info.process.poll() is not None:
                    logger.warning(f"Instance {instance_id} process died")
                    info.state = InstanceState.CRASHED
                    continue
                
                # Check for instance lifetime timeout
                if info.process_start_time:
                    elapsed = (datetime.now() - info.process_start_time).total_seconds()
                    if elapsed > self.max_instance_lifetime:
                        logger.warning(f"Instance {instance_id} exceeded max lifetime")
                        info.state = InstanceState.TIMEOUT
                        await self._terminate_instance(instance_id)
    
    async def _terminate_instance(self, instance_id: int):
        """Terminate a specific instance."""
        if instance_id not in self.instances:
            return
        
        info = self.instances[instance_id]
        if info.process and info.process.poll() is None:
            try:
                info.process.terminate()
                await asyncio.sleep(0.5)
                if info.process.poll() is None:
                    info.process.kill()
                info.state = InstanceState.STOPPED
            except Exception as e:
                logger.error(f"Error terminating instance {instance_id}: {e}")
    
    def get_total_transitions(self) -> int:
        """Get total transitions collected across all instances."""
        return sum(info.metrics.total_transitions for info in self.instances.values())
    
    def get_total_episodes(self) -> int:
        """Get total episodes completed across all instances."""
        return sum(info.metrics.episodes_completed for info in self.instances.values())
    
    def get_connected_instances(self) -> list[int]:
        """Get IDs of all connected instances."""
        return [
            instance_id for instance_id, info in self.instances.items()
            if info.state in (InstanceState.CONNECTED, InstanceState.EPISODE_ACTIVE, InstanceState.EPISODE_DONE)
        ]
