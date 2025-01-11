using System;
using System.Collections.Generic;
using System.Linq;
using Random = System.Random;

namespace KKL.Ronda.Core
{
    [Serializable]
    public class Deck
    {
        public List<Card> Cards => _cards.ToList();
        private Queue<Card> _cards;

        public Deck()
        {
            CreateDeck();
            ShuffleDeck();
        }
    
        public Deck(IEnumerable<int> cards)
        {
            _cards = new Queue<Card>();
            foreach (var card in cards)
            {
                var value = (Value)(card % 100);
                var suit = (Suit)((card - (int)value) / 100);
                _cards.Enqueue(new Card(suit, value));
            }
        }

        public Card PullCard()
        {
            return _cards.Dequeue();
        }

        public Card[] PullCards(int amount)
        {
            var cards = new List<Card>();
            for (var i = 0; i < amount && _cards.Count > 0; i++)
            {
                cards.Add(_cards.Dequeue());
            }

            return cards.ToArray();
        }

        private void CreateDeck()
        {
            _cards = new Queue<Card>();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Value value in Enum.GetValues(typeof(Value)))
                {
                    var cardObject = new Card(suit, value);
                    _cards.Enqueue(cardObject);
                }
            }
        }

        private void ShuffleDeck()
        {
            // Use a single Random instance to avoid potential issues with seeding.
            var random = new Random();

            // Fisher-Yates shuffle algorithm.
            var shuffledArray = _cards.ToArray();
            var n = shuffledArray.Length;
            while (n > 1)
            {
                var k = random.Next(n--);
                (shuffledArray[n], shuffledArray[k]) = (shuffledArray[k], shuffledArray[n]);
            }

            _cards = new Queue<Card>(shuffledArray);
        }
    }
}