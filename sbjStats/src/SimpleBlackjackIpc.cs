using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Dalamud.Plugin.Services;
using ECommons.EzIpcManager;
using ECommons.Logging;
using Serilog;

namespace sbjStats;

public class SimpleBlackjackIpc {
    
    public SimpleBlackjackIpc(Func<string, List<StatsRecording>> getStats, Func<Dictionary<string, string>> getArchives)
    {
        PluginLog.Information("SimpleBlackjackIpc constructor called.");
        GetStats = getStats;
        GetArchives = getArchives;
        EzIPC.Init(this, "SimpleBlackjack", safeWrapper: SafeWrapper.AnyException, false);
        PluginLog.Information("EzIPC.Init called for SimpleBlackjack.");
    }

    [EzIPC] public Func<string, List<StatsRecording>> GetStats;
    [EzIPC] public Func<Dictionary<string, string>> GetArchives;
    [EzIPCEvent]
    public void OnGameFinished() {
        PluginLog.Information("OnGameFinished called in SimpleBlackjackIpc.");
        if (GetStats == null) {
            PluginLog.Error("GetStats is null in SimpleBlackjackIpc.OnGameFinished.");
            return;
        }
        try {
            var stats = GetStats(Guid.Empty.ToString());
            var newestStat = stats.OrderByDescending(s => s.Time).FirstOrDefault();
            if (newestStat == null) {
                PluginLog.Error("No stats found in SimpleBlackjackIpc.OnGameFinished.");
                return;
            }
            
            newestStat.ArchiveID = Guid.NewGuid().ToString();
            
            try
            {
                SendStatToServer(newestStat);
                newestStat.Saved = true; // Mark as saved if needed
            }
            catch (Exception e)
            {
                Log.Warning("Error sending stat to server: {Message}", e.Message);
            }
        } catch (Exception ex) {
            PluginLog.Error($"Error in SimpleBlackjackIpc.OnGameFinished: {ex.Message}");
        }
    }

    public void SendStatToServer(sbjStats.StatsRecording stat)
    {
        var apiKey = Plugin.PluginInterface.GetPluginConfig() is Configuration config ? config.ApiKey : "";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            PluginLog.Error("API key is missing. Cannot send stat to server.");
            return;
        }
        PluginLog.Information("Sending stat to server...");
        try {
            var datetime = DateTimeOffset.FromUnixTimeMilliseconds(stat.Time).ToString("dd/MM/yyyy HH:mm:ss");
            var players = string.Join(", ", stat.Players);
            var collected = stat.BetsCollected.ToString("N0");
            var paid = stat.Payouts.ToString("N0");
            var profit = (stat.BetsCollected - stat.Payouts).ToString("N0");
            var handsJson = Newtonsoft.Json.JsonConvert.SerializeObject(stat.Hands);
            var details = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(handsJson));

            var payload = new {
                datetime,
                players,
                collected,
                paid,
                profit,
                details
            };
            
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = client.PostAsync("https://api.espi-couple.com/sbj/import", content).Result;
                if (response.IsSuccessStatusCode)
                {
                    PluginLog.Information("Stat sent successfully.");
                }
                else
                {
                    PluginLog.Error(
                        $"Failed to send stat. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                }
            }
            

            PluginLog.Information($"Payload to send: {Newtonsoft.Json.JsonConvert.SerializeObject(payload)}");
        } catch (Exception ex) {
            PluginLog.Error($"Error sending stat to server: {ex.Message}");
        }
    }
}

public class StatsRecording {
    public long Time;
    public int BetsCollected;
    public int Payouts;
    public List<string> Players = [];
    public bool Saved = false;
    public string ArchiveID = Guid.Empty.ToString();
    public List<HandStat> Hands = [];
}

public class HandStat {
    public string PlayerName;
    public List<Card> Cards;
    public int SplitNum = 0;
    public int Bet = 0;
    public int Payout = 0;
    public bool IsDoubleDown = false;
    public Result Result;
    public bool Dealer = false;
}

public enum Result : int {
    Bust=0, Win=1, Draw=2, Loss=3, Waiting=4, Blackjack=5, Surrender=6
}

public enum Card : int {
    Number_2 = 2, Number_3 = 3, Number_4 = 4, Number_5 = 5,
    Number_6 = 6, Number_7 = 7, Number_8 = 8, Number_9 = 9,
    Ace = 1, Jack = 11, Queen = 12, King = 13, Number_10 = 10
}
