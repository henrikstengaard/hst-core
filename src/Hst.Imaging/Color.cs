namespace Hst.Imaging
{
    public class Color
    {
        public readonly int R;
        public readonly int G;
        public readonly int B;
        public readonly int A;

        public Color(int r, int g, int b, int a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Color Transparent => new Color(0, 0, 0, 0);
    }
}