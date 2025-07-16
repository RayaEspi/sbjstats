using Dalamud.Configuration;
using System;

namespace sbjStats;

[Serializable]
public class Configuration : IPluginConfiguration {
    public event Action? OnApiKeyChanged;
    public int Version { get; set; } = 0;
    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (_apiKey != value)
            {
                _apiKey = value;
                OnApiKeyChanged?.Invoke();
            }
        }
    }
    private string _apiKey = "";

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
