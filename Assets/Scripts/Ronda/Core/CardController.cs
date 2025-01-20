using System;
using DG.Tweening;
using KKL.Ronda.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace KKL.Ronda.Core
{
    public class CardController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Image _cardImage;
        private Transform _parent;
        private Table _table;
        private Card _card;
        private bool _isDragging;
        private bool _isPlayerCard;
        private Outline _outline;
        
        [SerializeField] private float invalidMoveShakeDuration = 0.5f;
        [SerializeField] private float invalidMoveShakeIntensity = 10f;
        [SerializeField] private Color hoverOutlineColor = new(1f, 0.92f, 0.016f, 1f); // Golden yellow
        [SerializeField] private Color dragColor = new(1f, 1f, 1f, 0.8f); // Slightly transparent white
        
        // Animation parameters
        [SerializeField] private float hoverScaleDuration = 0.2f;
        [SerializeField] private float hoverScaleMultiplier = 1.1f;
        [SerializeField] private Ease hoverScaleEase = Ease.OutBack;
        
        private Vector3 _originalScale;
        private Color _originalCardColor;
        private readonly Vector2 _normalOutlineEffect = new(1, -1);
        private readonly Vector2 _hoverOutlineEffect = new(2, -2);
        
        private static GameManager GameManager => GameManager.Instance;
        private TurnManager TurnManager => GameManager.turnManager;
        private float _feedbackTimer;

        private void Awake()
        {
            try
            {
                // Get required components
                _cardImage = GetComponent<Image>();
                _outline = GetComponent<Outline>();
                
                if (_cardImage == null)
                {
                    Debug.LogError("Card Image component not found!");
                    return;
                }

                // Store original values
                _originalCardColor = _cardImage.color;
                _originalScale = transform.localScale;
                
                // Configure outline
                if (_outline != null)
                {
                    _outline.enabled = false;
                    _outline.effectColor = hoverOutlineColor;
                    _outline.effectDistance = _normalOutlineEffect;
                }

                _parent = transform.parent;
        
                // Determine if this is a player card - add null check
                _isPlayerCard = _parent != null && _parent.name == "PlayerHand";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in CardController Awake: {e.Message}");
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isPlayerCard && !_isDragging && TurnManager.IsPlayerTurn(GameManager.LocalPlayer.OwnerClientId))
            {
                EnableOutline(true);
                AnimateHoverScale(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                EnableOutline(false);
                AnimateHoverScale(false);
            }
        }

        private void EnableOutline(bool isHovering)
        {
            if (_outline != null)
            {
                _outline.enabled = true;
                _outline.effectDistance = isHovering ? _hoverOutlineEffect : _normalOutlineEffect;
            }
        }

        private void DisableOutline()
        {
            if (_outline != null)
            {
                _outline.enabled = false;
            }
        }

        private void AnimateHoverScale(bool isHovering)
        {
            // Kill any existing scale animations
            transform.DOKill();
            
            Vector3 targetScale = isHovering ? _originalScale * hoverScaleMultiplier : _originalScale;
            
            transform.DOScale(targetScale, hoverScaleDuration)
                .SetEase(hoverScaleEase);
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
            
            // Apply drag visual effects
            _cardImage.color = dragColor;
            // Keep outline enabled during drag
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
            
            // Reset visual effects
            _cardImage.color = _originalCardColor;
            
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
                // Only disable outline when card is played
                DisableOutline();
                // Reset scale when played
                transform.localScale = _originalScale;
            }
            else
            {
                ReturnCardToHand();
                // Re-enable outline if still hovering
                if (RectTransformUtility.RectangleContainsScreenPoint(
                    GetComponent<RectTransform>(),
                    Input.mousePosition,
                    null))
                {
                    EnableOutline(true);
                }
            }
        }

        private bool IsPointerReleasedOnTable(PointerEventData eventData)
        {
            if (eventData.pointerEnter == null) return false;
            
            // Check if the object is the table itself
            if (eventData.pointerEnter.name == "Table") return true;
            
            // Check if the object is a child of the table
            Transform current = eventData.pointerEnter.transform;
            while (current.parent != null)
            {
                if (current.parent.name == "Table")
                {
                    return true;
                }
                current = current.parent;
            }
            
            return false;
        }

        private void PlayCardOnTable(Transform releaseTarget)
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

            // Find the actual table transform if released on a child
            Transform tableTransform = releaseTarget;
            while (tableTransform != null && tableTransform.name != "Table")
            {
                tableTransform = tableTransform.parent;
            }

            if (tableTransform == null)
            {
                Debug.LogError("Could not find table transform!");
                ReturnCardToHand();
                return;
            }

            // Deactivate all visual effects when card is played
            DisableOutline();
            transform.DOKill(); // Kill any ongoing animations
            transform.localScale = _originalScale; // Reset scale immediately
            
            SetCardParentAndPosition(tableTransform);
            _card = CardConverter.GetCardValueFromGameObject(gameObject);
            
            GameManager.OnCardPlayedServerRpc(CardConverter.GetCodedCard(_card), localPlayerId);
            
            // Disable hover interactions once the card is on the table
            _isPlayerCard = false;
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
        
        private void ShowInvalidMoveMessage()
        {
            if (_isPlayerCard && !TurnManager.IsPlayerTurn(GameManager.LocalPlayer.OwnerClientId))
            {
                StartCoroutine(ShakeCard());
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

        private void OnDisable()
        {
            // Kill any running animations when the object is disabled
            transform.DOKill();
        }
    }
}