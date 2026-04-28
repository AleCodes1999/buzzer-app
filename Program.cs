using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// 👉 QUESTA RIGA MANCAVA
builder.Services.AddSignalR();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<BuzzerHub>("/buzzerHub");

app.Run();

public class BuzzerHub : Hub
{
    private static Dictionary<string, Room> Rooms = new();

    public async Task JoinRoom(string roomCode, string name)
{
    if (!Rooms.ContainsKey(roomCode))
        Rooms[roomCode] = new Room();

    var room = Rooms[roomCode];

    if (!room.Players.Contains(name))
        room.Players.Add(name);

    await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

    await Clients.Group(roomCode).SendAsync("UpdatePlayers", room.Players);
    await Clients.Caller.SendAsync("UpdateList", room.ClickOrder);
}

    public async Task Prenota(string roomCode, string name)
{
    var room = Rooms[roomCode];

    if (!room.ClickOrder.Contains(name))
    {
        room.ClickOrder.Add(name);

        await Clients.Group(roomCode).SendAsync("UpdateList", room.ClickOrder);
    }
}

    public async Task Reset(string roomCode)
{
    if (Rooms.ContainsKey(roomCode))
    {
        Rooms[roomCode].ClickOrder.Clear();

        await Clients.Group(roomCode).SendAsync("UpdateList", Rooms[roomCode].ClickOrder);
    }
}
}

public class Room
{
    public List<string> Players { get; set; } = new();
    public List<string> ClickOrder { get; set; } = new();
}