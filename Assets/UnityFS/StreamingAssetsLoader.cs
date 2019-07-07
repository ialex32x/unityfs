using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class StreamingAssetsLoader
    {
        // private string _streamingPrefix;

        private EmbeddedManifest _manifest;

        public StreamingAssetsLoader()
        {
            // Application.streamingAssetsPath;

// #if UNITY_STANDALONE_WIN
//             _streamingPrefix = "file://" + Application.dataPath + "/StreamingAssets/";
// #elif UNITY_STANDALONE_OSX
//             _streamingPrefix = "file://" + Application.dataPath + "/Data/StreamingAssets/";
// #elif UNITY_IPHONE
//             _streamingPrefix = "file:///" + Application.dataPath + "/Raw/";
//             _streamingPrefix = _streamingPrefix.Replace(" ", "%20");
// #elif UNITY_ANDROID
//             _streamingPrefix = "jar:file://" + Application.dataPath + "!/assets/";
// #else
//             _streamingPrefix = "::Unsupported::";
// #endif
        }

        public IEnumerator Open()
        {
            //TODO: 读取 StreamingAssets 中的清单
            //...
            _manifest = new EmbeddedManifest(); // STUB
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
