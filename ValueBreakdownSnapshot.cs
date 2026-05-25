using System;

namespace MapValueTracker
{
    public struct ValueBreakdownSnapshot : IEquatable<ValueBreakdownSnapshot>
    {
        public bool IsVisible;
        public bool ShowCompactHud;
        public bool ShowMapPanel;
        public bool MapOpen;
        public bool HideAfterCompletion;
        public bool UseRemainingForPrimary;
        public float MapValue;
        public float CartsValue;
        public float HaulerValue;
        public float RemainingValue;

        public float PrimaryValue => UseRemainingForPrimary ? RemainingValue : MapValue;
        public string PrimaryLabel => UseRemainingForPrimary ? "Remaining" : "Map";

        public bool Equals(ValueBreakdownSnapshot other)
        {
            return IsVisible == other.IsVisible
                && ShowCompactHud == other.ShowCompactHud
                && ShowMapPanel == other.ShowMapPanel
                && MapOpen == other.MapOpen
                && HideAfterCompletion == other.HideAfterCompletion
                && UseRemainingForPrimary == other.UseRemainingForPrimary
                && Math.Abs(MapValue - other.MapValue) < 0.01f
                && Math.Abs(CartsValue - other.CartsValue) < 0.01f
                && Math.Abs(HaulerValue - other.HaulerValue) < 0.01f
                && Math.Abs(RemainingValue - other.RemainingValue) < 0.01f;
        }

        public override bool Equals(object obj)
        {
            return obj is ValueBreakdownSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = IsVisible.GetHashCode();
                hashCode = (hashCode * 397) ^ ShowCompactHud.GetHashCode();
                hashCode = (hashCode * 397) ^ ShowMapPanel.GetHashCode();
                hashCode = (hashCode * 397) ^ MapOpen.GetHashCode();
                hashCode = (hashCode * 397) ^ HideAfterCompletion.GetHashCode();
                hashCode = (hashCode * 397) ^ UseRemainingForPrimary.GetHashCode();
                hashCode = (hashCode * 397) ^ MapValue.GetHashCode();
                hashCode = (hashCode * 397) ^ CartsValue.GetHashCode();
                hashCode = (hashCode * 397) ^ HaulerValue.GetHashCode();
                hashCode = (hashCode * 397) ^ RemainingValue.GetHashCode();
                return hashCode;
            }
        }
    }
}
