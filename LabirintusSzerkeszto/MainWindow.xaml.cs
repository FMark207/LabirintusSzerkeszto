using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace LabirintusSzerkeszto
{
    public enum DrawMode
    {
        Path,
        Treasure,
        Delete
    }

    public partial class MainWindow : Window
    {
        private const int TileSize = 16;
        private const int TreasureMask = 16;

        private bool isPanning = false;
        private Point startPanPoint;
        private double startXOffset;
        private double startYOffset;

        private TranslateTransform CanvasTransform = new TranslateTransform();

        private int[,] mazeArray;
        private int currentWidthInTiles = 0;
        private int currentHeightInTiles = 0;

        private bool isDrawing = false;
        private UIElement[,] visualGrid;
        private Point? lastDrawnTile = null;

        private Dictionary<string, BitmapImage> tileTextures = new Dictionary<string, BitmapImage>();

        private DrawMode currentMode = DrawMode.Path;

        public MainWindow()
        {
            InitializeComponent();

            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(CanvasScale);
            transformGroup.Children.Add(CanvasTransform);
            BuildCanvas.RenderTransform = transformGroup;

            LoadTileTextures();

            CanvasTileBrush.ImageSource = tileTextures["Empty_Tile.png"];

            UpdateCanvasSize();
            UpdateModeUI();
        }

        private void LoadTileTextures()
        {
            string[] assets =
            {
                "Empty_Tile.png",
                "dead_end.png",
                "corridor.png",
                "corner_turn_simple.png",
                "t_turn.png",
                "4_turn.png",
                "Treasure_Chest.png"
            };

            foreach (var asset in assets)
            {
                Uri uri = new Uri($"pack://application:,,,/assets/{asset}", UriKind.Absolute);
                tileTextures[asset] = new BitmapImage(uri);
            }
        }

        private void BtnModePath_Click(object sender, RoutedEventArgs e)
        {
            currentMode = DrawMode.Path;
            UpdateModeUI();
        }

        private void BtnModeTreasure_Click(object sender, RoutedEventArgs e)
        {
            currentMode = DrawMode.Treasure;
            UpdateModeUI();
        }

        private void BtnModeDelete_Click(object sender, RoutedEventArgs e)
        {
            currentMode = DrawMode.Delete;
            UpdateModeUI();
        }

        private void UpdateModeUI()
        {
            BtnModePath.Background = currentMode == DrawMode.Path ? Brushes.LightGreen : Brushes.LightGray;
            BtnModeTreasure.Background = currentMode == DrawMode.Treasure ? Brushes.LightGreen : Brushes.LightGray;
            BtnModeDelete.Background = currentMode == DrawMode.Delete ? Brushes.LightCoral : Brushes.LightGray;
        }

        private void SizeChanged_Event(object sender, TextChangedEventArgs e)
        {
            UpdateCanvasSize();
        }

        private void UpdateCanvasSize()
        {
            if (TxtXSize == null || TxtYSize == null || BuildCanvas == null) return;

            if (int.TryParse(TxtXSize.Text, out int xTiles) && int.TryParse(TxtYSize.Text, out int yTiles))
            {
                if (xTiles <= 0 || yTiles <= 0)
                    return;

                if (xTiles != currentWidthInTiles || yTiles != currentHeightInTiles)
                {
                    currentWidthInTiles = xTiles;
                    currentHeightInTiles = yTiles;

                    mazeArray = new int[currentWidthInTiles, currentHeightInTiles];
                    visualGrid = new UIElement[currentWidthInTiles, currentHeightInTiles];

                    for (int x = 0; x < currentWidthInTiles; x++)
                    {
                        for (int y = 0; y < currentHeightInTiles; y++)
                        {
                            mazeArray[x, y] = -1;
                        }
                    }

                    BuildCanvas.Children.Clear();
                }

                BuildCanvas.Width = xTiles * TileSize;
                BuildCanvas.Height = yTiles * TileSize;
            }
        }

        private void BuildCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
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
                isDrawing = true;
                BuildCanvas.CaptureMouse();
                DrawPathAtPosition(e.GetPosition(BuildCanvas));
                e.Handled = true;
            }
        }

        private void BuildCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - startPanPoint.X;
                double deltaY = currentPoint.Y - startPanPoint.Y;

                CanvasTransform.X = startXOffset + deltaX;
                CanvasTransform.Y = startYOffset + deltaY;
            }
            else if (isDrawing)
            {
                DrawPathAtPosition(e.GetPosition(BuildCanvas));
            }
        }

        private void BuildCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                isPanning = false;
                BuildCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                isDrawing = false;
                lastDrawnTile = null;
                BuildCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void DrawPathAtPosition(Point position)
        {
            if (mazeArray == null) return;

            int tileXIndex = (int)Math.Floor(position.X / TileSize);
            int tileYIndex = (int)Math.Floor(position.Y / TileSize);

            if (currentMode == DrawMode.Path && lastDrawnTile != null)
            {
                int lastX = (int)lastDrawnTile.Value.X;
                int lastY = (int)lastDrawnTile.Value.Y;
                bool isExitCreated = false;

                if (tileXIndex < 0 && lastX == 0) { mazeArray[lastX, lastY] |= 8; isExitCreated = true; }
                else if (tileXIndex >= currentWidthInTiles && lastX == currentWidthInTiles - 1) { mazeArray[lastX, lastY] |= 2; isExitCreated = true; }
                else if (tileYIndex < 0 && lastY == 0) { mazeArray[lastX, lastY] |= 1; isExitCreated = true; }
                else if (tileYIndex >= currentHeightInTiles && lastY == currentHeightInTiles - 1) { mazeArray[lastX, lastY] |= 4; isExitCreated = true; }

                if (isExitCreated)
                {
                    UpdateTileVisual(lastX, lastY);
                }
            }

            if (tileXIndex >= 0 && tileXIndex < currentWidthInTiles &&
                tileYIndex >= 0 && tileYIndex < currentHeightInTiles)
            {
                if (currentMode == DrawMode.Delete)
                {
                    DeleteTile(tileXIndex, tileYIndex);
                    lastDrawnTile = new Point(tileXIndex, tileYIndex);
                }
                else if (currentMode == DrawMode.Treasure)
                {
                    if (mazeArray[tileXIndex, tileYIndex] == -1)
                    {
                        mazeArray[tileXIndex, tileYIndex] = TreasureMask;
                    }
                    else
                    {
                        mazeArray[tileXIndex, tileYIndex] |= TreasureMask;
                    }

                    UpdateTileVisual(tileXIndex, tileYIndex);
                    lastDrawnTile = new Point(tileXIndex, tileYIndex);
                }
                else if (currentMode == DrawMode.Path)
                {
                    if (lastDrawnTile == null)
                    {
                        if (mazeArray[tileXIndex, tileYIndex] == -1)
                        {
                            mazeArray[tileXIndex, tileYIndex] = 0;
                        }

                        UpdateTileVisual(tileXIndex, tileYIndex);
                        lastDrawnTile = new Point(tileXIndex, tileYIndex);
                    }
                    else
                    {
                        int lastX = (int)lastDrawnTile.Value.X;
                        int lastY = (int)lastDrawnTile.Value.Y;

                        if (lastX != tileXIndex || lastY != tileYIndex)
                        {
                            while (lastX != tileXIndex || lastY != tileYIndex)
                            {
                                int nextX = lastX;
                                int nextY = lastY;

                                if (lastX < tileXIndex) nextX++;
                                else if (lastX > tileXIndex) nextX--;
                                else if (lastY < tileYIndex) nextY++;
                                else if (lastY > tileYIndex) nextY--;

                                if (nextX >= 0 && nextX < currentWidthInTiles &&
                                    nextY >= 0 && nextY < currentHeightInTiles)
                                {
                                    ConnectTiles(lastX, lastY, nextX, nextY);
                                }

                                lastX = nextX;
                                lastY = nextY;
                            }

                            lastDrawnTile = new Point(tileXIndex, tileYIndex);
                        }
                    }
                }
            }
        }

        private void DeleteTile(int x, int y)
        {
            if (mazeArray[x, y] == -1) return;

            int value = mazeArray[x, y];
            int pathMask = value & 15;

            if (pathMask != 0)
            {
                if ((pathMask & 1) == 1 && y - 1 >= 0 && (mazeArray[x, y - 1] & 15) != 0)
                {
                    mazeArray[x, y - 1] &= ~4;
                    UpdateTileVisual(x, y - 1);
                }

                if ((pathMask & 2) == 2 && x + 1 < currentWidthInTiles && (mazeArray[x + 1, y] & 15) != 0)
                {
                    mazeArray[x + 1, y] &= ~8;
                    UpdateTileVisual(x + 1, y);
                }

                if ((pathMask & 4) == 4 && y + 1 < currentHeightInTiles && (mazeArray[x, y + 1] & 15) != 0)
                {
                    mazeArray[x, y + 1] &= ~1;
                    UpdateTileVisual(x, y + 1);
                }

                if ((pathMask & 8) == 8 && x - 1 >= 0 && (mazeArray[x - 1, y] & 15) != 0)
                {
                    mazeArray[x - 1, y] &= ~2;
                    UpdateTileVisual(x - 1, y);
                }
            }

            mazeArray[x, y] = -1;
            UpdateTileVisual(x, y);
        }

        private void ConnectTiles(int x1, int y1, int x2, int y2)
        {
            if (mazeArray[x1, y1] == -1) mazeArray[x1, y1] = 0;
            if (mazeArray[x2, y2] == -1) mazeArray[x2, y2] = 0;

            if (x2 == x1 + 1)
            {
                mazeArray[x1, y1] |= 2;
                mazeArray[x2, y2] |= 8;
            }
            else if (x2 == x1 - 1)
            {
                mazeArray[x1, y1] |= 8;
                mazeArray[x2, y2] |= 2;
            }
            else if (y2 == y1 + 1)
            {
                mazeArray[x1, y1] |= 4;
                mazeArray[x2, y2] |= 1;
            }
            else if (y2 == y1 - 1)
            {
                mazeArray[x1, y1] |= 1;
                mazeArray[x2, y2] |= 4;
            }

            UpdateTileVisual(x1, y1);
            UpdateTileVisual(x2, y2);
        }

        private void UpdateTileVisual(int x, int y)
        {
            if (x < 0 || x >= currentWidthInTiles || y < 0 || y >= currentHeightInTiles) return;

            if (mazeArray[x, y] == -1)
            {
                if (visualGrid[x, y] != null)
                {
                    BuildCanvas.Children.Remove(visualGrid[x, y]);
                    visualGrid[x, y] = null;
                }
                return;
            }

            int value = mazeArray[x, y];
            bool hasTreasure = (value & TreasureMask) != 0;
            int pathMask = value & 15;

            string assetName;
            double angle = 0;

            if (hasTreasure)
            {
                assetName = "Treasure_Chest.png";
                angle = 0;
            }
            else
            {
                GetTileAssetAndRotation(pathMask, out assetName, out angle);
            }

            if (visualGrid[x, y] != null)
            {
                BuildCanvas.Children.Remove(visualGrid[x, y]);
            }

            Image tileImage = new Image
            {
                Width = TileSize,
                Height = TileSize,
                Source = tileTextures[assetName]
            };

            if (angle != 0)
            {
                tileImage.RenderTransformOrigin = new Point(0.5, 0.5);
                tileImage.RenderTransform = new RotateTransform(angle);
            }

            Canvas.SetLeft(tileImage, x * TileSize);
            Canvas.SetTop(tileImage, y * TileSize);

            BuildCanvas.Children.Add(tileImage);
            visualGrid[x, y] = tileImage;
        }

        private void GetTileAssetAndRotation(int mask, out string assetName, out double angle)
        {
            assetName = "dead_end.png";
            angle = 0;

            switch (mask)
            {
                case 0:
                    assetName = "dead_end.png";
                    angle = 180;
                    break;

                case 1:
                    assetName = "dead_end.png";
                    angle = 180;
                    break;

                case 2:
                    assetName = "dead_end.png";
                    angle = -90;
                    break;

                case 4:
                    assetName = "dead_end.png";
                    angle = 0;
                    break;

                case 8:
                    assetName = "dead_end.png";
                    angle = -270;
                    break;

                case 5:
                    assetName = "corridor.png";
                    angle = 0;
                    break;

                case 10:
                    assetName = "corridor.png";
                    angle = 90;
                    break;

                case 3:
                    assetName = "corner_turn_simple.png";
                    angle = -90;
                    break;

                case 6:
                    assetName = "corner_turn_simple.png";
                    angle = 0;
                    break;

                case 12:
                    assetName = "corner_turn_simple.png";
                    angle = 90;
                    break;

                case 9:
                    assetName = "corner_turn_simple.png";
                    angle = 180;
                    break;

                case 11:
                    assetName = "t_turn.png";
                    angle = 180;
                    break;

                case 7:
                    assetName = "t_turn.png";
                    angle = 270;
                    break;

                case 14:
                    assetName = "t_turn.png";
                    angle = 0;
                    break;

                case 13:
                    assetName = "t_turn.png";
                    angle = 90;
                    break;

                case 15:
                    assetName = "4_turn.png";
                    angle = 0;
                    break;
            }
        }

        private void BuildCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                return;

            double zoomFactor = 0.1;
            double currentScale = CanvasScale.ScaleX;

            if (e.Delta > 0)
                currentScale += zoomFactor;
            else
                currentScale -= zoomFactor;

            if (currentScale < 0.5) currentScale = 0.5;
            if (currentScale > 5.0) currentScale = 5.0;

            CanvasScale.ScaleX = currentScale;
            CanvasScale.ScaleY = currentScale;

            e.Handled = true;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (mazeArray == null) return;

            if (!ValidateMapForExport(out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Exportálás sikertelen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "Map file (*.map)|*.map|Text file (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".map",
                FileName = "maze.map"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            string mapText = BuildExportMapText();
            File.WriteAllText(saveDialog.FileName, mapText, new UTF8Encoding(false));

            MessageBox.Show("Sikeres mentés", "Exportálás", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool ValidateMapForExport(out string errorMessage)
        {
            bool hasTreasure = false;
            bool hasEdgeExit = false;

            for (int x = 0; x < currentWidthInTiles; x++)
            {
                for (int y = 0; y < currentHeightInTiles; y++)
                {
                    int value = mazeArray[x, y];
                    if (value == -1) continue;

                    if ((value & TreasureMask) != 0)
                        hasTreasure = true;

                    int pathMask = value & 15;
                    if (pathMask == 0) continue;

                    if (y == 0 && (pathMask & 1) != 0) hasEdgeExit = true;
                    if (x == currentWidthInTiles - 1 && (pathMask & 2) != 0) hasEdgeExit = true;
                    if (y == currentHeightInTiles - 1 && (pathMask & 4) != 0) hasEdgeExit = true;
                    if (x == 0 && (pathMask & 8) != 0) hasEdgeExit = true;
                }
            }

            if (!hasEdgeExit)
            {
                errorMessage = "A térképen legalább egy, a szélen lévő kijáratnak lennie kell.";
                return false;
            }

            if (!hasTreasure)
            {
                errorMessage = "A térképen legalább egy kincsesládának lennie kell.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private string BuildExportMapText()
        {
            StringBuilder sb = new StringBuilder();

            for (int y = 0; y < currentHeightInTiles; y++)
            {
                for (int x = 0; x < currentWidthInTiles; x++)
                {
                    sb.Append(GetExportChar(mazeArray[x, y]));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private char GetExportChar(int value)
        {
            if (value == -1)
                return '.';

            if ((value & TreasureMask) != 0)
                return '█';

            int mask = value & 15;

            return mask switch
            {
                0 => '╬',

                1 => '║',
                2 => '═',
                3 => '╚',
                4 => '║',
                5 => '║',
                6 => '╔',
                7 => '╠',
                8 => '═',
                9 => '╝',
                10 => '═',
                11 => '╩',
                12 => '╗',
                13 => '╣',
                14 => '╦',
                15 => '╬',
                _ => '.'
            };
        }
    }
}