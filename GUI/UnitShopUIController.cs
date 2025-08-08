using UnityEngine;

public class UnitShopUIController : MonoBehaviour
{
    [SerializeField] private GameObject unitCardUIPrefab;
    [SerializeField] private Transform cardContainer;

    private void OnEnable()
    {
        ShopManager.Instance.OnShopRefreshed.AddListener(UpdateShopUI);
        UpdateShopUI();
    }

    private void OnDisable()
    {
        ShopManager.Instance.OnShopRefreshed.RemoveListener(UpdateShopUI);
    }

    private void UpdateShopUI()
    {
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        foreach (var unit in ShopManager.Instance.currentShop)
        {
            var cardGO = Instantiate(unitCardUIPrefab, cardContainer);
            var cardUI = cardGO.GetComponent<UnitCardUIController>();
            cardUI.Setup(unit);
        }
    }
}