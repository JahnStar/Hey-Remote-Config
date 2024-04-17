# Hey Remote Config

Hey Remote Config is a Unity package that enables users to create and edit JSON config files in a custom way, akin to Unity's Remote Config editor window. Users can upload generated config files to their preferred host and fetch the settings from the remote config file's URL for runtime remote config usage in games. The package seamlessly handles offline scenarios by caching config data until an internet connection is available.

## Features

- **Create** and **edit** JSON config files within the editor (The UI in *Unity Remote Config package* was used)
- **Save** and **load** config files to desired locations within the project.
- **Encrypt** config files with a secret key via the editor window.
- **Fetch** config file at runtime from any URL.
- **Easily integrate** the package into Unity projects.
- **Automatically cache** config files for offline usage.

### Installation

#### via Git URL

1. Open `Packages/manifest.json` in your project with a text editor.
2. Add the following line to the `dependencies` block:
```json
{
  "dependencies": {
    "com.jahnstar.hey-remote-config": "https://github.com/jahnstar/hey-remote-config.git"
  }
}
```

## Usage

### Config Editor

To create or edit a config file:
1. Open `Unity > Window > Hey Remote Config > Config Editor`.
2. Modify the config settings as desired.
3. Encrypt if you want with changing Secret key any diffrent from "none".
4. Save the config file to the desired location within your project to editing another time.

### Using Example Runtime Integration

To use the example sample:
1. After installing the package, Implement the provided example runtime script `ExampleHeyRemoteConfig` script to a game object.
2. Click the play button in the Unity editor to see the script fetch the remote config settings from the specified URL.

```csharp
using UnityEngine;
using Hey.Services.RemoteConfig;

public class ExampleHeyRemoteConfig : MonoBehaviour
{
    RuntimeConfigObject configObject;
    
    async void Start()
    {
        configObject = new RuntimeConfigObject("settings", "none", "https://raw.githubusercontent.com/JahnStar/Hey-Remote-Config/master/Runtime/Samples/.example_remote_config.json");
        configObject.FetchCompleted += ApplyRemoteConfig;
        await configObject.FetchAsync((response) => Debug.Log("Fetch status: " + response.status.ToString()));
    }

    void ApplyRemoteConfig(ConfigResponse configResponse)
    {
        string setting = configObject.GetString("Setting0");
        switch (configResponse.requestOrigin)
        {
            case ConfigOrigin.Default:
                Debug.Log("No settings loaded this session and no local cache file exists; using default values.");
                break;
            case ConfigOrigin.Cached:
                Debug.Log("No settings loaded this session; using cached values from a previous session.");
                break;
            case ConfigOrigin.Remote:
                Debug.Log("New settings loaded this session; update values accordingly.");
                break;
        }
        Debug.Log("Setting0: " + setting); // Console Log: forty-two
    }
}
```

## Contributing

Contributions to this project are welcome! Here's how you can contribute:

1. Make an [Issue](https://github.com/jahnstar/hey-remote-config/issues/new).
2. Fork this repository. (For further details, see this [article](https://docs.github.com/en/github/getting-started-with-github/fork-a-repo))
3. Develop changes to a new branch to your forked repository.
4. Create a Pull Request from your forked repository against this repository.
   1. Insert a reference in the description to the issue created earlier eg. "Closes #1" where "1" is the issue number.
   2. Pull request description should answer these questions: "What has been changed" and "What is this for"

Thank you for contributing! :smile:

## License

This project is licensed under the CC-BY-4.0 License - see the [LICENSE](LICENSE) file for details.

## Credits

Developed by Halil Emre Yildiz ([GitHub](https://github.com/JahnStar))
