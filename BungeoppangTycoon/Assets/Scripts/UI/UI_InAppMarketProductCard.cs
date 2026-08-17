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
    [SerializeField] private Image productImage;
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
        Image icon,
        Button purchase)
    {
        productNameText = productName;
        descriptionText = description;
        priceText = price;
        ownedText = owned;
        buttonText = purchaseLabel;
        productImage = icon;
        purchaseButton = purchase;
    }

    public void SetData(
        InAppMarketProduct value,
        int ownedQuantity,
        bool isLoggedIn,
        bool opensWebShop,
        bool isEquipped,
        Action<InAppMarketProduct> onPurchase,
        Action<InAppMarketProduct, bool> onEquipmentChanged)
    {
        product = value;
        Sprite icon = Resources.Load<Sprite>($"Sprites/StoreProducts/{product.id}") ??
            Resources.Load<Sprite>("Sprites/UI/coin");
        productImage.sprite = icon;
        productImage.preserveAspect = true;
        productNameText.text = string.IsNullOrWhiteSpace(product.name) ? "이름 없는 상품" : product.name;
        descriptionText.text = string.IsNullOrWhiteSpace(product.description)
            ? "상품 설명이 없습니다."
            : product.description;
        priceText.text = string.IsNullOrWhiteSpace(product.priceLabel) ? "가격 확인 필요" : product.priceLabel;
        RefreshOwned(ownedQuantity);

        bool alreadyOwnsPermanentItem = product.IsPermanent && ownedQuantity > 0;
        purchaseButton.onClick.RemoveAllListeners();

        if (alreadyOwnsPermanentItem)
        {
            bool isGoldenPan = product.grant.itemId == "golden-pan";
            idleButtonText = isGoldenPan ? (isEquipped ? "장착 해제" : "장착") : "보유 중";
            baseInteractable = isGoldenPan && isLoggedIn;
            purchaseButton.interactable = baseInteractable;
            buttonText.text = idleButtonText;
            if (baseInteractable)
                purchaseButton.onClick.AddListener(() => onEquipmentChanged?.Invoke(product, !isEquipped));
            return;
        }

        if (!isLoggedIn)
            idleButtonText = "로그인 후 구매";
        else
            idleButtonText = opensWebShop ? "웹 상점 열기" : "데모 구매";

        baseInteractable = true;
        purchaseButton.interactable = true;
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
