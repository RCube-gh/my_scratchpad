using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WannaDoWidget
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private HwndSource? _hwndSource;

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        public DataManager DataManager { get; } = new DataManager();

        private bool _isShowing = false;
        private bool _isAnimating = false;
        private bool _reallyExit = false;

        private string _activeTab = "Todo";
        private readonly DispatcherTimer _expiryTimer;

        public MainWindow()
        {
            InitializeComponent();

            // Set up background expiry timer (checks every 5 minutes)
            _expiryTimer = new DispatcherTimer();
            _expiryTimer.Interval = TimeSpan.FromMinutes(5);
            _expiryTimer.Tick += (s, e) => { DataManager.CheckAllExpirations(); };
            _expiryTimer.Start();

            // Hook data manager event
            DataManager.DataUpdated += DataManager_DataUpdated;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Position initially
            PositionWindow();

            // Register HotKey (Ctrl + Alt + S)
            RegisterGlobalHotKey();

            // Setup System Tray Icon
            SetupTrayIcon();

            // Load items
            RefreshLists();

            // Hide initially as it runs resident
            this.Visibility = Visibility.Hidden;
            WindowTransform.Y = -SystemParameters.WorkArea.Height;
        }

        private void PositionWindow()
        {
            double screenHeight = SystemParameters.WorkArea.Height;
            double screenTop = SystemParameters.WorkArea.Top;
            double screenLeft = SystemParameters.WorkArea.Left;

            this.Height = screenHeight;
            this.Top = screenTop;
            this.Left = screenLeft; // Left side of the screen
        }

        private void RegisterGlobalHotKey()
        {
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource.AddHook(HwndHook);

            // Ctrl = 0x0002, Alt = 0x0001
            // S = 0x53
            RegisterHotKey(helper.Handle, HOTKEY_ID, 0x0002 | 0x0001, 0x53);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                try
                {
                    ToggleVisibility();
                }
                catch (Exception ex)
                {
                    System.IO.File.WriteAllText("crash.txt", ex.ToString());
                    System.Windows.MessageBox.Show(ex.ToString(), "Wanna Do Widget Crash");
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            _notifyIcon.Text = "Wanna Do Widget";
            _notifyIcon.Visible = true;
            _notifyIcon.Click += (s, e) => { ToggleVisibility(); };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Show / Hide", null, (s, e) => { ToggleVisibility(); });
            contextMenu.Items.Add("Exit", null, (s, e) => { ExitApp(); });
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        public void ToggleVisibility()
        {
            if (_isAnimating) return;
            if (_isShowing) HideWidget();
            else ShowWidget();
        }

        public void ShowWidget()
        {
            if (_isShowing || _isAnimating) return;
            _isShowing = true;
            _isAnimating = true;

            // Run expiration check
            DataManager.CheckAllExpirations();

            // Make sure the window is positioned correctly
            PositionWindow();

            // Show window
            this.Show();

            // Run staggered waterfall drop-down animation on elements
            StartStaggeredAnimation();
        }

        private System.Windows.Controls.ListBox GetActiveListBox()
        {
            if (_activeTab == "Todo") return TodoListView;
            if (_activeTab == "Completed") return CompletedListView;
            return OverListView;
        }

        private void StartStaggeredAnimation()
        {
            // Reset main window Y-translation (the window stays static on left)
            WindowTransform.Y = 0;

            // 1. Animate Header (immediately)
            double offscreenY = -this.Height;

            var activeListBox = GetActiveListBox();
            int itemCount = 0;
            if (activeListBox != null)
            {
                // Force layout so container items exist
                activeListBox.UpdateLayout();
                SetScrollClipping(activeListBox, false);
                itemCount = activeListBox.Items.Count;
            }

            // 1. Animate Input Area (starts first at 0ms)
            AnimateElement(InputAreaBorder, 0, offscreenY, 0, true);

            // 4. Animate active ListBox items (starts from 40ms, bottom item first)
            if (activeListBox != null)
            {
                for (int i = 0; i < itemCount; i++)
                {
                    var item = activeListBox.Items[i];
                    var container = activeListBox.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        double itemFromY = offscreenY - GetLayoutY(container);

                        AnimateElement(container, 40 + ((itemCount - 1 - i) * 40), itemFromY, 0, true);
                    }
                }
            }

            // 3. Animate Tab Buttons (starts after the last card starts)
            AnimateElement(TabButtonsGrid, 40 + (itemCount * 40), offscreenY, 0, true);

            // 4. Animate Header (at the very top, starts last)
            AnimateElement(HeaderGrid, 40 + ((itemCount + 1) * 40), offscreenY, 0, true);

            // End animating block after cascade finishes (last anim starts at 40+(itemCount+1)*40 and takes 600ms)
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(700 + (itemCount * 40));
            timer.Tick += (s, ev) =>
            {
                if (activeListBox != null)
                {
                    SetScrollClipping(activeListBox, true);
                }
                _isAnimating = false;
                timer.Stop();
            };
            timer.Start();
        }

        public void HideWidget()
        {
            if (!_isShowing || _isAnimating) return;
            _isAnimating = true;

            var activeListBox = GetActiveListBox();
            int count = activeListBox?.Items.Count ?? 0;

            double offscreenY = -this.Height;

            // Animate items sliding up & fading out (reverse stagger)
            // Tab buttons slide up first, then header, then items top-to-bottom, and finally input area
            AnimateElement(TabButtonsGrid, 0, 0, offscreenY, false);
            AnimateElement(HeaderGrid, 30, 0, offscreenY, false);

            if (activeListBox != null)
            {
                SetScrollClipping(activeListBox, false);
                for (int i = 0; i < count; i++)
                {
                    var item = activeListBox.Items[i];
                    var container = activeListBox.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        double itemToY = offscreenY - GetLayoutY(container);

                        // Stagger going upwards: top-most (i=0) leaves first
                        AnimateElement(container, 60 + (i * 40), 0, itemToY, false);
                    }
                }
            }

            // Bottom input area trails after the last list item
            AnimateElement(InputAreaBorder, 60 + (count * 40), 0, offscreenY, false);

            // Set hidden at the end of the slide out (last animation starts at 60 + count*40 and takes 580ms)
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(650 + count * 40);
            timer.Tick += (s, ev) =>
            {
                if (activeListBox != null)
                {
                    SetScrollClipping(activeListBox, true);
                }
                this.Visibility = Visibility.Hidden;
                _isShowing = false;
                _isAnimating = false;
                timer.Stop();
            };
            timer.Start();
        }

        private double GetLayoutY(FrameworkElement element)
        {
            var renderedPoint = element
                .TransformToAncestor(this)
                .Transform(new System.Windows.Point(0, 0));

            // TransformToAncestor includes the element's previous RenderTransform.
            // Remove that translation so repeated animations use the stable layout position.
            double renderTranslationY = element.RenderTransform is TranslateTransform tt
                ? tt.Y
                : 0;

            return renderedPoint.Y - renderTranslationY;
        }

        private void AnimateElement(FrameworkElement element, double delayMs, double fromY, double toY, bool isShowing)
        {
            if (element.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                element.RenderTransform = tt;
            }

            // Remove the previous animation clock before setting the next start position.
            // Otherwise the completed hide animation still controls the effective Y value.
            tt.BeginAnimation(TranslateTransform.YProperty, null);
            tt.Y = fromY;

            element.Opacity = 1.0;
            element.BeginAnimation(UIElement.OpacityProperty, null);

            var daY = new DoubleAnimation
            {
                From = fromY,
                To = toY,
                Duration = new Duration(TimeSpan.FromMilliseconds(isShowing ? 600 : 580)),
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = isShowing 
                    ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.15 } 
                    : (IEasingFunction)new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 4 }
            };

            daY.Completed += (s, e) =>
            {
                tt.BeginAnimation(TranslateTransform.YProperty, null);
                tt.Y = toY;
            };

            tt.BeginAnimation(TranslateTransform.YProperty, daY);
        }

        private void DataManager_DataUpdated(object? sender, EventArgs e)
        {
            RefreshLists();
        }

        private void RefreshLists()
        {
            var items = DataManager.Items;

            TodoListView.ItemsSource = items
                .Where(i => i.State == WannaDoState.Todo)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

            CompletedListView.ItemsSource = items
                .Where(i => i.State == WannaDoState.Completed)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

            OverListView.ItemsSource = items
                .Where(i => i.State == WannaDoState.Aborted || i.State == WannaDoState.Expired)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

            TodoCountText.Text = TodoListView.Items.Count.ToString();
            DoneCountText.Text = CompletedListView.Items.Count.ToString();
            OverCountText.Text = OverListView.Items.Count.ToString();

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    UpdateScrollMetrics(TodoListView);
                    UpdateScrollMetrics(CompletedListView);
                    UpdateScrollMetrics(OverListView);
                }));
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewItem();
        }

        private void MemoTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    AddNewItem();
                    e.Handled = true;
                }
            }
        }

        private void AddNewItem()
        {
            string memo = MemoTextBox.Text.Trim();
            if (string.IsNullOrEmpty(memo)) return;

            DateTime? dueDate = DueDatePicker.SelectedDate;

            DataManager.AddItem(memo, dueDate);

            // Clear inputs
            MemoTextBox.Text = string.Empty;
            DueDatePicker.SelectedDate = null;
        }

        private void DueDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DateActiveBadge != null)
            {
                DateActiveBadge.Visibility = DueDatePicker.SelectedDate.HasValue 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void CompleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string id)
            {
                DataManager.UpdateItemState(id, WannaDoState.Completed);
            }
        }

        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string id)
            {
                DataManager.UpdateItemState(id, WannaDoState.Aborted);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string id)
            {
                DataManager.DeleteItem(id);
            }
        }

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject depObj && IsInActionPanel(depObj))
            {
                return;
            }

            if (sender is Border cardBorder)
            {
                var actionPanel = FindVisualChild<Border>(cardBorder, "ActionPanel");
                if (actionPanel != null)
                {
                    double maxWidth = double.Parse(actionPanel.Tag?.ToString() ?? "60");
                    bool isOpened = actionPanel.Width > 0;
                    double targetWidth = isOpened ? 0 : maxWidth;
                    double targetOpacity = isOpened ? 0 : 1;

                    if (!isOpened)
                    {
                        CloseAllActionPanelsExcept(cardBorder);
                    }

                    var daW = new DoubleAnimation
                    {
                        To = targetWidth,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    actionPanel.BeginAnimation(FrameworkElement.WidthProperty, daW);

                    var daO = new DoubleAnimation
                    {
                        To = targetOpacity,
                        Duration = TimeSpan.FromMilliseconds(150),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    actionPanel.BeginAnimation(UIElement.OpacityProperty, daO);
                }
            }
        }

        private bool IsInActionPanel(DependencyObject obj)
        {
            while (obj != null)
            {
                if (obj is Border border && border.Name == "ActionPanel")
                    return true;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        private T? FindVisualChild<T>(DependencyObject depObj, string name) where T : DependencyObject
        {
            if (depObj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t &&
                    (string.IsNullOrEmpty(name) ||
                     child is FrameworkElement fe && fe.Name == name))
                {
                    return t;
                }
                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void CloseAllActionPanelsExcept(Border? exceptionCard = null)
        {
            ClosePanelsInListBox(TodoListView, exceptionCard);
            ClosePanelsInListBox(CompletedListView, exceptionCard);
            ClosePanelsInListBox(OverListView, exceptionCard);
        }

        private void ClosePanelsInListBox(System.Windows.Controls.ListBox listBox, Border? exceptionCard)
        {
            if (listBox == null) return;
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                var item = listBox.Items[i];
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    var cardBorder = FindVisualChild<Border>(container, "CardBorder");
                    if (cardBorder != null && cardBorder != exceptionCard)
                    {
                        var actionPanel = FindVisualChild<Border>(cardBorder, "ActionPanel");
                        if (actionPanel != null)
                        {
                            if (actionPanel.Width > 0 || actionPanel.ActualWidth > 0)
                            {
                                var daW = new DoubleAnimation
                                {
                                    To = 0,
                                    Duration = TimeSpan.FromMilliseconds(200),
                                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                                };
                                actionPanel.BeginAnimation(FrameworkElement.WidthProperty, daW);

                                var daO = new DoubleAnimation
                                {
                                    To = 0,
                                    Duration = TimeSpan.FromMilliseconds(150),
                                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                                };
                                actionPanel.BeginAnimation(UIElement.OpacityProperty, daO);
                            }
                        }
                    }
                }
            }
        }

        private void TabBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tabName)
            {
                _activeTab = tabName;

                // Update visual tab buttons enabled state
                TabTodoBtn.IsEnabled = (_activeTab != "Todo");
                TabDoneBtn.IsEnabled = (_activeTab != "Completed");
                TabOverBtn.IsEnabled = (_activeTab != "Over");

                // Toggle visibility of ListBoxes
                TodoListView.Visibility = (_activeTab == "Todo") ? Visibility.Visible : Visibility.Collapsed;
                CompletedListView.Visibility = (_activeTab == "Completed") ? Visibility.Visible : Visibility.Collapsed;
                OverListView.Visibility = (_activeTab == "Over") ? Visibility.Visible : Visibility.Collapsed;

                Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(() => UpdateScrollMetrics(GetActiveListBox())));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideWidget();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_reallyExit)
            {
                e.Cancel = true;
                HideWidget();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not System.Windows.Controls.ListBox listBox)
                return;

            UpdateScrollMetrics(listBox);

            var scrollBar = FindVisualChild<System.Windows.Controls.Primitives.ScrollBar>(
                listBox,
                "ListScrollBar");
            if (scrollBar == null || scrollBar.Maximum <= 0)
                return;

            double nextValue = scrollBar.Value - Math.Sign(e.Delta) * scrollBar.SmallChange;
            scrollBar.Value = Math.Clamp(nextValue, scrollBar.Minimum, scrollBar.Maximum);
            e.Handled = true;
        }

        private void ListScrollBar_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is not System.Windows.Controls.Primitives.ScrollBar scrollBar)
                return;

            var listBox = FindVisualParent<System.Windows.Controls.ListBox>(scrollBar);
            if (listBox == null)
                return;

            var itemsPanel = FindVisualChild<NoClipStackPanel>(listBox, string.Empty);
            if (itemsPanel == null)
                return;

            if (itemsPanel.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                itemsPanel.RenderTransform = transform;
            }

            transform.Y = -e.NewValue;
            UpdateScrollIndicators(listBox, scrollBar);
        }

        private void UpdateScrollMetrics(System.Windows.Controls.ListBox listBox)
        {
            listBox.ApplyTemplate();
            listBox.UpdateLayout();

            var scrollBar = FindVisualChild<System.Windows.Controls.Primitives.ScrollBar>(
                listBox,
                "ListScrollBar");
            var itemsPanel = FindVisualChild<NoClipStackPanel>(listBox, string.Empty);
            if (scrollBar == null || itemsPanel == null)
                return;

            double contentHeight = Math.Max(
                itemsPanel.ActualHeight,
                itemsPanel.DesiredSize.Height);
            double maximum = Math.Max(0, contentHeight - listBox.ActualHeight);
            scrollBar.Maximum = maximum;
            scrollBar.ViewportSize = listBox.ActualHeight;
            scrollBar.Visibility = Visibility.Collapsed;

            if (scrollBar.Value > maximum)
            {
                scrollBar.Value = maximum;
            }

            UpdateScrollIndicators(listBox, scrollBar);
        }

        private void UpdateScrollIndicators(
            System.Windows.Controls.ListBox listBox,
            System.Windows.Controls.Primitives.ScrollBar scrollBar)
        {
            var topIndicator = FindVisualChild<Border>(listBox, "TopScrollIndicator");
            var bottomIndicator = FindVisualChild<Border>(listBox, "BottomScrollIndicator");
            if (topIndicator == null || bottomIndicator == null)
                return;

            const double edgeTolerance = 0.5;
            bool canScroll = scrollBar.Maximum > edgeTolerance;

            topIndicator.Visibility =
                canScroll && scrollBar.Value > edgeTolerance
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            bottomIndicator.Visibility =
                canScroll && scrollBar.Value < scrollBar.Maximum - edgeTolerance
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void SetScrollClipping(System.Windows.Controls.ListBox listBox, bool enabled)
        {
            listBox.ApplyTemplate();

            var viewport = FindVisualChild<ToggleClipGrid>(listBox, "ScrollViewport");
            if (viewport != null)
            {
                viewport.IsLayoutClippingEnabled = enabled;
                viewport.ClipToBounds = enabled;
                viewport.InvalidateArrange();
            }

            var topIndicator = FindVisualChild<Border>(listBox, "TopScrollIndicator");
            var bottomIndicator = FindVisualChild<Border>(listBox, "BottomScrollIndicator");

            if (!enabled)
            {
                if (topIndicator != null)
                    topIndicator.Visibility = Visibility.Collapsed;
                if (bottomIndicator != null)
                    bottomIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            var scrollBar = FindVisualChild<System.Windows.Controls.Primitives.ScrollBar>(
                listBox,
                "ListScrollBar");
            if (scrollBar != null)
            {
                UpdateScrollIndicators(listBox, scrollBar);
            }
        }

        private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? current = child;
            while (current != null)
            {
                if (current is T parent)
                    return parent;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        public void ExitApp()
        {
            _reallyExit = true;
            _expiryTimer.Stop();

            if (_hwndSource != null)
            {
                var helper = new WindowInteropHelper(this);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource.Dispose();
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            Close();
        }
    }
}
