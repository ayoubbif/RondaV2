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
        private List<Player> Players => players.ToList();
        [SerializeField] private List<Player> players = new();
        public Player LocalPlayer => GetLocalPlayer();
    
        // Deck Management
        private Deck _deck;
        private bool _isDeckInitialized;
        private Card _scoredCard;
    
        // Game State
        private int _numCardsToDeal;
        private const int InitialTableCards = 4;
        private const int CardsPerDeal = 3;
        private Action<ulong> _onCardsDealt;
        private readonly NetworkVariable<bool> _isEmptyHanded = new();
        private readonly NetworkVariable<GameState> _currentGameState = new();
        private bool _canCapture;
        private bool _isFirstDeal = true;
        [SerializeField] public TurnManager turnManager;
        [SerializeField] private TMP_Text turnIndicatorText;
        [SerializeField] private TMP_Text announcementText;
        [SerializeField] private float specialAnnouncementDelay = 2f;
        private readonly NetworkVariable<bool> _hasAnnouncedRonda = new();
        private readonly NetworkVariable<bool> _hasAnnouncedTringa = new();

        private enum GameState
        {
            Dealing,
            Playing
        }

        #endregion

        #region Unity Lifecycle Methods

        private void OnEnable()
        {
            // Subscribe to card dealing events
            _onCardsDealt += SpawnCardsClientRpc;
            _onCardsDealt += SpawnEnemyClientRpc;
            
            _isEmptyHanded.OnValueChanged += OnEmptyHandedChanged;
        }
    
        private void OnDisable()
        {
            // Unsubscribe from card dealing events
            _onCardsDealt -= SpawnCardsClientRpc;
            _onCardsDealt -= SpawnEnemyClientRpc;
            
            _isEmptyHanded.OnValueChanged -= OnEmptyHandedChanged;
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

        #region Server Logic
        
        private List<Card> DealInitialTableCards()
        {
            if (!IsServer) return null;

            List<Card> initialTableCards;
            bool isValid;
            int maxAttempts = 10;
            int attempts = 0;

            do
            {
                // Verify deck is valid before attempting to deal
                if (attempts > 0 && !Rules.IsValidDeck(_deck))
                {
                    _deck = new Deck();
                }

                // Create a deep copy of the full deck before dealing
                var deckBackup = new Deck();
                deckBackup.Cards.Clear();
                foreach (var card in _deck.Cards)
                {
                    deckBackup.Cards.Add(new Card(card.Suit, card.Value));
                }

                // Deal initial cards
                initialTableCards = new List<Card>();
                for (int i = 0; i < InitialTableCards; i++)
                {
                    initialTableCards.Add(_deck.PullCard());
                }

                isValid = Rules.AreValidTableCards(initialTableCards);

                // If invalid, restore deck from backup and try again
                if (!isValid && attempts < maxAttempts - 1)
                {
                    _deck = deckBackup;
                    _deck.ShuffleDeck();
                    initialTableCards.Clear();
                }

                attempts++;

            } while (!isValid && attempts < maxAttempts);

            return isValid ? initialTableCards : null;
        }
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
                
                List<Card> initialTableCards = DealInitialTableCards();
                if (initialTableCards == null || initialTableCards.Count != InitialTableCards)
                {
                    Debug.LogError("Failed to deal valid initial table cards");
                    return;
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
            
            if (_isFirstDeal)
            {
                InitializeTurnOrder();
                _isFirstDeal = false;
            }
        }
        private void DealFromDeck()
        {
            _numCardsToDeal = _deck.Cards.Count <= CardsPerDeal * 2 ? _deck.Cards.Count / 2 : CardsPerDeal;

            ResetSpecialCombinations();
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
                CheckForSpecialCombinations(player, dealtCards);
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
        private void CheckForSpecialCombinations(Player player, List<Card> cards)
        {
            if (!IsServer) return;

            bool hasRonda = Rules.HasRonda(cards);
            bool hasTringa = Rules.HasTringa(cards);

            if (hasTringa && !_hasAnnouncedTringa.Value)
            {
                Value tringaValue = Rules.GetHighestTringaValue(cards);
                AnnounceSpecialCombinationClientRpc(player.OwnerClientId, "Tringa", (int)tringaValue);
                _hasAnnouncedTringa.Value = true;
        
                // Award points for Tringa
                player.AddScore(5); 
                UpdateScoreClientRpc(player.OwnerClientId, player.Score);
            }
            else if (hasRonda && !_hasAnnouncedRonda.Value)
            {
                Value rondaValue = Rules.GetHighestRondaValue(cards);
                AnnounceSpecialCombinationClientRpc(player.OwnerClientId, "Ronda", (int)rondaValue);
                _hasAnnouncedRonda.Value = true;
        
                // Award points for Ronda
                player.AddScore(1); 
                UpdateScoreClientRpc(player.OwnerClientId, player.Score);
            }
        }
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
        
        private void OnEmptyHandedChanged(bool previousValue, bool newValue)
        {
            if (IsServer && newValue && Players.Count == 2)
            {
                if (_deck.Cards.Count > 0)  // Only deal if there are cards remaining
                {
                    StartCoroutine(DealAfterDelay(5f));
                }
                else
                {
                    // Handle end of game or final scoring
                    HandleEndOfGame();
                }
            }
        }
        
        private void CheckForEmptyHandAndDeal()
        {
            Debug.Log("Players: " + Players.Count + " IsEmptyHanded: " + _isEmptyHanded);
        }
        
        private void HandleCardCapture(Card playedCard, Player player)
        {
            var captureableCards = Rules.GetMandatoryCaptureCards(playedCard, table.Cards);
            
            if (captureableCards.Any())
            {
                int capturePoints = captureableCards.Count;
                
                // Award capture points to the player
                player.AddScore((uint)capturePoints);
                Debug.Log($"Player {player.OwnerClientId} scored {capturePoints} points. Total: {player.Score}");
                
                NotifyServerToRemoveMatchingCardsClientRpc(playedCard, captureableCards.ToArray());
                UpdateScoreClientRpc(player.OwnerClientId, player.Score);
            }
        }
        
        private void HandleEndOfGame()
        {
            // Example: Calculate final scores and declare winner
            var winner = Players.OrderByDescending(p => p.Score).First();
            Debug.Log($"Game Over! Player {winner.OwnerClientId} wins with {winner.Score} points!");
        
            // Notify clients about game end
            GameOverClientRpc(winner.OwnerClientId, winner.Score);
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
                
                // Notify server about cards being added
                NotifyCardsAddedServerRpc();
            }
        }
        
        [ClientRpc]
        private void AnnounceSpecialCombinationClientRpc(ulong playerId, string combinationType, int value)
        {
            StartCoroutine(ShowSpecialCombinationAnnouncement(playerId, combinationType, value));
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
        
        [ClientRpc]
        private void GameOverClientRpc(ulong winnerId, uint finalScore)
        {
            // Update UI or show game over screen
            Debug.Log($"Game Over! Player {winnerId} wins with {finalScore} points!");
            // Add your UI update logic here
        }
        
        #endregion

        #region ServerRPC Methods
        
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
            _isEmptyHanded.Value = players.All(p => p.CardsInHand.Count == 0);
            
            // Advance to next player's turn after card is played
            AdvanceTurn();
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void NotifyCardsAddedServerRpc()
        {
            if (IsServer)
            {
                _isEmptyHanded.Value = false;
            }
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
        
        private IEnumerator ShowSpecialCombinationAnnouncement(ulong playerId, string combinationType, int value)
        {
            // Here you would show a UI element announcing the special combination
            string playerName = playerId == NetworkManager.Singleton.LocalClientId ? "You" : "Opponent";
            string announcement = $"{playerName} announced {combinationType} of {value}s!";
    
            // Assuming you have a UI text element for announcements
            if (announcementText)
            {
                string originalText = announcementText.text;
                announcementText.text = announcement;
        
                yield return new WaitForSeconds(specialAnnouncementDelay);
        
                announcementText.text = originalText;
            }
        }

        private void ResetSpecialCombinations()
        {
            if (!IsServer) return;
            _hasAnnouncedRonda.Value = false;
            _hasAnnouncedTringa.Value = false;
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
    }
}