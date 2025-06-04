#!/usr/bin/env python
# -*- coding: utf-8 -*-

import json
import asyncio
import websockets
from jetracer.nvidia_racecar import NvidiaRacecar

async def hello():
    car = NvidiaRacecar()
    car.steering_gain = -1
    car.steering_offset = 0
    car.steering = 0
    car.throttle_gain = 0.8
    print("ready to go!")
    async with websockets.connect("ws://192.168.0.241:5000/receive") as websocket:
        await websocket.send('{"Hello world!": "Hello WebSocket!"}')
        while True:
            message = json.loads(await websocket.recv())
            jsonCar = message.get('Car', {})
            car.steering = jsonCar.get('Steering', 0.0)
            car.throttle = jsonCar.get('Throttle', 0.0)

if __name__ == "__main__":
    asyncio.get_event_loop().run_until_complete(hello())