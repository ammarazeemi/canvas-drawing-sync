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
        private Point _lastPoint;
        private CancellationTokenSource _cts;

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
                            Dispatcher.Invoke(() => DrawLine(drawEvent));
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
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _isDrawing = true;
                _lastPoint = e.GetPosition(DrawingCanvas);
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing)
            {
                var currentPoint = e.GetPosition(DrawingCanvas);
                var colorTag = ((ComboBoxItem)ColorPicker.SelectedItem).Tag.ToString();

                var drawEvent = new DrawEvent
                {
                    StartX = _lastPoint.X,
                    StartY = _lastPoint.Y,
                    EndX = currentPoint.X,
                    EndY = currentPoint.Y,
                    Color = colorTag,
                    Width = 2
                };

                // Draw locally immediately
                DrawLine(drawEvent);

                // Send to server
                _ = SendDrawEventAsync(drawEvent);

                _lastPoint = currentPoint;
            }
        }

        private void DrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = false;
        }

        private void DrawLine(DrawEvent drawEvent)
        {
            var line = new Line
            {
                X1 = drawEvent.StartX,
                Y1 = drawEvent.StartY,
                X2 = drawEvent.EndX,
                Y2 = drawEvent.EndY,
                Stroke = (SolidColorBrush)new BrushConverter().ConvertFromString(drawEvent.Color),
                StrokeThickness = drawEvent.Width
            };

            DrawingCanvas.Children.Add(line);
        }

        private async Task SendDrawEventAsync(DrawEvent drawEvent)
        {
            if (_webSocket.State == WebSocketState.Open)
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