namespace RemoteKeyboardController.Models
{
    public record MoveMessage(RaceCar Car) : Message("move")
    {
    }
}
