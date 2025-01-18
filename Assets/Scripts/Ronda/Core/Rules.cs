using System;
using System.Collections.Generic;
using System.Linq;

namespace KKL.Ronda.Core
{
    public abstract class Rules
    {
        private const int TotalDeckSize = 40;
        private const int FirstCapturePoints = 1;
        private const int SecondCapturePoints = 5;
        private const int ThirdCapturePoints = 10;
        private const int WinningScore = 41;
        private const int ExtraCardPoint = 1;
        private const int MaxExtraCardPoints = 20;

        /// <summary>
        /// Validates initial table cards according to game rules.
        /// </summary>
        public static bool AreValidTableCards(List<Card> tableCards)
        {
            if (tableCards == null || tableCards.Count != 4)
                return false;

            // Check for pairs
            var hasPairs = tableCards.GroupBy(c => c.Value)
                                   .Any(g => g.Count() > 1);
            if (hasPairs) 
                return false;

            // Check for sequences
            var sortedValues = tableCards.Select(c => (int)c.Value)
                                       .OrderBy(v => v)
                                       .ToList();
            for (int i = 0; i < sortedValues.Count - 1; i++)
            {
                if (sortedValues[i + 1] == sortedValues[i] + 1)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a player has a Ronda (pair) in their hand.
        /// </summary>
        public static bool HasRonda(List<Card> handCards)
        {
            if (handCards == null) 
                return false;

            return handCards.GroupBy(c => c.Value)
                          .Any(g => g.Count() == 2);
        }

        /// <summary>
        /// Checks if a player has a Tringa (three of a kind) in their hand.
        /// </summary>
        public static bool HasTringa(List<Card> handCards)
        {
            if (handCards == null) 
                return false;

            return handCards.GroupBy(c => c.Value)
                          .Any(g => g.Count() == 3);
        }

        /// <summary>
        /// Gets the value of the highest Ronda in case of multiple announcements.
        /// </summary>
        public static Value GetHighestRondaValue(List<Card> handCards)
        {
            if (handCards == null || !HasRonda(handCards))
                throw new InvalidOperationException("No Ronda found in hand");

            return handCards.GroupBy(c => c.Value)
                          .Where(g => g.Count() == 2)
                          .Select(g => g.Key)
                          .Max();
        }

        /// <summary>
        /// Checks if there are any cards on the table that can be captured by the played card.
        /// </summary>
        public static bool CanCapture(Card playedCard, List<Card> tableCards)
        {
            if (playedCard == null || tableCards == null)
                return false;

            return tableCards.Any(card => card.Value == playedCard.Value);
        }

        /// <summary>
        /// Gets all cards that must be captured according to game rules.
        /// Players must capture all possible cards including sequences.
        /// </summary>
        public static List<Card> GetMandatoryCaptureCards(Card playedCard, List<Card> tableCards)
        {
            if (playedCard == null || tableCards == null)
                return new List<Card>();

            var capturable = new List<Card> { playedCard };

            // Find all matching cards
            var matchingCards = tableCards.Where(card => card.Value == playedCard.Value).ToList();
            
            foreach (var matchingCard in matchingCards)
            {
                capturable.Add(matchingCard);
                
                // Check for sequences starting from the matching card
                var sequence = GetSequenceStartingFrom(matchingCard, tableCards);
                capturable.AddRange(sequence);
            }

            return capturable.Distinct().ToList();
        }

        /// <summary>
        /// Gets a sequence of cards starting from the given card.
        /// </summary>
        private static List<Card> GetSequenceStartingFrom(Card startCard, List<Card> tableCards)
        {
            var sequence = new List<Card>();
            var currentValue = (int)startCard.Value;
            
            while (true)
            {
                var nextCard = tableCards.FirstOrDefault(c => (int)c.Value == currentValue + 1);
                if (nextCard == null)
                    break;
                
                sequence.Add(nextCard);
                currentValue = (int)nextCard.Value;
            }

            return sequence;
        }

        /// <summary>
        /// Calculates points for captured cards based on consecutive captures.
        /// </summary>
        public static int CalculateCapturePoints(int consecutiveCaptureCount)
        {
            return consecutiveCaptureCount switch
            {
                1 => FirstCapturePoints,
                2 => SecondCapturePoints,
                3 => ThirdCapturePoints,
                _ => 0
            };
        }

        /// <summary>
        /// Calculates end-game points based on number of captured cards.
        /// </summary>
        public static int CalculateExtraCardPoints(int playerCardCount, int opponentCardCount)
        {
            if (playerCardCount <= opponentCardCount)
                return 0;
            
            var extraCards = playerCardCount - opponentCardCount;
            var points = extraCards * ExtraCardPoint;
            return Math.Min(points, MaxExtraCardPoints);
        }

        /// <summary>
        /// Checks if the game should end (reaching winning score).
        /// </summary>
        public static bool IsGameOver(int playerScore)
        {
            return playerScore >= WinningScore;
        }

        /// <summary>
        /// Validates that the deck contains the correct number and distribution of cards.
        /// </summary>
        public static bool IsValidDeck(Deck deck)
        {
            if (deck?.Cards == null || deck.Cards.Count != TotalDeckSize)
                return false;

            var cards = deck.Cards;
            
            // Check for exactly one of each card
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Value value in Enum.GetValues(typeof(Value)))
                {
                    if (cards.Count(c => c.Suit == suit && c.Value == value) != 1)
                        return false;
                }
            }

            return true;
        }
    }
}