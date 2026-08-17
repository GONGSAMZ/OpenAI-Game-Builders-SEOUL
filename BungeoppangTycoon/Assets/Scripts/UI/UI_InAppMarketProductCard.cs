using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UI_InAppMarketProductCard : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI productNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI ownedText;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private Button purchaseButton;

    private InAppMarketProduct product;
    private bool baseInteractable;
    private string idleButtonText;

    public Button PurchaseButton => purchaseButton;

    public void SetReferences(
        TextMeshProUGUI productName,
        TextMeshProUGUI description,
        TextMeshProUGUI price,
        TextMeshProUGUI owned,
        TextMeshProUGUI purchaseLabel,
        Button purchase)
    {
        productNameText = productName;
        descriptionText = description;
        priceText = price;
        ownedText = owned;
        buttonText = purchaseLabel;
        purchaseButton = purchase;
    }

    public void SetData(
        InAppMarketProduct value,
        int ownedQuantity,
        bool isLoggedIn,
        bool opensWebShop,
        Action<InAppMarketProduct> onPurchase)
    {
        product = value;
        productNameText.text = string.IsNullOrWhiteSpace(product.name) ? "이름 없는 상품" : product.name;
        descriptionText.text = string.IsNullOrWhiteSpace(product.description)
            ? "상품 설명이 없습니다."
            : product.description;
        priceText.text = string.IsNullOrWhiteSpace(product.priceLabel) ? "가격 확인 필요" : product.priceLabel;
        RefreshOwned(ownedQuantity);

        bool alreadyOwnsPermanentItem = product.IsPermanent && ownedQuantity > 0;
        purchaseButton.onClick.RemoveAllListeners();
        purchaseButton.interactable = !alreadyOwnsPermanentItem;

        if (alreadyOwnsPermanentItem)
        {
            idleButtonText = "보유 중";
            baseInteractable = false;
            buttonText.text = idleButtonText;
            return;
        }

        if (!isLoggedIn)
            idleButtonText = "로그인 후 구매";
        else
            idleButtonText = opensWebShop ? "웹 상점 열기" : "데모 구매";

        baseInteractable = true;
        buttonText.text = idleButtonText;
        purchaseButton.onClick.AddListener(() => onPurchase?.Invoke(product));
    }

    public void RefreshOwned(int quantity)
    {
        ownedText.text = quantity > 0 ? $"보유 {quantity:N0}개" : "미보유";
    }

    public void SetBusy(bool isBusy)
    {
        if (purchaseButton == null)
            return;

        purchaseButton.interactable = !isBusy && baseInteractable;
        buttonText.text = isBusy ? "처리 중…" : idleButtonText;
    }
}
