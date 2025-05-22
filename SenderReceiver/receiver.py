import socket

# Set up the socket
UDP_IP = "0.0.0.0"   # Listen on all interfaces
UDP_PORT = 9002      # Match this with your sender

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))

print(f"Listening for UDP packets on port {UDP_PORT}...")

try:
    while True:
        data, addr = sock.recvfrom(1024)  # Buffer size
        if data:
            print(f"Received from {addr}: {data.decode(errors='ignore')}")
except KeyboardInterrupt:
    print("\nReceiver stopped.")
finally:
    sock.close()
