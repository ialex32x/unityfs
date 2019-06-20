using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 仅编辑器运行时可用
    public class AssetDatabaseAssetProvider : IAssetProvider
    {
        protected class EditorUAsset : UAsset
        {
            public EditorUAsset(string assetPath)
            : base(assetPath)
            {

            }

            public override bool Load()
            {
                if (this._state == AssetState.None)
                {
                    #if UNITY_EDITOR
                    this._object = UnityEditor.AssetDatabase.LoadMainAssetAtPath(this._assetPath);
                    #endif
                    this._state = AssetState.Loaded;
                }
                throw new NotImplementedException();
            }
        }

        public UAsset GetAsset(string assetPath)
        {
            throw new NotImplementedException();
        }
    }
}
