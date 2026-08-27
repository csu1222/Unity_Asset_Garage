#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace AssetGarage.CombatDemo.Editor
{
    public static class CombatDemoSceneBuilder
    {
        private const string Root = "Assets/06.Data/SO/CombatDemo/Assets";
        [MenuItem("Tools/CombatDemo/Build CombatDemo v0.1")]
        public static void Build()
        {
            EnsureFolder("Assets/01.Scenes"); EnsureFolder("Assets/02.Scripts/CambatDemo"); EnsureFolder("Assets/04.Prefab/CombatDemo/Combatants"); EnsureFolder("Assets/04.Prefab/CombatDemo/UI"); EnsureFolder("Assets/04.Prefab/CombatDemo/Debug"); EnsureFolder(Root);
            var manual = Asset<ManualCombatBalanceConfig>($"{Root}/ManualCombatBalance.asset"); var auto = Asset<AutoCombatBalanceConfig>($"{Root}/AutoCombatBalance.asset"); var resolution = Asset<ResolutionBalanceConfig>($"{Root}/ResolutionBalance.asset"); var timing = Asset<TimingPresentationConfig>($"{Root}/TimingPresentation.asset");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); scene.name = "CombatDemo";
            var systems = new GameObject("Systems"); var bootstrapGo = new GameObject("CombatPrototypeBootstrap"); bootstrapGo.transform.SetParent(systems.transform); var bootstrap = bootstrapGo.AddComponent<CombatPrototypeBootstrap>(); SerializedObject so = new SerializedObject(bootstrap); so.FindProperty("manualBalance").objectReferenceValue=manual;so.FindProperty("autoBalance").objectReferenceValue=auto;so.FindProperty("resolutionBalance").objectReferenceValue=resolution;so.FindProperty("presentation").objectReferenceValue=timing;so.ApplyModifiedPropertiesWithoutUndo();
            var combatants=new GameObject("Combatants"); CreateCombatant("Player",new Vector3(-2.5f,1,0),Color.cyan,combatants.transform);CreateCombatant("Enemy",new Vector3(2.5f,1,0),Color.red,combatants.transform);
            var environment=new GameObject("Environment");var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Ground";ground.transform.SetParent(environment.transform);var lightGo=new GameObject("Directional Light",typeof(Light));lightGo.transform.SetParent(environment.transform);lightGo.transform.rotation=Quaternion.Euler(50,-30,0);lightGo.GetComponent<Light>().type=LightType.Directional;
            var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));cameraGo.tag="MainCamera";cameraGo.transform.position=new Vector3(0,4,-10);cameraGo.transform.rotation=Quaternion.Euler(12,0,0);
            new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene,"Assets/01.Scenes/CombatDemo.unity");
            SavePrefab(CreatePrefabObject("PlayerCombatant",PrimitiveType.Capsule,Color.cyan),"Assets/04.Prefab/CombatDemo/Combatants/PlayerCombatant.prefab"); SavePrefab(CreatePrefabObject("EnemyCombatant",PrimitiveType.Capsule,Color.red),"Assets/04.Prefab/CombatDemo/Combatants/EnemyCombatant.prefab");
            var hud=new GameObject("CombatHUD",typeof(RectTransform),typeof(Canvas));SavePrefab(hud,"Assets/04.Prefab/CombatDemo/UI/CombatHUD.prefab");var debug=new GameObject("CombatDebugHUD",typeof(RectTransform));SavePrefab(debug,"Assets/04.Prefab/CombatDemo/Debug/CombatDebugHUD.prefab");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("CombatDemo v0.1 scene, prefabs, and configs built.");
        }
        private static T Asset<T>(string path) where T:ScriptableObject {T value=AssetDatabase.LoadAssetAtPath<T>(path);if(value)return value;value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);return value;}
        private static void EnsureFolder(string path){string current="";foreach(string part in path.Split('/')){string next=string.IsNullOrEmpty(current)?part:$"{current}/{part}";if(!AssetDatabase.IsValidFolder(next)&&!string.IsNullOrEmpty(current))AssetDatabase.CreateFolder(current,part);current=next;}}
        private static GameObject CreatePrefabObject(string name,PrimitiveType type,Color color){var go=GameObject.CreatePrimitive(type);go.name=name;go.AddComponent<CombatantGizmoDebug>();var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=color};go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        private static void CreateCombatant(string name,Vector3 position,Color color,Transform parent){var go=CreatePrefabObject(name,PrimitiveType.Capsule,color);go.transform.SetParent(parent);go.transform.position=position;}
        private static void SavePrefab(GameObject go,string path){PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);}
    }
}
#endif
