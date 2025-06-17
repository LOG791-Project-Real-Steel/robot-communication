let controllerIndex = null;

window.addEventListener("gamepadconnected", (event) => {
  handleConnectDisconnect(event, true);
});

window.addEventListener("gamepaddisconnected", (event) => {
  handleConnectDisconnect(event, false);
});

function handleConnectDisconnect(event, connected) {
  const gamepad = event.gamepad;
  if (connected) {
    controllerIndex = gamepad.index;
  } else {
    controllerIndex = null;
  }
}

function gameLoop() {
  if (controllerIndex !== null) {
    const gamepad = navigator.getGamepads()[controllerIndex];
    const vals = {up: gamepad.buttons[6].value, down: gamepad.buttons[7].value, leftRight: gamepad.axes[0]};
    console.log(vals);
  }
  requestAnimationFrame(gameLoop);
}

gameLoop();