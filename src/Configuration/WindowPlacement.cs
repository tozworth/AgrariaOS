namespace AgrariaOS.Configuration;

public sealed record WindowPlacement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed record WindowOpenState
{
    public string Id { get; set; } = "";
    public bool IsMinimized { get; set; }
}
