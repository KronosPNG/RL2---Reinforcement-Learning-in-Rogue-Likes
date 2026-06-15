import asyncio
import websockets

from connection import PacketHandler

async def connection_handler(websocket):
    handler = PacketHandler()

    print("client connected")
    try:
        async for packet in websocket:
            if isinstance(packet, str):
                continue

            msg_type = packet[0]
            payload = packet[1:]
            await handler.handle(websocket, msg_type, payload)

    except websockets.ConnectionClosed:
        print("client disconnected")

async def main():
    async with websockets.serve(connection_handler, "localhost", 7000):
        print("listening on ws://localhost:7000")
        await asyncio.Future()

if __name__ == "__main__":
    asyncio.run(main())