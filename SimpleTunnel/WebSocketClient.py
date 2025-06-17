#!/usr/bin/env python
# -*- coding: utf-8 -*-

import json
import asyncio
import websockets
from jetracer.nvidia_racecar import NvidiaRacecar
import cv2
import numpy as np

def __gstreamer_pipeline(
        camera_id,
        capture_width=1920,
        capture_height=1080,
        display_width=1920,
        display_height=1080,
        framerate=30,
        flip_method=0,
    ):
    return (
            "nvarguscamerasrc sensor-id=%d ! "
            "video/x-raw(memory:NVMM), "
            "width=(int)%d, height=(int)%d, "
            "format=(string)NV12, framerate=(fraction)%d/1 ! "
            "nvvidconv flip-method=%d ! "
            "video/x-raw, width=(int)%d, height=(int)%d, format=(string)BGRx ! "
            "videoconvert ! "
            "video/x-raw, format=(string)BGR ! appsink max-buffers=1 drop=True"
            % (
                    camera_id,
                    capture_width,
                    capture_height,
                    framerate,
                    flip_method,
                    display_width,
                    display_height,
            )
    )
   
async def send_image(websocket, stream): 
    while True:
        await asyncio.sleep(0.017)
        if not stream.isOpened():
            print("Error: Could not open camera.")
        else:
            ret, frame = stream.read()
            if ret:
                frame = np.ascontiguousarray(frame, dtype=np.uint8)
                _, encoded_image = cv2.imencode('.jpg', frame)
                await websocket.send(encoded_image.tobytes())
            else:
                print("Error: Could not read frame.")

async def receive_commands(websocket, car):
    while True:
        message = json.loads(await websocket.recv())
        print("Received message:", message)
        jsonCar = message.get('Car', {})
        car.steering = jsonCar.get('Steering', 0.0)
        car.throttle = jsonCar.get('Throttle', 0.0)

async def handle():
    stream = cv2.VideoCapture(__gstreamer_pipeline(
        camera_id=0, 
        capture_width=1280,
        capture_height=720,
        display_width=1280,
        display_height=720,
        framerate=60,
        flip_method=0), cv2.CAP_GSTREAMER)    
    car = NvidiaRacecar()
    car.steering_gain = -1
    car.steering_offset = 0
    car.steering = 0
    car.throttle_gain = 0.8
    print("ready to go!")
    async with websockets.connect("ws://192.168.0.241:5000/receive") as websocket:
        await asyncio.gather(
            receive_commands(websocket, car),
            send_image(websocket, stream),
        )
    stream.release()
    await websocket.close()
    

if __name__ == "__main__":
    asyncio.get_event_loop().run_until_complete(handle())