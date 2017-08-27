using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace UUebViewCore {

    // キャッシュ保持と各種coroutineの生成を行う。

    public class ResourceLoader {
        private class SpriteCache : Dictionary<string, Sprite> {};
        private class PrefabCache : Dictionary<string, GameObject> {};
        private class GameObjCache : Dictionary<string, GameObject> {};

        

        /*
            global cache.

            sprites and prefabs are cached statically.
         */
        private static SpriteCache spriteCache = new SpriteCache();
        private static List<string> spriteDownloadingUris = new List<string>();


        private static PrefabCache prefabCache = new PrefabCache();
        private static List<string> loadingPrefabNames = new List<string>();

        private GameObjCache goCache = new GameObjCache();

        private readonly Autoya.HttpRequestHeaderDelegate requestHeader;
		private Dictionary<string, string> BasicRequestHeaderDelegate (HttpMethod method, string url, Dictionary<string, string> requestHeader, string data) {
			return requestHeader;
		}

		private readonly Autoya.HttpResponseHandlingDelegate httpResponseHandlingDelegate;
		private void BasicResponseHandlingDelegate (string connectionId, Dictionary<string, string> responseHeaders, int httpCode, object data, string errorReason, Action<string, object> succeeded, Action<string, int, string, AutoyaStatus> failed) {
			if (200 <= httpCode && httpCode < 299) {
				succeeded(connectionId, data);
				return;
			}
			failed(connectionId, httpCode, errorReason, new AutoyaStatus());
		}

        

        public readonly GameObject cacheBox;
        public readonly Action<IEnumerator> LoadParallel;

        public ResourceLoader (Action<IEnumerator> LoadParallel, Autoya.HttpRequestHeaderDelegate requestHeader=null, Autoya.HttpResponseHandlingDelegate httpResponseHandlingDelegate=null) {
            this.LoadParallel = LoadParallel;

            cacheBox = new GameObject("UUebViewGOPool");
            cacheBox.SetActive(false);

            defaultTagStrIntPair = new Dictionary<string, int>();
            defaultTagIntStrPair = new Dictionary<int, string>();
           
            foreach (var tag in Enum.GetValues(typeof(HTMLTag))) {
                var tagStr = tag.ToString();
                var index = (int)tag;

                defaultTagStrIntPair[tagStr] = index;
                defaultTagIntStrPair[index] = tagStr;
            }

            /*
                set request header generation func and response validation delegate.
             */
            if (requestHeader != null) {
				this.requestHeader = requestHeader;
			} else {
				this.requestHeader = BasicRequestHeaderDelegate;
			}
			
			if (httpResponseHandlingDelegate != null) {
				this.httpResponseHandlingDelegate = httpResponseHandlingDelegate;
			} else {
				this.httpResponseHandlingDelegate = BasicResponseHandlingDelegate;
			}
        }

        public IEnumerator<string> DownloadHTMLFromWeb (string url, Action<ContentType, int, string> failed) {
            var timeoutSec = ConstSettings.TIMEOUT_SEC;
            var timeoutTick = (DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSec)).Ticks;
            var connectionId = ConstSettings.CONNECTIONID_DOWNLOAD_HTML_PREFIX + Guid.NewGuid().ToString();
            using(var request = UnityWebRequest.Get(url)) {
                var reqHeader = requestHeader(HttpMethod.Get, url, new Dictionary<string, string>(), string.Empty);
                foreach (var item in reqHeader) {
                    request.SetRequestHeader(item.Key, item.Value);
                }

                var p = request.Send();
				
				while (!p.isDone) {
					yield return null;

					// check timeout.
					if (timeoutSec != 0 && timeoutTick < DateTime.UtcNow.Ticks) {
						request.Abort();
						failed(ContentType.HTML, -1, "failed to download html:" + url + " by timeout.");
						yield break;
					}
				}

                var responseCode = (int)request.responseCode;
				var responseHeaders = request.GetResponseHeaders();

                if (request.isNetworkError) {
                    httpResponseHandlingDelegate(
                        connectionId,
                        responseHeaders,
                        responseCode,
                        null, 
                        request.error, 
                        (conId, data) => {
                            throw new Exception("request encountered some kind of error.");
                        }, 
                        (conId, code, reason, autoyaStatus) => {
                            failed(ContentType.HTML, code, "failed to download html:" + url + " reason:" + reason);
                        }
                    );
                    yield break;
                }

                var htmlStr = string.Empty;
                
                httpResponseHandlingDelegate(
                    connectionId,
                    responseHeaders,
                    responseCode,
                    request, 
                    request.error,
                    (conId, data) => {
                        htmlStr = Encoding.UTF8.GetString(request.downloadHandler.data);
                    }, 
                    (conId, code, reason, autoyaStatus) => {
                        failed(ContentType.HTML, code, "failed to download html:" + url + " reason:" + reason);
                    }
                );

                if (!string.IsNullOrEmpty(htmlStr)) {
                    yield return htmlStr;
                }
            }
        }

        public IEnumerator<GameObject> LoadPrefab (int tagValue, TreeType treeType) {
            GameObject prefab = null;
            
            var prefabName = GetTagFromValue(tagValue);
            
            while (loadingPrefabNames.Contains(prefabName)) {
                yield return null;
            }
            
            if (prefabCache.ContainsKey(prefabName)) {
                // Debug.LogError("return cached loadingPrefabName:" + loadingPrefabName);
                yield return prefabCache[prefabName];
            } else {
                loadingPrefabNames.Add(prefabName);

                {
                    switch (IsDefaultTag(tagValue)) {
                        case true: {

                            switch (treeType) {
                                case TreeType.Container: {
                                    throw new Exception("unexpected loading.");
                                }
                                default: {
                                    // コンテナ以外、いろんなデフォルトコンテンツがここにくる。
                                    
                                    var loadingPrefabName = ConstSettings.PREFIX_PATH_INFORMATION_RESOURCE + ConstSettings.UUEBTAGS_DEFAULT + "/" + prefabName;

                                    var cor = LoadPrefabFromResourcesOrCache(loadingPrefabName);
                                    while (cor.MoveNext()) {
                                        if (cor.Current != null) {
                                            break;
                                        }                           
                                        yield return null;
                                    }
                                    var loadedPrefab = cor.Current;
                                    
                                    prefab = loadedPrefab;
                                    break;
                                }
                            }
                            break;
                        }

                        // 非デフォルトタグ、customBox以外はloadpathが存在する。
                        default: {
                            switch (treeType) {
                                case TreeType.Container: 
                                case TreeType.CustomBox: {
                                    throw new Exception("unexpected loading.");
                                }
                                default: {
                                    var tag = GetTagFromValue(tagValue);
                                    var loadPath = GetCustomTagLoadPath(tagValue, treeType);

                                    var cor = LoadCustomPrefabFromLoadPathOrCache(loadPath);
                                    while (cor.MoveNext()) {
                                        if (cor.Current != null) {
                                            break;
                                        }
                                        yield return null;
                                    }

                                    var loadedPrefab = cor.Current;
                                    
                                    prefab = loadedPrefab;
                                    break;
                                }
                            }
                            break;
                        }
                    }

                    // cache.
                    prefabCache[prefabName] = prefab;
                }

                loadingPrefabNames.Remove(prefabName);
                yield return prefab;
            }
        }

        public IEnumerator<GameObject> LoadGameObjectFromPrefab (string id, int tagValue, TreeType treeType) {
            GameObject gameObj = null;
            var tagName = GetTagFromValue(tagValue);

            if (goCache.ContainsKey(id)) {
                gameObj = goCache[id];
            } else {
                switch (IsDefaultTag(tagValue)) {
                    case true: {
                        switch (treeType) {
                            case TreeType.Container: {
                                var containerObj = new GameObject(tagName);
                                var trans = containerObj.AddComponent<RectTransform>();
                                trans.anchorMin = Vector2.up;
                                trans.anchorMax = Vector2.up;
                                trans.offsetMin = Vector2.up;
                                trans.offsetMax = Vector2.up;
                                trans.pivot = Vector2.up;

                                gameObj = containerObj;
                                break;
                            }
                            default: {
                                // コンテナ以外、いろんなデフォルトコンテンツがここにくる。
                                var prefabName = GetTagFromValue(tagValue);
                                var loadingPrefabName = ConstSettings.PREFIX_PATH_INFORMATION_RESOURCE + ConstSettings.UUEBTAGS_DEFAULT + "/" + prefabName;

                                var cor = LoadPrefabFromResourcesOrCache(loadingPrefabName);
                                while (cor.MoveNext()) {
                                    if (cor.Current != null) {
                                        break;
                                    }                           
                                    yield return null;
                                }

                                var loadedPrefab = cor.Current;
                                
                                gameObj = GameObject.Instantiate(loadedPrefab);
                                break;
                            }
                        }
                        break;
                    }

                    // 非デフォルトタグ、customBox以外はloadpathが存在する。
                    default: {
                        switch (treeType) {
                            case TreeType.Container: {
                                var containerObj = new GameObject(tagName);
                                var trans = containerObj.AddComponent<RectTransform>();
                                trans.anchorMin = Vector2.up;
                                trans.anchorMax = Vector2.up;
                                trans.offsetMin = Vector2.up;
                                trans.offsetMax = Vector2.up;
                                trans.pivot = Vector2.up;

                                gameObj = containerObj;
                                break;
                            }
                            case TreeType.CustomBox: {
                                var customBoxObj = new GameObject(tagName);
                                var trans = customBoxObj.AddComponent<RectTransform>();
                                trans.anchorMin = Vector2.up;
                                trans.anchorMax = Vector2.up;
                                trans.offsetMin = Vector2.up;
                                trans.offsetMax = Vector2.up;
                                trans.pivot = Vector2.up;

                                gameObj = customBoxObj;
                                break;
                            }
                            default: {
                                var tag = GetTagFromValue(tagValue);
                                var loadPath = GetCustomTagLoadPath(tagValue, treeType);

                                var cor = LoadCustomPrefabFromLoadPathOrCache(loadPath);
                                while (cor.MoveNext()) {
                                    if (cor.Current != null) {
                                        break;
                                    }
                                    yield return null;
                                }

                                var loadedPrefab = cor.Current;
                                
                                gameObj = GameObject.Instantiate(loadedPrefab);
                                break;
                            }
                        }
                        break;
                    }
                }

                // set name.
                gameObj.name = tagName;

                // cache.
                goCache[id] = gameObj;
            }

            // Debug.LogError("loaded, tagName:" + tagName);

            yield return gameObj;
        }


        /**
            loadPathからcustomTag = レイヤーのprefabを返す。
         */
        public IEnumerator<GameObject> LoadCustomPrefabFromLoadPathOrCache (string loadPath) {
            // Debug.LogError("loadPath:" + loadPath);
            var schemeAndPath = loadPath.Split(new char[]{'/'}, 2);
            var scheme = schemeAndPath[0];

            var extLen = Path.GetExtension(loadPath).Length;
            var uri = loadPath.Substring(0, loadPath.Length - extLen);
            
            IEnumerator<GameObject> cor = null;

            switch (scheme) {
                case "assetbundle:": {
                    cor = LoadPrefabFromAssetBundle(uri);
                    break;
                }
                case "https:":
                case "http:": {
                    throw new Exception("http|https are not supported scheme for downloading prefab. use assetbundle:// instead.");
                }
                case "resources:": {
                    cor = LoadPrefabFromResourcesOrCache(uri.Substring("resources://".Length));
                    break;
                }
                default: {// other.
                    throw new Exception("unsupported scheme:" + scheme + " found when loading custom tag prefab:" + loadPath);
                }
            }

            while (cor.MoveNext()) {
                if (cor.Current != null) {
                    break;
                }
                yield return null;
            }

            yield return cor.Current;
        }

        /**
            resourcesからprefabを返す。
            キャッシュヒット処理込み。
         */
        private IEnumerator<GameObject> LoadPrefabFromResourcesOrCache (string loadingPrefabName) {
            // Debug.LogError("start loadingPrefabName:" + loadingPrefabName);

            var cor = Resources.LoadAsync(loadingPrefabName);
        
            while (!cor.isDone) {
                yield return null;
            }
            var obj = cor.asset as GameObject;

            if (obj == null) {
                var failedObj = new GameObject("failed to load element:" + loadingPrefabName);
                loadingPrefabNames.Remove(loadingPrefabName);
                yield return failedObj;
            } else {
                // cache.
                prefabCache[loadingPrefabName] = obj;
                loadingPrefabNames.Remove(loadingPrefabName);
                yield return obj;
            }
        }

        private IEnumerator<GameObject> LoadPrefabFromAssetBundle (string loadingPrefabName) {
            while (loadingPrefabNames.Contains(loadingPrefabName)) {
                yield return null;
            }

            if (prefabCache.ContainsKey(loadingPrefabName)) {
                // Debug.LogError("キャッシュから読み出す");
                var cachedPrefab = prefabCache[loadingPrefabName];
                yield return cachedPrefab;
            } else {
                // アセット名が書いてあると思うんで、assetBundleListとかから取り寄せる
                Debug.LogError("まだ実装してないassetBundleからprefabを読む仕掛け");
                yield return null;
            }
        }

        /**
            layout, materialize時に画像を読み込む。
            キャッシュヒット処理込み。
         */
        public IEnumerator<Sprite> LoadImageAsync (string uriSource) {
            while (spriteDownloadingUris.Contains(uriSource)) {
                yield return null;
            }
            
            if (spriteCache.ContainsKey(uriSource)) {
                yield return spriteCache[uriSource];
            } else {
                // start downloading.
                spriteDownloadingUris.Add(uriSource);
                {
                    /*
                        supported schemes are,
                            
                            ^http://		http scheme => load asset from web.
                            ^https://		https scheme => load asset from web.
                            ^assetbundle://	assetbundle scheme => load asset from assetBundle.
                            ^resources://   resources scheme => (Resources/)somewhere/resource path.
                            ^./				relative path => (Resources/)somewhere/resource path.
                    */
                    var schemeAndPath = uriSource.Split(new char[]{'/'}, 2);
                    var scheme = schemeAndPath[0];

                    IEnumerator<Sprite> cor = null;
                    switch (scheme) {
                        case "assetbundle:": {
                            var bundleName = uriSource;
                            cor = LoadImageFromAssetBundle(uriSource);
                            break;
                        }
                        case "https:":
                        case "http:": {
                            cor = LoadImageFromWeb(uriSource);
                            break;
                        }
                        case "resources:": {
                            var resourcePath = uriSource.Substring("resources:".Length + 2);
                            cor = LoadImageFromResources(resourcePath);
                            break;
                        }
                        default: {// other.
                            throw new Exception("unsupported scheme:" + scheme);
                        }
                    }

                    while (cor.MoveNext()) {
                        if (cor.Current != null) {
                            // set cache.
                            spriteCache[uriSource] = cor.Current;
                            break;
                        }
                        yield return null;
                    }
                    
                    if (cor.Current == null) {
                        // failed to get image.
                        spriteDownloadingUris.Remove(uriSource);
                        yield break;
                    }

                    spriteDownloadingUris.Remove(uriSource);
                    yield return spriteCache[uriSource];
                }
            }
        }


        /*
            return imageLoad iEnum functions.   
         */

        private IEnumerator<Sprite> LoadImageFromAssetBundle (string assetName) {
            yield return null;
            Debug.LogError("LoadImageFromAssetBundle bundleName:" + assetName);
        }

        private IEnumerator<Sprite> LoadImageFromResources (string uriSource) {
            var extLen = Path.GetExtension(uriSource).Length;
            var uri = uriSource.Substring(0, uriSource.Length - extLen);

            var resourceLoadingCor = Resources.LoadAsync(uri);
            while (!resourceLoadingCor.isDone) {
                yield return null;
            }
            
            if (resourceLoadingCor.asset == null) {
                yield break;
            }

            // create tex.
            var tex = resourceLoadingCor.asset as Texture2D;
            var spr = Sprite.Create(tex, new Rect(0,0, tex.width, tex.height), Vector2.zero);
            
            yield return spr;
        }

        private IEnumerator<Sprite> LoadImageFromWeb (string url) {
            var connectionId = ConstSettings.CONNECTIONID_DOWNLOAD_IMAGE_PREFIX + Guid.NewGuid().ToString();
            var reqHeaders = requestHeader(HttpMethod.Get, url, new Dictionary<string, string>(), string.Empty);

            // start download tex from url.
            using (var request = UnityWebRequestTexture.GetTexture(url)) {
                foreach (var reqHeader in reqHeaders) {
                    request.SetRequestHeader(reqHeader.Key, reqHeader.Value);
                }

                var p = request.Send();

                var timeoutSec = ConstSettings.TIMEOUT_SEC;
                var limitTick = DateTime.UtcNow.AddSeconds(timeoutSec).Ticks;

                while (!p.isDone) {
                    yield return null;

                    // check timeout.
                    if (0 < timeoutSec && limitTick < DateTime.UtcNow.Ticks) {
                        Debug.LogError("timeout. load aborted, dataPath:" + url);
                        request.Abort();
                        yield break;
                    }
                }

                var responseCode = (int)request.responseCode;
                var responseHeaders = request.GetResponseHeaders();
                
                
                if (request.isNetworkError) {
                    httpResponseHandlingDelegate(
                        connectionId,
                        responseHeaders,
                        responseCode,
                        null, 
                        request.error, 
                        (conId, data) => {
                            throw new Exception("request encountered some kind of error.");
                        }, 
                        (conId, code, reason, autoyaStatus) => {
                            // do nothing.
                        }
                    );
                    yield break;
                }

                Sprite spr = null;

                httpResponseHandlingDelegate(
                    connectionId,
                    responseHeaders,
                    responseCode,
                    request, 
                    request.error,
                    (conId, data) => {
                        // create tex.
                        var tex = DownloadHandlerTexture.GetContent(request);

                        // cache this sprite for other requests.
                        spr = Sprite.Create(tex, new Rect(0,0, tex.width, tex.height), Vector2.zero);
                    }, 
                    (conId, code, reason, autoyaStatus) => {
                        // do nothing.
                    }
                );

                if (spr != null) {
                    yield return spr;
                }
            }
        }

        private string GetCustomTagLoadPath (int tagValue, TreeType treeType) {
            var tag = GetTagFromValue(tagValue);
            var targetPrefab = string.Empty;

            switch (treeType) {
                case TreeType.CustomLayer:
                case TreeType.CustomEmptyLayer: {
                    return uuebTags.layerInfos.Where(t => t.layerName == tag).Select(t => t.loadPath).FirstOrDefault();
                }
                case TreeType.Content_Img:
                case TreeType.Content_Text: {
                    return uuebTags.contents.Where(t => t.contentName == tag).Select(t => t.loadPath).FirstOrDefault();
                }
                default: {
                    throw new Exception("unexpected tree type:" + treeType + " of tag:" + tag);
                }
            }            
        }

        public void BackGameObjects (params string[] usingIds) {
            var cachedKeys = goCache.Keys.ToArray();

            var unusingIds = cachedKeys.Except(usingIds);
            foreach (var unusingCacheId in unusingIds) {
                var cache = goCache[unusingCacheId];
                cache.transform.SetParent(cacheBox.transform);
            }
        }


        public void Reset () {
            BackGameObjects();
            goCache.Clear();
        }
        
        public int GetAdditionalTagCount () {
            return undefinedTagDict.Count;
        }

        public bool IsDefaultTag (int tag) {
            if (defaultTagIntStrPair.ContainsKey(tag)) {
                return true;
            }
            return false;
        }

        public TreeType GetTreeType (int tag) {
            // 組み込みtagであれば、静的に解決できる。
            if (defaultTagIntStrPair.ContainsKey(tag)) {
				switch (tag) {
                    case (int)HTMLTag.img: {
                        return TreeType.Content_Img;
                    }
                    case (int)HTMLTag.hr:
                    case (int)HTMLTag.br: {
                        return TreeType.Content_CRLF;
                    }
                    default: {
                        return TreeType.Container;
                    }
                }
			}

            // tag is not default.
            
            var customTagStr = GetTagFromValue(tag);
            // Debug.LogError("customTagStr:" + customTagStr);
            // foreach (var s in customTagTypeDict.Keys) {
            //     Debug.LogError("s:" + s);
            // }
            if (!customTagTypeDict.ContainsKey(customTagStr)) {
                return TreeType.NotFound;
            }
            return customTagTypeDict[customTagStr];
        }

        private class AssetLoadingConstraint : IDisposable {
			private string target;
			private List<string> list;
			
			public AssetLoadingConstraint (string target, List<string> list) {
				this.target = target;
				this.list = list;

				this.list.Add(this.target);
			}

			private bool disposedValue = false;

			protected virtual void Dispose (bool disposing) {
				if (!disposedValue) {
					if (disposing) {
                        Debug.LogError("remove from list:" + target);
						list.Remove(target);
					}
					disposedValue = true;
				}
			}

			void IDisposable.Dispose () {
				Dispose(true);
			}
		}








        private UUebTags uuebTags;
        public bool IsLoadingUUebTags {
            get; private set;
        }

        private Dictionary<string, TreeType> customTagTypeDict = new Dictionary<string, TreeType>();
        private Dictionary<string, BoxConstraint[]> layerDict;
        private Dictionary<string, BoxPos>unboxedLayerSizeDict;
        

        public IEnumerator LoadUUebTags (string uriSource) {
            if (IsLoadingUUebTags) {
                throw new Exception("multiple uuebTags description found. only one uuebTags description is valid.");
            }

            var schemeEndIndex = uriSource.IndexOf("//");
            var scheme = uriSource.Substring(0, schemeEndIndex);
            
            IsLoadingUUebTags = true;


            Action<UUebTags> succeeded = uuebTags => {
                this.uuebTags = uuebTags;
                this.customTagTypeDict = this.uuebTags.GetTagTypeDict();

                // レイヤー名:constraintsの辞書を生成しておく。
                this.layerDict = new Dictionary<string, BoxConstraint[]>();
                this.unboxedLayerSizeDict = new Dictionary<string, BoxPos>();

                var layerInfos = uuebTags.layerInfos;

                foreach (var layerInfo in layerInfos) {
                    layerDict[layerInfo.layerName.ToLower()] = layerInfo.boxes;
                    unboxedLayerSizeDict[layerInfo.layerName.ToLower()] = layerInfo.unboxedLayerSize;
                }

                IsLoadingUUebTags = false;
            };
            
            Action<int, string> failed = (code, reason) => {
                throw new Exception("未対処なリストのロードエラー。failed to load uuebTags. code:" + code + " reason:" + reason + " from:" + uriSource);
                this.uuebTags = new UUebTags(ConstSettings.UUEBTAGS_DEFAULT, new ContentInfo[0], new LayerInfo[0]);// set empty list.
                IsLoadingUUebTags = false;
            };

            IEnumerator cor = null;
            switch (scheme) {
                case "assetbundle:": {
                    var bundleName = uriSource;
                    cor = LoadTagsFromAssetBundle(uriSource, succeeded, failed);
                    break;
                }
                case "https:":
                case "http:": {
                    cor =  LoadTagsFromWeb(uriSource, succeeded, failed);
                    break;
                }
                case "resources:": {
                    var resourcePath = uriSource.Substring("resources:".Length + 2);
                    cor = LoadTagsFromResources(resourcePath, succeeded, failed);
                    break;
                }
                default: {// other.
                    throw new Exception("unsupported scheme found, scheme:" + scheme);
                }
            }

            while (cor.MoveNext()) {
                yield return null;
            }
        }

        public BoxPos GetUnboxedLayerSize (int tagValue) {
            var key = GetTagFromValue(tagValue);
            return unboxedLayerSizeDict[key];
        }

        public BoxConstraint[] GetConstraints (int tagValue) {
            var key = GetTagFromValue(tagValue);
            return layerDict[key];
        }

        public string GetLayerBoxName (int layerTag, int boxTag) {
            return GetTagFromValue(layerTag) + "_" + GetTagFromValue(boxTag);
        }

        private IEnumerator LoadTagsFromAssetBundle (string url, Action<UUebTags> succeeded, Action<int, string> failed) {
            Debug.LogError("not yet applied. LoadListFromAssetBundle url:" + url);
            failed(-1, "not yet applied.");
            yield break;
        }
        
        private IEnumerator LoadTagsFromWeb (string url, Action<UUebTags> loadSucceeded, Action<int, string> loadFailed) {
            var connectionId = ConstSettings.CONNECTIONID_DOWNLOAD_UUEBTAGS_PREFIX + Guid.NewGuid().ToString();
            var reqHeaders = requestHeader(HttpMethod.Get, url, new Dictionary<string, string>(), string.Empty);

            using (var request = UnityWebRequest.Get(url)) {
                foreach (var reqHeader in reqHeaders) {
                    request.SetRequestHeader(reqHeader.Key, reqHeader.Value);
                }

                var p = request.Send();

                var timeoutSec = ConstSettings.TIMEOUT_SEC;
                var limitTick = DateTime.UtcNow.AddSeconds(timeoutSec).Ticks;

                while (!p.isDone) {
                    yield return null;

                    // check timeout.
                    if (0 < timeoutSec && limitTick < DateTime.UtcNow.Ticks) {
                        request.Abort();
                        loadFailed(-1, "timeout to download list from url:" + url);
                        yield break;
                    }
                }

                var responseCode = (int)request.responseCode;
                var responseHeaders = request.GetResponseHeaders();
                
                if (request.isNetworkError) {
                    httpResponseHandlingDelegate(
                        connectionId,
                        responseHeaders,
                        responseCode,
                        null, 
                        request.error, 
                        (conId, data) => {
                            throw new Exception("request encountered some kind of error.");
                        }, 
                        (conId, code, reason, autoyaStatus) => {
                            loadFailed(code, reason);
                        }
                    );
                    yield break;
                }

                httpResponseHandlingDelegate(
                    connectionId,
                    responseHeaders,
                    responseCode,
                    string.Empty, 
                    request.error,
                    (conId, unusedData) => {
                        var jsonStr = request.downloadHandler.text;
                        var newDepthAssetList = JsonUtility.FromJson<UUebTags>(jsonStr);

                        loadSucceeded(newDepthAssetList);
                    }, 
                    (conId, code, reason, autoyaStatus) => {
                        loadFailed(code, reason);
                    }
                );
            }
        }
        
        private IEnumerator LoadTagsFromResources (string path, Action<UUebTags> succeeded, Action<int, string> failed) {
            var requestCor = Resources.LoadAsync(path);

            while (!requestCor.isDone) {
                yield return null;
            }

            if (requestCor.asset == null) {
                failed(-1, "no list found in resources.");
                yield break;
            }

            var jsonStr = (requestCor.asset as TextAsset).text;
            var depthAssetList = JsonUtility.FromJson<UUebTags>(jsonStr);
            succeeded(depthAssetList);
		}




        private readonly Dictionary<string, int> defaultTagStrIntPair;
        private readonly Dictionary<int, string> defaultTagIntStrPair;

        private Dictionary<string, int> undefinedTagDict = new Dictionary<string, int>();

        public string GetTagFromValue (int index) {
			if (index < defaultTagStrIntPair.Count) {
				return defaultTagIntStrPair[index];
			}

			if (undefinedTagDict.ContainsValue(index)) {
				return undefinedTagDict.FirstOrDefault(x => x.Value == index).Key;
			}
			
			throw new Exception("failed to get tag from index. index:" + index);
		}

        public int FindOrCreateTag (string tagCandidateStr) {
            if (defaultTagStrIntPair.ContainsKey(tagCandidateStr)) {
				return defaultTagStrIntPair[tagCandidateStr];
			}
            // collect undefined tag.
            // Debug.LogError("tagCandidateStr:" + tagCandidateStr);

            if (undefinedTagDict.ContainsKey(tagCandidateStr)) {
                return undefinedTagDict[tagCandidateStr];
            }
            
            var count = (int)HTMLTag._END + undefinedTagDict.Count + 1;
            undefinedTagDict[tagCandidateStr] = count;
            return count;
        }

    }
}