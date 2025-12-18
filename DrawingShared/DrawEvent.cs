namespace DrawingShared
{
    public enum EventType { Draw, Clear }
    public enum ShapeType { Line, Rectangle, Ellipse }

    public class DrawEvent
    {
        public EventType Type { get; set; } = EventType.Draw;
        public ShapeType Shape { get; set; } = ShapeType.Line;
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public string Color { get; set; } = "#000000";
        public double Width { get; set; } = 2.0;
    }
}
