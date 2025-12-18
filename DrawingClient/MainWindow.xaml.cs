using DrawingShared;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DrawingClient
{
    public partial class MainWindow : Window
    {
        private ClientWebSocket _webSocket;
        private bool _isDrawing;
        private Point _startPoint;
        private Point _lastPoint;
        private CancellationTokenSource _cts;
        private Shape _previewShape;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-connect removed. User must click Connect.
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                MessageBox.Show("Already connected.");
                return;
            }

            var url = ServerUrlInput.Text;
            await ConnectToServerAsync(url);
        }

        private async Task ConnectToServerAsync(string url)
        {
            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            try
            {
                StatusText.Text = "Connecting...";
                StatusText.Foreground = Brushes.Orange;

                await _webSocket.ConnectAsync(new Uri(url), _cts.Token);
                
                StatusText.Text = "Connected";
                StatusText.Foreground = Brushes.Green;

                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed";
                StatusText.Foreground = Brushes.Red;
                MessageBox.Show($"Connection Failed: {ex.Message}");
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[1024 * 4];

            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var drawEvent = JsonSerializer.Deserialize<DrawEvent>(json);

                        if (drawEvent != null)
                        {
                            Dispatcher.Invoke(() => DrawShape(drawEvent));
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Disconnected";
                    StatusText.Foreground = Brushes.Red;
                });
            }
        }

        private void DrawingCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDrawing = true;
                _startPoint = e.GetPosition(DrawingCanvas);
                _lastPoint = _startPoint;
                DrawingCanvas.CaptureMouse();
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(DrawingCanvas);
                var colorTag = ((ComboBoxItem)ColorPicker.SelectedItem).Tag.ToString();
                var shapeTag = ((ComboBoxItem)ShapePicker.SelectedItem).Tag.ToString();
                var shapeType = Enum.Parse<ShapeType>(shapeTag);

                if (shapeType == ShapeType.Line)
                {
                    var drawEvent = new DrawEvent
                    {
                        Type = EventType.Draw,
                        Shape = ShapeType.Line,
                        StartX = _lastPoint.X,
                        StartY = _lastPoint.Y,
                        EndX = currentPoint.X,
                        EndY = currentPoint.Y,
                        Color = colorTag,
                        Width = 2
                    };

                    DrawShape(drawEvent);
                    _ = SendDrawEventAsync(drawEvent);
                    _lastPoint = currentPoint;
                }
                else
                {
                    // Update preview
                    if (_previewShape != null)
                    {
                        DrawingCanvas.Children.Remove(_previewShape);
                    }

                    var drawEvent = new DrawEvent
                    {
                        Type = EventType.Draw,
                        Shape = shapeType,
                        StartX = _startPoint.X,
                        StartY = _startPoint.Y,
                        EndX = currentPoint.X,
                        EndY = currentPoint.Y,
                        Color = colorTag,
                        Width = 2
                    };

                    _previewShape = CreateShape(drawEvent);
                    _previewShape.Opacity = 0.5;
                    DrawingCanvas.Children.Add(_previewShape);
                }
            }
            else if (_isDrawing)
            {
                // Safety: if button is released but MouseUp didn't fire correctly
                StopDrawing(e.GetPosition(DrawingCanvas));
            }
        }

        private void DrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawing)
            {
                StopDrawing(e.GetPosition(DrawingCanvas));
            }
        }

        private void StopDrawing(Point currentPoint)
        {
            var shapeTag = ((ComboBoxItem)ShapePicker.SelectedItem).Tag.ToString();
            var shapeType = Enum.Parse<ShapeType>(shapeTag);

            if (shapeType != ShapeType.Line)
            {
                if (_previewShape != null)
                {
                    DrawingCanvas.Children.Remove(_previewShape);
                    _previewShape = null;
                }

                var colorTag = ((ComboBoxItem)ColorPicker.SelectedItem).Tag.ToString();
                var drawEvent = new DrawEvent
                {
                    Type = EventType.Draw,
                    Shape = shapeType,
                    StartX = _startPoint.X,
                    StartY = _startPoint.Y,
                    EndX = currentPoint.X,
                    EndY = currentPoint.Y,
                    Color = colorTag,
                    Width = 2
                };

                DrawShape(drawEvent);
                _ = SendDrawEventAsync(drawEvent);
            }

            _isDrawing = false;
            DrawingCanvas.ReleaseMouseCapture();
        }

        private void DrawShape(DrawEvent drawEvent)
        {
            if (drawEvent.Type == EventType.Clear)
            {
                DrawingCanvas.Children.Clear();
                return;
            }

            var shape = CreateShape(drawEvent);
            DrawingCanvas.Children.Add(shape);
        }

        private Shape CreateShape(DrawEvent drawEvent)
        {
            Shape shape;
            switch (drawEvent.Shape)
            {
                case ShapeType.Rectangle:
                    shape = new Rectangle
                    {
                        Width = Math.Abs(drawEvent.EndX - drawEvent.StartX),
                        Height = Math.Abs(drawEvent.EndY - drawEvent.StartY),
                        Stroke = (SolidColorBrush)new BrushConverter().ConvertFromString(drawEvent.Color),
                        StrokeThickness = drawEvent.Width
                    };
                    Canvas.SetLeft(shape, Math.Min(drawEvent.StartX, drawEvent.EndX));
                    Canvas.SetTop(shape, Math.Min(drawEvent.StartY, drawEvent.EndY));
                    break;
                case ShapeType.Ellipse:
                    shape = new Ellipse
                    {
                        Width = Math.Abs(drawEvent.EndX - drawEvent.StartX),
                        Height = Math.Abs(drawEvent.EndY - drawEvent.StartY),
                        Stroke = (SolidColorBrush)new BrushConverter().ConvertFromString(drawEvent.Color),
                        StrokeThickness = drawEvent.Width
                    };
                    Canvas.SetLeft(shape, Math.Min(drawEvent.StartX, drawEvent.EndX));
                    Canvas.SetTop(shape, Math.Min(drawEvent.StartY, drawEvent.EndY));
                    break;
                case ShapeType.Line:
                default:
                    shape = new Line
                    {
                        X1 = drawEvent.StartX,
                        Y1 = drawEvent.StartY,
                        X2 = drawEvent.EndX,
                        Y2 = drawEvent.EndY,
                        Stroke = (SolidColorBrush)new BrushConverter().ConvertFromString(drawEvent.Color),
                        StrokeThickness = drawEvent.Width
                    };
                    break;
            }
            return shape;
        }

        private async void Clear_Click(object sender, RoutedEventArgs e)
        {
            var clearEvent = new DrawEvent { Type = EventType.Clear };
            DrawShape(clearEvent);
            await SendDrawEventAsync(clearEvent);
        }

        private async Task SendDrawEventAsync(DrawEvent drawEvent)
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var json = JsonSerializer.Serialize(drawEvent);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts?.Cancel();
            if (_webSocket != null)
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                }
                catch { }
                _webSocket.Dispose();
            }
        }
    }
}