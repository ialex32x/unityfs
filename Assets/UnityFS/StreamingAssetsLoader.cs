using System;
using System.IO;
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
            // Application.streamingAssetsPath;
            _streamingAssetsPathRoot = Application.streamingAssetsPath + "/bundles/";
            if (Application.platform != RuntimePlatform.Android)
            {
                _streamingAssetsPathRoot = "file://" + _streamingAssetsPathRoot;
            }

            // #if UNITY_STANDALONE_WIN
            //             _streamingAssetsPathRoot = "file://" + Application.dataPath + "/StreamingAssets/";
            // #elif UNITY_STANDALONE_OSX
            //             _streamingAssetsPathRoot = "file://" + Application.dataPath + "/Data/StreamingAssets/";
            // #elif UNITY_IPHONE
            //             _streamingAssetsPathRoot = "file:///" + Application.dataPath + "/Raw/";
            //             _streamingAssetsPathRoot = _streamingAssetsPathRoot.Replace(" ", "%20");
            // #elif UNITY_ANDROID
            //             _streamingAssetsPathRoot = "jar:file://" + Application.dataPath + "!/assets/";
            // #else
            //             _streamingAssetsPathRoot = "::Unsupported::";
            // #endif
        }

        public IEnumerator Open()
        {
            //TODO: 读取 StreamingAssets 中的清单
            //...
            _manifest = new EmbeddedManifest(); // STUB
            // UnityWebRequest
            yield return null;
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

        public IEnumerator LoadBundle(string bundleName)
        {
            //TODO: create UWR and get assetbundle ...
            yield return null;
        }
    }
}
