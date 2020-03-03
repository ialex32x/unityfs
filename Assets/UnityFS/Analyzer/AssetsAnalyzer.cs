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
    }

    public class DefaultAssetsAnalyzer : IAssetsAnalyzer
    {
        private bool _stop;
        private AssetListData _listData;
        private AnalyzerTimeline _timeline = new AnalyzerTimeline();

        public DefaultAssetsAnalyzer(AssetListData listData)
        {
            _listData = listData;
            _timeline.Start();
            _listData?.Begin();
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

        public void Stop()
        {
            _stop = true;
            _timeline.Stop();
            _listData?.End();
        }

        public void OnAssetOpen(string assetPath)
        {
            if (_timeline.OpenAsset(assetPath))
            {
                _listData?.AddObject(_timeline.frameTime, assetPath);
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
