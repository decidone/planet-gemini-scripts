using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Shop : MonoBehaviour
{
    public delegate void OnTotalPriceChanged();
    public OnTotalPriceChanged onTotalPriceChangedCallback;

    [SerializeField] MerchandiseListSO merchandiseList;
    [SerializeField] GameObject merchListObj;
    [HideInInspector] public Merch[] merchList;
    [SerializeField] Finance finance;
    [SerializeField] Button btn;
    [SerializeField] Button scrapBtn;
    [SerializeField] bool isPurchase;
    [SerializeField] int sliderBasicValue;

    public int totalPrice;

    void Awake()
    {
        onTotalPriceChangedCallback += SumTotalPrice;

        if (merchListObj != null)
        {
            merchList = merchListObj.GetComponentsInChildren<Merch>();

            for (int i = 0; i < merchList.Length; i++)
            {
                Merch merch = merchList[i];
                merch.SetMerch(this, merchandiseList.MerchandiseSOList[i], isPurchase);
            }

            if (isPurchase)
                btn.onClick.AddListener(BuyMerch);
            else
                btn.onClick.AddListener(SellMerch);

            if (scrapBtn != null)
            {
                scrapBtn.onClick.AddListener(() => SellScrapBtnClicked());
            }
        }
    }

    void OnDestroy()
    {
        onTotalPriceChangedCallback -= SumTotalPrice;
    }

    void SellScrapBtnClicked()
    {
        GameManager.instance.SellScrapServerRpc();
        scrapBtn.interactable = false;
        SoundManager.instance.PlayUISFX("PaySound");
    }

    public void OpenUI()
    {
        this.gameObject.SetActive(true);
        btn.interactable = false;
        if (scrapBtn != null)
        {
            if (GameManager.instance.scrap.scrap >= 10)
            {
                scrapBtn.interactable = true;
            }
            else
            {
                scrapBtn.interactable = false;
            }
        }

        GameManager.instance.onUIChangedCallback?.Invoke(this.gameObject);
        if (!isPurchase)
        {
            SetUIAmount();
            GameManager.instance.inventory.onItemChangedCallback += SetUIAmount;
            GameManager.instance.inventory.invenAllSlotUpdate += SetUIAmount;
        }
    }

    public void CloseUI()
    {
        if (!isPurchase)
        {
            GameManager.instance.inventory.onItemChangedCallback -= SetUIAmount;
            GameManager.instance.inventory.invenAllSlotUpdate -= SetUIAmount;
        }

        foreach (Merch merch in merchList)
        {
            merch.ResetValue();
        }

        this.gameObject.SetActive(false);
        GameManager.instance.inventoryUiCanvas.GetComponent<ItemInfoWindow>().CloseWindow();
        GameManager.instance.onUIChangedCallback?.Invoke(this.gameObject);
    }

    void SetUIAmount()
    {
        SetUIAmount(0);
    }

    public void SetUIAmount(int slotindex)
    {
        foreach (Merch merch in merchList)
        {
            int itemAmount = GameManager.instance.inventory.GetItemAmount(merch.item);
            merch.SetOwnedItemAmount(itemAmount);
        }
    }

    public void SumTotalPrice()
    {
        totalPrice = 0;
        for (int i = 0; i < merchList.Length; i++)
        {
            Merch merch = merchList[i];
            totalPrice += merch.SumPrice();
        }

        if (isPurchase)
        {
            if (totalPrice <= GameManager.instance.finance.GetFinance())
            {
                finance.SetFinance(totalPrice);
                btn.interactable = true;
            }
            else
            {
                finance.SetFinance(totalPrice, false);
                btn.interactable = false;
            }

            if (totalPrice == 0)
            {
                btn.interactable = false;
            }
        }
        else
        {
            finance.SetFinance(totalPrice);
            if (totalPrice > 0)
            {
                btn.interactable = true;
            }
            else
            {
                btn.interactable = false;
            }
        }
    }

    public void BuyMerch()
    {
        if (GameManager.instance.inventory.MultipleSpaceCheck(merchList))
        {
            if (GameManager.instance.finance.finance >= totalPrice)
            {
                GameManager.instance.inventory.BuyMerch(merchList, totalPrice);
                ResetMerchList();
                SoundManager.instance.PlayUISFX("PaySound");
            }
            else
            {
                Debug.Log("Not enough money");
            }
        }
        else
        {
            Debug.Log("Not enough space");
            ShopPopup.instance.NotEnoughSpacePopup();
        }
    }

    public void SellMerch()
    {
        GameManager.instance.inventory.SellMerch(merchList);
        ResetMerchList();
        SoundManager.instance.PlayUISFX("PaySound");
    }

    void ResetMerchList()
    {
        foreach (Merch merch in merchList)
        {
            merch.ResetValue();
        }
    }
}
