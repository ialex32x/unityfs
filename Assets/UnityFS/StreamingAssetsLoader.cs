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
            _manifest = new EmbeddedManifest();
            _streamingAssetsPathRoot = Utils.Helpers.GetStreamingAssetsPath(Manifest.EmbeddedBundlesBasePath);
        }

        // 载入 StreamingAssets 中的内嵌清单, 完成后回调 callback
        public void LoadEmbeddedManifest(Action<StreamingAssetsLoader> callback)
        {
            JobScheduler.DispatchCoroutine(LoadEmbeddedManifestCo(callback));
        }

        private IEnumerator LoadEmbeddedManifestCo(Action<StreamingAssetsLoader> callback)
        {
            var uri = _streamingAssetsPathRoot + Manifest.EmbeddedManifestFileName;
            var uwr = UnityWebRequest.Get(uri);
            yield return uwr.SendWebRequest();
            try
            {
                if (uwr.error == null && uwr.responseCode == 200)
                {
                    var json = uwr.downloadHandler.text;
                    JsonUtility.FromJsonOverwrite(json, _manifest);
                }
                else
                {
                    Debug.LogWarning($"StreamingAssetsLoader open failed {uwr.error}: {uwr.responseCode}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"StreamingAssetsLoader open failed: {exception}");
            }
            finally
            {
                callback(this);
            }
        }

        public bool Contains(Manifest.BundleInfo bundleInfo)
        {
            for (int i = 0, count = _manifest.bundles.Count; i < count; i++)
            {
                var embeddedBundleInfo = _manifest.bundles[i];
                if (embeddedBundleInfo.name == bundleInfo.name)
                {
                    if (embeddedBundleInfo.size == bundleInfo.size &&
                        embeddedBundleInfo.checksum == bundleInfo.checksum)
                    {
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        public IEnumerator LoadStream(Manifest.BundleInfo bundleInfo, Action<Stream> callback)
        {
            MemoryStream stream = null;
            if (Contains(bundleInfo))
            {
                var uri = _streamingAssetsPathRoot + bundleInfo.name;
                var uwr = UnityWebRequest.Get(uri);
                yield return uwr.SendWebRequest();
                if (uwr.error == null && uwr.responseCode == 200)
                {
                    try
                    {
                        var bytes = uwr.downloadHandler.data;
                        stream = new MemoryStream(bytes);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"StreamingAssetsLoader load failed: {exception}");
                    }
                }
                else
                {
                    Debug.LogWarning($"load failed {uwr.error}: {uwr.responseCode}");
                }
            }

            callback(stream);
        }
    }
}