using System;
using KKL.Ronda.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;

namespace KKL.Ronda.Core
{
    public class CardController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private Image _cardImage;
        private Transform _parent;
        private Table _table;
        private Card _card;
        private bool _isDragging;
        private bool _isPlayerCard;
        
        [SerializeField] private float invalidMoveShakeDuration = 0.5f;
        [SerializeField] private float invalidMoveShakeIntensity = 10f;
        
        private static GameManager GameManager => GameManager.Instance;
        private TurnManager TurnManager => GameManager.turnManager;
        
        // Feedback UI - Only for player cards
        private TMP_Text _feedbackText;
        private readonly float _feedbackDisplayTime = 2f;
        private float _feedbackTimer;

        private void Awake()
        {
            try
            {
                _cardImage = GetComponent<Image>();
                if (_cardImage == null)
                {
                    Debug.LogError("Card Image component not found!");
                    return;
                }

                _parent = transform.parent;
        
                // Determine if this is a player card - add null check
                _isPlayerCard = _parent != null && _parent.name == "PlayerHand";
        
                // Initialize feedback UI
                if (_isPlayerCard)
                {
                    InitializeFeedbackUI();
            
                    // Only subscribe to turn events if TurnManager exists
                    if (GameManager.Instance != null && GameManager.Instance.turnManager != null)
                    {
                        var turnManager = GameManager.Instance.turnManager;
                        turnManager.OnTurnChanged += HandleTurnChanged;
                        turnManager.OnTurnWarning += HandleTurnWarning;
                        turnManager.OnTurnTimeout += HandleTurnTimeout;
                    }
                    else
                    {
                        Debug.LogWarning("TurnManager not found during CardController initialization");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in CardController Awake: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (_isPlayerCard && TurnManager != null)
            {
                TurnManager.OnTurnChanged -= HandleTurnChanged;
                TurnManager.OnTurnWarning -= HandleTurnWarning;
                TurnManager.OnTurnTimeout -= HandleTurnTimeout;
            }
        }

        private void Update()
        {
            if (_isPlayerCard)
            {
                UpdateFeedbackUI();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (CardCannotBeDragged())
            {
                if (_isPlayerCard && !GameManager.IsPlayerTurn(GameManager.LocalPlayer.OwnerClientId))
                {
                    ShowInvalidMoveMessage();
                }
                return;
            }
            
            _isDragging = true;
            SetCardDragProperties();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _parent == null || (_parent.name == "Table" || _parent.name == "EnemyHand"))
                return;
                
            DragCardWithPointer(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isDragging)
                return;
                
            _isDragging = false;
            _cardImage.raycastTarget = true;
            AnalyzePointerUp(eventData);
        }

        private bool CardCannotBeDragged()
        {
            bool isInPlayerHand = _parent != null && _parent.name == "PlayerHand";
            bool isPlayerTurn = TurnManager.IsPlayerTurn(GameManager.LocalPlayer.OwnerClientId);
            
            return !isInPlayerHand || !isPlayerTurn;
        }

        private void SetCardDragProperties()
        {
            transform.SetParent(transform.root);
            _cardImage.raycastTarget = false;
        }

        private void DragCardWithPointer(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }

        private void AnalyzePointerUp(PointerEventData eventData)
        {
            if (IsPointerReleasedOnTable(eventData))
            {
                PlayCardOnTable(eventData.pointerEnter.transform);
            }
            else
            {
                ReturnCardToHand();
            }
        }

        private bool IsPointerReleasedOnTable(PointerEventData eventData)
        {
            return eventData.pointerEnter != null && eventData.pointerEnter.name == "Table";
        }

        private void PlayCardOnTable(Transform table)
        {
            var localPlayerId = GameManager.LocalPlayer.OwnerClientId;
        
            // Double-check turn validity
            if (!TurnManager.IsPlayerTurn(localPlayerId))
            {
                if (_isPlayerCard)
                {
                    ShowInvalidMoveMessage();
                }
                ReturnCardToHand();
                return;
            }

            SetCardParentAndPosition(table);
            _card = CardConverter.GetCardValueFromGameObject(gameObject);
            
            GameManager.OnCardPlayedServerRpc(CardConverter.GetCodedCard(_card), localPlayerId);
        }

        private void ReturnCardToHand()
        {
            SetCardParentAndPosition(_parent);
        }

        private void SetCardParentAndPosition(Transform parent)
        {
            transform.SetParent(parent);
            transform.localPosition = Vector3.zero;
            _parent = parent;
        }

        #region Visual Feedback
        
        private void InitializeFeedbackUI()
        {
            if (!_isPlayerCard) return; // Skip if not a player card
    
            try 
            {
                // Create feedback text if needed
                if (_feedbackText == null)
                {
                    var feedbackObj = new GameObject("CardFeedback");
                    feedbackObj.transform.SetParent(transform);
            
                    // Set the local position to zero
                    feedbackObj.transform.localPosition = Vector3.zero;
            
                    _feedbackText = feedbackObj.AddComponent<TextMeshProUGUI>(); // Note: Changed to TextMeshProUGUI
                    if (_feedbackText != null)
                    {
                        _feedbackText.fontSize = 14;
                        _feedbackText.alignment = TextAlignmentOptions.Center;
                        // Set additional properties
                        _feedbackText.color = Color.white;
                        _feedbackText.raycastTarget = false;
                        _feedbackText.gameObject.SetActive(false);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error initializing feedback UI: {e.Message}");
            }
        }

        private void UpdateFeedbackUI()
        {
            if (_feedbackText == null || !_feedbackText.gameObject.activeSelf)
                return;

            _feedbackTimer -= Time.deltaTime;
            if (_feedbackTimer <= 0)
            {
                _feedbackText.gameObject.SetActive(false);
            }
        }

        private void ShowInvalidMoveMessage()
        {
            if (_isPlayerCard && !TurnManager.IsPlayerTurn(GameManager.LocalPlayer.OwnerClientId))
            {
                ShowFeedback("Not your turn!");
                StartCoroutine(ShakeCard());
            }
        }

        private void ShowFeedback(string message)
        {
            if (_feedbackText != null)
            {
                _feedbackText.text = message;
                _feedbackText.gameObject.SetActive(true);
                _feedbackTimer = _feedbackDisplayTime;
            }
        }

        private System.Collections.IEnumerator ShakeCard()
        {
            Vector3 originalPosition = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < invalidMoveShakeDuration)
            {
                float x = originalPosition.x + Random.Range(-1f, 1f) * invalidMoveShakeIntensity;
                float y = originalPosition.y + Random.Range(-1f, 1f) * invalidMoveShakeIntensity;
                
                transform.localPosition = new Vector3(x, y, originalPosition.z);
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = originalPosition;
        }
        
        #endregion

        #region Turn Management Events
        
        private void HandleTurnChanged(ulong playerId)
        {
            if (_isPlayerCard && playerId == GameManager.LocalPlayer.OwnerClientId)
            {
                ShowFeedback("Your Turn!");
            }
        }

        private void HandleTurnWarning()
        {
            if (_isPlayerCard && TurnManager.IsPlayerTurn(GameManager.LocalPlayer.OwnerClientId))
            {
                ShowFeedback("Time running out!");
            }
        }

        private void HandleTurnTimeout()
        {
            if (_isPlayerCard && TurnManager.IsPlayerTurn(GameManager.LocalPlayer.OwnerClientId))
            {
                ShowFeedback("Turn timed out!");
            }
        }
        
        #endregion
    }
}