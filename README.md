# Robot Communication

![Component: Middleware Server](https://img.shields.io/badge/component-middleware--server-green?style=for-the-badge)
![Status: No Longer Maintained](https://img.shields.io/badge/status-no--longer--maintained-red?style=for-the-badge)
![Purpose: Production](https://img.shields.io/badge/purpose-production-blue?style=for-the-badge)
![Final Solution](https://img.shields.io/badge/final--solution-YES-success?style=for-the-badge)


Code that will run on the middleware server between the robot and the oculus headset.

## SimpleTunnel (Used for WebSocket final solution)

There are a bunch of projects and artifacts from trial and errors ,made during the conception of our final solution. Feel free to explore other C# projects made in this folder as they have somewhat valid code used mostly for testing. These folders will not be covered by this README.

### Running and building the project
This part shows you how to build and run the project locally on your machine or anywhere you want using Visual Studio or the command line.

#### In Visual Studio
1. First, open the solution which is located in the `SimpleTunnel` directory which is at the root of the project.

2. Then, in the solution explorer on the right of your screen, right click on the solution `SimpleTunnel` and click on `Build solution` in the menu.

3. Then, in the second tool bar at the top of Visual Studio, between what should be a dropdown with the `Any CPU` option selected and the green start button, there should be a dropdown. Open that dropdown and select the option `WebSocketServer`.

4. Now, you can click on the green arrow arrow to start the app. If you get prompted for permissions, allow and/or accept all of them. It should now be available on port `5000` (default port which can be changed in `WebSocketServer/Properties/launchsettings.json`).

#### In the Command Line (CLI)
1. First of all, you should be at the root of the project. Now you can run this to go in the directory where you'll be able to build the solution.
    ```shell
    cd SimpleTunnel
    ```

2. To build the solution, run the following command:
    ```shell
    dotnet build
    ```

3. Once the solution is built, it's time to run the project. To do so, run the command below to go to the right directory:
    ```shell
    cd WebSocketServer
    ```

4. Then, once you are in the right directory, you can run these different run commands to run the Web Socket server with different configurations:

    To run on port 5000 locally and test the app **(default settings)**:
    ```shell
    dotnet run
    ```

    To run on port 8765 **(made for public server use)**:
    ```shell
    dotnet run --launch-profile "server"
    ```

> [!TIP]
> If you want to run a second server simultaneously for easier parallel testing, you can run the `dotnet run` command in a seperate terminal and launch it with the `server2` **launch-profile**. The server will be accessible on port `8764` with the exact same functionalities as the first server that runs on port `8765`.

## Sender Receiver (For local command testing)

Code that is not the middleware server but, is helpful to control the robot locally from the keyboard.

### Prerequisites

- Python 3.6
- GoLang (Go) SDK

### Running the Code

> [!NOTE]
> This will only work locally unless you use something like **TailScale** or **ZeroTier** to create a fake local server on which your PC and the robot are accessible.

1. Put the `receiver.py` script on the robot and run it.

    ```bash
    cd SenderReceiver
    python3 receiver.py
    ```

2. Replace the **IP** in the `sender.go` file to match the local **IP** of the robot (should be displayed on the OLED screen that is physically on the robot).

3. Run the `sender.go` script on your PC or other device that has the **Golang SDK**.

    ```bash
    go run sender.go
    ```

## WebRTC (For WbRTC solution tests)

This was used for testing the WebRTC solution. Unfortunately, due to hardware limitations, we could not make this solution work thus, this folder is not maintained and the code in it is not clean or organized at all. You can try to make sense of it but it will not be explained in this README.

This code might not even run at all.

These scripts were executed and tested on the robot directly.

---

> Made with care by [@Funnyadd](https://github.com/Funnyadd), [@ImprovUser](https://github.com/ImprovUser), [@cjayneb](https://github.com/cjayneb) and [@RaphaelCamara](https://github.com/RaphaelCamara) ❤️
