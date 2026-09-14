using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEditor.TestTools;
using Object = UnityEngine.Object;

namespace RiceXR.Tests.EditMode
{
    public class DiagnosisFlowTests
    {
        private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static Component[] Options(object view) => ((Component)Field(view, "optionsRoot"))
            .GetComponentsInChildren(Runtime("DiagnosisOptionView"));
        private static void Click(object button) => ((UnityEvent)button.GetType().GetProperty("onClick").GetValue(button)).Invoke();
        private static void ClickOption(object view, int index) => Click(Field(Options(view)[index], "button"));

        [UnityTest]
        public IEnumerator DiagnosticoReal_AnclaPasosFallosAciertosYSanas()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunDiagnosisFlow();
            yield return new ExitPlayMode();
        }

        private static IEnumerator RunDiagnosisFlow()
        {
            var cameraObject = new GameObject("Camara QA", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1.7f, 0f);
            var input = new GameObject("Entrada Meta QA",
                Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", true),
                Type.GetType("Oculus.Interaction.PointableCanvasModule, Oculus.Interaction", true),
                typeof(AudioListener));
            yield return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RiceXR/Prefabs/UI/DiseaseSeveritySelectorUI.prefab");
            var root = Object.Instantiate(prefab);
            var system = root.GetComponent(Runtime("DiseaseSelectionSystem"));
            var view = Field(system, "view");
            var listener = root.GetComponent(Runtime("GrabbableLeafListener"))
                ?? root.AddComponent(Runtime("GrabbableLeafListener"));
            var catalog = Field(system, "catalog");
            var diseases = (IList)Field(catalog, "diseases");
            var diseasedObject = new GameObject("Hoja QA", Runtime("Leaf"));
            var healthyObject = new GameObject("Hoja sana QA", Runtime("Leaf"));
            var leaf = diseasedObject.GetComponent(Runtime("Leaf"));
            var healthy = healthyObject.GetComponent(Runtime("Leaf"));
            var spot = Activator.CreateInstance(Runtime("DiseaseSpot"));
            Set(spot, "disease", diseases[1]);
            Set(spot, "severity", 3);
            ((IList)Field(leaf, "diseaseSpots")).Add(spot);
            int attempts = 0, correct = 0;
            Action<string, int, bool> handler = (d, s, ok) => { attempts++; if (ok) correct++; };
            var diagnosisEvent = Runtime("GameEventBus").GetEvent("OnDiagnosisAttemptEvaluated");
            Assert.That(diagnosisEvent, Is.Not.Null, "El bus expone el evento de diagnóstico.");
            diagnosisEvent.AddEventHandler(null, handler);
            try
            {
                yield return null;
                object right = Enum.Parse(Runtime("GrabbableLeafListener+SelectionHand"), "Right");
                object left = Enum.Parse(Runtime("GrabbableLeafListener+SelectionHand"), "Left");
                Call(listener, "SetActiveSelection", leaf, right, diseasedObject.transform);
                yield return null;
                Assert.That(Options(view).Length, Is.EqualTo(2));
                Vector3 anchored = root.transform.position;
                Assert.That(anchored.x, Is.LessThan(cameraObject.transform.position.x));
                Assert.That(Vector3.Distance(anchored, cameraObject.transform.position), Is.InRange(0.65f, 0.7f));
                diseasedObject.transform.position += Vector3.one;
                yield return null;
                Assert.That(root.transform.position, Is.EqualTo(anchored), "El panel no sigue el temblor de la hoja.");
                ClickOption(view, 1);
                Assert.That(Options(view).Length, Is.EqualTo(5));
                var confirm = Field(view, "confirmButton");
                Assert.That((bool)confirm.GetType().GetProperty("interactable").GetValue(confirm), Is.False);
                Call(listener, "SetActiveSelection", leaf, left, diseasedObject.transform);
                yield return null;
                Assert.That(root.transform.position.x, Is.GreaterThan(cameraObject.transform.position.x));
                Assert.That(Options(view).Length, Is.EqualTo(5), "Cambiar de mano conserva el paso actual.");
                ClickOption(view, 2);
                Click(confirm);
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(correct, Is.Zero);
                ClickOption(view, 1);
                Click(confirm);
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(correct, Is.EqualTo(1));
                Call(system, "OnSubmit");
                Assert.That(attempts, Is.EqualTo(2), "Una hoja resuelta no genera otro intento.");

                Call(listener, "SetActiveSelection", healthy, right, healthyObject.transform);
                yield return null;
                Assert.That(Options(view).Length, Is.EqualTo(2), "Cambiar de hoja vuelve al primer paso.");
                ClickOption(view, 0);
                Assert.That(Options(view).Length, Is.EqualTo(9));
                ClickOption(view, 0);
                Call(system, "SetPanelAvailability", false);
                Call(system, "OnSubmit");
                Assert.That(attempts, Is.EqualTo(2), "Opciones/tutorial bloquean el diagnóstico.");
                Call(system, "SetPanelAvailability", true);
                yield return null;
                Click(confirm);
                Assert.That(attempts, Is.EqualTo(3));
                Assert.That(correct, Is.EqualTo(1), "La hoja sana es un distractor.");
                Call(listener, "ClearActiveSelection", healthy);
                yield return null;
                Assert.That(((GameObject)Field(system, "diseaseSelectionPanel")).activeSelf, Is.False);
            }
            finally
            {
                diagnosisEvent.RemoveEventHandler(null, handler);
                Object.Destroy(root);
                Object.Destroy(diseasedObject);
                Object.Destroy(healthyObject);
                Object.Destroy(input);
                Object.Destroy(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator AnclajeCorporal_SigueConZonaMuertaYGiroEnY()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            var cameraObject = new GameObject("Camara QA", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1.7f, 0f);
            var panel = new GameObject("Panel QA");
            var follower = panel.AddComponent(Runtime("BodyLeashedPanel"));
            object right = Enum.Parse(Runtime("GrabbableLeafListener+SelectionHand"), "Right");

            Call(follower, "Attach", right);
            Vector3 initial = panel.transform.position;
            Assert.That(initial, Is.EqualTo(new Vector3(-0.3f, 1.52f, 0.6f)).Using(Vector3ComparerWithEqualsOperator.Instance));

            cameraObject.transform.position += Vector3.right * 0.05f;
            yield return null;
            yield return null;
            Assert.That(panel.transform.position, Is.EqualTo(initial).Using(Vector3ComparerWithEqualsOperator.Instance));

            cameraObject.transform.position += Vector3.right * 0.2f;
            for (int i = 0; i < 30; i++)
                yield return null;
            Assert.That(panel.transform.position.x, Is.GreaterThan(initial.x + 0.05f));

            Vector3 beforeTurn = panel.transform.position;
            cameraObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            for (int i = 0; i < 40; i++)
                yield return null;
            Assert.That(Vector3.Distance(panel.transform.position, beforeTurn), Is.GreaterThan(0.05f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(panel.transform.eulerAngles.x, 0f)), Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(panel.transform.eulerAngles.z, 0f)), Is.LessThan(0.01f));

            cameraObject.transform.position += Vector3.right * 2f;
            yield return null;
            Vector3 expected = cameraObject.transform.position
                + Quaternion.Euler(0f, 45f, 0f) * new Vector3(-0.3f, -0.18f, 0.6f);
            Assert.That(panel.transform.position, Is.EqualTo(expected).Using(Vector3ComparerWithEqualsOperator.Instance));

            Call(follower, "Detach");
            Vector3 detached = panel.transform.position;
            cameraObject.transform.position += Vector3.forward;
            yield return null;
            Assert.That(panel.transform.position, Is.EqualTo(detached).Using(Vector3ComparerWithEqualsOperator.Instance));

            Object.Destroy(panel);
            Object.Destroy(cameraObject);
            yield return new ExitPlayMode();
        }
    }
}
