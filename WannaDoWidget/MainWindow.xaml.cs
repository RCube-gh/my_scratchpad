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
        private readonly DispatcherTimer _longPressTimer;
        private FrameworkElement? _longPressElement;

        public MainWindow()
        {
            InitializeComponent();

            // Set up background expiry timer (checks every 5 minutes)
            _expiryTimer = new DispatcherTimer();
            _expiryTimer.Interval = TimeSpan.FromMinutes(5);
            _expiryTimer.Tick += (s, e) => { DataManager.CheckAllExpirations(); };
            _expiryTimer.Start();

            // Setup long press timer (600ms)
            _longPressTimer = new DispatcherTimer();
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(600);
            _longPressTimer.Tick += LongPressTimer_Tick;

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
            return ArchivedListView;
        }

        private void StartStaggeredAnimation()
        {
            // Reset main window Y-translation (the window stays static on left)
            WindowTransform.Y = 0;

            // 1. Animate Header (immediately)
            AnimateElement(HeaderGrid, 0, -800, 0, true);

            // 2. Animate Tab Buttons (delay 30ms)
            AnimateElement(TabButtonsGrid, 30, -800, 0, true);

            // 3. Animate Input Area (delay 60ms)
            AnimateElement(InputAreaBorder, 60, -800, 0, true);

            // 4. Animate active ListBox items (delay starting at 100ms, staggered by 40ms per item)
            var activeListBox = GetActiveListBox();
            int itemCount = 0;
            if (activeListBox != null)
            {
                // Force layout so container items exist
                activeListBox.UpdateLayout();

                itemCount = activeListBox.Items.Count;
                for (int i = 0; i < itemCount; i++)
                {
                    var item = activeListBox.Items[i];
                    var container = activeListBox.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        AnimateElement(container, 100 + ((itemCount - 1 - i) * 40), -1000, 0, true);
                    }
                }
            }

            // End animating block after cascade finishes (accounting for 500ms anim duration)
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500 + (itemCount * 40));
            timer.Tick += (s, ev) =>
            {
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

            // Animate items sliding up & fading out (reverse stagger)
            AnimateElement(HeaderGrid, 60, 0, -50, false);
            AnimateElement(InputAreaBorder, 30, 0, -50, false);
            AnimateElement(TabButtonsGrid, 0, 0, -50, false);

            if (activeListBox != null)
            {
                for (int i = 0; i < count; i++)
                {
                    var item = activeListBox.Items[i];
                    var container = activeListBox.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        // Stagger going upwards
                        AnimateElement(container, (count - 1 - i) * 15, 0, -50, false);
                    }
                }
            }

            // Set hidden at the end of the fade out
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(250 + count * 15);
            timer.Tick += (s, ev) =>
            {
                this.Visibility = Visibility.Hidden;
                _isShowing = false;
                _isAnimating = false;
                timer.Stop();
            };
            timer.Start();
        }

        private void AnimateElement(FrameworkElement element, double delayMs, double fromY, double toY, bool isShowing)
        {
            // Ensure TranslateTransform exists
            if (!(element.RenderTransform is TranslateTransform tt))
            {
                tt = new TranslateTransform(0, fromY);
                element.RenderTransform = tt;
            }
            else
            {
                if (isShowing)
                {
                    tt.Y = fromY;
                }
            }

            if (isShowing)
            {
                element.Opacity = 0;
            }

            var beginTime = TimeSpan.FromMilliseconds(delayMs);

            // Y Animation
            var daY = new DoubleAnimation
            {
                From = fromY,
                To = toY,
                Duration = new Duration(TimeSpan.FromMilliseconds(isShowing ? 500 : 250)),
                BeginTime = beginTime,
                EasingFunction = isShowing 
                    ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.15 } 
                    : (IEasingFunction)new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 4 }
            };
            tt.BeginAnimation(TranslateTransform.YProperty, daY);

            // Opacity Animation
            var daO = new DoubleAnimation
            {
                From = isShowing ? 0 : 1,
                To = isShowing ? 1 : 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(isShowing ? 350 : 200)),
                BeginTime = beginTime
            };
            element.BeginAnimation(UIElement.OpacityProperty, daO);
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

            ArchivedListView.ItemsSource = items
                .Where(i => i.State == WannaDoState.Aborted || i.State == WannaDoState.Expired)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
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

        private void LongPressTimer_Tick(object? sender, EventArgs e)
        {
            _longPressTimer.Stop();
            if (_longPressElement != null && _longPressElement.ContextMenu != null)
            {
                _longPressElement.ContextMenu.PlacementTarget = _longPressElement;
                _longPressElement.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                _longPressElement.ContextMenu.IsOpen = true;
            }
            _longPressElement = null;
        }

        private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                _longPressElement = element;
                _longPressTimer.Start();
            }
        }

        private void Card_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _longPressTimer.Stop();
            _longPressElement = null;
        }

        private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _longPressTimer.Stop();
            _longPressElement = null;
        }

        private void TabBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tabName)
            {
                _activeTab = tabName;

                // Update visual tab buttons enabled state
                TabTodoBtn.IsEnabled = (_activeTab != "Todo");
                TabDoneBtn.IsEnabled = (_activeTab != "Completed");
                TabOverBtn.IsEnabled = (_activeTab != "Archived");

                // Toggle visibility of ListBoxes
                TodoListView.Visibility = (_activeTab == "Todo") ? Visibility.Visible : Visibility.Collapsed;
                CompletedListView.Visibility = (_activeTab == "Completed") ? Visibility.Visible : Visibility.Collapsed;
                ArchivedListView.Visibility = (_activeTab == "Archived") ? Visibility.Visible : Visibility.Collapsed;
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