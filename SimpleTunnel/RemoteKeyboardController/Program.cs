// See https://aka.ms/new-console-template for more information
using RemoteKeyboardController;

ConsoleHandler handler = new("localhost:5000");

handler.Run();