using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

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

    public async Task LeaveRoom(string roomCode, string name)
    {
        if (Rooms.ContainsKey(roomCode))
        {
            var room = Rooms[roomCode];

            room.Players.Remove(name);
            room.ClickOrder.RemoveAll(x => x.Name == name);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);

            await Clients.Group(roomCode).SendAsync("UpdatePlayers", room.Players);
            await Clients.Group(roomCode).SendAsync("UpdateList", room.ClickOrder);
        }
    }

    public async Task Prenota(string roomCode, string name)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        var room = Rooms[roomCode];

        if (!room.ClickOrder.Any(x => x.Name == name))
        {
            room.ClickOrder.Add(new ClickEntry
            {
                Name = name,
                Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            await Clients.Group(roomCode).SendAsync("UpdateList", room.ClickOrder);
        }
    }

    public async Task Reset(string roomCode)
    {
        if (Rooms.ContainsKey(roomCode))
        {
            Rooms[roomCode].ClickOrder.Clear();

            await Clients.Group(roomCode)
                .SendAsync("UpdateList", Rooms[roomCode].ClickOrder);
        }
    }
}

public class Room
{
    public List<string> Players { get; set; } = new();
    public List<ClickEntry> ClickOrder { get; set; } = new();
}

public class ClickEntry
{
    public string Name { get; set; } = "";
    public long Time { get; set; }
}
