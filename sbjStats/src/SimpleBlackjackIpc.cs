using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ECommons.EzIpcManager;
using ECommons.Logging;
using Newtonsoft.Json;
using Serilog;

namespace sbjStats;

public class SimpleBlackjackIpc
{
    public SimpleBlackjackIpc(Func<string, List<StatsRecording>> getStats, Func<Dictionary<string, string>> getArchives)
    {
        PluginLog.Information("SimpleBlackjackIpc constructor called.");
        GetStats = getStats;
        GetArchives = getArchives;
        EzIPC.Init(this, "SimpleBlackjack", safeWrapper: SafeWrapper.AnyException, false);
        PluginLog.Information("EzIPC.Init called for SimpleBlackjack.");
    }

    [EzIPC]
    public Func<string, List<StatsRecording>> GetStats;

    [EzIPC]
    public Func<Dictionary<string, string>> GetArchives;

    [EzIPCEvent]
    public void OnGameFinished()
    {
        PluginLog.Information("OnGameFinished called in SimpleBlackjackIpc.");
        if (GetStats == null)
        {
            PluginLog.Error("GetStats is null in SimpleBlackjackIpc.OnGameFinished.");
            return;
        }

        try
        {
            var stats = GetStats(Guid.Empty.ToString());
            var newestStat = stats.OrderByDescending(s => s.Time).FirstOrDefault();
            if (newestStat == null)
            {
                PluginLog.Error("No stats found in SimpleBlackjackIpc.OnGameFinished.");
                return;
            }

            newestStat.ArchiveID = Guid.NewGuid().ToString();

            try
            {
                var config = Plugin.PluginInterface.GetPluginConfig() as Configuration;
                if (config != null && config.EnableLiveUploading)
                {
                    SendStatToServer(newestStat);
                }
                else
                {
                    PluginLog.Information("Live uploading is disabled. Stat not sent to server.");
                }
            }
            catch (Exception e)
            {
                Log.Warning("Error sending stat to server: {Message}", e.Message);
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error in SimpleBlackjackIpc.OnGameFinished: {ex.Message}");
        }
    }
    
    private string GetApiKey()
    {
        var config = Plugin.PluginInterface.GetPluginConfig() as Configuration;
        return config?.ApiKey?.Trim() ?? string.Empty;
    }
    
    private object CreatePayload(StatsRecording stat)
    {
        var datetime = DateTimeOffset
                       .FromUnixTimeMilliseconds(stat.Time)
                       .ToString("dd/MM/yyyy HH:mm:ss");
        var players = string.Join(", ", stat.Players);
        var collected = stat.BetsCollected.ToString("N0");
        var paid = stat.Payouts.ToString("N0");
        var profit = (stat.BetsCollected - stat.Payouts).ToString("N0");
        var handsJson = JsonConvert.SerializeObject(stat.Hands);
        var details = Convert.ToBase64String(Encoding.UTF8.GetBytes(handsJson));

        return new
        {
            datetime,
            players,
            collected,
            paid,
            profit,
            details
        };
    }
    
    private void PostPayload(object payload, string endpoint, string apiKey)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = client.PostAsync(endpoint, content).Result;
            if (response.IsSuccessStatusCode)
            {
                var message = payload is IEnumerable<object>
                                  ? "Mass stats sent successfully."
                                  : "Stat sent successfully.";
                PluginLog.Information(message);
                Plugin.ChatGui.Print("[SBJStats] Upload successful! ♥");
            }
            else
            {
                var typeLabel = payload is IEnumerable<object> ? "mass stats" : "stat";
                PluginLog.Error(
                    $"Failed to send {typeLabel}. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                Plugin.ChatGui.Print("[SBJStats] Upload failed q.q");
            }
        }
        
    }
    
    public void SendStatToServer(StatsRecording stat)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            PluginLog.Error("API key is missing. Cannot send stat to server.");
            return;
        }

        PluginLog.Information("Sending stat to server...");
        try
        {
            var payload = CreatePayload(stat);
            PostPayload(payload, "https://api.espi-couple.com/sbj/import", apiKey);
            PluginLog.Information(
                $"Payload to send: {JsonConvert.SerializeObject(payload)}");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error sending stat to server: {ex.Message}");
        }
    }
    
    public void SendMassStatsToServer()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            PluginLog.Error("API key is missing. Cannot send stats to server.");
            return;
        }

        PluginLog.Information("Sending mass stats to server...");
        try
        {
            var stats = GetStats(Guid.Empty.ToString());
            var statsList = stats.OrderByDescending(s => s.Time);
            var payloadList = statsList.Select(CreatePayload).ToList();
            PostPayload(payloadList, "https://api.espi-couple.com/sbj/import/mass", apiKey);
            PluginLog.Information(
                $"Payload to send: {JsonConvert.SerializeObject(payloadList)}");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error sending mass stats to server: {ex.Message}");
        }
    }
}

public class StatsRecording
{
    public long Time;
    public int BetsCollected;
    public int Payouts;
    public List<string> Players = [];
    public bool Saved = false;
    public string ArchiveID = Guid.Empty.ToString();
    public List<HandStat> Hands = [];
}

public class HandStat
{
    public string PlayerName;
    public List<Card> Cards = new();
    public int SplitNum = 0;
    public int Bet = 0;
    public int Payout = 0;
    public bool IsDoubleDown = false;
    public Result Result;
    public bool Dealer = false;
}

public enum Result : int
{
    Bust = 0,
    Win = 1,
    Draw = 2,
    Loss = 3,
    Waiting = 4,
    Blackjack = 5,
    Surrender = 6
}

public enum Card : int
{
    Number_2 = 2,
    Number_3 = 3,
    Number_4 = 4,
    Number_5 = 5,
    Number_6 = 6,
    Number_7 = 7,
    Number_8 = 8,
    Number_9 = 9,
    Ace = 1,
    Jack = 11,
    Queen = 12,
    King = 13,
    Number_10 = 10
}
