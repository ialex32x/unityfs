using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;
    using UnityEngine.Networking;

    public class StreamingAssetsLoader
    {
        private string _streamingAssetsPathRoot;

        private EmbeddedManifest _manifest;

        public StreamingAssetsLoader()
        {
            _streamingAssetsPathRoot = Application.streamingAssetsPath + "/bundles/";
            if (Application.platform != RuntimePlatform.Android)
            {
                _streamingAssetsPathRoot = "file://" + _streamingAssetsPathRoot;
            }
        }

        public IEnumerator OpenManifest()
        {
            var uri = _streamingAssetsPathRoot + EmbeddedManifest.FileName;
            var uwr = UnityWebRequest.Get(uri);
            yield return uwr.SendWebRequest();
            if (uwr.error == null && uwr.responseCode == 200)
            {
                try
                {
                    var json = uwr.downloadHandler.text;
                    _manifest = JsonUtility.FromJson<EmbeddedManifest>(json);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"StreamingAssetsLoader open failed: {exception}");
                }
                foreach (var bundleInfo in _manifest.bundles)
                {
                    Debug.Log($"read embedded bundle {bundleInfo.name}");
                }
            }
            else
            {
                Debug.Log($"open failed {uwr.error}: {uwr.responseCode}");
            }
        }

        public bool Contains(string bundleName, string checksum, int size)
        {
            if (_manifest != null)
            {
                for (int i = 0, count = _manifest.bundles.Count; i < count; i++)
                {
                    var bundleInfo = _manifest.bundles[i];
                    if (bundleInfo.name == bundleName)
                    {
                        if (bundleInfo.size == size && bundleInfo.checksum == checksum)
                        {
                            return true;
                        }
                        return false;
                    }
                }
            }
            return false;
        }

        public IEnumerator LoadBundle(string bundleName, string checksum, int size, Action<AssetBundle> callback)
        {
            if (!Contains(bundleName, checksum, size))
            {
                callback(null);
                yield break;
            }
            var uri = _streamingAssetsPathRoot + bundleName;
            var uwr = UnityWebRequestAssetBundle.GetAssetBundle(uri);
            yield return uwr.SendWebRequest();
            if (uwr.error == null && uwr.responseCode == 200)
            {
                AssetBundle assetBundle = null;
                try
                {
                    assetBundle = DownloadHandlerAssetBundle.GetContent(uwr);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"StreamingAssetsLoader load failed: {exception}");
                }
                callback(assetBundle);
            }
            else
            {
                Debug.Log($"load failed {uwr.error}: {uwr.responseCode}");
            }
        }
    }
}
