using System.Threading.Tasks;
using UnityEngine;
using Hey.Services.RemoteConfig;

public class ExampleHeyRemoteConfig : MonoBehaviour
{
    [Header("Fetch Config")]
    public string key_name = "Setting0";
    public string config_url = "https://raw.githubusercontent.com/JahnStar/Hey-Remote-Config/master/Runtime/Samples/.example_remote_config.json";
    public string secretKey = "none";
    [Header("Loaded From Remote")]
    public string remote_data;
    [Header("Loaded From Cache")]
    public string cached_data;
    

    RuntimeConfigObject configObject;
    
    async Task Start()
    {
        configObject = new("settings", secretKey, config_url);
        configObject.FetchCompleted += ApplyRemoteConfig;
        await configObject.FetchAsync((response) => Debug.Log("Fetch status: " + response.status.ToString()));
    }

    void ApplyRemoteConfig(ConfigResponse configResponse)
    {
        string setting = configObject.GetString(key_name);
        switch (configResponse.requestOrigin)
        {
            case ConfigOrigin.Default:
                Debug.Log("No settings loaded this session and no local cache file exists; using default values.");
                break;
            case ConfigOrigin.Cached:
                Debug.Log("No settings loaded this session; using cached values from a previous session.");
                cached_data = setting;
                break;
            case ConfigOrigin.Remote:
                Debug.Log("New settings loaded this session; update values accordingly.");
                remote_data = setting;
                break;
        }
        Debug.Log("Setting0: " + setting);
    }
}