using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace sbjStats.Windows;

public class MainWindow : Window, IDisposable {
    private Plugin Plugin;

    public MainWindow(Plugin plugin)
        : base("SBJStats##Main_1", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw() {
        if (!string.IsNullOrEmpty(Plugin.Configuration.ApiKey))
        {
            //ImGui.TextUnformatted($"{_apiResponse}");
            ImGui.TextUnformatted("Heya love! The plugin is working in the background ♥");
        }
        else
        {
            ImGui.TextUnformatted("Please set your API key in the configuration window first.");
        }
        if (ImGui.Button("Show Settings"))
        {
            Plugin.ToggleConfigUI();
        }
    }
}
