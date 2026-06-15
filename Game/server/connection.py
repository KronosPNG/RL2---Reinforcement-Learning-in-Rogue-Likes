from state import SessionState
from ai import BossAI
from protocol import (
    STATIC_STATE, DYNAMIC_STATE, OUTCOME, COMMAND,
    decode_static_state, decode_dynamic_state, encode_action
)

class PacketHandler:
    def __init__(self):
        self.state = SessionState()
        self.ai = BossAI()

    async def handle(self, websocket, msg_type: int, payload: bytes):
        if msg_type == STATIC_STATE:
            self.state.static = decode_static_state(payload)

        elif msg_type == DYNAMIC_STATE:
            self.state.dynamic = decode_dynamic_state(payload)

            x, y, action_id = self.ai.choose_action(self.state.dynamic)
            await websocket.send(encode_action(x, y, action_id))

        elif msg_type == OUTCOME:
            self.state.done = True
            won = payload[0] != 0
            print("Episode ended:", won)

        elif msg_type == COMMAND:
            cmd = payload[0]
            if cmd == 0:
                self.state = SessionState()
            elif cmd == 1:
                await websocket.close()