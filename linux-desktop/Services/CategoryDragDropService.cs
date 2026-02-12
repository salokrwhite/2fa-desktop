using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Services;

public class CategoryDragDropService
{
    private readonly Window _window;
    private CategoryItemViewModel? _draggedCategory;
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private Border? _dragPreview;
    private Canvas? _dragCanvas;
    private ListBoxItem? _originalItem;
    private CategoryItemViewModel? _dropTargetCategory;
    private Point _lastPointerPosition;
    private Point _dragOffset;
    private System.Threading.Timer? _autoScrollTimer;
    private ListBox? _categoryListBox;
    
    private const double AutoScrollThreshold = 50;
    private const double AutoScrollSpeed = 10;

    public Func<CategoryItemViewModel, CategoryItemViewModel, System.Threading.Tasks.Task>? DropAsync { get; set; }

    public CategoryDragDropService(Window window)
    {
        _window = window;
    }

    public void OnDragStart(object? sender, PointerPressedEventArgs e, CategoryItemViewModel categoryVm)
    {
        if (sender is not Border border) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        _draggedCategory = categoryVm;
        _isDragging = false;
        _dragStartPoint = e.GetPosition(border);
        _originalItem = FindParent<ListBoxItem>(border);
        _categoryListBox = FindParent<ListBox>(border);
        
        if (_originalItem != null)
        {
            var mouseInWindow = e.GetPosition(_window);
            var itemPosition = _originalItem.TranslatePoint(new Point(0, 0), _window) ?? new Point(0, 0);
            _dragOffset = new Point(mouseInWindow.X - itemPosition.X, mouseInWindow.Y - itemPosition.Y);
        }
        
        e.Pointer.Capture(border);
        _window.PointerMoved += OnWindowPointerMoved;
        _window.PointerReleased += OnWindowPointerReleased;
        e.Handled = true;
    }

    public void OnDragMove(object? sender, PointerEventArgs e)
    {
        if (_draggedCategory == null || sender is not Border border) return;
        if (!e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed)
        {
            CancelDrag();
            return;
        }

        var currentPoint = e.GetPosition(border);
        var diff = currentPoint - _dragStartPoint;
        
        if (!_isDragging && (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3))
        {
            _isDragging = true;
            StartDragPreview(border);
        }

        if (_isDragging && _dragPreview != null)
        {
            _lastPointerPosition = e.GetPosition(_window);
            UpdateDragPreviewPosition();
            UpdateDropTarget();
            CheckAndStartAutoScroll();
        }
    }

    public CategoryItemViewModel? OnDragEnd(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Border) return null;

        e.Pointer.Capture(null);

        CategoryItemViewModel? targetVm = null;
        if (_isDragging && _draggedCategory != null)
        {
            var point = e.GetPosition(_window);
            var target = GetCategoryFromPoint(point);
            if (target != null && target != _draggedCategory)
            {
                targetVm = target;
                if (_dropTargetCategory != null)
                {
                    _dropTargetCategory.IsDropTarget = false;
                }
                _dropTargetCategory = null;
            }
        }

        CancelDrag();
        e.Handled = true;
        return targetVm;
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedCategory == null) return;
        
        if (!e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed)
        {
            CancelDrag();
            return;
        }

        if (_isDragging && _dragPreview != null)
        {
            _lastPointerPosition = e.GetPosition(_window);
            UpdateDragPreviewPosition();
            UpdateDropTarget();
            CheckAndStartAutoScroll();
        }
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedCategory == null) return;
        
        if (_isDragging)
        {
            e.Pointer.Capture(null);

            var source = _draggedCategory;
            var point = e.GetPosition(_window);
            var target = GetCategoryFromPoint(point);

            CancelDrag();

            if (target != null && target != source && DropAsync != null)
            {
                _ = DropAsync(source, target);
            }

            e.Handled = true;
            return;
        }

        CancelDrag();
        e.Handled = true;
    }

    private void StartDragPreview(Border originalBorder)
    {
        if (_originalItem == null || _draggedCategory == null) return;
        
        _draggedCategory.IsDragSource = true;
        
        if (_dragCanvas == null)
        {
            _dragCanvas = new Canvas
            {
                IsHitTestVisible = false,
                ZIndex = 1000
            };
            
            var mainGrid = _window.FindControl<Grid>("MainGrid");
            mainGrid?.Children.Add(_dragCanvas);
        }

        var clonedContent = CreateDragPreviewContent(_draggedCategory);

        _dragPreview = new Border
        {
            Background = originalBorder.Background,
            BorderBrush = originalBorder.BorderBrush,
            BorderThickness = originalBorder.BorderThickness,
            CornerRadius = originalBorder.CornerRadius,
            Width = _originalItem.Bounds.Width,
            Height = _originalItem.Bounds.Height,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 8,
                Blur = 24,
                Spread = 0,
                Color = Color.FromArgb(80, 0, 0, 0)
            }),
            Opacity = 0.95,
            RenderTransform = new ScaleTransform(1.05, 1.05),
            Child = clonedContent
        };

        _dragCanvas.Children.Add(_dragPreview);
        UpdateDragPreviewPosition();
    }

    private Control CreateDragPreviewContent(CategoryItemViewModel categoryVm)
    {
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Margin = new Thickness(16, 0, 16, 0)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "☰",
            FontSize = 20,
            Opacity = 0.5,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.Black
        });

        panel.Children.Add(new TextBlock
        {
            Text = categoryVm.Name,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.Black
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"({categoryVm.Count})",
            FontSize = 16,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Opacity = 0.7,
            Foreground = Brushes.Black
        });

        return panel;
    }

    private void UpdateDragPreviewPosition()
    {
        if (_dragPreview == null || _originalItem == null) return;
        
        var offsetX = _lastPointerPosition.X - _dragOffset.X;
        var offsetY = _lastPointerPosition.Y - _dragOffset.Y;
        Canvas.SetLeft(_dragPreview, offsetX);
        Canvas.SetTop(_dragPreview, offsetY);
    }

    private void UpdateDropTarget()
    {
        var newTarget = GetCategoryFromPoint(_lastPointerPosition);
        if (newTarget == _draggedCategory) newTarget = null;

        if (!ReferenceEquals(newTarget, _dropTargetCategory))
        {
            if (_dropTargetCategory != null)
            {
                _dropTargetCategory.IsDropTarget = false;
            }

            _dropTargetCategory = newTarget;

            if (_dropTargetCategory != null)
            {
                _dropTargetCategory.IsDropTarget = true;
            }
        }
    }

    private CategoryItemViewModel? GetCategoryFromPoint(Point windowPoint)
    {
        if (_categoryListBox == null) return null;

        var listBoxTopLeft = _categoryListBox.TranslatePoint(new Point(0, 0), _window);
        if (!listBoxTopLeft.HasValue) return null;

        var left = listBoxTopLeft.Value.X;
        var top = listBoxTopLeft.Value.Y;
        var width = _categoryListBox.Bounds.Width;
        var height = _categoryListBox.Bounds.Height;

        if (windowPoint.X < left || windowPoint.X > left + width) return null;
        if (windowPoint.Y < top || windowPoint.Y > top + height) return null;

        ListBoxItem? best = null;
        var bestDistance = double.MaxValue;

        foreach (var item in GetRealizedCategoryItems())
        {
            if (ReferenceEquals(item, _originalItem)) continue;

            var itemTopLeft = item.TranslatePoint(new Point(0, 0), _window);
            if (!itemTopLeft.HasValue) continue;

            var itemTop = itemTopLeft.Value.Y;
            var itemBottom = itemTop + item.Bounds.Height;
            var centerY = (itemTop + itemBottom) / 2;

            if (windowPoint.Y >= itemTop && windowPoint.Y <= itemBottom)
            {
                best = item;
                bestDistance = 0;
                break;
            }

            var distance = Math.Abs(windowPoint.Y - centerY);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = item;
            }
        }

        return best?.DataContext as CategoryItemViewModel;
    }

    private IEnumerable<ListBoxItem> GetRealizedCategoryItems()
    {
        if (_categoryListBox == null) yield break;

        var queue = new Queue<Visual>();
        queue.Enqueue(_categoryListBox);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in current.GetVisualChildren())
            {
                if (child is Visual visual)
                {
                    queue.Enqueue(visual);
                }

                if (child is ListBoxItem item)
                {
                    var ownerListBox = FindParent<ListBox>(item);
                    if (ReferenceEquals(ownerListBox, _categoryListBox))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    private void CancelDrag()
    {
        _window.PointerMoved -= OnWindowPointerMoved;
        _window.PointerReleased -= OnWindowPointerReleased;
        
        if (_draggedCategory != null) _draggedCategory.IsDragSource = false;
        if (_dropTargetCategory != null) _dropTargetCategory.IsDropTarget = false;

        if (_dragPreview != null && _dragCanvas != null)
        {
            _dragCanvas.Children.Remove(_dragPreview);
            _dragPreview = null;
        }

        StopAutoScroll();

        _draggedCategory = null;
        _dropTargetCategory = null;
        _isDragging = false;
        _originalItem = null;
        _categoryListBox = null;
    }

    private void CheckAndStartAutoScroll()
    {
        if (_categoryListBox == null) return;

        var listBoxPosition = _categoryListBox.TranslatePoint(new Point(0, 0), _window);
        if (!listBoxPosition.HasValue) return;

        var listBoxBounds = _categoryListBox.Bounds;
        var relativeY = _lastPointerPosition.Y - listBoxPosition.Value.Y;
        
        var scrollViewer = FindScrollViewer(_categoryListBox);
        if (scrollViewer != null)
        {
            if ((relativeY >= 0 && relativeY < AutoScrollThreshold && scrollViewer.Offset.Y > 0) ||
                (relativeY < 0 && scrollViewer.Offset.Y > 0))
            {
                StartAutoScroll(scrollViewer, -AutoScrollSpeed);
                return;
            }
            else if (((relativeY > listBoxBounds.Height - AutoScrollThreshold && relativeY <= listBoxBounds.Height) ||
                      relativeY > listBoxBounds.Height) &&
                     scrollViewer.Offset.Y < scrollViewer.Extent.Height - scrollViewer.Viewport.Height)
            {
                StartAutoScroll(scrollViewer, AutoScrollSpeed);
                return;
            }
        }
        else
        {
            if (relativeY >= 0 && relativeY < AutoScrollThreshold || relativeY < 0)
            {
                StartAutoScrollAlternative(_categoryListBox, -1);
                return;
            }
            else if (relativeY > listBoxBounds.Height - AutoScrollThreshold)
            {
                StartAutoScrollAlternative(_categoryListBox, 1);
                return;
            }
        }
        
        StopAutoScroll();
    }

    private void StartAutoScroll(ScrollViewer scrollViewer, double speed)
    {
        if (_autoScrollTimer != null) return;

        _autoScrollTimer = new System.Threading.Timer(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_isDragging && scrollViewer != null)
                {
                    var newOffset = scrollViewer.Offset.Y + speed;
                    var maxOffset = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
                    newOffset = Math.Max(0, Math.Min(newOffset, maxOffset));
                    
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, newOffset);
                    UpdateDropTarget();
                }
            });
        }, null, 0, 30);
    }

    private void StartAutoScrollAlternative(ListBox listBox, int direction)
    {
        if (_autoScrollTimer != null) return;

        _autoScrollTimer = new System.Threading.Timer(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_isDragging && listBox != null)
                {
                    var scrollViewer = FindScrollViewer(listBox);
                    if (scrollViewer != null)
                    {
                        var delta = direction * 20;
                        var newOffset = scrollViewer.Offset.Y + delta;
                        var maxOffset = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
                        newOffset = Math.Max(0, Math.Min(newOffset, maxOffset));
                        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, newOffset);
                    }
                    
                    UpdateDropTarget();
                }
            });
        }, null, 0, 100);
    }

    private void StopAutoScroll()
    {
        if (_autoScrollTimer != null)
        {
            _autoScrollTimer.Dispose();
            _autoScrollTimer = null;
        }
    }

    private static ScrollViewer? FindScrollViewer(Control control)
    {
        if (control is ScrollViewer sv) return sv;
        
        var queue = new Queue<Visual>();
        queue.Enqueue(control);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
            
            foreach (var child in current.GetVisualChildren())
            {
                queue.Enqueue(child);
            }
        }
        
        return null;
    }

    private static T? FindParent<T>(Visual? visual) where T : class
    {
        while (visual != null)
        {
            if (visual is T target)
            {
                return target;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }
}
