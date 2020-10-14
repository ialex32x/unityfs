using System;
using System.IO;
using System.Collections;

namespace UnityFS.Analyzer
{
    using UnityEngine;

    public interface IAssetsAnalyzer
    {
        void OnAssetOpen(string assetPath);
        void OnAssetAccess(string assetPath);
        void OnAssetClose(string assetPath);

        void Begin();
        void End();
    }

    public class DefaultAssetsAnalyzer : IAssetsAnalyzer
    {
        private bool _stop;
        private bool _dirty;
        private string _listDataPath;
        private AssetListData _listData;
        private AnalyzerTimeline _timeline = new AnalyzerTimeline();

        public DefaultAssetsAnalyzer(string listDataPath)
        {
            _listDataPath = listDataPath;
            _listData = AssetListData.ReadFrom(_listDataPath) ?? new AssetListData();
            _timeline.Start();
            if (_listData.Begin())
            {
                _dirty = true;
            }
            JobScheduler.DispatchCoroutine(_Update());
        }

        private IEnumerator _Update()
        {
            while (!_stop)
            {
                _timeline.Update();
                yield return null;
            }
        }

        public void Begin()
        {
            if (_stop)
            {
                throw new NotSupportedException();
            }
        }

        public void End()
        {
            _stop = true;
            _timeline.Stop();
            _listData.End();
            if (_dirty)
            {
                AssetListData.WriteTo(_listDataPath, _listData);
            }
        }

        public void OnAssetOpen(string assetPath)
        {
            if (_timeline.OpenAsset(assetPath))
            {
                if (_listData.AddObject(_timeline.frameTime, assetPath))
                {
                    _dirty = true;
                }
            }
        }

        public void OnAssetAccess(string assetPath)
        {
        }

        public void OnAssetClose(string assetPath)
        {
        }
    }
}
