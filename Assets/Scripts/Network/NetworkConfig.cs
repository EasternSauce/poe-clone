using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PoeClone.Network
{
    [Serializable]
    public class NetworkConfigData
    {
        // Overwritten by Assets/StreamingAssets/network-config.json at runtime; this literal
        // only matters if that file is ever missing from the build output.
        public string serverUrl = "wss://REPLACE_WITH_YOUR_RENDER_URL.onrender.com";
    }

    /// <summary>
    /// Loads the session server's URL from StreamingAssets/network-config.json so it can be
    /// changed after the WebGL build is exported (just edit the JSON file in the hosted output)
    /// without needing Unity installed to rebuild. StreamingAssets must be read via
    /// UnityWebRequest, not File I/O - WebGL has no filesystem and serves it as a normal HTTP asset.
    /// </summary>
    public static class NetworkConfig
    {
        private const string ConfigFileName = "network-config.json";

        public static IEnumerator Load(Action<NetworkConfigData> onLoaded)
        {
            string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);

            using UnityWebRequest request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();

            NetworkConfigData data = new NetworkConfigData();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    data = JsonUtility.FromJson<NetworkConfigData>(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"NetworkConfig: failed to parse {ConfigFileName}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"NetworkConfig: could not load {ConfigFileName} ({request.error}); using default server URL.");
            }

            onLoaded?.Invoke(data);
        }
    }
}
