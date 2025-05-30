#!/usr/bin/env python
from websockets.sync.client import connect
from websockets.exceptions import ConnectionClosedOK
import json
import time


def hello():
    with connect("ws://localhost:5000/receive") as websocket:
        websocket.send("{\"hello\":\"world\"}")
        while True:
            try:
                message = json.loads(websocket.recv())
                print(message)
            except ConnectionClosedOK:
                break

def test():
    with connect("ws://localhost:5000/receive") as websocket:
        start_time = time.time()
        websocket.send("{\"hello\":\"world\",\"test\":\"test\",\"test2\":\"test2\"}")
        message = json.loads(websocket.recv().rstrip("\x00"))
        print(message)
        end_time = time.time()
        elapsed_time = (end_time - start_time) * 1000 / 2 # Convert to milliseconds
        print(f"Elapsed time: {elapsed_time} ms")


if __name__ == "__main__":
    hello()