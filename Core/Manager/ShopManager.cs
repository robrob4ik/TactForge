using System.Collections.Generic;
using OneBitRob;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(-100)]
public class ShopManager : MonoBehaviour
{
    [Title("Shop Configuration")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    public List<UnitDefinition> allAvailableUnits;

    [MinValue(1)]
    public int shopSlotCount = 5;

    [Title("Runtime")]
    [ReadOnly]
    public List<UnitDefinition> currentShop = new();

    public UnityEvent OnShopRefreshed;

    public static ShopManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RefreshShop();
    }

    [Button(ButtonSizes.Large)]
    public void RefreshShop()
    {
        currentShop.Clear();

        for (int i = 0; i < shopSlotCount; i++)
        {
            if (allAvailableUnits.Count == 0) break;
            var unit = allAvailableUnits[Random.Range(0, allAvailableUnits.Count)];
            currentShop.Add(unit);
        }

        OnShopRefreshed?.Invoke();
    }

    public void BuyUnit(UnitDefinition unit)
    {
        Debug.Log($"Bought unit: {unit.name} for {unit.price}g");
        // TODO: Add unit to bench or spawn logic
    }
}