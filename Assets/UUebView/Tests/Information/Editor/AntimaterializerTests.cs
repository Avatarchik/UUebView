using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Collections;
using UnityEditor;
using System.IO;
using UUebViewCore;

/**
	test for materializer
 */
namespace AutoyaFramework.Information {
	public class AntimaterializerTests {
		
		private static GameObject editorCanvas;

		private static void Run (string testTargetSampleObjName, Action run) {
			editorCanvas = GameObject.Find("Canvas");
			
			var sample = GameObject.Find("Editor/Canvas/" + testTargetSampleObjName);

			var target = GameObject.Instantiate(sample);
			target.name = sample.name;// set name.

			target.transform.SetParent(editorCanvas.transform, false);

			Selection.activeGameObject = target;

			try {
				run();
			} catch (Exception e) {
				Debug.LogError("e:" + e);
			}

			GameObject.DestroyImmediate(target);

			// テストのために作り出したview以下のものも消す
			var viewAssetPath = "Assets/InformationResources/Resources/Views/" + testTargetSampleObjName;
			if (Directory.Exists(viewAssetPath)) {
				Directory.Delete(viewAssetPath, true);
				AssetDatabase.Refresh();
			}
		}

		[MenuItem("/Window/TestMaterialize")] public static void RunTests () {
			EditorSampleMaterial();
			EditorSampleMaterialWithDepth();
			CheckIfSameCustomTagInOneView();
			EditorSampleMaterialWithMoreDepth();
			ExportedCustomTagPrefabHasZeroPos();
			ExportedCustomTagPrefabHasLeftTopFixedAnchor();
			ExportedCustomTagPrefabHasOriginalSize();
			ExportedCustomTagNotContainedBox();
			ExportedCustomTagChildHasZeroPos();
			ExportMultipleBoxConstraints();
		}

		// 階層なしのものを分解する
		private static void EditorSampleMaterial () {
			var testTargetSampleObjName = "EditorSampleMaterial";
			Run(testTargetSampleObjName,
				() => {
					Antimaterializer.Antimaterialize();
					
					// カスタムタグを生成する。
					var jsonAsset = Resources.Load("Views/" + testTargetSampleObjName + "/DepthAssetList");
					Debug.Assert(jsonAsset, "no output file.");
				}
			);
		}

		// 階層付きのレイヤーを分解する
		public static void EditorSampleMaterialWithDepth () {
			var testTargetSampleObjName = "EditorSampleMaterialWithDepth";
			Run(testTargetSampleObjName,
				() => {
					Antimaterializer.Antimaterialize();
					/*
						で、吐き出したものが存在していて、そのツリー構造を読み込んで意図とあってれば良し。
					 */

					var jsonAsset = Resources.Load("Views/" + testTargetSampleObjName + "/DepthAssetList") as TextAsset;
					var jsonStr = jsonAsset.text;
					
					var list = JsonUtility.FromJson<UUebTags>(jsonStr);
					Debug.Assert(list.viewName == testTargetSampleObjName);

					var boxConstraintes = list.layerInfos;
					
					// 本体 + MyImgItemの2レイヤーで2
					Debug.Assert(boxConstraintes.Length == 2, "boxConstraints:" + boxConstraintes.Length);

					// prefabファイルが生成されているかチェック
					var createdAsset = Resources.Load("Views/" + testTargetSampleObjName + "/MyImgItem") as GameObject;
					Debug.Assert(createdAsset != null, "createdAsset:" + createdAsset + " is null.");

					// 作成されたprefabのRectTransがあるか
					var rectTrans = createdAsset.GetComponent<RectTransform>();
					Debug.Assert(rectTrans != null);

					// 原点を指しているか
					Debug.Assert(rectTrans.anchoredPosition == Vector2.zero);
				}
			);
		}

		public static void CheckIfSameCustomTagInOneView () {
			// 同一名称のカスタムタグが存在するという違反があるので、キャンセルされる。
			var testTargetSampleObjName = "CheckIfSameCustomTagInOneView";
			Run(testTargetSampleObjName,
				() => {
					try {
						Antimaterializer.Antimaterialize();
						Debug.Assert(false, "never done.");
					} catch {
						// pass.
					}
				}
			);
		}

		public static void EditorSampleMaterialWithMoreDepth () {
			var testTargetSampleObjName = "EditorSampleMaterialWithMoreDepth";
			Run(testTargetSampleObjName,
				() => {
					Antimaterializer.Antimaterialize();

					var jsonAsset = Resources.Load("Views/" + testTargetSampleObjName + "/DepthAssetList") as TextAsset;
					var jsonStr = jsonAsset.text;
					
					var list = JsonUtility.FromJson<UUebTags>(jsonStr);
					Debug.Assert(list.viewName == testTargetSampleObjName);

					var boxConstraints = list.layerInfos;
					
					// MyImgAndTextItem, IMG, Text_CONTAINER, Text の4つが吐かれる
					Debug.Assert(boxConstraints.Length == 4, "boxConstraints:" + boxConstraints.Length);
				}
			);
		}
		

		public static void ExportedCustomTagPrefabHasZeroPos () {
			var testTargetSampleObjName = "EditorSampleMaterialWithMoreDepth";
			Run(testTargetSampleObjName,
				() => {
					Antimaterializer.Antimaterialize();
					
					// EditorSampleMaterialWithMoreDepthプレファブの原点が0
					var prefab = Resources.Load("Views/" + testTargetSampleObjName + "/MyImgAndTextItem") as GameObject;
					if (prefab != null) {
						var rectTrans = prefab.GetComponent<RectTransform>();
						Debug.Assert(rectTrans.anchoredPosition == Vector2.zero, "not zero, pos:" + rectTrans.anchoredPosition);
					}
				}
			);
		}

		public static void ExportedCustomTagPrefabHasLeftTopFixedAnchor () {
			var testTargetSampleObjName = "EditorSampleMaterialWithMoreDepth";
			Run(testTargetSampleObjName,
				() => {
					Antimaterializer.Antimaterialize();
					
					var prefab = Resources.Load("Views/" + testTargetSampleObjName + "/MyImgAndTextItem") as GameObject;
					if (prefab != null) {
						var rectTrans = prefab.GetComponent<RectTransform>();
						
						Debug.Assert(rectTrans.anchorMin == new Vector2(0,1) && rectTrans.anchorMax == new Vector2(0,1), "not match.");
					}
				}
			);
		}

		public static void ExportedCustomTagPrefabHasOriginalSize () {
			var testTargetSampleObjName = "EditorSampleMaterialWithMoreDepth";
			Run(testTargetSampleObjName,
				() => {
					var original = GameObject.Find("MyImgAndTextItem");
					
					Antimaterializer.Antimaterialize();
					
					var prefab = Resources.Load("Views/" + testTargetSampleObjName + "/MyImgAndTextItem") as GameObject;
					if (prefab != null) {
						var rectTrans = prefab.GetComponent<RectTransform>();
						var originalRectTrans = original.GetComponent<RectTransform>();

						Debug.Assert(rectTrans.sizeDelta == originalRectTrans.sizeDelta, "not match.");
					}
				}
			);
		}

		public static void ExportedCustomTagNotContainedBox () {
			var testTargetSampleObjName = "EditorSampleMaterialWithMoreDepth";
			Run(testTargetSampleObjName,
				() => {
					Antimaterializer.Antimaterialize();
					
					var prefab = Resources.Load("Views/" + testTargetSampleObjName + "/MyImgAndTextItem") as GameObject;
					if (prefab != null) {
						var childCount = prefab.transform.childCount;
						
						Debug.Assert(childCount == 0, "not match. childCount:" + childCount);
					}
				}
			);
		}

		public static void ExportedCustomTagChildHasZeroPos () {
			var testTargetSampleObjName = "EditorSampleMaterialWithMoreDepth";
			Run(testTargetSampleObjName,
				() => {
					Antimaterializer.Antimaterialize();
					
					// EditorSampleMaterialWithMoreDepthプレファブの原点が0
					var prefab = Resources.Load("Views/" + testTargetSampleObjName + "/IMG") as GameObject;
					if (prefab != null) {
						var rectTrans = prefab.GetComponent<RectTransform>();
						Debug.Assert(rectTrans.anchoredPosition == Vector2.zero, "not zero, pos:" + rectTrans.anchoredPosition);
					}
				}
			);
		}

		public static void ExportMultipleBoxConstraints () {
			var testTargetSampleObjName = "MultipleBoxConstraints";
			Run(testTargetSampleObjName,
				() => {
					Antimaterializer.Antimaterialize();
					
					var jsonAsset = Resources.Load("Views/" + testTargetSampleObjName + "/DepthAssetList") as TextAsset;
					var jsonText = jsonAsset.text;
					// Debug.LogError("jsontext:" + jsonText);
					var list = JsonUtility.FromJson<UUebTags>(jsonText);
					var boxConstraints = list.layerInfos[0].boxes;
					Debug.Assert(boxConstraints.Length == 4, "not 4, boxConstraints.Length:" + boxConstraints.Length);

					Debug.Assert(boxConstraints[0].collisionGroupId == 0, "not contains");
					Debug.Assert(boxConstraints[1].collisionGroupId == 0, "not contains");
					Debug.Assert(boxConstraints[2].collisionGroupId == 1, "not contains");
					Debug.Assert(boxConstraints[3].collisionGroupId == 2, "not contains");
				}
			);
		}

	}
}
