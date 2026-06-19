"""Session state tracking for each connection."""

from dataclasses import dataclass

@dataclass
class SessionState:
    """Per-connection game session state."""
    static: dict | None = None
    dynamic: dict | None = None
    episode: int = 0
    done: bool = False
