#!/bin/bash
# whoami && pulseaudio --start && sleep 1 && XDG_RUNTIME_DIR=/run/user/$(id -u) ~/portes-ouvertes/WebSocket/ugv-env/bin/python ~/portes-ouvertes/WebSocket/WebSocketClient.py >> ~/ugv.log 2>&1

whoami && pulseaudio --start && sleep 1 && XDG_RUNTIME_DIR=/run/user/$(id -u) ~/portes-ouvertes/WebSocket/ugv-env/bin/python ~/portes-ouvertes/WebSocket/WebSocketClient.py