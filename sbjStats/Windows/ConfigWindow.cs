using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace sbjStats.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private string apiKeyInput;
    private bool enableLiveUploadingInput;

    public ConfigWindow(Plugin plugin) : base("SBJStats config###config_1") {
        Flags = ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
        apiKeyInput = Configuration.ApiKey ?? string.Empty;
        enableLiveUploadingInput = Configuration.EnableLiveUploading;
    }

    public void Dispose() { }

    public override void Draw() {
        ImGui.InputText("API Key", ref apiKeyInput, 64);
        
        if (ImGui.Checkbox("Enable live uploading", ref enableLiveUploadingInput)) {
            Configuration.EnableLiveUploading = enableLiveUploadingInput;
            Configuration.Save();
        }

        if (ImGui.Button("Save"))
        {
            Configuration.ApiKey = apiKeyInput;
            Configuration.EnableLiveUploading = enableLiveUploadingInput;
            Configuration.Save();
            ImGui.TextUnformatted("Configuration saved successfully.");
        }
    }
}
