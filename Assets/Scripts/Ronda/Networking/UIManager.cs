using KKL.Ronda.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KKL.Ronda.Networking
{
    public class UIManager : MonoBehaviour
    {
        #region Singleton
        public static UIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region UI References
        [Header("Card References")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform playerHand;
        [SerializeField] private Transform enemyHand;
        [SerializeField] private Transform tableTransform;

        [Header("Text Elements")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text turnIndicatorText;
        [SerializeField] private TMP_Text announcementText;

        [Header("Settings")]
        [SerializeField] private float specialAnnouncementDelay = 3f;
        #endregion

        #region Public Methods
        public void UpdateScore(uint score)
        {
            scoreText.text = score.ToString();
        }

        private void ShowAnnouncement(string message, float duration)
        {
            StartCoroutine(ShowAnnouncementCoroutine(message, duration));
        }

        public void SpawnCardOnTable(Card card)
        {
            GameObject cardObject = Instantiate(cardPrefab, tableTransform);
            Image cardImage = cardObject.GetComponent<Image>();

            string path = $"Sprites/Cards/{(int)card.Suit}_{(int)card.Value}";
            Sprite sprite = Resources.Load<Sprite>(path);

            if (!sprite)
            {
                Debug.LogError($"Sprite not found at path: {path}");
                return;
            }

            cardImage.sprite = sprite;
            cardObject.name = $"{(int)card.Suit}_{(int)card.Value}";
        }

        public void UpdatePlayerHand(Card[] cards)
        {
            ClearTransform(playerHand);
            SpriteConverter.UpdatePlayerCardImages(cards, cardPrefab, playerHand);
        }

        public void UpdateEnemyHand(Card[] cards)
        {
            ClearTransform(enemyHand);
            SpriteConverter.UpdateEnemyCardImages(cards, cardPrefab, enemyHand);
        }

        public void RemoveCardFromEnemyHand()
        {
            if (enemyHand.childCount <= 0) return;
            GameObject lastCard = enemyHand.GetChild(enemyHand.childCount - 1).gameObject;
            Destroy(lastCard);
        }

        public void ClearTable()
        {
            ClearTransform(tableTransform);
        }

        private GameObject FindCardObjectOnTable(Card card)
        {
            foreach (Transform child in tableTransform)
            {
                if (child.name == $"{(int)card.Suit}_{(int)card.Value}")
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        public void SetCardsInactive(Card[] cards)
        {
            foreach (var card in cards)
            {
                GameObject cardObject = FindCardObjectOnTable(card);
                if (cardObject != null)
                {
                    cardObject.SetActive(false);
                }
            }
        }

        public void ShowSpecialCombination(bool isLocalPlayer, string combinationType, int value)
        {
            string playerName = isLocalPlayer ? "You" : "Opponent";
            string announcement = $"{playerName} announced {combinationType} of {value}s!";
            ShowAnnouncement(announcement, specialAnnouncementDelay);
        }

        public void ShowRoundEnd(bool isLocalPlayer, int extraPoints)
        {
            string playerName = isLocalPlayer ? "You" : "Opponent";
            string announcement = $"Round Over! {playerName} captured most cards (+{extraPoints} points)";
            ShowAnnouncement(announcement, specialAnnouncementDelay);
        }

        public void ShowGameOver(bool isLocalPlayer, uint finalScore)
        {
            string playerName = isLocalPlayer ? "You" : "Opponent";
            string announcement = $"Game Over! {playerName} wins with {finalScore} points!";
            ShowAnnouncement(announcement, specialAnnouncementDelay);
        }
        #endregion

        #region Private Methods
        private void ClearTransform(Transform t)
        {
            foreach (Transform child in t)
            {
                Destroy(child.gameObject);
            }
        }

        private System.Collections.IEnumerator ShowAnnouncementCoroutine(string message, float duration)
        {
            string originalText = announcementText.text;
            announcementText.text = message;

            yield return new WaitForSeconds(duration);

            announcementText.text = originalText;
        }
        #endregion
    }
}