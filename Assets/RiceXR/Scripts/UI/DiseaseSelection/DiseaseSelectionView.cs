using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Presentación uGUI. Los callbacks transmiten valores reales, nunca índices.</summary>
public class DiseaseSelectionView : MonoBehaviour
{
    public TMP_Text title, subtitle, summary, pageLabel;
    public RectTransform optionsRoot;
    public GridLayoutGroup optionsLayout;
    public DiagnosisOptionView optionPrefab;
    public Button backButton, confirmButton, previousButton, nextButton;
    public event Action<DiseaseDefinition> DiseaseChosen;
    public event Action<int> SeverityChosen;
    public event Action BackRequested, ConfirmRequested;
    private DiseaseCatalog _catalog;
    private DiseaseDefinition _disease;
    private int _severity = -1;
    private int _page;
    private bool _interactionEnabled = true;
    private readonly List<DiagnosisOptionView> _options = new List<DiagnosisOptionView>();

    private void OnEnable()
    {
        backButton.onClick.AddListener(Back);
        confirmButton.onClick.AddListener(Confirm);
        previousButton.onClick.AddListener(Previous);
        nextButton.onClick.AddListener(Next);
    }
    private void OnDisable()
    {
        backButton.onClick.RemoveListener(Back);
        confirmButton.onClick.RemoveListener(Confirm);
        previousButton.onClick.RemoveListener(Previous);
        nextButton.onClick.RemoveListener(Next);
    }
    private void Back() => BackRequested?.Invoke();
    private void Confirm()
    {
        if (_interactionEnabled) ConfirmRequested?.Invoke();
    }
    private void Previous()
    {
        if (!_interactionEnabled) return;
        _page--;
        Render();
    }
    private void Next()
    {
        if (!_interactionEnabled) return;
        _page++;
        Render();
    }

    public void ShowDiseases(DiseaseCatalog catalog)
    {
        _catalog = catalog;
        _disease = null;
        _severity = -1;
        _page = 0;
        Render();
    }
    public void ShowSeverities(DiseaseDefinition disease, int selected = -1)
    {
        _disease = disease;
        _severity = selected;
        Render();
    }
    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;
        ApplyInteractionState();
    }

    private DiagnosisOptionView Option(int index)
    {
        while (_options.Count <= index) _options.Add(Instantiate(optionPrefab, optionsRoot));
        _options[index].gameObject.SetActive(true);
        return _options[index];
    }
    private void Render()
    {
        foreach (var option in _options) option.gameObject.SetActive(false);
        bool choosingDisease = _disease == null;
        backButton.gameObject.SetActive(!choosingDisease);
        confirmButton.gameObject.SetActive(!choosingDisease);
        confirmButton.interactable = !choosingDisease && _disease.AllowsSeverity(_severity);
        previousButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
        pageLabel.text = "";
        if (choosingDisease)
        {
            title.text = "Elige la enfermedad";
            subtitle.text = "Observa la hoja que tienes en la mano";
            summary.text = "Después elegirás la severidad.";
            optionsLayout.constraintCount = 1;
            optionsLayout.cellSize = new Vector2(512, 62);
            if (_catalog == null) return;
            int pages = Mathf.Max(1, (_catalog.diseases.Count + 3) / 4);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            for (int i = 0; i < 4 && _page * 4 + i < _catalog.diseases.Count; i++)
            {
                var disease = _catalog.diseases[_page * 4 + i];
                Option(i).Bind($"{disease.DisplayName}\n<size=18><i>{disease.data.scientificName}</i></size>",
                    false, () => DiseaseChosen?.Invoke(disease));
            }
            previousButton.gameObject.SetActive(pages > 1);
            nextButton.gameObject.SetActive(pages > 1);
            pageLabel.text = pages > 1 ? $"{_page + 1} / {pages}" : "";
        }
        else
        {
            title.text = "Elige la severidad";
            subtitle.text = $"{_disease.DisplayName} · {_disease.data.scientificName}";
            optionsLayout.constraintCount = 3;
            optionsLayout.cellSize = new Vector2(162, 76);
            var severities = _disease.AvailableSeverities.ToList();
            for (int i = 0; i < severities.Count; i++)
            {
                var severity = severities[i];
                Option(i).Bind(severity.DisplayLabel, _severity == severity.value,
                    () => SeverityChosen?.Invoke(severity.value));
            }
            summary.text = _severity < 0 ? "Selecciona un valor para confirmar."
                : $"{_disease.DisplayName} · Severidad {_severity}";
        }

        ApplyInteractionState();
    }

    private void ApplyInteractionState()
    {
        bool choosingDisease = _disease == null;
        backButton.interactable = _interactionEnabled;
        confirmButton.interactable = _interactionEnabled
                                     && !choosingDisease
                                     && _disease.AllowsSeverity(_severity);
        previousButton.interactable = _interactionEnabled && _page > 0;

        int pageCount = _catalog == null ? 1 : Mathf.Max(1, (_catalog.diseases.Count + 3) / 4);
        nextButton.interactable = _interactionEnabled && _page < pageCount - 1;

        foreach (DiagnosisOptionView option in _options)
            option.button.interactable = _interactionEnabled;
    }
}
