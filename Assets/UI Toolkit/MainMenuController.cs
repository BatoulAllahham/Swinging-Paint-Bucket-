using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement rootVisual;

    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        rootVisual = uiDocument.rootVisualElement;

        rootVisual.Q<Button>("btn-main-sim")?.RegisterCallback<ClickEvent>(evt => LoadSimulation("Main Scene"));
        rootVisual.Q<Button>("btn-fluid-only")?.RegisterCallback<ClickEvent>(evt => LoadSimulation("Fluid Particles"));
        rootVisual.Q<Button>("btn-color-mix")?.RegisterCallback<ClickEvent>(evt => LoadSimulation("MixingScene"));
    }

    private void LoadSimulation(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}