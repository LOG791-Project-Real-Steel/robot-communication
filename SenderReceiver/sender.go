// +build windows

package main

import (
	"fmt"
	"net"
	"time"

	"golang.org/x/sys/windows"
)

var (
	user32             = windows.NewLazySystemDLL("user32.dll")
	getAsyncKeyState   = user32.NewProc("GetAsyncKeyState")
)

func isKeyDown(vk int) bool {
	ret, _, _ := getAsyncKeyState.Call(uintptr(vk))
	return ret&0x8000 != 0
}

func main() {
	conn, err := net.Dial("udp", "10.42.0.1:9002") // Replace with receiver's IP
	if err != nil {
		panic(err)
	}
	defer conn.Close()

	fmt.Println("Press W/A/S/D to send. Press ESC to quit.")

	keyMap := map[int]byte{
		0x57: 'w', // W
		0x41: 'a', // A
		0x53: 's', // S
		0x44: 'd', // D
	}

	const vkESC = 0x1B

	for {
		if isKeyDown(vkESC) {
			fmt.Println("Exiting...")
			break
		}

		for vk, char := range keyMap {
			if isKeyDown(vk) {
				_, err := conn.Write([]byte{char})
				if err != nil {
					fmt.Println("Send error:", err)
				} else {
					fmt.Printf("Sent: %c\n", char)
				}
			}
		}

		time.Sleep(50 * time.Millisecond) // control rate of sending
	}
}
