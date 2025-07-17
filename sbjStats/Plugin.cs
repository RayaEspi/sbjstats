using System;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using sbjStats.Windows;
using ECommons;
using ECommons.Logging;

namespace sbjStats;

public sealed class Plugin : IDalamudPlugin {
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/sbjstats";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("sbjStats");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private SimpleBlackjackIpc SimpleBlackjackIpc { get; set; }
    private bool ipcInitialized = false;

    public Plugin() {
        ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
        InitializeIpc();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
            HelpMessage = "The stats are being uploaded in background ^^"
        });
    }
    
    private void InitializeIpc() {
        try
        {
            Log.Information("Initializing IPC for SimpleBlackjack...");
            SimpleBlackjackIpc = new SimpleBlackjackIpc(
                getStats: HandleGetStats,
                getArchives: HandleGetArchives
            );
            ipcInitialized = true;
            Log.Information("IPC initialized.");
        } catch (Exception ex)
        {
            Log.Error($"Failed to initialize IPC: {ex.Message}");
            ipcInitialized = false;
        }
    }

    public void Dispose() {
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
        ECommonsMain.Dispose();
    }

    private void OnCommand(string command, string args) {
        if (!ipcInitialized) {
            Log.Error("IPC is not initialized yet. Please try again in a moment.");
            return;
        }
        var trimmedArgs = args?.Trim().ToLowerInvariant();
        if (trimmedArgs == "archive") {
            var archives = SimpleBlackjackIpc.GetArchives();
            Log.Information(Newtonsoft.Json.JsonConvert.SerializeObject(archives));
            var output = string.Join(", ", archives.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            Log.Information(output);
            Log.Information("========== Available Archives ==========");
        } else if(trimmedArgs == "stats") {
            var stats = SimpleBlackjackIpc.GetStats(Guid.Empty.ToString());
            Log.Information(Newtonsoft.Json.JsonConvert.SerializeObject(stats));
            
            Log.Information("========== Current Stats ==========");
            if (stats.Count == 0) {
                Log.Information("No stats available.");
                return;
            }
            Log.Information($"Total Stats Count: {stats.Count}");
            
            
            var output = string.Join(", ", stats.Select(s => $"Time: {s.Time}, Bets: {s.BetsCollected}, Payouts: {s.Payouts}"));
            Log.Information(output);
            Log.Information("========== Current Stats ==========");
        } else if (trimmedArgs == "config") {
            ToggleConfigUI();
        } else {
            ToggleMainUI();
        }
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    private List<StatsRecording> HandleGetStats(string archiveId) {
        return new List<StatsRecording>();
    }

    private Dictionary<string, string> HandleGetArchives() {
        return new Dictionary<string, string>();
    }
}
