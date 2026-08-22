#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

/// <summary>모든 손님 시트가 표정 이름으로 안정적으로 연결되는지 확인합니다.</summary>
public sealed class CustomerExpressionSpriteTests
{
    [TestCase(CustomerType.JeongHyun)]
    [TestCase(CustomerType.HaYoung)]
    [TestCase(CustomerType.MiJu)]
    [TestCase(CustomerType.Sunja)]
    [TestCase(CustomerType.Geonwoo)]
    [TestCase(CustomerType.Taesu)]
    [TestCase(CustomerType.Nari)]
    [TestCase(CustomerType.Junho)]
    public void CustomerExpressions_ResolveToTheirNamedSlices(CustomerType customerType)
    {
        CustomerData data = Resources.Load<CustomerData>($"Data/So/{customerType}");
        Assert.That(data, Is.Not.Null, $"{customerType} 손님 데이터를 찾지 못했습니다.");

        Assert.That(data.GetImage(CustomerExpression.Default)?.name, Does.EndWith("_Default"));
        Assert.That(data.GetImage(CustomerExpression.Joy)?.name, Does.EndWith("_Joy"));
        Assert.That(data.GetImage(CustomerExpression.Disappointed)?.name, Does.EndWith("_Disappointed"));
    }
}
#endif
