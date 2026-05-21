using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LabirintusSzerkeszto
{
    public partial class MainWindow : Window
    {
        // Ne valtoztasd lecci mer furan neznenek ki az assetek
        private const int TileSize = 16;

        private bool isPanning = false;
        private Point startPanPoint;
        private double startXOffset;
        private double startYOffset;

        private TranslateTransform CanvasTransform = new TranslateTransform();

        private int[,] mazeArray;
        private int currentWidthInTiles = 0;
        private int currentHeightInTiles = 0;

        public MainWindow()
        {
            InitializeComponent();

            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(CanvasScale);
            transformGroup.Children.Add(CanvasTransform);
            BuildCanvas.RenderTransform = transformGroup;

            // 15 initilazation error utan a Bitmap miatt kenytelen voltam ilyen path megadasahoz
            Uri imageUri = new Uri("pack://application:,,,/assets/Empty_Tile.png", UriKind.Absolute);
            CanvasTileBrush.ImageSource = new System.Windows.Media.Imaging.BitmapImage(imageUri);

            // Lefutasnal Meret Beallitas
            UpdateCanvasSize();
        }

        // Minden Valtozasnal Dinamikus Valtozas
        private void SizeChanged_Event(object sender, TextChangedEventArgs e)
        {
            UpdateCanvasSize();
        }

        private void UpdateCanvasSize()
        {
            if (TxtXSize == null || TxtYSize == null || BuildCanvas == null) return;

            // 16*16 ertekek mer abba rajzoltam az asseteket :P
            if (int.TryParse(TxtXSize.Text, out int xTiles) && int.TryParse(TxtYSize.Text, out int yTiles))
            {

                if (xTiles != currentWidthInTiles || yTiles != currentHeightInTiles)
                {
                    currentWidthInTiles = xTiles;
                    currentHeightInTiles = yTiles;

                    // mazeArray -> mindent tarol szam ertekekben; 0 = ut; 1 = fal;
                    mazeArray = new int[currentWidthInTiles, currentHeightInTiles];

                    // array feloltese falakkal ; ez nem a gen-hez tartozik
                    for (int x = 0; x < currentWidthInTiles; x++)
                    {
                        for (int y = 0; y < currentHeightInTiles; y++)
                        {
                            mazeArray[x, y] = 1;
                        }
                    }


                    BuildCanvas.Children.Clear();
                }

                BuildCanvas.Width = xTiles * TileSize;
                BuildCanvas.Height = yTiles * TileSize;
            }
        }

        //Egyelore csak feketit kockakat, majd kivalasztott targyakat tud lerakni
        private void BuildCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Jobb eger section, itt mozgat
            if (e.ChangedButton == MouseButton.Right)
            {
                isPanning = true;

                startPanPoint = e.GetPosition(this);


                startXOffset = CanvasTransform.X;
                startYOffset = CanvasTransform.Y;


                BuildCanvas.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                //Kattintott negyzetet kiszamol 
                Point clickPosition = e.GetPosition(BuildCanvas);

                int tileXIndex = (int)clickPosition.X / TileSize;
                int tileYIndex = (int)clickPosition.Y / TileSize;

                // Határon belul ellenorzes
                if (tileXIndex >= 0 && tileXIndex < currentWidthInTiles && tileYIndex >= 0 && tileYIndex < currentHeightInTiles)
                {
                    if (mazeArray[tileXIndex, tileYIndex] != 0)
                    {
                        // Elmenteshez szukseges sor
                        mazeArray[tileXIndex, tileYIndex] = 0;

                        // Specialis elemek szamolas

                        int tileX = tileXIndex * TileSize;
                        int tileY = tileYIndex * TileSize;

                        Rectangle road = new Rectangle
                        {
                            Width = TileSize,
                            Height = TileSize,
                            Fill = Brushes.Black
                        };

                        // Specialis elementnek pozicio adas
                        Canvas.SetLeft(road, tileX);
                        Canvas.SetTop(road, tileY);

                        BuildCanvas.Children.Add(road);
                    }
                }
            }
        }

        private void BuildCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                //Mozgas Kiszamolasa

                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - startPanPoint.X;
                double deltaY = currentPoint.Y - startPanPoint.Y;

                CanvasTransform.X = startXOffset + deltaX;
                CanvasTransform.Y = startYOffset + deltaY;
            }
        }

        // Mozgatas Vege
        private void BuildCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                isPanning = false;
                BuildCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        // Zoom function (lehet hogy chatgpt assist volt ebben)
        private void BuildCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                return;

            double zoomFactor = 0.1;

            double currentScale = CanvasScale.ScaleX;

            if (e.Delta > 0)
            {
                currentScale += zoomFactor;
            }
            else
            {
                currentScale -= zoomFactor;
            }

            if (currentScale < 0.5) currentScale = 0.5;
            if (currentScale > 5.0) currentScale = 5.0;

            CanvasScale.ScaleX = currentScale;
            CanvasScale.ScaleY = currentScale;

            e.Handled = true;
        }

        // Export Gomb (Meg szarul nez ki majd megcsinalom)
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (mazeArray == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- LABIRINTUS EXPORT ---");

            for (int y = 0; y < currentHeightInTiles; y++)
            {
                for (int x = 0; x < currentWidthInTiles; x++)
                {
                    sb.Append(mazeArray[x, y] + " ");
                }
                sb.AppendLine();
            }


            System.Diagnostics.Debug.WriteLine(sb.ToString());


            MessageBox.Show("Sikeres Export", "Exportálás", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
