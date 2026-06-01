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
    // This enum defines the drawing mode: Path, Treasure, or Delete.
    public enum DrawMode
    {
        Path,      // Draw or connect path tiles
        Treasure,  // Place treasure chests
        Delete     // Remove tiles
    }

    public partial class MainWindow : Window
    {
        // Size of each tile in pixels (tiles are 16x16)
        private const int TileSize = 16;

        // Bit used to mark a tile as containing a treasure.
        // Path directions use bits 1,2,4,8; treasure uses bit 16 (TreasureMask).
        private const int TreasureMask = 16;

        // Variables for panning (moving the view with the mouse).
        private bool isPanning = false;
        private Point startPanPoint;  // Where the pan started
        private double startXOffset;  // Canvas X offset when pan started
        private double startYOffset;  // Canvas Y offset when pan started

        // Transform used to move (translate) the canvas for panning.
        private TranslateTransform CanvasTransform = new TranslateTransform();

        // The main data structure for the maze: a 2D array of ints.
        // Each cell's value stores path bits and treasure bit (or -1 if empty).
        private int[,] mazeArray;
        private int currentWidthInTiles = 0;
        private int currentHeightInTiles = 0;

        // Variables for drawing with the mouse.
        private bool isDrawing = false;
        private UIElement[,] visualGrid;       // Holds the drawn images for each tile
        private Point? lastDrawnTile = null;   // The last tile index drawn during dragging

        // Dictionary to hold loaded tile textures by filename.
        private Dictionary<string, BitmapImage> tileTextures = new Dictionary<string, BitmapImage>();

        // Current drawing mode (default is Path mode).
        private DrawMode currentMode = DrawMode.Path;

        // Constructor: runs when the window is created.
        public MainWindow()
        {
            InitializeComponent();

            // Combine scaling and translation transforms so we can zoom and pan.
            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(CanvasScale);
            transformGroup.Children.Add(CanvasTransform);
            BuildCanvas.RenderTransform = transformGroup;

            // Load all tile images into the dictionary.
            LoadTileTextures();

            // Set the canvas background (or brush) to an empty tile image.
            CanvasTileBrush.ImageSource = tileTextures["Empty_Tile.png"];

            // Initialize or update the canvas size and mode UI.
            UpdateCanvasSize();
            UpdateModeUI();
        }

        // Load tile images from the assets folder into the tileTextures dictionary.
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
                // The images are stored as resources, loaded with a pack URI.
                Uri uri = new Uri($"pack://application:,,,/assets/{asset}", UriKind.Absolute);
                tileTextures[asset] = new BitmapImage(uri);
            }
        }

        // Button click: set drawing mode to Path.
        private void BtnModePath_Click(object sender, RoutedEventArgs e)
        {
            currentMode = DrawMode.Path;
            UpdateModeUI();  // Update button colors
        }

        // Button click: set drawing mode to Treasure.
        private void BtnModeTreasure_Click(object sender, RoutedEventArgs e)
        {
            currentMode = DrawMode.Treasure;
            UpdateModeUI();
        }

        // Button click: set drawing mode to Delete.
        private void BtnModeDelete_Click(object sender, RoutedEventArgs e)
        {
            currentMode = DrawMode.Delete;
            UpdateModeUI();
        }

        // Update the UI (button backgrounds) based on the current mode.
        private void UpdateModeUI()
        {
            BtnModePath.Background = currentMode == DrawMode.Path ? Brushes.LightGreen : Brushes.LightGray;
            BtnModeTreasure.Background = currentMode == DrawMode.Treasure ? Brushes.LightGreen : Brushes.LightGray;
            BtnModeDelete.Background = currentMode == DrawMode.Delete ? Brushes.LightCoral : Brushes.LightGray;
        }

        // Triggered when the size text boxes change; update the canvas size.
        private void SizeChanged_Event(object sender, TextChangedEventArgs e)
        {
            UpdateCanvasSize();
        }

        // Update the canvas size and reinitialize the arrays if needed.
        private void UpdateCanvasSize()
        {
            // If controls aren't ready, do nothing.
            if (TxtXSize == null || TxtYSize == null || BuildCanvas == null) return;

            // Parse the text from size boxes.
            if (int.TryParse(TxtXSize.Text, out int xTiles) && int.TryParse(TxtYSize.Text, out int yTiles))
            {
                // Ignore invalid sizes.
                if (xTiles <= 0 || yTiles <= 0)
                    return;

                // If the size changed, create new arrays.
                if (xTiles != currentWidthInTiles || yTiles != currentHeightInTiles)
                {
                    currentWidthInTiles = xTiles;
                    currentHeightInTiles = yTiles;

                    // Initialize the maze array and visual grid with new sizes.
                    mazeArray = new int[currentWidthInTiles, currentHeightInTiles];
                    visualGrid = new UIElement[currentWidthInTiles, currentHeightInTiles];

                    // Mark all tiles as empty (-1).
                    for (int x = 0; x < currentWidthInTiles; x++)
                    {
                        for (int y = 0; y < currentHeightInTiles; y++)
                        {
                            mazeArray[x, y] = -1;
                        }
                    }

                    // Clear any old drawn tiles from the canvas.
                    BuildCanvas.Children.Clear();
                }

                // Set the canvas pixel size based on tile count.
                BuildCanvas.Width = xTiles * TileSize;
                BuildCanvas.Height = yTiles * TileSize;
            }
        }

        // Mouse down on canvas: handle panning or start drawing.
        private void BuildCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Right-click = start panning.
            if (e.ChangedButton == MouseButton.Right)
            {
                isPanning = true;
                startPanPoint = e.GetPosition(this);
                startXOffset = CanvasTransform.X;
                startYOffset = CanvasTransform.Y;

                // Capture the mouse so we keep receiving events.
                BuildCanvas.CaptureMouse();
                e.Handled = true;
                return;
            }

            // Left-click = start drawing (depending on mode).
            if (e.ChangedButton == MouseButton.Left)
            {
                isDrawing = true;
                BuildCanvas.CaptureMouse();
                // Perform one drawing action at this position.
                DrawPathAtPosition(e.GetPosition(BuildCanvas));
                e.Handled = true;
            }
        }

        // Mouse move on canvas: continue panning or drawing.
        private void BuildCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                // Calculate how far the mouse moved since panning started.
                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - startPanPoint.X;
                double deltaY = currentPoint.Y - startPanPoint.Y;

                // Update canvas translation based on mouse movement.
                CanvasTransform.X = startXOffset + deltaX;
                CanvasTransform.Y = startYOffset + deltaY;
            }
            else if (isDrawing)
            {
                // If in drawing mode (left button held down), draw as mouse moves.
                DrawPathAtPosition(e.GetPosition(BuildCanvas));
            }
        }

        // Mouse up on canvas: end panning or drawing.
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
                lastDrawnTile = null;  // Reset last drawn tile when releasing.
                BuildCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        // Main drawing logic: places path or treasure based on current mode.
        private void DrawPathAtPosition(Point position)
        {
            // If the maze array isn't initialized, do nothing.
            if (mazeArray == null) return;

            // Convert the mouse pixel position to tile indices.
            int tileXIndex = (int)Math.Floor(position.X / TileSize);
            int tileYIndex = (int)Math.Floor(position.Y / TileSize);

            // If we are in Path mode and have a previous tile, check if we crossed an edge.
            if (currentMode == DrawMode.Path && lastDrawnTile != null)
            {
                int lastX = (int)lastDrawnTile.Value.X;
                int lastY = (int)lastDrawnTile.Value.Y;
                bool isExitCreated = false;

                // If the drag moves outside the grid, add an exit to the edge.
                if (tileXIndex < 0 && lastX == 0)
                {
                    // Moved left off the left edge: open the left side of last tile.
                    mazeArray[lastX, lastY] |= 8; // 8 = left direction bit
                    isExitCreated = true;
                }
                else if (tileXIndex >= currentWidthInTiles && lastX == currentWidthInTiles - 1)
                {
                    // Moved right off the right edge: open the right side.
                    mazeArray[lastX, lastY] |= 2; // 2 = right direction bit
                    isExitCreated = true;
                }
                else if (tileYIndex < 0 && lastY == 0)
                {
                    // Moved above the top edge: open the top side.
                    mazeArray[lastX, lastY] |= 1; // 1 = up direction bit
                    isExitCreated = true;
                }
                else if (tileYIndex >= currentHeightInTiles && lastY == currentHeightInTiles - 1)
                {
                    // Moved below the bottom edge: open the bottom side.
                    mazeArray[lastX, lastY] |= 4; // 4 = down direction bit
                    isExitCreated = true;
                }

                if (isExitCreated)
                {
                    // Redraw that tile to show the new exit.
                    UpdateTileVisual(lastX, lastY);
                }
            }

            // If the current tile is within the grid...
            if (tileXIndex >= 0 && tileXIndex < currentWidthInTiles &&
                tileYIndex >= 0 && tileYIndex < currentHeightInTiles)
            {
                // DELETE mode: remove the tile.
                if (currentMode == DrawMode.Delete)
                {
                    DeleteTile(tileXIndex, tileYIndex);
                    lastDrawnTile = new Point(tileXIndex, tileYIndex);
                }
                // TREASURE mode: place or mark treasure on the tile.
                else if (currentMode == DrawMode.Treasure)
                {
                    if (mazeArray[tileXIndex, tileYIndex] == -1)
                    {
                        // If empty, start a new tile with just treasure bit.
                        mazeArray[tileXIndex, tileYIndex] = TreasureMask;
                    }
                    else
                    {
                        // If tile already exists, add the treasure bit.
                        mazeArray[tileXIndex, tileYIndex] |= TreasureMask;
                    }

                    // Redraw the tile (it now shows a treasure).
                    UpdateTileVisual(tileXIndex, tileYIndex);
                    lastDrawnTile = new Point(tileXIndex, tileYIndex);
                }
                // PATH mode: draw or connect path tiles.
                else if (currentMode == DrawMode.Path)
                {
                    // If this is the first tile being drawn (no last tile).
                    if (lastDrawnTile == null)
                    {
                        if (mazeArray[tileXIndex, tileYIndex] == -1)
                        {
                            // If tile is empty, set it to a path (0 means no connections yet).
                            mazeArray[tileXIndex, tileYIndex] = 0;
                        }

                        UpdateTileVisual(tileXIndex, tileYIndex);
                        lastDrawnTile = new Point(tileXIndex, tileYIndex);
                    }
                    else
                    {
                        // We have a last drawn tile, so we connect from it to the new tile.
                        int lastX = (int)lastDrawnTile.Value.X;
                        int lastY = (int)lastDrawnTile.Value.Y;

                        // If the tile changed (user moved the mouse to a different tile).
                        if (lastX != tileXIndex || lastY != tileYIndex)
                        {
                            // Fill in all tiles between last and current (straight line).
                            while (lastX != tileXIndex || lastY != tileYIndex)
                            {
                                int nextX = lastX;
                                int nextY = lastY;

                                // Move one step toward the target tile.
                                if (lastX < tileXIndex) nextX++;
                                else if (lastX > tileXIndex) nextX--;
                                else if (lastY < tileYIndex) nextY++;
                                else if (lastY > tileYIndex) nextY--;

                                // If the next step is inside the grid, connect them.
                                if (nextX >= 0 && nextX < currentWidthInTiles &&
                                    nextY >= 0 && nextY < currentHeightInTiles)
                                {
                                    ConnectTiles(lastX, lastY, nextX, nextY);
                                }

                                // Move to next point and continue until we reach target.
                                lastX = nextX;
                                lastY = nextY;
                            }

                            lastDrawnTile = new Point(tileXIndex, tileYIndex);
                        }
                    }
                }
            }
        }

        // Remove a tile at (x,y) and adjust neighbors' connections.
        private void DeleteTile(int x, int y)
        {
            // If the tile is already empty, do nothing.
            if (mazeArray[x, y] == -1) return;

            int value = mazeArray[x, y];
            // Keep only the path bits (ignore treasure for connectivity).
            int pathMask = value & 15;

            if (pathMask != 0)
            {
                // If there's an upward connection (bit 1) and a tile above,
                // remove the downward connection (bit 4) of the above tile.
                if ((pathMask & 1) == 1 && y - 1 >= 0 && (mazeArray[x, y - 1] & 15) != 0)
                {
                    mazeArray[x, y - 1] &= ~4; // Clear bit 4
                    UpdateTileVisual(x, y - 1);
                }

                // If there's a rightward connection (bit 2) and a tile to the right,
                // remove the left connection (bit 8) of the right tile.
                if ((pathMask & 2) == 2 && x + 1 < currentWidthInTiles && (mazeArray[x + 1, y] & 15) != 0)
                {
                    mazeArray[x + 1, y] &= ~8; // Clear bit 8
                    UpdateTileVisual(x + 1, y);
                }

                // If there's a downward connection (bit 4) and a tile below,
                // remove the up connection (bit 1) of the below tile.
                if ((pathMask & 4) == 4 && y + 1 < currentHeightInTiles && (mazeArray[x, y + 1] & 15) != 0)
                {
                    mazeArray[x, y + 1] &= ~1; // Clear bit 1
                    UpdateTileVisual(x, y + 1);
                }

                // If there's a left connection (bit 8) and a tile to the left,
                // remove the right connection (bit 2) of the left tile.
                if ((pathMask & 8) == 8 && x - 1 >= 0 && (mazeArray[x - 1, y] & 15) != 0)
                {
                    mazeArray[x - 1, y] &= ~2; // Clear bit 2
                    UpdateTileVisual(x - 1, y);
                }
            }

            // Finally, remove the tile itself.
            mazeArray[x, y] = -1;
            UpdateTileVisual(x, y);
        }

        // Connect two adjacent tiles (x1,y1) and (x2,y2) by setting bits on each.
        private void ConnectTiles(int x1, int y1, int x2, int y2)
        {
            // If either tile is empty, make it a path (value 0).
            if (mazeArray[x1, y1] == -1) mazeArray[x1, y1] = 0;
            if (mazeArray[x2, y2] == -1) mazeArray[x2, y2] = 0;

            // Determine the direction of the connection and set bits accordingly.
            if (x2 == x1 + 1)
            {
                // x2 is to the right of x1.
                mazeArray[x1, y1] |= 2; // tile1 connects right
                mazeArray[x2, y2] |= 8; // tile2 connects left
            }
            else if (x2 == x1 - 1)
            {
                // x2 is to the left of x1.
                mazeArray[x1, y1] |= 8; // tile1 connects left
                mazeArray[x2, y2] |= 2; // tile2 connects right
            }
            else if (y2 == y1 + 1)
            {
                // y2 is below y1.
                mazeArray[x1, y1] |= 4; // tile1 connects down
                mazeArray[x2, y2] |= 1; // tile2 connects up
            }
            else if (y2 == y1 - 1)
            {
                // y2 is above y1.
                mazeArray[x1, y1] |= 1; // tile1 connects up
                mazeArray[x2, y2] |= 4; // tile2 connects down
            }

            // Redraw both tiles so the new connection is visible.
            UpdateTileVisual(x1, y1);
            UpdateTileVisual(x2, y2);
        }

        // Update the visual representation (image) of tile (x,y) on the canvas.
        private void UpdateTileVisual(int x, int y)
        {
            // If coordinates are out of range, do nothing.
            if (x < 0 || x >= currentWidthInTiles || y < 0 || y >= currentHeightInTiles) return;

            // If tile is empty, remove any existing image and return.
            if (mazeArray[x, y] == -1)
            {
                if (visualGrid[x, y] != null)
                {
                    BuildCanvas.Children.Remove(visualGrid[x, y]);
                    visualGrid[x, y] = null;
                }
                return;
            }

            // Determine what to draw: a treasure or a path piece.
            int value = mazeArray[x, y];
            bool hasTreasure = (value & TreasureMask) != 0;
            int pathMask = value & 15; // just the path bits

            string assetName;
            double angle = 0;

            if (hasTreasure)
            {
                // If there's treasure, always show the treasure chest image.
                assetName = "Treasure_Chest.png";
                angle = 0;
            }
            else
            {
                // Otherwise, pick the correct tile image and rotation based on pathMask.
                GetTileAssetAndRotation(pathMask, out assetName, out angle);
            }

            // If there was already an image at (x,y), remove it.
            if (visualGrid[x, y] != null)
            {
                BuildCanvas.Children.Remove(visualGrid[x, y]);
            }

            // Create a new Image element for this tile.
            Image tileImage = new Image
            {
                Width = TileSize,
                Height = TileSize,
                Source = tileTextures[assetName]
            };

            // If needed, rotate the image to match the direction.
            if (angle != 0)
            {
                tileImage.RenderTransformOrigin = new Point(0.5, 0.5);
                tileImage.RenderTransform = new RotateTransform(angle);
            }

            // Position the image on the canvas.
            Canvas.SetLeft(tileImage, x * TileSize);
            Canvas.SetTop(tileImage, y * TileSize);

            // Add the image to the canvas and store it in visualGrid.
            BuildCanvas.Children.Add(tileImage);
            visualGrid[x, y] = tileImage;
        }

        // Determine which tile image and rotation to use based on path bits.
        private void GetTileAssetAndRotation(int mask, out string assetName, out double angle)
        {
            // Default to a dead end (will override below).
            assetName = "dead_end.png";
            angle = 0;

            switch (mask)
            {
                // Cases for dead ends (single-connection or none).
                case 0:  // No connections (isolated or fully surrounded)
                    assetName = "dead_end.png"; angle = 180; break;
                case 1:  // Up only
                    assetName = "dead_end.png"; angle = 180; break;
                case 2:  // Right only
                    assetName = "dead_end.png"; angle = -90; break;
                case 4:  // Down only
                    assetName = "dead_end.png"; angle = 0; break;
                case 8:  // Left only
                    assetName = "dead_end.png"; angle = -270; break;

                // Straight corridors (two opposite connections).
                case 5:  // Up and Down
                    assetName = "corridor.png"; angle = 0; break;
                case 10: // Left and Right
                    assetName = "corridor.png"; angle = 90; break;

                // Corners (two perpendicular connections).
                case 3:  // Up and Right
                    assetName = "corner_turn_simple.png"; angle = -90; break;
                case 6:  // Right and Down
                    assetName = "corner_turn_simple.png"; angle = 0; break;
                case 12: // Down and Left
                    assetName = "corner_turn_simple.png"; angle = 90; break;
                case 9:  // Left and Up
                    assetName = "corner_turn_simple.png"; angle = 180; break;

                // T-junctions (three connections).
                case 11: // Up, Right, Down
                    assetName = "t_turn.png"; angle = 180; break;
                case 7:  // Up, Right, Left
                    assetName = "t_turn.png"; angle = 270; break;
                case 14: // Right, Down, Left
                    assetName = "t_turn.png"; angle = 0; break;
                case 13: // Up, Down, Left
                    assetName = "t_turn.png"; angle = 90; break;

                // Cross (four connections).
                case 15: // Up, Right, Down, Left
                    assetName = "4_turn.png"; angle = 0; break;
            }
        }

        // Zoom the canvas with Ctrl + Mouse Wheel.
        private void BuildCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Only zoom if Ctrl key is held.
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                return;

            double zoomFactor = 0.1;
            double currentScale = CanvasScale.ScaleX;

            // Increase or decrease scale.
            if (e.Delta > 0)
                currentScale += zoomFactor;
            else
                currentScale -= zoomFactor;

            // Clamp scale between 0.5 and 5.0.
            if (currentScale < 0.5) currentScale = 0.5;
            if (currentScale > 5.0) currentScale = 5.0;

            CanvasScale.ScaleX = currentScale;
            CanvasScale.ScaleY = currentScale;

            e.Handled = true;
        }

        // Export the maze to a text file.
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (mazeArray == null) return;

            // Check if the map is valid (has exit and treasure).
            if (!ValidateMapForExport(out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Exportálás sikertelen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show save file dialog.
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "Map file (*.map)|*.map|Text file (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".map",
                FileName = "maze.map"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            // Build the text content of the map and save it.
            string mapText = BuildExportMapText();
            File.WriteAllText(saveDialog.FileName, mapText, new UTF8Encoding(false));

            MessageBox.Show("Sikeres mentés", "Exportálás", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Check that the maze has at least one exit and one treasure before exporting.
        private bool ValidateMapForExport(out string errorMessage)
        {
            bool hasTreasure = false;
            bool hasEdgeExit = false;

            // Loop through every tile.
            for (int x = 0; x < currentWidthInTiles; x++)
            {
                for (int y = 0; y < currentHeightInTiles; y++)
                {
                    int value = mazeArray[x, y];
                    if (value == -1) continue;  // empty tile

                    // Check for treasure.
                    if ((value & TreasureMask) != 0)
                        hasTreasure = true;

                    int pathMask = value & 15;
                    if (pathMask == 0) continue;

                    // If a path opening is at the edge, mark that as an exit.
                    if (y == 0 && (pathMask & 1) != 0) hasEdgeExit = true;
                    if (x == currentWidthInTiles - 1 && (pathMask & 2) != 0) hasEdgeExit = true;
                    if (y == currentHeightInTiles - 1 && (pathMask & 4) != 0) hasEdgeExit = true;
                    if (x == 0 && (pathMask & 8) != 0) hasEdgeExit = true;
                }
            }

            // If no exit found, error.
            if (!hasEdgeExit)
            {
                errorMessage = "A térképen legalább egy, a szélen lévő kijáratnak lennie kell.";
                return false;
            }

            // If no treasure found, error.
            if (!hasTreasure)
            {
                errorMessage = "A térképen legalább egy kincsesládának lennie kell.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        // Build the text of the map for exporting (one line per row).
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

        // Convert a tile value to a character for the map export.
        private char GetExportChar(int value)
        {
            if (value == -1)
                return '.';  // empty space

            if ((value & TreasureMask) != 0)
                return '█';  // treasure as a solid block

            int mask = value & 15;  // path bits

            // Use box-drawing characters for different path shapes.
            return mask switch
            {
                0 => '╬',  // no direction or four-way
                1 => '║',  // up
                2 => '═',  // right
                3 => '╚',  // up+right (corner)
                4 => '║',  // down
                5 => '║',  // up+down (vertical)
                6 => '╔',  // right+down (corner)
                7 => '╠',  // up+right+down (T)
                8 => '═',  // left
                9 => '╝',  // up+left (corner)
                10 => '═', // left+right (horizontal)
                11 => '╩', // up+left+right (T)
                12 => '╗', // down+left (corner)
                13 => '╣', // up+down+left (T)
                14 => '╦', // left+right+down (T)
                15 => '╬', // four-way
                _ => '.',
            };
        }
    }
}
