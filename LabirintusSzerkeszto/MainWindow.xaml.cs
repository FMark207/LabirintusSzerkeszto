using System;
using System.Collections.Generic;
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

        // --- ÚJ VÁLTOZÓK AZ AUTOMATIKUS ELHELYEZÉSHEZ ÉS HÚZÁSHOZ ---
        private bool isDrawing = false; // Megadja, hogy éppen bal klikkel rajzolunk-e húzással
        private UIElement[,] visualGrid; // Tárolja a canvasra lerakott Image elemeket a frissítésekhez

        // --- TEXTÚRA GYORSÍTÓTÁR (CACHE) ---
        // Itt tároljuk a betöltött képeket, hogy ne kelljen őket folyton újraalkotni drag közben
        private Dictionary<string, BitmapImage> tileTextures = new Dictionary<string, BitmapImage>();

        public MainWindow()
        {
            InitializeComponent();

            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(CanvasScale);
            transformGroup.Children.Add(CanvasTransform);
            BuildCanvas.RenderTransform = transformGroup;

            // Textúrák egyszeri beolvasása a memóriába
            LoadTileTextures();

            // Alapértelmezett háttérkefe beállítása a cache-ből
            CanvasTileBrush.ImageSource = tileTextures["Empty_Tile.png"];

            // Lefutasnal Meret Beallitas
            UpdateCanvasSize();
        }

        // Minden textúrát betöltünk egyszer az indításkor
        private void LoadTileTextures()
        {
            string[] assets = {
                "Empty_Tile.png",
                "dead_end.png",
                "corridor.png",
                "corner_turn_simple.png",
                "t_turn.png",
                "4_turn.png"
            };

            foreach (var asset in assets)
            {
                // Használhatsz relatív utat is ("/assets/{asset}"), ha a fájl tulajdonságainál a Build Action = Resource!
                Uri uri = new Uri($"pack://application:,,,/assets/{asset}", UriKind.Absolute);
                tileTextures[asset] = new BitmapImage(uri);
            }
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
                    visualGrid = new UIElement[currentWidthInTiles, currentHeightInTiles]; // Új vizuális tömb inicializálása

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
                isDrawing = true;
                BuildCanvas.CaptureMouse(); // Megragadja az egeret a folyamatos rajzoláshoz húzás közben
                DrawPathAtPosition(e.GetPosition(BuildCanvas));
                e.Handled = true;
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
            else if (isDrawing) // Checkolja, hogy éppen bal gombbal rajzolunk-e húzás közben
            {
                DrawPathAtPosition(e.GetPosition(BuildCanvas));
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
            else if (e.ChangedButton == MouseButton.Left) // Ellenőrzi, hogy a bal klikk fel lett-e engedve
            {
                isDrawing = false;
                BuildCanvas.ReleaseMouseCapture(); // Elengedi az egeret a rajzolási módból
                e.Handled = true;
            }
        }

        // --- ÚJ METÓDUSOK A FOLYAMATOS RAJZOLÁSHOZ ÉS AUTOMATIKUS TEXTÚRÁZÁSHOZ ---

        // Kiszámolja a koordinátát húzás közben, és elindítja a csempék frissítését
        private void DrawPathAtPosition(Point position)
        {
            int tileXIndex = (int)position.X / TileSize;
            int tileYIndex = (int)position.Y / TileSize;

            // Határon belul ellenorzes
            if (tileXIndex >= 0 && tileXIndex < currentWidthInTiles && tileYIndex >= 0 && tileYIndex < currentHeightInTiles)
            {
                if (mazeArray[tileXIndex, tileYIndex] != 0) // megnezi hogy van e mar ut ( 0 = ut )
                {
                    mazeArray[tileXIndex, tileYIndex] = 0; // utat tesz a mentesi array-ba

                    // Frissítjük a jelenlegi csempét és mind a 4 közvetlen szomszédját is!
                    UpdateTileVisual(tileXIndex, tileYIndex);
                    UpdateTileVisual(tileXIndex, tileYIndex - 1); // Észak
                    UpdateTileVisual(tileXIndex + 1, tileYIndex); // Kelet
                    UpdateTileVisual(tileXIndex, tileYIndex + 1); // Dél
                    UpdateTileVisual(tileXIndex - 1, tileYIndex); // Nyugat
                }
            }
        }

        // Újraértékeli az adott csempe szomszédait, kiválasztja az assetet és beállítja a rotációt
        private void UpdateTileVisual(int x, int y)
        {
            if (x < 0 || x >= currentWidthInTiles || y < 0 || y >= currentHeightInTiles) return;

            // Ha ez egy fal (1), akkor eltávolítjuk a rajta lévő utat, hogy az alapértelmezett háttér látszódjon
            if (mazeArray[x, y] == 1)
            {
                if (visualGrid[x, y] != null)
                {
                    BuildCanvas.Children.Remove(visualGrid[x, y]);
                    visualGrid[x, y] = null;
                }
                return;
            }

            // Bitmaszk kiszámítása a szomszédos utak alapján (0 = út)
            int mask = 0;
            if (y > 0 && mazeArray[x, y - 1] == 0) mask |= 1;                      // Észak (Bit 0)
            if (x < currentWidthInTiles - 1 && mazeArray[x + 1, y] == 0) mask |= 2;  // Kelet (Bit 1)
            if (y < currentHeightInTiles - 1 && mazeArray[x, y + 1] == 0) mask |= 4; // Dél (Bit 2)
            if (x > 0 && mazeArray[x - 1, y] == 0) mask |= 8;                      // Nyugat (Bit 3)

            // Megkeressük a maszkhoz tartozó fájlnevet és rotációs szöget
            GetTileAssetAndRotation(mask, out string assetName, out double angle);

            // Ha már volt itt korábban lerakott elem, töröljük a Canvasről az újrarajzolás előtt
            if (visualGrid[x, y] != null)
            {
                BuildCanvas.Children.Remove(visualGrid[x, y]);
            }

            // Új Image elem létrehozása a már memóriában lévő gyorsítótárazott (cached) textúrával
            Image tileImage = new Image
            {
                Width = TileSize,
                Height = TileSize,
                Source = tileTextures[assetName] // Sokkal gyorsabb, nincs fájl IO vagy URI parzolás futásidőben!
            };

            // Rotáció alkalmazása a csempe középpontjához képest
            if (angle != 0)
            {
                tileImage.RenderTransformOrigin = new Point(0.5, 0.5);
                tileImage.RenderTransform = new RotateTransform(angle);
            }

            // Pozíció beállítása a Canvasen
            Canvas.SetLeft(tileImage, x * TileSize);
            Canvas.SetTop(tileImage, y * TileSize);

            BuildCanvas.Children.Add(tileImage);
            visualGrid[x, y] = tileImage; // Eltároljuk a referenciát a későbbi frissítésekhez
        }

        // Egy elágazási táblázat, ami a szomszédok bitmaszkja alapján visszaadja az asset nevet és a forgatást
        private void GetTileAssetAndRotation(int mask, out string assetName, out double angle)
        {
            assetName = "dead_end.png";
            angle = 0;

            switch (mask)
            {
                // 0 vagy 1 szomszéd (Zsákutcák)
                case 0: assetName = "dead_end.png"; angle = 180; break;
                case 1: assetName = "dead_end.png"; angle = 180; break;   // Észak felé nyitott
                case 2: assetName = "dead_end.png"; angle = -90; break;  // Kelet felé nyitott
                case 4: assetName = "dead_end.png"; angle = 0; break; // Dél felé nyitott
                case 8: assetName = "dead_end.png"; angle = -270; break; // Nyugat felé nyitott

                // 2 szomszéd (Egyenes folyosók vagy Kanyarok)
                case 5: assetName = "corridor.png"; angle = 0; break;   // Észak + Dél folyosó
                case 10: assetName = "corridor.png"; angle = 90; break;  // Kelet + Nyugat folyosó
                case 3: assetName = "corner_turn_simple.png"; angle = -90; break;   // Észak + Kelet kanyar
                case 6: assetName = "corner_turn_simple.png"; angle = 0; break;  // Kelet + Dél kanyar
                case 12: assetName = "corner_turn_simple.png"; angle = 90; break; // Dél + Nyugat kanyar
                case 9: assetName = "corner_turn_simple.png"; angle = 180; break; // Nyugat + Észak kanyar

                // 3 szomszéd (T-elágazások)
                case 11: assetName = "t_turn.png"; angle = 180; break;   // Észak + Kelet + Nyugat
                case 7: assetName = "t_turn.png"; angle = 270; break;  // Észak + Kelet + Dél
                case 14: assetName = "t_turn.png"; angle = 0; break; // Kelet + Dél + Nyugat
                case 13: assetName = "t_turn.png"; angle = 90; break; // Dél + Nyugat + Észak

                // 4 szomszéd (Kereszteződés)
                case 15: assetName = "4_turn.png"; angle = 0; break;   // Mind a 4 irányba nyitott
            }
        }

        // Zoom function
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