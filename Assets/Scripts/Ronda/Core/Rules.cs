using System;
using System.Collections.Generic;
using System.Linq;

namespace KKL.Ronda.Core
{
    public abstract class Rules
    {
        private const int TotalDeckSize = 40;

        // Special move point values
        private const int WahedPoints = 1;
        private const int KhamsaPoints = 5;
        private const int AshraPoints = 10;
        private const int MissaPoints = 1;
        private const int FinalThrowPoints = 5;

        /// <summary>
        /// Checks if there are any cards on the table that can be captured by the played card.
        /// </summary>
        public static bool CanCapture(Card playedCard, List<Card> tableCards)
        {
            return tableCards.Any(card => card.Value == playedCard.Value);
        }

        /// <summary>
        /// Gets all cards that can be captured, including any sequences.
        /// </summary>
        public static List<Card> GetCaptureableCards(Card playedCard, List<Card> tableCards)
        {
            var captureable = new HashSet<Card>();
            
            // Find all matching cards
            var matchingCards = tableCards.Where(card => card.Value == playedCard.Value).ToList();
            
            foreach (var matchingCard in matchingCards)
            {
                captureable.Add(matchingCard);
                
                // Check for sequences starting from the matching card
                var sequence = GetSequenceStartingFrom(matchingCard, tableCards);
                foreach (var card in sequence)
                {
                    captureable.Add(card);
                }
            }

            return captureable.ToList();
        }

        /// <summary>
        /// Gets a sequence of cards starting from the given card.
        /// </summary>
        private static List<Card> GetSequenceStartingFrom(Card startCard, List<Card> tableCards)
        {
            var sequence = new List<Card>();
            var sortedCards = tableCards.OrderBy(x => x.Value).ToList();
            
            // Find the index of the starting card in the sorted list
            for (int i = 0; i < sortedCards.Count; i++)
            {
                if (sortedCards[i].Value != startCard.Value) continue;

                var j = i;
                // Look for cards to the right of the played card that continue the sequence
                while (j < sortedCards.Count - 1 && sortedCards[j + 1].Value == sortedCards[j].Value + 1)
                {
                    j++;
                    sequence.Add(sortedCards[j]);
                }
                break;
            }

            return sequence;
        }

        /// <summary>
        /// Calculates points for captured cards including special moves.
        /// </summary>
        public static int CalculateCapturePoints(List<Card> capturedCards, bool isLastCapture)
        {
            int points = 0;

            // Base points for matching cards
            points += 2; // Points for the basic match

            // Add points for sequence if present
            int sequenceLength = GetSequenceLength(capturedCards);
            if (sequenceLength > 0)
            {
                points += sequenceLength;
            }

            // Add special move points
            points += CalculateSpecialMovePoints(capturedCards);

            // Add final throw bonus if applicable
            if (isLastCapture)
            {
                points += CalculateFinalThrowPoints(capturedCards);
            }

            return points;
        }

        /// <summary>
        /// Calculates points from special moves (Wahed, Khamsa, Ashra, Missa).
        /// </summary>
        public static int CalculateSpecialMovePoints(List<Card> capturedCards)
        {
            int points = 0;

            // Check for Wahed (Capturing with a One)
            if (capturedCards.Any(c => c.Value == Value.One))
            {
                points += WahedPoints;
            }

            // Check for Khamsa (Capturing with a Five)
            if (capturedCards.Any(c => c.Value == Value.Five))
            {
                points += KhamsaPoints;
            }

            // Check for Ashra (Capturing with a Ten)
            if (capturedCards.Any(c => c.Value == Value.Ten))
            {
                points += AshraPoints;
            }

            // Check for Missa (Special combination - implementation depends on specific rules)
            if (IsMissa(capturedCards))
            {
                points += MissaPoints;
            }

            return points;
        }

        /// <summary>
        /// Calculates bonus points for the final throw of the game.
        /// </summary>
        private static int CalculateFinalThrowPoints(List<Card> capturedCards)
        {
            // Final throw with Rey (12) or As (1) gets bonus points
            if (capturedCards.Any(c => c.Value == Value.Twelve || c.Value == Value.One))
            {
                return FinalThrowPoints;
            }
            return 0;
        }

        /// <summary>
        /// Checks if the captured cards form a Missa combination.
        /// Implementation depends on specific Missa rules.
        /// </summary>
        private static bool IsMissa(List<Card> capturedCards)
        {
            // TODO: Implement specific Missa rules based on game requirements
            // This could involve checking for specific card combinations
            return false;
        }

        /// <summary>
        /// Gets the length of the longest sequence in the captured cards.
        /// </summary>
        private static int GetSequenceLength(List<Card> cards)
        {
            if (cards.Count < 2) return 0;

            var sortedCards = cards.OrderBy(x => x.Value).ToList();
            var maxSequence = 0;
            var currentSequence = 0;

            for (int i = 0; i < sortedCards.Count - 1; i++)
            {
                if (sortedCards[i + 1].Value == sortedCards[i].Value + 1)
                {
                    currentSequence++;
                    maxSequence = Math.Max(maxSequence, currentSequence);
                }
                else
                {
                    currentSequence = 0;
                }
            }

            return maxSequence;
        }

        /// <summary>
        /// Validates that the deck contains the correct number and distribution of cards.
        /// </summary>
        public static bool IsValidDeck(Deck deck)
        {
            var cards = deck.Cards;
            
            if (cards.Count != TotalDeckSize)
                return false;

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