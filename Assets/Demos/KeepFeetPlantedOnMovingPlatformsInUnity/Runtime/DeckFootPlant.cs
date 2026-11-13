using UnityEngine;

namespace GameFeelClinic.CloudRelay
{
    [DefaultExecutionOrder(-120)]
    public sealed class DeckFootPlant : MonoBehaviour
    {
        [SerializeField] Transform deck;
        [SerializeField] Transform footGoal;
        [SerializeField] bool useDeckSpace = true;

        Vector3 localAnchor;
        Vector3 worldAnchor;
        bool planted;

        public bool IsPlanted => planted;
        public bool UseDeckSpace => useDeckSpace;

        public void Plant()
        {
            if (planted)
                return;

            worldAnchor = footGoal.position;
            localAnchor = deck.InverseTransformPoint(footGoal.position);
            planted = true;
        }

        public void Lift()
        {
            planted = false;
        }

        void LateUpdate() => ApplyPlantedPose();

        public void ApplyPlantedPose()
        {
            if (!planted)
                return;

            footGoal.position = useDeckSpace
                ? deck.TransformPoint(localAnchor)
                : worldAnchor;
        }

        public void Configure(Transform movingDeck, Transform goal)
        {
            deck = movingDeck;
            footGoal = goal;
        }

        public void SetModeForComparison(bool deckSpace)
        {
            useDeckSpace = deckSpace;
        }
    }
}
