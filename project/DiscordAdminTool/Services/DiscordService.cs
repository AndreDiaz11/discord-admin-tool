using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordAdminTool.Models;

namespace DiscordAdminTool.Services;

public class DiscordService
{
    private DiscordSocketClient? _client;
    private readonly Dictionary<ulong, DateTime> _memberFetchTimestamps = new();
    private readonly ConcurrentDictionary<ulong, bool> _activePurges = new();

    public string Status { get; private set; } = "disconnected";
    public ulong? GuildId { get; private set; }
    public event Action<string>? StatusChanged;

    private void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }

    // ==SECCIÓN: Conexión==

    public async Task ConnectAsync(string token)
    {
        if (_client != null) await DisconnectAsync();
        SetStatus("connecting");

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages |
                              GatewayIntents.MessageContent | GatewayIntents.GuildPresences | GatewayIntents.GuildVoiceStates,
        };
        _client = new DiscordSocketClient(config);

        var readyTcs = new TaskCompletionSource<bool>();
        var errorTcs = new TaskCompletionSource<Exception>();

        Task OnReady()
        {
            readyTcs.TrySetResult(true);
            return Task.CompletedTask;
        }

        Task OnLog(LogMessage msg)
        {
            if (msg.Exception != null && !readyTcs.Task.IsCompleted) errorTcs.TrySetResult(msg.Exception);
            if (msg.Message != null && msg.Message.Contains("disallowed intents", StringComparison.OrdinalIgnoreCase) && !readyTcs.Task.IsCompleted)
                errorTcs.TrySetResult(new Exception("disallowed intents"));
            return Task.CompletedTask;
        }

        _client.Ready += OnReady;
        _client.Log += OnLog;

        try
        {
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            var winner = await Task.WhenAny(readyTcs.Task, errorTcs.Task, Task.Delay(20000));
            if (winner == errorTcs.Task) throw errorTcs.Task.Result;
            if (winner != readyTcs.Task) throw new Exception("Timeout al conectar con Discord");

            SetStatus("connected");
            _client.MessageReceived += OnMessageReceived;
            _client.PresenceUpdated += OnPresenceUpdated;
            _client.UserVoiceStateUpdated += OnVoiceStateUpdated;
            _client.UserJoined += OnUserJoined;
            _client.Disconnected += OnDisconnected;
        }
        catch (Exception ex)
        {
            SetStatus("disconnected");
            try { await _client.StopAsync(); } catch { }
            _client = null;

            var raw = ex.Message;
            if (raw.Contains("disallowed", StringComparison.OrdinalIgnoreCase) || raw.Contains("intent", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Faltan intents privilegiados: activa \"Server Members Intent\" y \"Message Content Intent\" en el Developer Portal (seccion Bot)");
            if (raw.Contains("401") || raw.Contains("token", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Token invalido: revisa que lo copiaste completo y sin espacios");
            if (raw.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Timeout al conectar con Discord: revisa tu conexion a internet");
            throw new Exception($"No se pudo conectar: {raw}");
        }
        finally
        {
            if (_client != null)
            {
                _client.Ready -= OnReady;
                _client.Log -= OnLog;
            }
        }
    }

    public async Task DisconnectAsync()
    {
        if (_client != null)
        {
            _client.MessageReceived -= OnMessageReceived;
            _client.PresenceUpdated -= OnPresenceUpdated;
            _client.UserVoiceStateUpdated -= OnVoiceStateUpdated;
            _client.UserJoined -= OnUserJoined;
            _client.Disconnected -= OnDisconnected;
            try { await _client.StopAsync(); await _client.LogoutAsync(); } catch { }
            _client.Dispose();
            _client = null;
        }
        SetStatus("disconnected");
    }

    private Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return Task.CompletedTask;
        ActivityStore.MarkSeen(message.Author.Id);
        return Task.CompletedTask;
    }

    private Task OnPresenceUpdated(SocketUser user, SocketPresence? oldPresence, SocketPresence newPresence)
    {
        if (newPresence == null || newPresence.Status == UserStatus.Offline || newPresence.Status == UserStatus.Invisible)
            return Task.CompletedTask;
        PresenceStore.MarkOnline(user.Id);
        return Task.CompletedTask;
    }

    private Task OnVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (newState.VoiceChannel == null) return Task.CompletedTask;
        PresenceStore.MarkVoice(user.Id);
        return Task.CompletedTask;
    }

    private Task OnUserJoined(SocketGuildUser member)
    {
        var entry = new LogEntry { Type = "join", Executor = "automatico", TargetCount = 1 };
        entry.TargetIds.Add(member.Id.ToString());
        entry.Details["username"] = member.Username;
        LogStore.Add(entry);
        return Task.CompletedTask;
    }

    private Task OnDisconnected(Exception ex)
    {
        SetStatus("disconnected");
        return Task.CompletedTask;
    }

    // ==SECCIÓN: Requisitos y guilds==

    private DiscordSocketClient RequireClient()
    {
        if (_client == null || Status != "connected") throw new Exception("Bot no conectado");
        return _client;
    }

    private SocketGuild RequireGuild()
    {
        var client = RequireClient();
        if (GuildId == null) throw new Exception("Ningun servidor seleccionado");
        var guild = client.GetGuild(GuildId.Value);
        if (guild == null) throw new Exception("Servidor no encontrado");
        return guild;
    }

    public List<GuildInfo> ListGuilds()
    {
        var client = RequireClient();
        return client.Guilds.Select(g => new GuildInfo { Id = g.Id, Name = g.Name, IconUrl = g.IconUrl, MemberCount = g.MemberCount }).ToList();
    }

    public void SelectGuild(ulong guildId)
    {
        var client = RequireClient();
        if (client.GetGuild(guildId) == null) throw new Exception("Servidor no encontrado");
        GuildId = guildId;
    }

    private async Task<IReadOnlyCollection<SocketGuildUser>> GetMembersAsync(SocketGuild guild, bool forceRefresh = false)
    {
        var now = DateTime.UtcNow;
        var lastFetch = _memberFetchTimestamps.TryGetValue(guild.Id, out var t) ? t : DateTime.MinValue;
        var cacheAge = now - lastFetch;
        if (!forceRefresh && cacheAge.TotalMilliseconds < 60000 && guild.Users.Count > 1) return guild.Users;

        try
        {
            await guild.DownloadUsersAsync();
            _memberFetchTimestamps[guild.Id] = now;
            return guild.Users;
        }
        catch
        {
            if (guild.Users.Count > 1) return guild.Users;
            throw;
        }
    }

    // ==SECCIÓN: Actividad y presencia==

    private static DateTimeOffset? GetLastSeen(ulong userId) => ActivityStore.GetLastSeen(userId);

    private static DateTimeOffset? GetLastOnline(ulong userId, SocketGuildUser? member)
    {
        if (member != null && member.Status != UserStatus.Offline && member.Status != UserStatus.Invisible)
        {
            PresenceStore.MarkOnline(userId);
            return DateTimeOffset.UtcNow;
        }
        return PresenceStore.GetLastOnline(userId);
    }

    private static DateTimeOffset? GetLastVoice(ulong userId, SocketGuildUser? member)
    {
        if (member?.VoiceChannel != null)
        {
            PresenceStore.MarkVoice(userId);
            return DateTimeOffset.UtcNow;
        }
        return PresenceStore.GetLastVoice(userId);
    }

    private static DateTimeOffset? GetLastConnection(ulong userId, SocketGuildUser? member)
    {
        var online = GetLastOnline(userId, member);
        var voice = GetLastVoice(userId, member);
        if (online == null) return voice;
        if (voice == null) return online;
        return online >= voice ? online : voice;
    }

    private static string ColorHex(Color c) => c.RawValue == 0 ? "#000000" : $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return new Color(r, g, b);
    }

    public MemberInfo MapMember(SocketGuildUser member)
    {
        var lastMessageAt = GetLastSeen(member.Id);
        var lastPresenceAt = GetLastOnline(member.Id, member);
        var lastVoiceAt = GetLastVoice(member.Id, member);
        var lastActivityAt = new[] { lastMessageAt, lastPresenceAt, lastVoiceAt }
            .Where(d => d.HasValue).Select(d => d!.Value).OrderByDescending(d => d).Cast<DateTimeOffset?>().FirstOrDefault();

        MemberStatus status;
        if (member.IsBot) status = MemberStatus.Bot;
        else if (lastActivityAt == null) status = MemberStatus.NeverSpoke;
        else
        {
            var daysSince = (DateTimeOffset.UtcNow - lastActivityAt.Value).TotalDays;
            status = daysSince > 30 ? MemberStatus.Inactive : MemberStatus.Active;
        }

        string avatar = member.GetGuildAvatarUrl() ?? member.GetAvatarUrl() ?? member.GetDefaultAvatarUrl();

        return new MemberInfo
        {
            Id = member.Id,
            Username = member.Username,
            DisplayName = member.DisplayName,
            Avatar = avatar,
            JoinedAt = member.JoinedAt,
            LastMessageAt = lastMessageAt,
            LastPresenceAt = lastPresenceAt,
            LastVoiceAt = lastVoiceAt,
            Roles = member.Roles.Where(r => r.Id != member.Guild.EveryoneRole.Id)
                .Select(r => new RoleChip { Id = r.Id, Name = r.Name, Color = ColorHex(r.Color) }).ToList(),
            Status = status,
            IsBot = member.IsBot,
        };
    }

    private bool IsPurgeCandidate(SocketGuildUser member, PurgeConfig config)
    {
        if (config.ExcludeBots && member.IsBot) return false;
        if (config.ExcludeRoles.Count > 0 && config.ExcludeRoles.Any(r => member.Roles.Any(role => role.Id == r))) return false;
        if (config.InactiveDays == null && config.OfflineDays == null) return false;

        var messageInactive = false;
        if (config.InactiveDays != null)
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-config.InactiveDays.Value);
            var lastSeen = GetLastSeen(member.Id);
            var reference = lastSeen ?? member.JoinedAt ?? DateTimeOffset.UtcNow;
            messageInactive = reference < threshold;
        }

        var connectionInactive = false;
        if (config.OfflineDays != null)
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-config.OfflineDays.Value);
            var lastConnection = GetLastConnection(member.Id, member);
            var reference = lastConnection ?? member.JoinedAt ?? DateTimeOffset.UtcNow;
            connectionInactive = reference < threshold;
        }

        return messageInactive || connectionInactive;
    }

    private List<SocketGuildUser> FilterMembers(IEnumerable<SocketGuildUser> members, MemberFilter? filter)
    {
        var list = members.ToList();
        filter ??= new MemberFilter();

        if (filter.Ids is { Count: > 0 })
        {
            var idSet = new HashSet<ulong>(filter.Ids);
            list = list.Where(m => idSet.Contains(m.Id)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var q = filter.Search.ToLowerInvariant();
            list = list.Where(m => m.Username.ToLowerInvariant().Contains(q)).ToList();
        }
        if (filter.Roles is { Count: > 0 })
            list = list.Where(m => filter.Roles.Any(rid => m.Roles.Any(r => r.Id == rid))).ToList();
        if (filter.ExcludeBots)
            list = list.Where(m => !m.IsBot).ToList();
        if (filter.ExcludeRoles is { Count: > 0 })
            list = list.Where(m => !filter.ExcludeRoles.Any(rid => m.Roles.Any(r => r.Id == rid))).ToList();
        if (filter.InactiveDays != null)
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-filter.InactiveDays.Value);
            list = list.Where(m =>
            {
                if (m.IsBot) return false;
                var lastSeen = GetLastSeen(m.Id);
                var reference = lastSeen ?? m.JoinedAt ?? DateTimeOffset.UtcNow;
                return reference < threshold;
            }).ToList();
        }
        if (filter.Status is { Count: > 0 })
            list = list.Where(m => filter.Status.Contains(MapMember(m).Status)).ToList();

        return list;
    }

    // ==SECCIÓN: Dashboard y usuarios==

    public async Task<DashboardData> GetDashboardDataAsync()
    {
        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);

        int active = 0, inactive = 0, bots = 0;
        foreach (var m in members)
        {
            var status = MapMember(m).Status;
            if (status == MemberStatus.Bot) bots++;
            else if (status is MemberStatus.Inactive or MemberStatus.NeverSpoke) inactive++;
            else active++;
        }

        var logs = LogStore.List(new LogFilter(), 1, 10);

        return new DashboardData
        {
            TotalUsers = active + inactive,
            ActiveUsers = active,
            InactiveUsers = inactive,
            BotUsers = bots,
            TotalRoles = guild.Roles.Count,
            RecentLogs = logs.Items,
        };
    }

    public async Task<MemberPage> GetUsersAsync(MemberFilter filter, int page = 1, int pageSize = 50)
    {
        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);
        var filtered = FilterMembers(members, filter);
        var total = filtered.Count;
        var start = Math.Max(0, (page - 1) * pageSize);
        var items = filtered.Skip(start).Take(pageSize).Select(MapMember).ToList();
        return new MemberPage { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<MemberInfo> GetUserDetailsAsync(ulong userId)
    {
        var guild = RequireGuild();
        var member = guild.GetUser(userId);
        if (member == null)
        {
            await guild.DownloadUsersAsync();
            member = guild.GetUser(userId);
        }
        if (member == null) throw new Exception("Usuario no encontrado");
        return MapMember(member);
    }

    private async Task<BatchResult<T>> RunBatchAsync<TItem, T>(List<TItem> items, Func<TItem, Task<T>> fn, int concurrency = 3, int delayMs = 300)
    {
        var results = new List<T>();
        var failures = new List<OperationFailure>();
        var index = 0;
        var gate = new object();

        async Task Worker()
        {
            while (true)
            {
                TItem current;
                lock (gate)
                {
                    if (index >= items.Count) return;
                    current = items[index++];
                }
                try
                {
                    var r = await fn(current);
                    lock (gate) results.Add(r);
                }
                catch (Exception ex)
                {
                    lock (gate) failures.Add(new OperationFailure { Item = current?.ToString() ?? "", Reason = ex.Message });
                }
                await Task.Delay(delayMs);
            }
        }

        var workers = Enumerable.Range(0, Math.Max(1, Math.Min(concurrency, items.Count))).Select(_ => Worker());
        await Task.WhenAll(workers);
        return new BatchResult<T> { Results = results, Failures = failures };
    }

    public async Task<OperationResult> KickUsersAsync(List<ulong> userIds, string? reason, int maxBatchSize)
    {
        var guild = RequireGuild();
        if (userIds.Count > maxBatchSize) throw new Exception($"Limite de seguridad: maximo {maxBatchSize} usuarios por operacion");

        var batch = await RunBatchAsync(userIds, async id =>
        {
            var member = guild.GetUser(id) ?? throw new Exception("Usuario no encontrado");
            await member.KickAsync(reason);
            return id;
        });

        var entry = new LogEntry { Type = "kick", TargetCount = batch.Results.Count, Success = batch.Failures.Count == 0 };
        entry.TargetIds.AddRange(batch.Results.Take(10).Select(x => x.ToString()));
        entry.Details["reason"] = reason;
        entry.Details["failedCount"] = batch.Failures.Count;
        LogStore.Add(entry);

        return new OperationResult { Success = true, AffectedCount = batch.Results.Count, FailedCount = batch.Failures.Count, Failures = batch.Failures };
    }

    public async Task<OperationResult> BanUsersAsync(List<ulong> userIds, string? reason, int deleteMessageSeconds, int maxBatchSize)
    {
        var guild = RequireGuild();
        if (userIds.Count > maxBatchSize) throw new Exception($"Limite de seguridad: maximo {maxBatchSize} usuarios por operacion");
        var pruneDays = Math.Clamp(deleteMessageSeconds / 86400, 0, 7);

        var batch = await RunBatchAsync(userIds, async id =>
        {
            await guild.AddBanAsync(id, pruneDays, reason);
            return id;
        });

        var entry = new LogEntry { Type = "ban", TargetCount = batch.Results.Count, Success = batch.Failures.Count == 0 };
        entry.TargetIds.AddRange(batch.Results.Take(10).Select(x => x.ToString()));
        entry.Details["reason"] = reason;
        entry.Details["failedCount"] = batch.Failures.Count;
        LogStore.Add(entry);

        return new OperationResult { Success = true, AffectedCount = batch.Results.Count, FailedCount = batch.Failures.Count, Failures = batch.Failures };
    }

    public async Task<OperationResult> TimeoutUsersAsync(List<ulong> userIds, long durationMs, string? reason, int maxBatchSize)
    {
        var guild = RequireGuild();
        if (userIds.Count > maxBatchSize) throw new Exception($"Limite de seguridad: maximo {maxBatchSize} usuarios por operacion");
        var span = TimeSpan.FromMilliseconds(durationMs);

        var batch = await RunBatchAsync(userIds, async id =>
        {
            var member = guild.GetUser(id) ?? throw new Exception("Usuario no encontrado");
            await member.SetTimeOutAsync(span, new RequestOptions { AuditLogReason = reason });
            return id;
        });

        var entry = new LogEntry { Type = "timeout", TargetCount = batch.Results.Count, Success = batch.Failures.Count == 0 };
        entry.TargetIds.AddRange(batch.Results.Take(10).Select(x => x.ToString()));
        entry.Details["reason"] = reason;
        entry.Details["durationMs"] = durationMs;
        entry.Details["failedCount"] = batch.Failures.Count;
        LogStore.Add(entry);

        return new OperationResult { Success = true, AffectedCount = batch.Results.Count, FailedCount = batch.Failures.Count, Failures = batch.Failures };
    }

    // ==SECCIÓN: Roles==

    public List<RoleInfo> GetRoles()
    {
        var guild = RequireGuild();
        return guild.Roles.Where(r => r.Id != guild.EveryoneRole.Id)
            .Select(r => new RoleInfo { Id = r.Id, Name = r.Name, Color = ColorHex(r.Color), Position = r.Position, MemberCount = r.Members.Count(), IsManaged = r.IsManaged })
            .OrderByDescending(r => r.Position).ToList();
    }

    public async Task<RoleInfo> CreateRoleAsync(string name, string? colorHex, bool hoist, bool mentionable)
    {
        var guild = RequireGuild();
        Color? color = string.IsNullOrEmpty(colorHex) ? null : ParseColor(colorHex);
        var role = await guild.CreateRoleAsync(name, null, color, hoist, mentionable);

        var entry = new LogEntry { Type = "role_add" };
        entry.Details["action"] = "create";
        entry.Details["roleId"] = role.Id;
        entry.Details["roleName"] = role.Name;
        LogStore.Add(entry);

        return new RoleInfo { Id = role.Id, Name = role.Name, Color = ColorHex(role.Color), Position = role.Position, MemberCount = 0, IsManaged = false };
    }

    public async Task EditRoleAsync(ulong roleId, string? name, string? colorHex, bool? hoist, bool? mentionable)
    {
        var guild = RequireGuild();
        var role = guild.GetRole(roleId) ?? throw new Exception("Rol no encontrado");

        await role.ModifyAsync(x =>
        {
            if (!string.IsNullOrEmpty(name)) x.Name = name;
            if (!string.IsNullOrEmpty(colorHex)) x.Color = ParseColor(colorHex);
            if (hoist.HasValue) x.Hoist = hoist.Value;
            if (mentionable.HasValue) x.Mentionable = mentionable.Value;
        });

        var entry = new LogEntry { Type = "role_add" };
        entry.Details["action"] = "edit";
        entry.Details["roleId"] = roleId;
        entry.Details["roleName"] = name ?? role.Name;
        LogStore.Add(entry);
    }

    public async Task DeleteRoleAsync(ulong roleId)
    {
        var guild = RequireGuild();
        var role = guild.GetRole(roleId) ?? throw new Exception("Rol no encontrado");
        var name = role.Name;
        await role.DeleteAsync(new RequestOptions { AuditLogReason = "Eliminado desde Discord Admin Tool" });

        var entry = new LogEntry { Type = "role_remove" };
        entry.Details["action"] = "delete";
        entry.Details["roleId"] = roleId;
        entry.Details["roleName"] = name;
        LogStore.Add(entry);
    }

    public async Task<List<MemberInfo>> GetRoleMembersAsync(ulong roleId)
    {
        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);
        return members.Where(m => m.Roles.Any(r => r.Id == roleId)).Select(MapMember).ToList();
    }

    public async Task<OperationResult> AddRoleToUsersAsync(ulong roleId, MemberFilter filter, int maxBatchSize)
    {
        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);
        var targets = FilterMembers(members, filter);
        if (targets.Count > maxBatchSize) throw new Exception($"Limite de seguridad: maximo {maxBatchSize} usuarios por operacion");
        var role = guild.GetRole(roleId) ?? throw new Exception("Rol no encontrado");

        var batch = await RunBatchAsync(targets, async member =>
        {
            await member.AddRoleAsync(role);
            return member.Id;
        });

        var entry = new LogEntry { Type = "role_add", TargetCount = batch.Results.Count, Success = batch.Failures.Count == 0 };
        entry.TargetIds.AddRange(batch.Results.Take(10).Select(x => x.ToString()));
        entry.Details["roleId"] = roleId;
        entry.Details["failedCount"] = batch.Failures.Count;
        LogStore.Add(entry);

        return new OperationResult { Success = true, AffectedCount = batch.Results.Count, FailedCount = batch.Failures.Count, Failures = batch.Failures };
    }

    public async Task<OperationResult> RemoveRoleFromUsersAsync(ulong roleId, MemberFilter filter, int maxBatchSize)
    {
        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);
        var targets = FilterMembers(members, filter).Where(m => m.Roles.Any(r => r.Id == roleId)).ToList();
        if (targets.Count > maxBatchSize) throw new Exception($"Limite de seguridad: maximo {maxBatchSize} usuarios por operacion");
        var role = guild.GetRole(roleId) ?? throw new Exception("Rol no encontrado");

        var batch = await RunBatchAsync(targets, async member =>
        {
            await member.RemoveRoleAsync(role);
            return member.Id;
        });

        var entry = new LogEntry { Type = "role_remove", TargetCount = batch.Results.Count, Success = batch.Failures.Count == 0 };
        entry.TargetIds.AddRange(batch.Results.Take(10).Select(x => x.ToString()));
        entry.Details["roleId"] = roleId;
        entry.Details["failedCount"] = batch.Failures.Count;
        LogStore.Add(entry);

        return new OperationResult { Success = true, AffectedCount = batch.Results.Count, FailedCount = batch.Failures.Count, Failures = batch.Failures };
    }

    public async Task<OperationResult> ReplaceRoleAsync(ulong sourceRoleId, ulong targetRoleId, MemberFilter filter, int maxBatchSize)
    {
        var removeResult = await RemoveRoleFromUsersAsync(sourceRoleId, filter, maxBatchSize);
        var addResult = await AddRoleToUsersAsync(targetRoleId, filter, maxBatchSize);
        return new OperationResult
        {
            Success = removeResult.Success && addResult.Success,
            AffectedCount = addResult.AffectedCount,
            FailedCount = removeResult.FailedCount + addResult.FailedCount,
            Failures = removeResult.Failures.Concat(addResult.Failures).ToList(),
        };
    }

    public async Task<RoleBackup> CreateRoleBackupAsync()
    {
        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);

        return new RoleBackup
        {
            GuildId = guild.Id,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            Roles = guild.Roles.Where(r => r.Id != guild.EveryoneRole.Id)
                .Select(r => new RoleBackupRole { Id = r.Id, Name = r.Name, Color = ColorHex(r.Color), Position = r.Position }).ToList(),
            MemberRoles = members.Select(m => new MemberRoleEntry
            {
                UserId = m.Id,
                RoleIds = m.Roles.Where(r => r.Id != guild.EveryoneRole.Id).Select(r => r.Id).ToList(),
            }).ToList(),
        };
    }

    public async Task<OperationResult> RestoreRoleBackupAsync(RoleBackup backup)
    {
        var guild = RequireGuild();
        if (backup.GuildId != guild.Id) throw new Exception("El backup pertenece a otro servidor");

        var batch = await RunBatchAsync(backup.MemberRoles, async entry =>
        {
            var member = guild.GetUser(entry.UserId);
            if (member == null) return entry.UserId;
            var wanted = entry.RoleIds.Select(id => guild.GetRole(id)).Where(r => r != null).Cast<SocketRole>().ToList();
            var current = member.Roles.Where(r => r.Id != guild.EveryoneRole.Id).ToList();
            var toRemove = current.Where(r => wanted.All(w => w.Id != r.Id)).ToList();
            var toAdd = wanted.Where(w => current.All(r => r.Id != w.Id)).ToList();
            if (toRemove.Count > 0) await member.RemoveRolesAsync(toRemove);
            if (toAdd.Count > 0) await member.AddRolesAsync(toAdd);
            return entry.UserId;
        });

        return new OperationResult { Success = true, AffectedCount = batch.Results.Count, FailedCount = batch.Failures.Count, Failures = batch.Failures };
    }

    // ==SECCIÓN: Purga==

    public async Task<PurgeResult> PreviewPurgeAsync(PurgeConfig config)
    {
        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);
        var targets = members.Where(m => IsPurgeCandidate(m, config)).ToList();

        return new PurgeResult
        {
            Success = true,
            AffectedCount = targets.Count,
            FailedCount = 0,
            Failures = new(),
            ExecutionTimeMs = 0,
            DryRun = true,
            Preview = targets.Take(200).Select(MapMember).ToList(),
        };
    }

    public async Task<PurgeResult> ExecutePurgeAsync(PurgeConfig config, int maxBatchSize)
    {
        var sw = Stopwatch.StartNew();
        if (config.DryRun) return await PreviewPurgeAsync(config);

        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);
        var targets = members.Where(m => IsPurgeCandidate(m, config)).ToList();
        if (targets.Count > maxBatchSize) throw new Exception($"Limite de seguridad: maximo {maxBatchSize} usuarios por operacion");

        var batch = await RunBatchAsync(targets, async member =>
        {
            await member.KickAsync("Purga automatica por inactividad");
            return member.Id;
        });

        var entry = new LogEntry { Type = "purge", TargetCount = batch.Results.Count, Success = batch.Failures.Count == 0 };
        entry.TargetIds.AddRange(batch.Results.Take(10).Select(x => x.ToString()));
        entry.Details["inactiveDays"] = config.InactiveDays;
        entry.Details["offlineDays"] = config.OfflineDays;
        entry.Details["failedCount"] = batch.Failures.Count;
        LogStore.Add(entry);

        return new PurgeResult
        {
            Success = true,
            AffectedCount = batch.Results.Count,
            FailedCount = batch.Failures.Count,
            Failures = batch.Failures,
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            DryRun = false,
        };
    }

    public async Task<OperationResult> SendMassDMAsync(ulong roleId, string message)
    {
        var guild = RequireGuild();
        var members = await GetMembersAsync(guild);
        var targets = members.Where(m => m.Roles.Any(r => r.Id == roleId) && !m.IsBot).ToList();

        var batch = await RunBatchAsync(targets, async member =>
        {
            await member.SendMessageAsync(message);
            return member.Id;
        }, concurrency: 2, delayMs: 800);

        var entry = new LogEntry { Type = "dm", TargetCount = batch.Results.Count, Success = batch.Failures.Count == 0 };
        entry.TargetIds.AddRange(batch.Results.Take(10).Select(x => x.ToString()));
        entry.Details["roleId"] = roleId;
        entry.Details["failedCount"] = batch.Failures.Count;
        LogStore.Add(entry);

        return new OperationResult { Success = true, AffectedCount = batch.Results.Count, FailedCount = batch.Failures.Count, Failures = batch.Failures };
    }

    // ==SECCIÓN: Canales==

    private static async Task<List<IMessage>> FetchMessagesAsync(IMessageChannel channel, int limit, ulong? beforeId)
    {
        var source = beforeId == null
            ? channel.GetMessagesAsync(limit).FlattenAsync()
            : channel.GetMessagesAsync(beforeId.Value, Direction.Before, limit).FlattenAsync();
        var messages = await source;
        return messages.ToList();
    }

    private static async Task<(int count, bool capped)> CountChannelMessagesAsync(ITextChannel channel, int cap = 300)
    {
        var count = 0;
        ulong? lastId = null;
        while (count < cap)
        {
            var batchSize = Math.Min(100, cap - count);
            var fetched = await FetchMessagesAsync(channel, batchSize, lastId);
            if (fetched.Count == 0) break;
            count += fetched.Count;
            lastId = fetched.Last().Id;
            if (fetched.Count < batchSize) break;
            await Task.Delay(150);
        }
        return (count, count >= cap);
    }

    public async Task<List<ChannelInfo>> ListChannelsAsync()
    {
        var guild = RequireGuild();
        var channels = guild.Channels.Where(c => c is SocketTextChannel or SocketVoiceChannel).ToList();

        var batch = await RunBatchAsync(channels, async channel =>
        {
            int messageCount = 0;
            bool capped = false;
            string? error = null;

            try
            {
                if (channel is ITextChannel textChannel)
                {
                    var counted = await CountChannelMessagesAsync(textChannel);
                    messageCount = counted.count;
                    capped = counted.capped;
                }
            }
            catch (Exception ex) { error = ex.Message; }

            var kind = channel is SocketNewsChannel ? ChannelKind.Announcement
                : channel is SocketVoiceChannel ? ChannelKind.Voice : ChannelKind.Text;

            return new ChannelInfo
            {
                Id = channel.Id,
                Name = channel.Name,
                Type = kind,
                MessageCount = messageCount,
                MessageCountCapped = capped,
                HasContent = error != null ? (bool?)null : messageCount > 0,
                Error = error,
            };
        }, concurrency: 5, delayMs: 200);

        return batch.Results.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
    }

    public bool CancelPurge(ulong channelId)
    {
        if (_activePurges.ContainsKey(channelId)) { _activePurges[channelId] = true; return true; }
        return false;
    }

    public async Task<PurgeChannelResult> PurgeChannelMessagesAsync(ulong channelId, string mode, int? amount, Action<int, int>? onProgress)
    {
        var guild = RequireGuild();
        if (guild.GetChannel(channelId) is not ITextChannel channel) throw new Exception("Canal no valido o no es de texto");

        _activePurges[channelId] = false;
        var limit = mode == "amount" ? Math.Max(0, amount ?? 0) : int.MaxValue;
        var fourteenDaysAgo = DateTimeOffset.UtcNow.AddDays(-14);
        var deleted = 0;
        var failed = 0;
        string? lastError = null;
        var consecutiveNoProgress = 0;
        var cancelledLocal = false;

        try
        {
            while (deleted < limit)
            {
                if (_activePurges[channelId]) { cancelledLocal = true; break; }
                var deletedBefore = deleted;
                var fetchSize = limit == int.MaxValue ? 100 : Math.Min(100, limit - deleted);
                var fetched = await FetchMessagesAsync(channel, fetchSize, null);
                if (fetched.Count == 0) break;

                var recent = fetched.Where(m => m.Timestamp > fourteenDaysAgo).ToList();
                var old = fetched.Where(m => m.Timestamp <= fourteenDaysAgo).ToList();

                if (recent.Count == 1)
                {
                    old.Add(recent[0]);
                    recent.Clear();
                }
                else if (recent.Count > 1)
                {
                    try
                    {
                        await channel.DeleteMessagesAsync(recent);
                        deleted += recent.Count;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        old.AddRange(recent);
                    }
                }

                foreach (var message in old)
                {
                    if (deleted >= limit || _activePurges[channelId]) { if (_activePurges[channelId]) cancelledLocal = true; break; }
                    try { await message.DeleteAsync(); deleted++; }
                    catch (Exception ex) { failed++; lastError = ex.Message; }
                    await Task.Delay(400);
                }

                onProgress?.Invoke(deleted, failed);

                if (fetched.Count < 100) break;
                if (deleted == deletedBefore)
                {
                    consecutiveNoProgress++;
                    if (consecutiveNoProgress >= 3) break;
                }
                else consecutiveNoProgress = 0;
            }
        }
        finally
        {
            _activePurges.TryRemove(channelId, out _);
        }

        var entry = new LogEntry { Type = "channel_clear", TargetCount = deleted, Success = failed == 0 };
        entry.TargetIds.Add(channelId.ToString());
        entry.Details["channelName"] = channel.Name;
        entry.Details["mode"] = mode;
        entry.Details["amount"] = amount;
        entry.Details["failedCount"] = failed;
        entry.Details["lastError"] = lastError;
        entry.Details["cancelled"] = cancelledLocal;
        LogStore.Add(entry);

        return new PurgeChannelResult { Success = true, DeletedCount = deleted, FailedCount = failed, LastError = lastError, Cancelled = cancelledLocal };
    }

    public async Task<CloneChannelResult> CloneAndReplaceChannelAsync(ulong channelId)
    {
        var guild = RequireGuild();
        var channel = guild.GetChannel(channelId) ?? throw new Exception("Canal no encontrado");
        var overwrites = channel.PermissionOverwrites.ToList();
        var name = channel.Name;

        ulong newChannelId;
        var movedCount = 0;
        var moveFailures = new List<OperationFailure>();

        if (channel is SocketVoiceChannel voiceChannel)
        {
            var membersInVoice = voiceChannel.ConnectedUsers.ToList();
            var newChannel = await guild.CreateVoiceChannelAsync(voiceChannel.Name, props =>
            {
                props.CategoryId = voiceChannel.CategoryId;
                props.Position = voiceChannel.Position;
                props.Bitrate = voiceChannel.Bitrate;
                props.UserLimit = voiceChannel.UserLimit;
                props.PermissionOverwrites = overwrites;
                props.RTCRegion = voiceChannel.RTCRegion;
            });
            newChannelId = newChannel.Id;

            foreach (var member in membersInVoice)
            {
                try { await member.ModifyAsync(x => x.Channel = newChannel); movedCount++; }
                catch (Exception ex) { moveFailures.Add(new OperationFailure { Item = member.Id.ToString(), Reason = ex.Message }); }
            }
        }
        else if (channel is SocketTextChannel textChannel)
        {
            var newChannel = await guild.CreateTextChannelAsync(textChannel.Name, props =>
            {
                props.CategoryId = textChannel.CategoryId;
                props.Position = textChannel.Position;
                props.PermissionOverwrites = overwrites;
                props.Topic = textChannel.Topic;
                props.IsNsfw = textChannel.IsNsfw;
                props.SlowModeInterval = textChannel.SlowModeInterval;
            });
            newChannelId = newChannel.Id;
        }
        else
        {
            throw new Exception("Tipo de canal no soportado para clonar");
        }

        await channel.DeleteAsync(new RequestOptions { AuditLogReason = "Reemplazado desde Discord Admin Tool (clonar y limpiar)" });

        var entry = new LogEntry { Type = "channel_clone", TargetCount = 1, Success = moveFailures.Count == 0 };
        entry.TargetIds.Add(channelId.ToString());
        entry.Details["channelName"] = name;
        entry.Details["newChannelId"] = newChannelId;
        entry.Details["movedMembers"] = movedCount;
        LogStore.Add(entry);

        return new CloneChannelResult { Success = true, NewChannelId = newChannelId, MovedMembers = movedCount, FailedMoves = moveFailures.Count };
    }
}
