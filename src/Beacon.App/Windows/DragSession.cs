using System.Runtime.InteropServices;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Connections;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>一次拖动会话（左键按住 tile 起手）；StartCursor = 按下时光标屏幕物理像素——
/// 目标 = 起点窗口位 + 光标位移，全程物理像素一个单位，跨缩放屏拖动不再混算两套 DIP 空间；
/// FreeX/FreeY = 吸附前的自由落点（收起判定用，防 snap 假贴边）。</summary>
internal sealed record DragSession(UIElement Element, (int X, int Y) StartCursor, int StartX, int StartY)
{
    public int LastFreeX { get; set; }
    public int LastFreeY { get; set; }
}
