﻿using PageProvider.Collection;
using PageProvider.Model;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Reader
{
    public sealed partial class Reader : Page
    {
        private VirtualizedRandomAccessCollection<MangaPage> pages = new VirtualizedRandomAccessCollection<MangaPage>();
        private int currentPage = 0;
        private Manga manga;

        const int PRELOAD_BEFORE = 1;
        const int PRELOAD_AFTER = 5;
        public Reader()
        {
            this.InitializeComponent();
            pages.CollectionChanged += Pages_CollectionChanged;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
        }

        private async void Pages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                if (currentPage >= e.NewStartingIndex && currentPage < e.NewStartingIndex + e.NewItems.Count)
                {
                    try
                    {
                        var page = e.NewItems[e.NewStartingIndex - currentPage] as MangaPage;
                        var bmp = page.Source;
                        var src = await loadImage(bmp);
                        mangaImage.Source = src;
                        mangaImage.Opacity = 1;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private void notifyRangesChanged(int newIndex)
        {
            var trackedItems = new List<ItemIndexRange>();
            var selected = new ItemIndexRange(newIndex, 1);
            trackedItems.Add(selected);

            int lastIndex = Math.Min(pages.Count - 1, newIndex + PRELOAD_AFTER);
            if (newIndex < lastIndex)
            {
                var newer = new ItemIndexRange(newIndex + 1, (uint)(lastIndex - newIndex));
                trackedItems.Add(newer);
            }

            int firstIndex = Math.Max(0, newIndex - PRELOAD_BEFORE);
            if (firstIndex < newIndex)
            {
                var older = new ItemIndexRange(firstIndex, (uint)(newIndex - firstIndex));
                trackedItems.Add(older);
            }

            pages.RangesChanged(selected, trackedItems);
        }

        private IAsyncAction _setBitmapAction;
        private IAsyncOperation<SoftwareBitmapSource> _loadImageOperation;
        private IAsyncOperation<SoftwareBitmapSource> loadImage(SoftwareBitmap bmp)
        {
            if (_setBitmapAction != null)
            {
                _setBitmapAction.Cancel();
                _setBitmapAction = null;
            }
            if (_loadImageOperation != null)
            {
                _loadImageOperation.Cancel();
                _loadImageOperation = null;
            }
            _loadImageOperation = AsyncInfo.Run(async (c) =>
            {
                c.ThrowIfCancellationRequested();
                var src = new SoftwareBitmapSource();
                _setBitmapAction = src.SetBitmapAsync(bmp);
                await _setBitmapAction;
                return src;
            });
            return _loadImageOperation;
        }

        private void sliderValueChanged(object sender, RangeBaseValueChangedEventArgs args)
        {
            cancelTimer();
            mangaImage.Opacity = 0;
        }
        
        private async Task<int> displayPage(int page)
        {           
            if (page < pages.Count && page >= 0)
            {
                if (page != currentPage)
                {
                    notifyRangesChanged(page);
                }
                currentPage = page;
                pageSlider.Value = (currentPage + 1);
                manga.LastReadPageIdx = currentPage;
                if (currentPage == (pages.Count - 1))
                {
                    manga.DoneReading = true;
                }
                try
                {
                    var pg = pages[page];
                    if (pg.Source != null)
                    {
                        var src = await loadImage(pg.Source);
                        mangaImage.Source = src;
                        mangaImage.Opacity = 1;
                    }
                }
                catch (OperationCanceledException)
                {
                    return -1;
                }        
            }
            return page;
        }

        private async void pageUp()
        {
            if (currentPage < pages.Count - 1)
            {
                await displayPage(currentPage + 1);
                hideSliderAfterDelay(5000);
            }
        }

        private async void pageDown()
        {
            if (currentPage > 0)
            {
                await displayPage(currentPage - 1);
                hideSliderAfterDelay(5000);
            }
        }

        private void keyPressHandler(object sender, KeyEventArgs args)
        {
            if (pageSlider.Visibility != Visibility.Collapsed && pageSlider.FocusState != FocusState.Unfocused)
            {
                return;
            }
            if (args.VirtualKey == Windows.System.VirtualKey.Right)
            {
                pageUp();
            }

            if (args.VirtualKey == Windows.System.VirtualKey.Left)
            {
                pageDown();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame.CanGoBack)
            {
                // Show UI in title bar if opted-in and in-app backstack is not empty.
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Visible;
            }
            else
            {
                // Remove the UI from the title bar if in-app back stack is empty.
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;
            }

            manga = e.Parameter as Manga;
            pages.SetDataSource(manga.Pages);

            var lastReadPage = manga.LastReadPageIdx;
            currentPage = 0;
            pageSlider.Minimum = 1;
            pageSlider.Maximum = pages.Count;
            if (lastReadPage > 0 && lastReadPage < pages.Count)
            {
                currentPage = lastReadPage;
            }
            await displayPage(currentPage);
            hideSliderAfterDelay(2000);

            base.OnNavigatedTo(e);
            Window.Current.CoreWindow.KeyDown += keyPressHandler;
            Window.Current.Activate();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Window.Current.CoreWindow.KeyDown -= keyPressHandler;
        }

        private async void pageSlider_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            await displayPage((int)pageSlider.Value - 1);
            hideSliderAfterDelay(5000);
        }

        private async void pageSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            await displayPage((int)pageSlider.Value - 1);
            hideSliderAfterDelay(5000);
        }

        private ThreadPoolTimer _timer;
        private void cancelTimer()
        {
            if (_timer != null)
            {
                _timer.Cancel();
                _timer = null;
            }
        }

        private IAsyncAction showSlider()
        {
            return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.High,
                new DispatchedHandler(() =>
                {
                    pageSlider.Visibility = Visibility.Visible;
                    pageSlider.IsEnabled = true;
                }));
        }

        private IAsyncAction hideSlider()
        {
            return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.High,
                new DispatchedHandler(() =>
                {
                    pageSlider.Visibility = Visibility.Collapsed;
                    pageSlider.IsEnabled = false;
                }));
        }

        private void hideSliderAfterDelay(int delay)
        {
            cancelTimer();
            TimeSpan dt = TimeSpan.FromMilliseconds(delay);
            _timer = ThreadPoolTimer.CreateTimer(async (source) =>
            {
                await hideSlider();
            }, dt);
        }

        private void showSliderAfterDelay(int delay)
        {
            cancelTimer();
            TimeSpan dt = TimeSpan.FromMilliseconds(delay);
            _timer = ThreadPoolTimer.CreateTimer(async (source) =>
            {
                await showSlider();
                hideSliderAfterDelay(5000);
            }, dt);
        }

        private void mangaImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var point_image = e.GetPosition(mangaImage);
            if (mangaImage.ActualWidth == 0 || mangaImage.ActualHeight == 0)
            {
                return;
            }

            var position = point_image.X;
            var max = mangaImage.ActualWidth;

            var relativeX = position / max;
            double latestPressLocationX = relativeX;

            position = point_image.Y;
            max = mangaImage.ActualHeight;

            var relativeY = position / max;
            double latestPressLocationY = relativeY;

            if (latestPressLocationX < 0.2)
            {
                pageDown();
            }
            else if (latestPressLocationX > 0.8)
            {
                pageUp();
            }
            else
            {
                if (pageSlider.Visibility != Visibility.Collapsed)
                {
                    hideSliderAfterDelay(100);
                }
                else
                {
                    showSliderAfterDelay(100);
                }
            }
        }
    }
}
