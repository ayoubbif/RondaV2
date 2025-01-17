using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KKL.Ronda.Core;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace KKL.Ronda.Networking
{
    /// <summary>
    /// Manages the core game logic and networking for the Ronda card game.
    /// Handles card dealing, player interactions, scoring, and synchronization across the network.
    /// </summary>
    public class GameManager : NetworkBehaviour
    {
        #region Fields and Properties

        // Singleton instance
        public static GameManager Instance { get; private set; }

        // UI References
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform playerHand;
        [SerializeField] private Transform enemyHand;
        [SerializeField] private Table table;
        [SerializeField] private TMP_Text scoreText;

        // Game Constants
        private const int MaxNumPlayers = 2;
        
        // Player Management
        public List<Player> Players => players.ToList();
        [SerializeField] private List<Player> players = new();
        public Player LocalPlayer => GetLocalPlayer();
    
        // Deck Management
        private Deck _deck;
        private bool _isDeckInitialized;
        private Card _scoredCard;
    
        // Game State
        private int _numCardsToDeal;
        private Action<ulong> _onCardsDealt;
        private bool _isEmptyHanded;
        private bool _canCapture;

        #endregion

        #region Unity Lifecycle Methods

        private void OnEnable()
        {
            // Subscribe to card dealing events
            _onCardsDealt += SpawnCardsClientRpc;
            _onCardsDealt += SpawnEnemyClientRpc;
        }
    
        private void OnDisable()
        {
            // Unsubscribe from card dealing events
            _onCardsDealt -= SpawnCardsClientRpc;
            _onCardsDealt -= SpawnEnemyClientRpc;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                turnManager.Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion
        
        #region Game State
        private const int InitialTableCards = 4;
        private const int CardsPerDeal = 3;
        private readonly NetworkVariable<GameState> _currentGameState = new();
        [SerializeField] public TurnManager turnManager;
        [SerializeField] private TMP_Text turnIndicatorText;
        private bool _isGameStarted;

        private enum GameState
        {
            Dealing,
            Playing,
        }
        #endregion

        #region Server Logic

        /// <summary>
        /// Handles the dealing of cards from the server to all players.
        /// Initializes the deck if necessary and validates the configuration.
        /// </summary>
        private void S_Deal()
        {
            if (!IsServer || Players.Count < MaxNumPlayers)
            {
                Debug.Log("Not enough players or not server");
                return;
            }

            // Initialize deck if needed
            if (!_isDeckInitialized)
            {
                _deck = new Deck();
                _isDeckInitialized = true;
                
                // Deal initial table cards
                List<Card> initialTableCards = new();
                for (int i = 0; i < InitialTableCards; i++)
                {
                    initialTableCards.Add(_deck.PullCard());
                }
                
                // Send initial table cards to clients
                InitializeTableCardsClientRpc(initialTableCards.ToArray());
            }

            // Convert deck to coded format for network transmission
            int[] codedCards = CardConverter.GetCodedCards(_deck.Cards);
            InitDeckClientRpc(codedCards);
            
            _currentGameState.Value = GameState.Dealing;
            DealFromDeck();
            _currentGameState.Value = GameState.Playing;
            
            Debug.Log($"Server: {_deck.Cards.Count} cards remaining in deck");
            
            if (!_isGameStarted)
            {
                InitializeTurnOrder();
            }
        }

        /// <summary>
        /// Deals cards from the deck to all players.
        /// </summary>
        private void DealFromDeck()
        {
            _numCardsToDeal = _deck.Cards.Count <= CardsPerDeal * 2 ? _deck.Cards.Count / 2 : CardsPerDeal;

            foreach (var player in players)
            {
                player.InitializeCards(_numCardsToDeal);

                if (_deck.Cards.Count == 0) return;
                
                List<Card> dealtCards = new();
                for (int i = 0; i < _numCardsToDeal; i++)
                {
                    var card = _deck.PullCard();
                    dealtCards.Add(card);
                }
                
                player.Cards = dealtCards.ToArray();
                SetPlayersCardsClientRpc(player.OwnerClientId, player.Cards);
            }
        }

        #endregion
        
        #region Turn Management
        private void InitializeTurnOrder()
        {
            if (!IsServer) return;
        
            int firstPlayer = UnityEngine.Random.Range(0, Players.Count);
            turnManager.StartFirstTurn(Players[firstPlayer].OwnerClientId);
        }
    
        public bool IsPlayerTurn(ulong playerId)
        {
            return turnManager.IsPlayerTurn(playerId);
        }
    
        private void AdvanceTurn()
        {
            if (!IsServer) return;

            // Get current player ID from TurnManager
            ulong currentPlayerId = turnManager.CurrentPlayerTurnId.Value;
            int currentIndex = Players.FindIndex(p => p.OwnerClientId == currentPlayerId);
            int nextIndex = (currentIndex + 1) % Players.Count;
    
            Debug.Log($"Advancing turn from player {currentPlayerId} to player {Players[nextIndex].OwnerClientId}");
            turnManager.AdvanceTurn(Players[nextIndex].OwnerClientId);
        }
        #endregion

        #region Game Logic
        
        /// <summary>
        /// Processes a played card, handling captures and table placement.
        /// </summary>
        /// <param name="playedCard">The card that was played</param>
        /// <param name="player">The player who played the card</param>
        private void ProcessPlayedCard(Card playedCard, Player player)
        {
            _canCapture = Rules.CanCapture(playedCard, table.Cards);
            
            if (_canCapture)
            {
                HandleCardCapture(playedCard, player);
            }
            else
            {
                AddCardToTableClientRpc(playedCard);
            }
            
            CheckForEmptyHandAndDeal();
        }

        /// <summary>
        /// Checks if all players have empty hands and triggers a new deal if necessary.
        /// </summary>
        private void CheckForEmptyHandAndDeal()
        {
            if (Players.Count != 2 || !_isEmptyHanded) return;
            StartCoroutine(DealAfterDelay(2f));
        }

        #endregion
        
        #region Special Moves Handling

        /// <summary>
        /// Handles special move scoring and effects
        /// </summary>
        private void HandleSpecialMoves(Card playedCard, Player player, List<Card> capturedCards)
        {
            var specialMovePoints = Rules.CalculateSpecialMovePoints(capturedCards);
            if (specialMovePoints > 0)
            {
                player.AddScore((uint)specialMovePoints);
                NotifySpecialMoveClientRpc(player.OwnerClientId, playedCard, specialMovePoints);
            }
        }

        /// <summary>
        /// Handles the capture logic when a card is played that can capture cards from the table.
        /// </summary>
        private void HandleCardCapture(Card playedCard, Player player)
        {
            var captureableCards = Rules.GetCaptureableCards(playedCard, table.Cards);
            
            if (captureableCards.Any())
            {
                // Check if this is the last capture of the game
                bool isLastCapture = _deck.Cards.Count == 0 && players.All(p => p.CardsInHand.Count == 0);
                int capturePoints = Rules.CalculateCapturePoints(captureableCards, isLastCapture);
                
                // Handle special moves
                HandleSpecialMoves(playedCard, player, captureableCards);
                
                // Award capture points to the player
                player.AddScore((uint)capturePoints);
                Debug.Log($"Player {player.OwnerClientId} scored {capturePoints} points. Total: {player.Score}");
                
                NotifyServerToRemoveMatchingCardsClientRpc(playedCard, captureableCards.ToArray());
                UpdateScoreClientRpc(player.OwnerClientId, player.Score);
            }
        }
        #endregion

        #region ClientRPC Methods

        [ClientRpc]
        private void InitDeckClientRpc(int[] deck)
        {
            _deck = new Deck(deck);
        }

        [ClientRpc]
        private void SetPlayersCardsClientRpc(ulong playerId, Card[] cards)
        {
            SetPlayersCards(playerId, cards);
        
            if (LocalPlayer.OwnerClientId == playerId)
            {
                LocalPlayer.AddCardsToHand(cards.ToList());
                _isEmptyHanded = false;
            }
        }

        [ClientRpc]
        private void NotifyServerOnCardPlayedClientRpc(int codedCard, ulong playerId)
        {
            Card playedCard = CardConverter.DecodeCodedCard(codedCard);
            LocalPlayer.RemoveCardFromHand(playedCard.Value, playedCard.Suit);

            if (LocalPlayer.OwnerClientId == playerId)
            {
                return;
            }

            SpawnCardOnTable(playedCard);
            _isEmptyHanded = players.All(player => player.CardsInHand.Count == 0);
        }

        [ClientRpc]
        private void NotifyServerToRemoveMatchingCardsClientRpc(Card card, Card[] capturedCards)
        {
            var cardsToRemove = new HashSet<Card>(capturedCards) { card };

            foreach (var cardToRemove in cardsToRemove)
            {
                table.RemoveCardFromTable(cardToRemove.Suit, cardToRemove.Value);
            }

            SetCardObjectsInactiveClientRpc(cardsToRemove.ToArray());
        }
        

        [ClientRpc]
        private void SetCardObjectsInactiveClientRpc(Card[] cardsToRemove)
        {
            foreach (var cardToRemove in cardsToRemove)
            {
                GameObject cardObject = FindCardObjectOnTable(cardToRemove);
                if (cardObject != null)
                {
                    cardObject.SetActive(false);
                }
            }
        }

        [ClientRpc]
        private void AddCardToTableClientRpc(Card card)
        {
            table.AddCardToTable(card);
        }

        [ClientRpc]
        private void RemoveCardFromEnemyHandClientRpc(ulong playerId)
        {
            if (NetworkManager.Singleton.LocalClientId != playerId)
            {
                if (enemyHand.childCount <= 0) return;
                GameObject lastCard = enemyHand.GetChild(enemyHand.childCount - 1).gameObject;
                Destroy(lastCard);
            }
        }

        [ClientRpc]
        private void SpawnCardsClientRpc(ulong playerId)
        {
            if (IsClient && LocalPlayer.OwnerClientId == playerId)
            {
                InitLocalPlayerCards();
            }
        }

        [ClientRpc]
        private void SpawnEnemyClientRpc(ulong playerId)
        {
            if (IsClient && LocalPlayer.OwnerClientId == playerId)
            {
                InitEnemyPlayerCards();
            }
        }
        [ClientRpc]
        private void InitializeTableCardsClientRpc(Card[] tableCards)
        {
            foreach (var card in tableCards)
            {
                table.AddCardToTable(card);
                SpawnCardOnTable(card);
            }
        }

        [ClientRpc]
        private void NotifySpecialMoveClientRpc(ulong playerId, Card playedCard, int points)
        {
            // Show special move animation or effect
            Debug.Log($"Special Move! Player {playerId} scored {points} points with {playedCard}");
            
            // You could trigger UI animations or effects here
            ShowSpecialMoveEffect(playedCard, points);
        }

        [ClientRpc]
        private void UpdateScoreClientRpc(ulong playerId, uint newScore)
        {
            var player = players.FirstOrDefault(p => p.OwnerClientId == playerId);
            if (player != null)
            {
                player.Score = newScore;
                if (player.IsLocalPlayer)
                {
                    UpdateScoreUI();
                }
            }
        }
        #endregion

        #region ServerRPC Methods

        /// <summary>
        /// Handles the server-side logic when a card is played.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void OnCardPlayedServerRpc(int codedCard, ulong playerId)
        {
            // Verify it's the player's turn
            if (!IsPlayerTurn(playerId))
            {
                Debug.LogWarning($"Player {playerId} tried to play out of turn!");
                return;
            }

            var player = Players.FirstOrDefault(p => p.OwnerClientId == playerId);
            if (player == null)
            {
                Debug.LogError($"Player not found with ID: {playerId}");
                return;
            }

            Card playedCard = CardConverter.DecodeCodedCard(codedCard);
            NotifyServerOnCardPlayedClientRpc(codedCard, playerId);
            RemoveCardFromEnemyHandClientRpc(playerId);
            
            ProcessPlayedCard(playedCard, player);
            
            // Advance to next player's turn after card is played
            AdvanceTurn();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Adds a new player to the game.
        /// </summary>
        public void AddPlayer(Player newPlayer)
        {
            players.Add(newPlayer);
            
            if (Players.Count == 2)
                StartCoroutine(DealAfterDelay(1f));
        }

        /// <summary>
        /// Gets the local player instance.
        /// </summary>
        private Player GetLocalPlayer()
        {
            Player localPlayer = players.FirstOrDefault(x => x != null && x.IsLocalPlayer);

            if (localPlayer == null)
            {
                Debug.LogError("Local player not found.");
            }

            return localPlayer;
        }

        /// <summary>
        /// Spawns a card visually on the table.
        /// </summary>
        private void SpawnCardOnTable(Card card)
        {
            GameObject cardObject = Instantiate(cardPrefab, table.transform);
            Image cardImage = cardObject.GetComponent<Image>();

            string path = $"Sprites/Cards/{(int)card.Suit}_{(int)card.Value}";
            Sprite sprite = Resources.Load<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogError($"Sprite not found at path: {path}");
                return;
            }

            cardImage.sprite = sprite;
            cardObject.name = $"{(int)card.Suit}_{(int)card.Value}";
        }

        /// <summary>
        /// Initializes cards for the local player.
        /// </summary>
        private void InitLocalPlayerCards()
        {
            if (LocalPlayer.Cards == null)
            {
                LocalPlayer.InitializeCards(_numCardsToDeal);
                return;
            }

            SpriteConverter.UpdatePlayerCardImages(LocalPlayer.Cards, cardPrefab, playerHand);
        }

        /// <summary>
        /// Initializes cards for the enemy player.
        /// </summary>
        private void InitEnemyPlayerCards()
        {
            if (LocalPlayer.Cards == null)
            {
                LocalPlayer.InitializeCards(_numCardsToDeal);
                return;
            }
            
            SpriteConverter.UpdateEnemyCardImages(LocalPlayer.Cards, cardPrefab, enemyHand);
        }

        /// <summary>
        /// Sets the cards for a specific player.
        /// </summary>
        private void SetPlayersCards(ulong playerId, Card[] cards)
        {
            Player player = players.FirstOrDefault(x => x != null && x.OwnerClientId == playerId);

            if (player == null)
            {
                return;
            }
        
            player.SetCards(cards);
            _onCardsDealt?.Invoke(player.OwnerClientId);
        }

        /// <summary>
        /// Finds a card object on the table by its card data.
        /// </summary>
        private GameObject FindCardObjectOnTable(Card card)
        {
            return (from Transform child in table.transform
                where child.name == $"{(int)card.Suit}_{(int)card.Value}"
                select child.gameObject).FirstOrDefault();
        }

        /// <summary>
        /// Updates the score UI for the local player.
        /// </summary>
        public void UpdateScoreUI()
        {
            scoreText.text = LocalPlayer.Score.ToString();
        }

        /// <summary>
        /// Coroutine to deal cards after a delay.
        /// </summary>
        private IEnumerator DealAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            S_Deal();
        }

        #endregion
        
        #region UI Effects

        /// <summary>
        /// Shows visual effects for special moves
        /// </summary>
        private void ShowSpecialMoveEffect(Card card, int points)
        {
            // Implementation would depend on your UI setup
            // Could show particles, animations, or temporary text
            Debug.Log($"Special move effect: {card} for {points} points!");
        }
        #endregion
    }
}