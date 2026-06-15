from dataclasses import dataclass

@dataclass
class SessionState:
    static: dict | None = None
    dynamic: dict | None = None
    episode: int = 0
    done: bool = False