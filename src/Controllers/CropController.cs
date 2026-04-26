using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfShapeRect = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using PowerShot.Models;


namespace PowerShot.Controllers
{
    internal class CropController
    {
        private const double EdgeMargin = 8.0;
        private const double MinSize = 10.0;

        private enum DragMode
        {
            None, DrawNew, Move,
            ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight,
            ResizeTop, ResizeBottom, ResizeLeft, ResizeRight
        }

        private readonly Canvas _canvas;
        private readonly WpfShapeRect _selectionRect;
        private readonly TextBox _xBox, _yBox, _wBox, _hBox;
        private readonly Bitmap _bitmap;
        private readonly AppSettings _settings;
        private readonly Action _onChanged;

        private DragMode _dragMode = DragMode.None;
        private WpfPoint _dragStart;
        private Rect _dragStartRect;
        private bool _suppressTextChange;

        public CropController(Canvas canvas, WpfShapeRect selectionRect,
            TextBox xBox, TextBox yBox, TextBox wBox, TextBox hBox,
            Bitmap bitmap, AppSettings settings, Action onChanged)
        {
            _canvas = canvas;
            _selectionRect = selectionRect;
            _xBox = xBox; _yBox = yBox; _wBox = wBox; _hBox = hBox;
            _bitmap = bitmap;
            _settings = settings;
            _onChanged = onChanged;
        }

        public void Initialize()
        {
            if (_canvas != null && _bitmap != null)
            {
                _canvas.Width = _bitmap.Width;
                _canvas.Height = _bitmap.Height;
                _canvas.MouseDown += OnMouseDown;
                _canvas.MouseMove += OnMouseMove;
                _canvas.MouseUp += OnMouseUp;
                _canvas.MouseLeave += OnMouseLeave;
            }

            if (_xBox != null) _xBox.TextChanged += OnTextChanged;
            if (_yBox != null) _yBox.TextChanged += OnTextChanged;
            if (_wBox != null) _wBox.TextChanged += OnTextChanged;
            if (_hBox != null) _hBox.TextChanged += OnTextChanged;

            SyncTextBoxesFromSettings();
            SyncRectFromSettings();
        }

        public void SetActive(bool active)
        {
            if (_canvas != null)
                _canvas.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Reset()
        {
            if (_bitmap == null || _settings == null) return;
            _settings.CropX = 0;
            _settings.CropY = 0;
            _settings.CropWidth = _bitmap.Width;
            _settings.CropHeight = _bitmap.Height;
            _onChanged();
            SyncTextBoxesFromSettings();
            SyncRectFromSettings();
        }

        private void SyncTextBoxesFromSettings()
        {
            if (_settings == null || _xBox == null) return;
            _suppressTextChange = true;
            _xBox.Text = _settings.CropX.ToString();
            _yBox.Text = _settings.CropY.ToString();
            _wBox.Text = _settings.CropWidth.ToString();
            _hBox.Text = _settings.CropHeight.ToString();
            _suppressTextChange = false;
        }

        private void SyncRectFromSettings()
        {
            if (_settings == null || _selectionRect == null || _bitmap == null) return;

            if (_settings.CropWidth <= 0 || _settings.CropHeight <= 0)
            {
                _settings.CropWidth = _bitmap.Width;
                _settings.CropHeight = _bitmap.Height;
            }

            UpdateRectUI(new Rect(_settings.CropX, _settings.CropY, _settings.CropWidth, _settings.CropHeight));
        }

        private void UpdateRectUI(Rect rect)
        {
            if (_selectionRect == null) return;
            Canvas.SetLeft(_selectionRect, rect.X);
            Canvas.SetTop(_selectionRect, rect.Y);
            _selectionRect.Width = rect.Width;
            _selectionRect.Height = rect.Height;
            _selectionRect.Visibility = Visibility.Visible;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChange || _settings == null || _bitmap == null) return;

            int x, y, w, h;
            if (int.TryParse(_xBox.Text, out x) &&
                int.TryParse(_yBox.Text, out y) &&
                int.TryParse(_wBox.Text, out w) &&
                int.TryParse(_hBox.Text, out h))
            {
                Rectangle clamped = ClampToBitmap(x, y, w, h);
                _settings.CropX = clamped.X;
                _settings.CropY = clamped.Y;
                _settings.CropWidth = clamped.Width;
                _settings.CropHeight = clamped.Height;

                UpdateRectUI(new Rect(clamped.X, clamped.Y, clamped.Width, clamped.Height));
                _onChanged();
            }
        }

        private DragMode HitTest(WpfPoint p)
        {
            if (_selectionRect.Visibility != Visibility.Visible) return DragMode.DrawNew;

            double x = Canvas.GetLeft(_selectionRect);
            double y = Canvas.GetTop(_selectionRect);
            double w = _selectionRect.Width;
            double h = _selectionRect.Height;

            bool left = Math.Abs(p.X - x) <= EdgeMargin;
            bool right = Math.Abs(p.X - (x + w)) <= EdgeMargin;
            bool top = Math.Abs(p.Y - y) <= EdgeMargin;
            bool bottom = Math.Abs(p.Y - (y + h)) <= EdgeMargin;

            bool insideX = p.X >= x && p.X <= x + w;
            bool insideY = p.Y >= y && p.Y <= y + h;

            if (top && left) return DragMode.ResizeTopLeft;
            if (top && right) return DragMode.ResizeTopRight;
            if (bottom && left) return DragMode.ResizeBottomLeft;
            if (bottom && right) return DragMode.ResizeBottomRight;

            if (top && insideX) return DragMode.ResizeTop;
            if (bottom && insideX) return DragMode.ResizeBottom;
            if (left && insideY) return DragMode.ResizeLeft;
            if (right && insideY) return DragMode.ResizeRight;

            if (insideX && insideY) return DragMode.Move;

            return DragMode.DrawNew;
        }

        private void SetCursor(DragMode mode)
        {
            if (_canvas == null) return;
            switch (mode)
            {
                case DragMode.ResizeTopLeft:
                case DragMode.ResizeBottomRight:
                    _canvas.Cursor = Cursors.SizeNWSE; break;
                case DragMode.ResizeTopRight:
                case DragMode.ResizeBottomLeft:
                    _canvas.Cursor = Cursors.SizeNESW; break;
                case DragMode.ResizeTop:
                case DragMode.ResizeBottom:
                    _canvas.Cursor = Cursors.SizeNS; break;
                case DragMode.ResizeLeft:
                case DragMode.ResizeRight:
                    _canvas.Cursor = Cursors.SizeWE; break;
                case DragMode.Move:
                    _canvas.Cursor = Cursors.SizeAll; break;
                default:
                    _canvas.Cursor = Cursors.Cross; break;
            }
        }

        private bool IsCropEnabled()
        {
            return _settings != null && _settings.CropEnabled && _bitmap != null;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsCropEnabled()) return;

            WpfPoint p = e.GetPosition(_canvas);
            _dragStart = p;
            _dragMode = HitTest(p);

            if (_dragMode == DragMode.DrawNew)
            {
                _dragStartRect = new Rect(p, new WpfSize(0, 0));
                UpdateRectUI(_dragStartRect);
            }
            else
            {
                _dragStartRect = new Rect(
                    Canvas.GetLeft(_selectionRect), Canvas.GetTop(_selectionRect),
                    _selectionRect.Width, _selectionRect.Height);
            }

            _canvas.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!IsCropEnabled()) return;

            WpfPoint p = e.GetPosition(_canvas);

            if (_dragMode == DragMode.None)
            {
                SetCursor(HitTest(p));
                return;
            }

            Rect newRect = ComputeDragRect(p);
            ClampToCanvas(ref newRect);
            UpdateRectUI(newRect);

            _suppressTextChange = true;
            if (_xBox != null) _xBox.Text = Math.Round(newRect.X).ToString();
            if (_yBox != null) _yBox.Text = Math.Round(newRect.Y).ToString();
            if (_wBox != null) _wBox.Text = Math.Round(newRect.Width).ToString();
            if (_hBox != null) _hBox.Text = Math.Round(newRect.Height).ToString();
            _suppressTextChange = false;
        }

        private Rect ComputeDragRect(WpfPoint p)
        {
            double dx = p.X - _dragStart.X;
            double dy = p.Y - _dragStart.Y;
            Rect r = _dragStartRect;

            if (_dragMode == DragMode.DrawNew)
            {
                double x = Math.Min(p.X, _dragStart.X);
                double y = Math.Min(p.Y, _dragStart.Y);
                return new Rect(x, y, Math.Abs(p.X - _dragStart.X), Math.Abs(p.Y - _dragStart.Y));
            }
            if (_dragMode == DragMode.Move)
            {
                r.X += dx; r.Y += dy;
                return r;
            }

            bool draggedLeft = _dragMode == DragMode.ResizeLeft
                || _dragMode == DragMode.ResizeTopLeft
                || _dragMode == DragMode.ResizeBottomLeft;
            bool draggedRight = _dragMode == DragMode.ResizeRight
                || _dragMode == DragMode.ResizeTopRight
                || _dragMode == DragMode.ResizeBottomRight;
            bool draggedTop = _dragMode == DragMode.ResizeTop
                || _dragMode == DragMode.ResizeTopLeft
                || _dragMode == DragMode.ResizeTopRight;
            bool draggedBottom = _dragMode == DragMode.ResizeBottom
                || _dragMode == DragMode.ResizeBottomLeft
                || _dragMode == DragMode.ResizeBottomRight;

            if (draggedLeft) { r.X += dx; r.Width -= dx; }
            if (draggedRight) r.Width += dx;
            if (draggedTop) { r.Y += dy; r.Height -= dy; }
            if (draggedBottom) r.Height += dy;

            if (r.Width < MinSize) { r.Width = MinSize; if (draggedLeft) r.X = _dragStartRect.Right - MinSize; }
            if (r.Height < MinSize) { r.Height = MinSize; if (draggedTop) r.Y = _dragStartRect.Bottom - MinSize; }
            return r;
        }

        private void ClampToCanvas(ref Rect r)
        {
            if (r.X < 0) r.X = 0;
            if (r.Y < 0) r.Y = 0;
            if (r.Width > _canvas.Width) r.Width = _canvas.Width;
            if (r.Height > _canvas.Height) r.Height = _canvas.Height;
            if (r.Right > _canvas.Width) r.X = _canvas.Width - r.Width;
            if (r.Bottom > _canvas.Height) r.Y = _canvas.Height - r.Height;
            if (r.X < 0) r.X = 0;
            if (r.Y < 0) r.Y = 0;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragMode == DragMode.None) return;

            _canvas.ReleaseMouseCapture();
            _dragMode = DragMode.None;

            if (_settings == null || _xBox == null) return;

            int x, y, w, h;
            int.TryParse(_xBox.Text, out x);
            int.TryParse(_yBox.Text, out y);
            int.TryParse(_wBox.Text, out w);
            int.TryParse(_hBox.Text, out h);

            Rectangle clamped = ClampToBitmap(x, y, w, h);
            _settings.CropX = clamped.X;
            _settings.CropY = clamped.Y;
            _settings.CropWidth = clamped.Width;
            _settings.CropHeight = clamped.Height;
            _onChanged();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_dragMode == DragMode.None && _canvas != null)
                _canvas.Cursor = Cursors.Arrow;
        }

        private Rectangle ClampToBitmap(int x, int y, int w, int h)
        {
            return ClampRect(x, y, w, h, _bitmap.Width, _bitmap.Height);
        }

        public static Rectangle ClampRect(int x, int y, int w, int h, int srcW, int srcH)
        {
            int min = (int)MinSize;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x > srcW - min) x = srcW - min;
            if (y > srcH - min) y = srcH - min;
            if (w < min) w = min;
            if (h < min) h = min;
            if (x + w > srcW) w = srcW - x;
            if (y + h > srcH) h = srcH - y;
            return new Rectangle(x, y, w, h);
        }
    }
}
