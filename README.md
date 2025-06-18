# Robot Communication
Repository containing the code that will run on the middleware server between the robot and the oculus headset.

## Prerequisites
- .NET 9 sdk
- Visual Studio or Rider **(to work on this project)**

## Running and building the project
This part shows you how to build and run the project locally on your machine or anywhere you want using Visual Studio or the command line.

### In Visual Studio
1. First, open the solution which is located in the `SimpleTunnel` directory which is at the root of the project.

2. Then, in the solution explorer on the right of your screen, right click on the solution 'SimpleTunnel' and click on `Build solution` in the menu.

3. Then, in the second tool bar at the top of Visual Studio, between what should be a dropdown with the `Any CPU` option selected and the green start button, there should be a dropdown. Open that dropdown and select the option `WebSocketServer`.

4. Now, you can click on the green arrow arrow to start the app. If you get prompted for permissions, allow and/or accept all of them. It should now be available on port 5000 (default port which can be changed in `WebSocketServer/Properties/launchsettings.json`).

### In the Command Line (CLI)
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