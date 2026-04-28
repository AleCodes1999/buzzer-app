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

    public async Task<bool> JoinRoom(string roomCode, string name)
{
    if (!Rooms.ContainsKey(roomCode))
        Rooms[roomCode] = new Room();

    var room = Rooms[roomCode];

    if (room.Players.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
    {
        await Clients.Caller.SendAsync("JoinError", "Nome già utilizzato");
        return false;
    }

    room.Players.Add(name);

    if (string.IsNullOrEmpty(room.Admin))
        room.Admin = name;

    room.ConnectionMap[Context.ConnectionId] = name;

    await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

    await Clients.Group(roomCode)
        .SendAsync("UpdatePlayers", room.Players, room.Admin);

    await Clients.Caller.SendAsync("UpdateList", room.ClickOrder);

    return true;
}

    public async Task LeaveRoom(string roomCode, string name)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        await RemoveUser(roomCode, name, Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var roomKey in Rooms.Keys.ToList())
        {
            var room = Rooms[roomKey];

            if (room.ConnectionMap.ContainsKey(Context.ConnectionId))
            {
                var name = room.ConnectionMap[Context.ConnectionId];
                await RemoveUser(roomKey, name, Context.ConnectionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveUser(string roomCode, string name, string connectionId)
    {
        var room = Rooms[roomCode];

        room.Players.Remove(name);
        room.ClickOrder.RemoveAll(x => x.Name == name);
        room.ConnectionMap.Remove(connectionId);

        await Groups.RemoveFromGroupAsync(connectionId, roomCode);

        if (room.Admin == name)
            room.Admin = room.Players.FirstOrDefault() ?? "";

        await Clients.Group(roomCode).SendAsync("UpdatePlayers", room.Players, room.Admin);
        await Clients.Group(roomCode).SendAsync("UpdateList", room.ClickOrder);
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

    public async Task Reset(string roomCode, string name)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        var room = Rooms[roomCode];

        if (room.Admin != name)
            return;

        room.ClickOrder.Clear();

        await Clients.Group(roomCode).SendAsync("UpdateList", room.ClickOrder);
    }
}

public class Room
{
    public List<string> Players { get; set; } = new();
    public List<ClickEntry> ClickOrder { get; set; } = new();
    public string Admin { get; set; } = "";
    public Dictionary<string, string> ConnectionMap { get; set; } = new();
}

public class ClickEntry
{
    public string Name { get; set; } = "";
    public long Time { get; set; }
}
