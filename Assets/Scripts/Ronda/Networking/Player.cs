using System.Collections.Generic;
using System.Linq;
using KKL.Ronda.Core;
using Unity.Netcode;

namespace KKL.Ronda.Networking
{
    public class Player : NetworkBehaviour
    {
        private readonly NetworkVariable<uint> _score = new();
        
        // Add NetworkList for captured cards
        private readonly NetworkList<int> _capturedCards = new();

        private static GameManager GameManager => GameManager.Instance;

        private List<Card> _cardsInHand = new();

        public Card[] Cards { get; set; }
        public uint Score
        {
            get => _score.Value;
            set => _score.Value = value;
        }

        public List<Card> CardsInHand => _cardsInHand.ToList();
        
        // Add property to access captured cards
        public List<Card> CapturedCards
        {
            get
            {
                var cards = new List<Card>();
                foreach (var codedCard in _capturedCards)
                {
                    cards.Add(CardConverter.DecodeCodedCard(codedCard));
                }
                return cards;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        
            if (IsOwner)
            {
                AddPlayerServerRpc();
            }
        }
    
        private void Start()
        {
            _score.OnValueChanged += ScoreChanged;
        }

        public override void OnDestroy()
        {
            _score.OnValueChanged -= ScoreChanged;
        }

        private static void ScoreChanged(uint oldValue, uint newValue)
        {
            GameManager.Instance.UpdateScoreUI();
        }
        
        public void InitializeCards(int size)
        {
            Cards = new Card[size];
        }

        public void SetCards(Card[] cards)
        {
            Cards = cards;
        }

        public void AddCardsToHand(List<Card> cards)
        {
            _cardsInHand = cards;
        }
    
        public void AddScore(uint score)
        {
            _score.Value += score;
        }
    
        public void RemoveCardFromHand(Value value, Suit suit)
        {
            Card cardToRemove = _cardsInHand.Find(c => c.Value == value && c.Suit == suit);

            if (cardToRemove != null)
            {
                _cardsInHand.Remove(cardToRemove);
            }
        }
        
        public void AddCapturedCards(IEnumerable<Card> cards)
        {
            if (!IsServer) return;

            foreach (var card in cards)
            {
                int codedCard = CardConverter.GetCodedCard(card);
                _capturedCards.Add(codedCard);
            }
        }
        [ClientRpc]
        private void AddPlayerClientRpc()
        {
            GameManager.AddPlayer(this);
        }

        [ServerRpc]
        private void AddPlayerServerRpc()
        {
            AddPlayerClientRpc();
        }
    }
}