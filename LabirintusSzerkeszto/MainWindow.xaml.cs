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
        private const int TileSize = 16; // Pixel art miatt fix 16, de amugy a textura pixel meretei

        private bool isPanning = false; // Megadja, hogy eppen draggelve van-e a targy
        private Point startPanPoint; // Megadja pont formatumba azt, ahol le lett nyomva a jobb gomb
        private double startXOffset; // Offset az eredeti helytol a Canvasnak X-tengelyen
        private double startYOffset; // Offset az eredeti helytol a Canvasnak Y-tengelyen

        private TranslateTransform CanvasTransform = new TranslateTransform(); 

        private int[,] mazeArray; // Menteshez szukseges, az egesz map array valtozata
        private int currentWidthInTiles = 0; // Canvas mennyi kocka X-tengelyen
        private int currentHeightInTiles = 0; // Canvas mennyi kocka Y-tengelyen

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

        // Minden Text Valtozasnal Dinamikus Valtozas
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
                isPanning = true; // elkezdi a draget

                startPanPoint = e.GetPosition(this); // megadja a kezdo pozijat a dragnek


                startXOffset = CanvasTransform.X; // megadja a dragging kezdo offsetjet
                startYOffset = CanvasTransform.Y; // canvastransform alapbol ad egy tengelyt amin mozoghat a canvas


                BuildCanvas.CaptureMouse(); // megragadja az egeret, drag modba lep, kimehet az ablakbol is akar
                e.Handled = true; // elvegzi az eventet, lezarja
                return;
            }

            if (e.ChangedButton == MouseButton.Left) // kifesti a negyzeteket, checkol bal clicket
            {
                Point clickPosition = e.GetPosition(BuildCanvas); //kattintas pozicioja a bal felso sarokhoz kepest

                int tileXIndex = (int)clickPosition.X / TileSize; // X-edik tile
                int tileYIndex = (int)clickPosition.Y / TileSize; // Y-adik tile

                // Határon belul ellenorzes
                if (tileXIndex >= 0 && tileXIndex < currentWidthInTiles && tileYIndex >= 0 && tileYIndex < currentHeightInTiles) // hataron belul kattintas
                {
                    if (mazeArray[tileXIndex, tileYIndex] != 0) //megnezi hogy van e mar ut ( 0 = ut )
                    {

                        mazeArray[tileXIndex, tileYIndex] = 0; // utat tesz a mentesi array-ba

                        // Specialis elemek szamolas

                        int tileX = tileXIndex * TileSize; // kiszamolja a poziciojat a tile-nak ( bal felso saroktol)
                        int tileY = tileYIndex * TileSize;

                        Rectangle road = new Rectangle // ut kinezete
                        {
                            Width = TileSize,
                            Height = TileSize,
                            Fill = Brushes.Black
                        };

                        // Specialis elementnek pozicio adas
                        Canvas.SetLeft(road, tileX); // elem elhelyezese
                        Canvas.SetTop(road, tileY);

                        BuildCanvas.Children.Add(road); // hozzaadja az utata
                    }
                }
            }
        }

        private void BuildCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning) // checkolja hogy eppen draggel-e
            {
                //Mozgas Kiszamolasa

                Point currentPoint = e.GetPosition(this); // megnezi az eger poziciojat
                double deltaX = currentPoint.X - startPanPoint.X; // megnezi a delta mozgast (az eger poziciojanak es drag startjanak kulonbsege)
                double deltaY = currentPoint.Y - startPanPoint.Y;

                CanvasTransform.X = startXOffset + deltaX; // eltoljuk a poziciojat, offsethez adjuk, (ha mar alapbol el volt tolva onnan szamoljuk)
                CanvasTransform.Y = startYOffset + deltaY;
            }
        }

        // Mozgatas Vege
        private void BuildCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right) // ellenorzi hogy a jobb click lett lenyomva
            {
                isPanning = false; // dragging leallitasa
                BuildCanvas.ReleaseMouseCapture(); // ha meg mindig captureolve lenne akkor ha ki menne az ablakbol is "ra lenne tapadva az eger"
                e.Handled = true; // esemeny lezarasa
            }
        }

        // Zoom function (lehet hogy chatgpt assist volt ebben)
        private void BuildCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl)) // szukseges gombok lenyomasanak ellenorzese
                return;

            double zoomFactor = 0.1; // zoom mennyisege

            double currentScale = CanvasScale.ScaleX; // jelenlegi scale

            if (e.Delta > 0) // Delta = irany; ha pozitiv akkor befele, ha negativ akkor kifele
            {
                currentScale += zoomFactor;
            }
            else
            {
                currentScale -= zoomFactor;
            }

            if (currentScale < 0.5) currentScale = 0.5;
            if (currentScale > 5.0) currentScale = 5.0; // itt a hatar a zoomra

            CanvasScale.ScaleX = currentScale; // modositja a zoomot X
            CanvasScale.ScaleY = currentScale; // modositja a zoomot Y

            e.Handled = true; //esemeny lezarasa
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
