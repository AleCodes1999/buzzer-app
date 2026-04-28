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
    private static readonly object LockObj = new();
    private static Dictionary<string, Room> Rooms = new();

    public async Task<bool> JoinRoom(string roomCode, string name)
    {
        Room room;

        lock (LockObj)
        {
            if (!Rooms.ContainsKey(roomCode))
                Rooms[roomCode] = new Room();

            room = Rooms[roomCode];

            if (room.Players.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return false;

            room.Players.Add(name);
            room.Points[name] = room.Points.ContainsKey(name) ? room.Points[name] : 0;

            if (!room.Stats.ContainsKey(name))
                room.Stats[name] = new PlayerStat();

            if (string.IsNullOrEmpty(room.Admin))
                room.Admin = name;

            room.ConnectionMap[Context.ConnectionId] = name;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

        await SendFullState(roomCode);

        return true;
    }

    public async Task LeaveRoom(string roomCode, string name)
    {
        await RemoveUser(roomCode, name, Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var key in Rooms.Keys.ToList())
        {
            var room = Rooms[key];

            if (room.ConnectionMap.ContainsKey(Context.ConnectionId))
            {
                var name = room.ConnectionMap[Context.ConnectionId];
                await RemoveUser(key, name, Context.ConnectionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveUser(string roomCode, string name, string connectionId)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        lock (LockObj)
        {
            var room = Rooms[roomCode];

            room.Players.Remove(name);
            room.ClickOrder.RemoveAll(x => x.Name == name);
            room.ConnectionMap.Remove(connectionId);
            room.Points.Remove(name);

            if (room.Admin == name)
                room.Admin = room.Players.FirstOrDefault() ?? "";
        }

        await Groups.RemoveFromGroupAsync(connectionId, roomCode);

        await SendFullState(roomCode);
    }

    public async Task Prenota(string roomCode, string name)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        bool first = false;

        lock (LockObj)
        {
            var room = Rooms[roomCode];

            if (!room.RoundOpen)
                return;

            if (room.ClickOrder.Any(x => x.Name == name))
                return;

            room.ClickOrder.Add(new ClickEntry
            {
                Name = name,
                Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            first = room.ClickOrder.Count == 1;

            if (first)
            {
                room.History.Insert(0, name);
                room.Stats[name].Wins++;
                room.Stats[name].LastWin = DateTime.Now.ToString("HH:mm");
            }
        }

        if (first)
            await Clients.Group(roomCode).SendAsync("Winner", name);

        await SendFullState(roomCode);
    }

    public async Task Reset(string roomCode, string name)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        lock (LockObj)
        {
            var room = Rooms[roomCode];

            if (room.Admin != name)
                return;

            room.ClickOrder.Clear();
        }

        await SendFullState(roomCode);
    }

    public async Task ToggleRound(string roomCode, string name)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        lock (LockObj)
        {
            var room = Rooms[roomCode];

            if (room.Admin != name)
                return;

            room.RoundOpen = !room.RoundOpen;
        }

        await SendFullState(roomCode);
    }

    public async Task AddPoint(string roomCode, string adminName, string target)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        lock (LockObj)
        {
            var room = Rooms[roomCode];

            if (room.Admin != adminName)
                return;

            if (room.Points.ContainsKey(target))
                room.Points[target]++;
        }

        await SendFullState(roomCode);
    }

    public async Task RemovePoint(string roomCode, string adminName, string target)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        lock (LockObj)
        {
            var room = Rooms[roomCode];

            if (room.Admin != adminName)
                return;

            if (room.Points.ContainsKey(target) && room.Points[target] > 0)
                room.Points[target]--;
        }

        await SendFullState(roomCode);
    }

    private async Task SendFullState(string roomCode)
    {
        if (!Rooms.ContainsKey(roomCode))
            return;

        Room room = Rooms[roomCode];

        await Clients.Group(roomCode).SendAsync(
            "UpdatePlayers",
            room.Players,
            room.Admin,
            room.RoundOpen);

        await Clients.Group(roomCode).SendAsync(
            "UpdateList",
            room.ClickOrder);

        await Clients.Group(roomCode).SendAsync(
            "UpdatePoints",
            room.Points);

        await Clients.Group(roomCode).SendAsync(
            "UpdateHistory",
            room.History);

        await Clients.Group(roomCode).SendAsync(
            "UpdateStats",
            room.Stats);
    }
}

public class Room
{
    public List<string> Players { get; set; } = new();
    public List<ClickEntry> ClickOrder { get; set; } = new();
    public string Admin { get; set; } = "";
    public Dictionary<string, string> ConnectionMap { get; set; } = new();
    public Dictionary<string, int> Points { get; set; } = new();
    public bool RoundOpen { get; set; } = true;
    public List<string> History { get; set; } = new();
    public Dictionary<string, PlayerStat> Stats { get; set; } = new();
}

public class ClickEntry
{
    public string Name { get; set; } = "";
    public long Time { get; set; }
}

public class PlayerStat
{
    public int Wins { get; set; }
    public string LastWin { get; set; } = "";
}
