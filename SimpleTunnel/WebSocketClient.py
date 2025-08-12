#!/usr/bin/env python
# -*- coding: utf-8 -*-

import os
import json
import asyncio
import websockets
from jetracer.nvidia_racecar import NvidiaRacecar
import cv2
import numpy as np

class DummyRacecar:
    def __init__(self):
        self._steering = 0.0
        self._throttle = 0.0

    @property
    def steering(self):
        return self._steering

    @steering.setter
    def steering(self, value):
        self._steering = value

    @property
    def throttle(self):
        return self._throttle

    @throttle.setter
    def throttle(self, value):
        self._throttle = value

def __gstreamer_pipeline(
        camera_id,
        capture_width=1920,
        capture_height=1080,
        framerate=30,
        flip_method=0,
    ):
    return (
            "nvarguscamerasrc sensor-id=%d ! "
            "video/x-raw(memory:NVMM), "
            "width=(int)%d, height=(int)%d, "
            "format=(string)NV12, framerate=(fraction)%d/1 ! "
            "nvvidconv flip-method=%d ! "
            "videoconvert ! "
            "video/x-raw, format=YUY2 ! jpegenc ! appsink max-buffers=1 drop=True"
            % (
                    camera_id,
                    capture_width,
                    capture_height,
                    framerate,
                    flip_method,
            )
    )
   
async def send_image(websocket, stream): 
    while True:
        # await asyncio.sleep(0.017)
        if not stream.isOpened():
            print("Error: Could not open camera.")
        else:
            ret, frame = stream.read()
            if ret:
                await websocket.send(frame.tobytes())
            else:
                print("Error: Could not read frame.")

async def receive_commands(websocket, car):
    while True:
        message = json.loads(await websocket.recv())
        os.system('clear')
        print("Received message:", message)
        jsonCar = message.get('Car', {})
        car.steering = jsonCar.get('Steering', 0.0)
        car.throttle = jsonCar.get('Throttle', 0.0)

async def handle():
    stream = cv2.VideoCapture(__gstreamer_pipeline(
        camera_id=0, 
        capture_width=1280,
        capture_height=720,
        framerate=60,
        flip_method=0), cv2.CAP_GSTREAMER)    
    try:
        car = NvidiaRacecar()
        car = NvidiaRacecar()
        car.steering_gain = -1
        car.steering_offset = 0
        car.steering = 0
        car.throttle_gain = 0.8
    except Exception as e:
        print(f"Warning: Failed to initialize NvidiaRacecar due to I2C error: {e}")
        print("Using DummyRacecar instead.")
        car = DummyRacecar()
    print("ready to go!")
    async with websockets.connect("ws://192.168.0.39:5000/robot") as websocket:
        await asyncio.gather(
            receive_commands(websocket, car),
            send_image(websocket, stream),
        )
    stream.release()
    await websocket.close()
    

if __name__ == "__main__":
    asyncio.get_event_loop().run_until_complete(handle())