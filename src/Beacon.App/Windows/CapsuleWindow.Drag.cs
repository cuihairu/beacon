using System.Runtime.InteropServices;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;


public sealed partial class CapsuleWindow
{
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed)
        {
            return;
        }
        HidePreview(); // 拖动起手先收预览：拖动中 ApplySize 的 Move 会与拖拽 Move 互抢（WM_DPICHANGED 同型）
        _dragging = true;
        _dragStartCursor = CursorPx();
        _dragOrigin = (_x, _y);
        Root.CapturePointer(e.Pointer);
    }

    /// <summary>悬停展开预览（BrimDeck 对照，2026-10-29 UI 升级）：入窗即展、出窗即收；
    /// 按住进入/拖动中/无数据不展——展开=窗变高，与拖动互抢必须挡。</summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hovered = true;
        if (_dragging || e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed || _previewRows.Count == 0)
        {
            return;
        }
        ShowPreview();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hovered = false;
        HidePreview();
    }

    private void ShowPreview()
    {
        _morphGeneration++; // 作废未落定的收面回调（收起动效进行中又悬停回来）
        if (SourcePreview.Visibility == Visibility.Visible)
        {
            _motion.StopLoops(SourcePreview); // 淡出还没落定：复位后重新展开，否则从半透明继续
            if (_island)
            {
                _motion.MorphIn(SourcePreview);
            }

            return;
        }

        RebuildPreview();
        SourcePreview.Visibility = Visibility.Visible;
        if (_island)
        {
            IslandShadowStack.Visibility = Visibility.Collapsed; // 展开=岛变高，阴影随其口径退场（不跟窗底走）
        }
        ApplySize(); // 高随预览行数（岛=顶缘贴屏顶不动、向下长；经典=锚点角贴位）
        if (_island)
        {
            _motion.MorphIn(SourcePreview); // BrimDeck 口径展开动效（IslandMotion：360ms Bounce，off 档瞬时）；经典形态行为不动
        }
    }

    private void HidePreview()
    {
        if (SourcePreview.Visibility == Visibility.Collapsed)
        {
            return;
        }

        if (!_island || _dragging || _motion.Mode == MotionMode.Off)
        {
            CollapsePreview(); // 经典形态/拖动起手/动效关闭：立即收（拖动中收面尺寸与拖拽 Move 互抢必须瞬时）
            return;
        }

        var generation = ++_morphGeneration;
        _motion.MorphOut(SourcePreview, () =>
        {
            if (generation == _morphGeneration)
            {
                CollapsePreview(); // 内容淡出走完才收面（BrimDeck：内容先撤、曲面随后）
            }
        });
    }

    private void CollapsePreview()
    {
        if (SourcePreview.Visibility == Visibility.Collapsed)
        {
            return;
        }

        SourcePreview.Visibility = Visibility.Collapsed;
        _motion.StopLoops(SourcePreview); // 停掉残留淡出（Opacity/Scale 回基值，下次展开从 0 起）
        // 阴影层恒退场（2026-11-08 实机诊断：刘海贴顶平齐无落影带，见 ApplyIslandChrome 同口径）——
        // 不再随收回重新挂上（旧逻辑曾在此回场 Visible，与 ApplyIslandChrome 的挂载口径一致）。
        ApplySize();
    }

    /// <summary>预览行重建：级别点 + 来源名 + 最差态摘要（裁尾），上限 5 行。
    /// 行用 Grid 三列（点/来源 Auto、摘要 Star）：岛形态预览宽被 MaxWidth 钉在曲面内容宽内，
    /// 摘要列吃剩余宽度并裁尾——文本只会在岛内裁，不越出曲面缘（横向 StackPanel 会量到无限宽而溢出）。
    /// 经典形态预览宽=内容宽（星列退化为 Auto），摘要仍受 92 上限，观感同旧。</summary>
    private void RebuildPreview()
    {
        var panel = new Grid();
        var rows = _previewRows.Take(5).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var line = new Grid { Margin = i == rows.Count - 1 ? new Thickness(0) : new Thickness(0, 0, 0, 5) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // WinUI 3 无 GridLength.Star 静态成员（WPF 才有）：星列显式构造
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(_palette.SeverityColor(row.Worst)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            };
            var source = new TextBlock
            {
                Text = row.Source,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ThemeColors.TextPrimary()),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var summary = new TextBlock
            {
                Text = row.Summary,
                FontSize = 11,
                Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = _island ? double.PositiveInfinity : 92, // 岛=由星列按曲面内容宽裁尾；经典=旧 92 上限
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(dot, 0);
            Grid.SetColumn(source, 1);
            Grid.SetColumn(summary, 2);
            line.Children.Add(dot);
            line.Children.Add(source);
            line.Children.Add(summary);
            Grid.SetRow(line, i);
            panel.Children.Add(line);
        }

        SourcePreview.Child = panel; // Border 装饰件只有 Child（Content 是 ContentControl 面）
    }

    /// <summary>右键直达设置（左键拖动已在 Pressed 过滤非左键，互不干扰）。</summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        // 光标屏幕物理像素锚定：目标 = 按下窗口位 + 光标位移，与 AppWindow.Position 同一坐标系——
        // 跨缩放屏拖动不换算不混算（旧实现每帧重读 GetDpi 与按下时 DIP 空间相减，跨屏比例错乱）；
        // 高频事件下目标没变不重发 Move（SetWindowPos 抖动是跨屏闪烁放大器）
        var (cursorX, cursorY) = CursorPx();
        var x = _dragOrigin.X + cursorX - _dragStartCursor.X;
        // 岛=刘海贴顶（BrimDeck Notch 口径）：垂直分量锁死在按下时的屏顶位，只许水平拖（落点 X 由 Locate 只记水平分量）
        var y = _island ? _dragOrigin.Y : _dragOrigin.Y + cursorY - _dragStartCursor.Y;
        if (x == _x && y == _y)
        {
            return;
        }
        _x = x;
        _y = y;
        _appWindow.Move(new PointInt32(_x, _y));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        Root.ReleasePointerCapture(e.Pointer);
        // 净位移 < 6 dip 视为单击（不是拖动）→ 打开 L2（B-504）；按住抖动由净位移抵消。
        // ISLAND-D4：岛形态下先按命中面路由——真实组件面单击走组件级下钻，虚拟环/条面与整岛其余
        // 区域照旧开板（口径不变）。
        var dragDips = (Math.Abs(_x - _dragOrigin.X) + Math.Abs(_y - _dragOrigin.Y)) / GetDpi();
        if (dragDips < 6)
        {
            RouteIslandClick(e);
        }
        SavePosition();
        if (_island)
        {
            RestorePosition(); // 岛落点即贴回屏顶并夹回屏幕内（Y 恒 0、X 夹取都在 CapsuleIslandMath.Place）
        }

        if (_dpiChangedDuringDrag)
        {
            _dpiChangedDuringDrag = false;
            ApplySize(); // 拖动中跨了 DPI 的消息被跳过：落点后按新 DPI 补排尺寸
        }
    }
}
