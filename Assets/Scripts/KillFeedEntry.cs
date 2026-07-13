using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class KillFeedEntry : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI killText;
    [SerializeField] private RectTransform textRect;

    [Header("Animation")]
    [SerializeField] private float slideInDuration = 0.2f;

    [SerializeField] private float holdDuration = 2.5f;
    [SerializeField] private float slideOutDuration = 0.15f;
    [SerializeField] private float slideDistance = 200f;

    public void Show(string enemyName)
    {
        killText.text = $"Killed {enemyName}";
        textRect.anchoredPosition = new Vector2(-slideDistance, 0f);

        // Chain the whole sequence
        Sequence seq = DOTween.Sequence();

        seq.Append(textRect.DOAnchorPosX(0f, slideInDuration)
            .SetEase(Ease.OutBack));

        seq.AppendInterval(holdDuration);

        seq.Append(textRect.DOAnchorPosX(slideDistance, slideOutDuration)
            .SetEase(Ease.InBack));

        seq.OnComplete(() => Destroy(gameObject));
    }

    //public void Show(string enemyName)
    //{
    //    killText.text = $"Killed {enemyName}";
    //    textRect.anchoredPosition = Vector2.zero; // no animation, just center it
    //}
}