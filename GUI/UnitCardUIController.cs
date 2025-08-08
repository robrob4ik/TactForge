using OneBitRob;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitCardUIController : MonoBehaviour
{
    [Title("UI References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Transform modelParent;

    [Title("Icon Display")]
    [SerializeField] private Transform traitIconContainer;
    [SerializeField] private Transform classIconContainer;
    [SerializeField] private GameObject iconPrefab;

    [Title("Icon Libraries")]
    [SerializeField] private IconLibrary library;

    private UnitDefinition unitData;
    private GameObject spawnedModel;

    public void Setup(UnitDefinition unit)
    {
        unitData = unit;

        nameText.text = unit.unitName;
        priceText.text = $"{unit.price}";

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);

        DisplayIcons();
        SetupModelPreview(unit.unitModel);
    }

    private void DisplayIcons()
    {
        ClearContainer(traitIconContainer);
        ClearContainer(classIconContainer);

        foreach (var trait in unitData.traits)
        {
            var icon = library?.GetTraitIcon(trait);
            if (icon != null)
                AddIconToContainer(traitIconContainer, icon);
        }

        foreach (var cls in unitData.classes)
        {
            var icon = library?.GetClassIcon(cls);
            if (icon != null)
                AddIconToContainer(classIconContainer, icon);
        }
    }

    private void AddIconToContainer(Transform container, Sprite sprite)
    {
        var iconGO = Instantiate(iconPrefab, container);
        var img = iconGO.GetComponent<Image>();
        if (img != null) img.sprite = sprite;
        
        var rectTransform = iconGO.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.sizeDelta = new Vector2(90, 90); 
        }
    }

    private void ClearContainer(Transform container)
    {
        foreach (Transform child in container)
            Destroy(child.gameObject);
    }

    private void SetupModelPreview(GameObject prefab)
    {
        if (spawnedModel != null)
            Destroy(spawnedModel);

        if (prefab != null && modelParent != null)
        {
            spawnedModel = Instantiate(prefab, modelParent);
            ResetModelTransform(spawnedModel.transform);
        }
    }

    private void ResetModelTransform(Transform model)
    {
        model.localPosition = new Vector3(0f,100f,0f);
        model.localRotation = Quaternion.Euler(0, 180, 0);
        model.localScale = Vector3.one * 100f;
    }

    private void OnBuyClicked()
    {
        ShopManager.Instance.BuyUnit(unitData);
    }

    private void OnDestroy()
    {
        if (spawnedModel != null)
            Destroy(spawnedModel);
    }
}