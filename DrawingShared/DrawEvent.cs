namespace DrawingShared
{
    public class DrawEvent
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public string Color { get; set; } = "#000000";
        public double Width { get; set; } = 2.0;
    }
}
