namespace Aor.UI
{
    public class UniqueID<T>
    {
        private static int currentUID = 0;

        public static int NextUID {
            get {
                currentUID++;
                return currentUID;
            }
        }
    }
}