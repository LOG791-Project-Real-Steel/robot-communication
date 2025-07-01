#!/usr/bin/env python
# -*- coding: utf-8 -*-

import json
import asyncio
import websockets
import cv2
import av
import numpy as np
from jetracer.nvidia_racecar import NvidiaRacecar
from aiortc import RTCPeerConnection, RTCSessionDescription
from aiortc.contrib.media import VideoStreamTrack

class CameraStreamTrack(VideoStreamTrack):
    def __init__(self):
        super().__init__()
        self.cap = cv2.VideoCapture(self.__gstreamer_pipeline(camera_id=0, framerate=30, flip_method=0), cv2.CAP_GSTREAMER)
        
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
  
    async def recv(self):
        ret, frame = self.cap.read()
        if not ret:
            return None
        frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        frame = np.ascontiguousarray(frame, dtype=np.uint8)
        return av.VideoFrame.from_ndarray(frame, format='rgb24')

pc = RTCPeerConnection()
await pc.addTrack(CameraStreamTrack())

offer = await pc.createOffer()
await pc.setLocalDescription(offer)

# await pc.setRemoteDescription(RTCSessionDescription(
#     sdp=data["sdp"],
#     type=data["type"]
# ))
# await pc.addIceCandidate(candidate)

async def send_msg(websocket, offer):

# WebSocket connection to receive commands and handle SDP exchange
async def receive_msgs(websocket, car):
    while True:
        message = json.loads(await websocket.recv())
        jsonType = message.get('type', '')
        if jsonType == 'signal':
            jsonData = message.get('data', None)
            jsonCandidate = jsonData.get('candidate', None)
            if jsonCandidate is not None:
                await pc.addIceCandidate(jsonData['candidate'])
            if jsonData is not None:
                await pc.setRemoteDescription(RTCSessionDescription(
                    sdp=jsonData.get('sdp', ''),
                    type=jsonData.get('type', '')
                ))
        elif jsonType == 'move':
            jsonCar = message.get('Car', {})
            if jsonCar:
                car.steering = jsonCar.get('Steering', 0.0)
                car.throttle = jsonCar.get('Throttle', 0.0)

