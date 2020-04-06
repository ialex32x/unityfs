using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class UScene
    {
        protected enum SceneState
        {
            Ready,
            Loading,
            Loaded,
            Unloading,
        }

        protected UAsset _asset;
        protected LoadSceneMode _mode;
        private SceneState _state = SceneState.Ready;

        private List<Action<UScene>> _callbacks = new List<Action<UScene>>();

        public event Action<UScene> completed
        {
            add
            {
                if (_state == SceneState.Loaded)
                {
                    value(this);
                }
                else
                {
                    _callbacks.Add(value);
                }
            }

            remove
            {
                _callbacks.Remove(value);
            }
        }

        public UScene(UAsset asset)
        {
            _asset = asset;
            _asset.completed += OnAssetCompleted;
        }

        private void OnAssetCompleted(UAsset asset)
        {
            if (_state == SceneState.Loading && asset.isLoaded)
            {
                JobScheduler.DispatchCoroutine(_LoadAsync());
            }
        }

        public UScene Load()
        {
            if (_state == SceneState.Ready)
            {
                _state = SceneState.Loading;
                _mode = LoadSceneMode.Single;
                OnAssetCompleted(_asset);
            }
            return this;
        }

        public UScene LoadAdditive()
        {
            if (_state == SceneState.Ready)
            {
                _state = SceneState.Loading;
                _mode = LoadSceneMode.Additive;
                OnAssetCompleted(_asset);
            }
            return this;
        }

        protected virtual AsyncOperation LoadSceneAsync()
        {
            return SceneManager.LoadSceneAsync(_asset.assetPath, _mode);
        }

        protected virtual AsyncOperation UnloadSceneAsync()
        {
            return SceneManager.UnloadSceneAsync(_asset.assetPath);
        }

        private IEnumerator _LoadAsync()
        {
            yield return LoadSceneAsync();

            if (_state == SceneState.Loading)
            {
                _state = SceneState.Loaded;
                while (_callbacks.Count > 0)
                {
                    var callback = _callbacks[0];
                    _callbacks.RemoveAt(0);
                    callback(this);
                }
            }
            else if (_state == SceneState.Unloading)
            {
                Debug.LogWarning("未加载完成时已经请求卸载场景");
                var e = _UnloadAsync();
                while (e.MoveNext())
                {
                    yield return e.Current;
                }
            }
        }

        private IEnumerator _UnloadAsync()
        {
            yield return UnloadSceneAsync();
            _state = SceneState.Ready;
        }

        public void UnloadScene()
        {
            if (_state != SceneState.Ready)
            {
                if (_state == SceneState.Loaded)
                {
                    _state = SceneState.Unloading;
                    JobScheduler.DispatchCoroutine(_UnloadAsync());
                }
                else
                {
                    if (_asset.isLoaded)
                    {
                        _state = SceneState.Unloading;
                    }
                    else
                    {
                        _state = SceneState.Ready;
                    }
                }
            }
        }
    }

    public class UEditorScene : UScene
    {
        public UEditorScene(UAsset asset)
        : base(asset)
        {
        }

        protected override AsyncOperation LoadSceneAsync()
        {
#if UNITY_EDITOR && UNITY_2018_3_OR_NEWER
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(_asset.assetPath, new LoadSceneParameters(_mode));
#else
            return null;
#endif
        }

        protected override AsyncOperation UnloadSceneAsync()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.UnloadSceneAsync(_asset.assetPath);
#else
            return null;
#endif
        }

    }
}
