using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
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
    private Coroutine imageLoadRoutine;
    private Texture2D remoteTexture;
    private Sprite remoteSprite;

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
        string storeMode,
        bool isEquipped,
        Action<InAppMarketProduct> onPurchase,
        Action<InAppMarketProduct, bool> onEquipmentChanged)
    {
        product = value;
        SetProductImage(product);
        productNameText.text = string.IsNullOrWhiteSpace(product.name) ? "이름 없는 상품" : product.name;
        descriptionText.text = string.IsNullOrWhiteSpace(product.description)
            ? "상품 설명이 없습니다."
            : product.description;
        bool usesExternalPayment = string.Equals(storeMode, "hive-web-shop", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(storeMode, "nicepay-test", StringComparison.OrdinalIgnoreCase);
        priceText.text = usesExternalPayment
            ? (string.IsNullOrWhiteSpace(product.priceLabel) ? "가격 확인 필요" : product.priceLabel)
            : $"{product.testPointPrice:N0} P";
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
            idleButtonText = string.Equals(storeMode, "nicepay-test", StringComparison.OrdinalIgnoreCase)
                ? "NICEPAY 테스트 결제"
                : (usesExternalPayment ? "웹 상점 열기" : "포인트 결제");

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

    private void SetProductImage(InAppMarketProduct value)
    {
        ClearRemoteImage();
        Sprite fallback = Resources.Load<Sprite>($"Sprites/StoreProducts/{value.id}") ??
            Resources.Load<Sprite>("Sprites/UI/coin");
        productImage.sprite = fallback;
        productImage.preserveAspect = true;

        if (!string.IsNullOrWhiteSpace(value.imageUrl))
            imageLoadRoutine = StartCoroutine(LoadRemoteImage(value.imageUrl));
    }

    private IEnumerator LoadRemoteImage(string imageUrl)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();
        imageLoadRoutine = null;

        if (request.result != UnityWebRequest.Result.Success)
            yield break;

        remoteTexture = DownloadHandlerTexture.GetContent(request);
        if (remoteTexture == null)
            yield break;

        remoteTexture.wrapMode = TextureWrapMode.Clamp;
        remoteSprite = Sprite.Create(
            remoteTexture,
            new Rect(0f, 0f, remoteTexture.width, remoteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        productImage.sprite = remoteSprite;
    }

    private void OnDestroy()
    {
        ClearRemoteImage();
    }

    private void ClearRemoteImage()
    {
        if (imageLoadRoutine != null)
        {
            StopCoroutine(imageLoadRoutine);
            imageLoadRoutine = null;
        }
        if (remoteSprite != null)
        {
            Destroy(remoteSprite);
            remoteSprite = null;
        }
        if (remoteTexture != null)
        {
            Destroy(remoteTexture);
            remoteTexture = null;
        }
    }
}
